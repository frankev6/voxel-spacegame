using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Game.App.DevRuntime.Playground;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>DEV-025: the Hollow from above without entering Play. Slices the rock (ArtSource/Hub/hollow/hollow_blocks.bin) at the flight
    /// plane into Logs/hollow-map/rock.png (2 m a pixel, north up: walls at flight height light, the rock's inside mid brown, tunnels dark
    /// blue and caverns dark brown shaded by floor depth, space black),
    /// and writes everything the game places there to overlay.json: stations, traffic routes, the paths and junctions, the wall guides in
    /// their district colours (HollowHolograms.PlanGuides, the game's own planner), lamps, beacons, signs, guns, berths, patrols, the
    /// entrance and the race portal. tools/hollow-map.py draws the map from the two.</summary>
    public static class HollowMap
    {
        const string Source = "ArtSource/Hub/hollow/hollow_blocks.bin", Out = "Logs/hollow-map";
        const float Cell = 2;

        [MenuItem("Game/Content/Export Hollow Map")]
        public static void Export()
        {
            var origins = new Dictionary<string, (float2 origin, HollowLayout.Layer layer, int fort)>();
            foreach (var p in HollowLayout.Pieces) origins[p.Id] = (p.Origin, p.Layer, p.Fort);
            var blocks = new List<(float x0, float z0, float size, float y0, float y1, bool fort, bool high)>();
            using (var reader = new BinaryReader(File.OpenRead(Source)))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "HOLW" || reader.ReadInt32() != 1) throw new InvalidDataException(Source);
                int count = reader.ReadInt32();
                for (int p = 0; p < count; p++)
                {
                    string id = Read(reader); Read(reader);
                    int n = reader.ReadInt32();
                    bool known = origins.TryGetValue(id, out var piece);
                    for (int b = 0; b < n; b++)
                    {
                        short i = reader.ReadInt16(), j = reader.ReadInt16(), k = reader.ReadInt16(); byte s = reader.ReadByte(); reader.ReadByte(); reader.ReadByte();
                        if (known) blocks.Add((piece.origin.x + i * Cell, piece.origin.y + k * Cell, s * Cell, j * Cell, (j + s) * Cell, piece.fort >= 0 || piece.layer >= HollowLayout.Layer.Struct, piece.layer == HollowLayout.Layer.High));
                    }
                    int guns = reader.ReadInt32();
                    reader.ReadBytes(guns * 8);
                }
            }
            float2 lo = float.MaxValue, hi = float.MinValue;
            foreach (var b in blocks) { lo = math.min(lo, new float2(b.x0, b.z0)); hi = math.max(hi, new float2(b.x0 + b.size, b.z0 + b.size)); }
            lo -= 40; hi += 40;
            int w = (int)math.ceil((hi.x - lo.x) / Cell), h = (int)math.ceil((hi.y - lo.y) / Cell);
            // per pixel: a wall at the flight plane, the highest floor below it, the lowest roof above
            var wall = new byte[w * h]; var floor = new float[w * h]; var roof = new float[w * h];
            for (int c = 0; c < floor.Length; c++) { floor[c] = float.NegativeInfinity; roof[c] = float.PositiveInfinity; }
            foreach (var b in blocks)
            {
                int i0 = (int)((b.x0 - lo.x) / Cell), k0 = (int)((b.z0 - lo.y) / Cell), n = math.max(1, (int)(b.size / Cell));
                for (int k = k0; k < k0 + n; k++)
                    for (int i = i0; i < i0 + n; i++)
                    {
                        int c = k * w + i;
                        if (b.y0 < 1 && b.y1 > -1) wall[c] = (byte)(b.fort ? 2 : 1);
                        else if (b.y1 <= 0) floor[c] = math.max(floor[c], b.y1);
                        else roof[c] = math.min(roof[c], b.y0);
                    }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var px = new Color32[w * h];
            for (int c = 0; c < px.Length; c++)
            {
                bool roofed = !float.IsPositiveInfinity(roof[c]), floored = !float.IsNegativeInfinity(floor[c]);
                // the rock is a shell: inside it there is roof but no floor; a tunnel has both; a cavern under the cutaway only floor
                if (wall[c] == 1) px[c] = new Color32(176, 158, 130, 255);
                else if (wall[c] == 2) px[c] = new Color32(120, 150, 190, 255);
                else if (roofed && !floored) px[c] = new Color32(92, 84, 74, 255);
                else if (floored)
                {
                    float t = math.saturate((-floor[c] - 8) / 40);   // shallow floors lighter
                    byte v = (byte)math.lerp(54, 26, t);
                    px[c] = roofed ? new Color32((byte)(v * .7f), (byte)(v * .8f), v, 255) : new Color32(v, (byte)(v * .9f), (byte)(v * .8f), 255);
                }
                else px[c] = new Color32(6, 7, 10, 255);
            }
            tex.SetPixels32(px); tex.Apply();
            Directory.CreateDirectory(Out);
            File.WriteAllBytes(Path.Combine(Out, "rock.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            File.WriteAllText(Path.Combine(Out, "overlay.json"), Overlay(lo, w, h));
            Debug.Log($"[HollowMap] wrote {Out}/rock.png ({w} x {h}, {Cell} m/px) and overlay.json from {blocks.Count} blocks");
        }

        static string Overlay(float2 lo, int w, int h)
        {
            var j = new Json();
            j.Open().Num("x0", lo.x).Num("z0", lo.y).Num("cell", Cell).Num("w", w).Num("h", h);
            j.Key("stations").Arr(); foreach (var s in HollowLayout.Stations) j.Open().Str("id", s.Id).Str("title", s.Title).P(s.Position).Num("yaw", s.Yaw).Col(Colour(s.Id)).Close(); j.End();
            j.Key("traffic").Arr(); foreach (var r in HollowLayout.Traffic) { j.Open().Str("ship", r.Blueprint).Bool("loop", r.Loop).Pts("points", r.Points).Close(); } j.End();
            j.Key("paths").Arr(); foreach (var (station, pts) in HollowLayout.Paths) j.Open().Str("station", station).Col(Colour(station)).Pts("points", pts).Close(); j.End();
            j.Key("junctions").Arr(); foreach (var wp in HollowLayout.Waypoints) j.Open().P(wp.Position).Close(); j.End();
            var guides = HollowHolograms.PlanGuides();
            j.Key("neons").Arr();
            for (int i = 0; i < HollowLayout.Neons.Length; i++)
            {
                var n = HollowLayout.Neons[i]; int path = guides[i].path;
                j.Open().Pts("ab", new[] { n.A, n.B }).Num("y", n.Y).Col(path >= 0 ? Colour(HollowLayout.Paths[path].Station) : n.Colour)
                    .Str("guides", path >= 0 ? HollowLayout.Paths[path].Station : "").Close();
            }
            j.End();
            j.Key("lights").Arr(); foreach (var l in HollowLayout.Lights) j.Open().P(l.Position.xz).Num("y", l.Position.y).Num("r", l.Radius).Str("kind", l.Kind).Col(l.Colour).Close(); j.End();
            j.Key("beacons").Arr(); foreach (var b in HollowLayout.Beacons) j.Open().P(b.Position).Col(b.Colour).Close(); j.End();
            j.Key("signs").Arr(); foreach (var s in HollowLayout.Signs) j.Open().Str("text", s.Text).P(s.Position.xz).Num("yaw", s.Yaw).Col(s.Colour).Close(); j.End();
            j.Key("guns").Arr(); foreach (var g in HollowLayout.Guns) j.Open().P(g.Position).Num("yaw", g.Yaw).Bool("big", g.Big).Close(); j.End();
            j.Key("moored").Arr(); foreach (var m in HollowLayout.Moorings) j.Open().Str("ship", m.Blueprint).P(m.Position).Num("yaw", m.Yaw).Close(); j.End();
            j.Key("patrols").Arr(); foreach (var g in HollowLayout.Gunships) j.Open().Str("ship", g.Blueprint).P(g.Position).Num("r", g.Radius).Close(); j.End();
            j.Key("marks").Arr();
            j.Open().Str("text", "ENTRANCE (spawn)").P(HollowLayout.Entrance).Num("yaw", HollowLayout.EntranceYaw).Close();
            j.Open().Str("text", "RACE PORTAL").P(HollowLayout.RacePortal).Num("yaw", HollowLayout.RacePortalYaw).Num("width", HollowLayout.RacePortalWidth).Close();
            j.End();
            j.Close();
            return j.ToString();
        }

        static Color Colour(string station) => HollowHolograms.TryDistrictColour(station, out var c) ? c : Color.white;

        static string Read(BinaryReader r) => Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt16()));

        /// <summary>A small JSON writer (the overlay has a fixed shape; JsonUtility cannot write arrays of tuples).</summary>
        sealed class Json
        {
            readonly StringBuilder sb = new(); bool first = true;
            void Sep() { if (!first) sb.Append(','); first = false; }
            internal Json Open() { Sep(); sb.Append('{'); first = true; return this; }
            internal Json Close() { sb.Append('}'); first = false; return this; }
            internal Json Arr() { sb.Append('['); first = true; return this; }
            internal Json End() { sb.Append(']'); first = false; return this; }
            internal Json Key(string k) { Sep(); sb.Append('"').Append(k).Append("\":"); first = true; return this; }
            internal Json Num(string k, float v) { Key(k); sb.Append(v.ToString("0.##", CultureInfo.InvariantCulture)); first = false; return this; }
            internal Json Bool(string k, bool v) { Key(k); sb.Append(v ? "true" : "false"); first = false; return this; }
            internal Json Str(string k, string v) { Key(k); sb.Append('"').Append((v ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ")).Append('"'); first = false; return this; }
            internal Json P(float2 p) => Num("x", p.x).Num("z", p.y);
            internal Json Col(Color c) { Key("colour"); sb.Append('"').Append(ColorUtility.ToHtmlStringRGB(c)).Append('"'); first = false; return this; }
            internal Json Pts(string k, float2[] pts)
            {
                Key(k); sb.Append('[');
                for (int i = 0; i < pts.Length; i++)
                    sb.Append(i > 0 ? "," : "").Append('[').Append(pts[i].x.ToString("0.#", CultureInfo.InvariantCulture)).Append(',').Append(pts[i].y.ToString("0.#", CultureInfo.InvariantCulture)).Append(']');
                sb.Append(']'); first = false; return this;
            }
            public override string ToString() => sb.ToString();
        }
    }
}
