using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Presentation.Racing
{
    /// <summary>RACE-016: a mesh in lists for the hologram shader (Game/Race/Holo): position, colour, uv0, uv1, uv2 (glow kind, phase). Shared by the track dressing and the playground hub.</summary>
    public sealed class HoloMesh
    {
        readonly List<Vector3> v = new(); readonly List<Color> c = new(); readonly List<Vector2> uv0 = new(), uv1 = new(), uv2 = new(); readonly List<int> tri = new();
        public int Count => v.Count;
        float glowPad;
        public void Vertex(Vector3 p, Color colour, Vector2 a, Vector2 b, Vector2 d = default) { v.Add(p); c.Add(colour); uv0.Add(a); uv1.Add(b); uv2.Add(d); }
        public void Quad(int a, int b, int c2, int d) { tri.Add(a); tri.Add(b); tri.Add(c2); tri.Add(a); tri.Add(c2); tri.Add(d); }
        /// A camera-facing sprite (Glow mode): four corners at the centre, offsets in uv1.
        public void Glow(Vector3 p, float size, Color colour, float kind, float phase)
        {
            int k = Count; var d = new Vector2(kind, phase); glowPad = Mathf.Max(glowPad, size);
            Vertex(p, colour, new Vector2(-1, -1), new Vector2(-size, -size), d); Vertex(p, colour, new Vector2(1, -1), new Vector2(size, -size), d);
            Vertex(p, colour, new Vector2(1, 1), new Vector2(size, size), d); Vertex(p, colour, new Vector2(-1, 1), new Vector2(-size, size), d);
            Quad(k, k + 1, k + 2, k + 3);
        }
        /// A tapered prism round the y axis at c (flat shaded: each side its own quad), with a top cap.
        public void Prism(Vector3 c, int sides, float r0, float r1, float y0, float y1, Color colour)
        {
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * Mathf.PI * 2 / sides + Mathf.PI / sides, a1 = (s + 1) * Mathf.PI * 2 / sides + Mathf.PI / sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                int k = Count;
                Vertex(c + d0 * r0 + Vector3.up * y0, colour, new Vector2(0, 0), default); Vertex(c + d1 * r0 + Vector3.up * y0, colour, new Vector2(1, 0), default);
                Vertex(c + d1 * r1 + Vector3.up * y1, colour, new Vector2(1, 1), default); Vertex(c + d0 * r1 + Vector3.up * y1, colour, new Vector2(0, 1), default);
                Quad(k, k + 3, k + 2, k + 1);
                int t = Count;
                Vertex(c + Vector3.up * y1, colour, default, default); Vertex(c + d0 * r1 + Vector3.up * y1, colour, default, default); Vertex(c + d1 * r1 + Vector3.up * y1, colour, default, default);
                tri.Add(t); tri.Add(t + 2); tri.Add(t + 1);
            }
        }
        /// A band round a prism (Solid mode: uv.y 0..1 up the band).
        public void Ring(Vector3 c, int sides, float r, float y0, float y1, Color colour)
        {
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * Mathf.PI * 2 / sides + Mathf.PI / sides, a1 = (s + 1) * Mathf.PI * 2 / sides + Mathf.PI / sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r, d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r;
                int k = Count; var size = new Vector2(Vector3.Distance(d0, d1), y1 - y0);
                Vertex(c + d0 + Vector3.up * y0, colour, new Vector2(0, 0), size); Vertex(c + d1 + Vector3.up * y0, colour, new Vector2(1, 0), size);
                Vertex(c + d1 + Vector3.up * y1, colour, new Vector2(1, 1), size); Vertex(c + d0 + Vector3.up * y1, colour, new Vector2(0, 1), size);
                Quad(k, k + 1, k + 2, k + 3);
            }
        }
        /// A flat strip from y0 to y1 at c, `width` across `across`, facing `facing`.
        public void Strip(Vector3 c, Vector3 up, Vector3 across, float width, float y0, float y1, Color colour)
        {
            Vector3 side = Vector3.Cross(up, across).normalized * width * .5f;
            int k = Count; var size = new Vector2(y1 - y0, width);
            Vertex(c - side + up * y0, colour, new Vector2(0, 0), size); Vertex(c + side + up * y0, colour, new Vector2(0, 1), size);
            Vertex(c + side + up * y1, colour, new Vector2(1, 1), size); Vertex(c - side + up * y1, colour, new Vector2(1, 0), size);
            Quad(k, k + 1, k + 2, k + 3);
        }
        /// A box beam from a to b, `width` across (flat) and `height` tall (uv.y 0..1 across the top face and the sides).
        public void Beam(Vector3 a, Vector3 b, float width, float height, Color colour)
        {
            Vector3 along = (b - a).normalized, side = Vector3.Cross(Vector3.up, along).normalized * width * .5f, up = Vector3.up * height * .5f;
            void Face(Vector3 o, Vector3 u)
            {
                int k = Count; var size = new Vector2(Vector3.Distance(a, b), 2 * u.magnitude);
                Vertex(a + o - u, colour, new Vector2(0, 0), size); Vertex(a + o + u, colour, new Vector2(0, 1), size);
                Vertex(b + o + u, colour, new Vector2(1, 1), size); Vertex(b + o - u, colour, new Vector2(1, 0), size);
                // front faces wind clockwise seen from outside: their normal cross(v1 − v0, v2 − v0) points along o
                if (Vector3.Dot(Vector3.Cross(2 * u, b - a), o) >= 0) Quad(k, k + 1, k + 2, k + 3); else Quad(k, k + 3, k + 2, k + 1);
            }
            Face(up, side); Face(-up, side); Face(side, up); Face(-side, up);
        }
        public enum Align : byte { Left, Centre, Right }

        /// <summary>DEV-022: dot-matrix text in Solid mode, one dot per glyph pixel (PixelFont). The block is centred vertically on `at` and
        /// aligned along `right` (a unit vector the lines run along); `down` (unit) runs from one row to the next. For floor text
        /// read by a pilot heading f, right = (f.z, 0, −f.x) and down = −f.</summary>
        public void Text(string text, Vector3 at, Vector3 right, Vector3 down, float pixel, Color colour, Align align = Align.Centre)
        {
            if (string.IsNullOrEmpty(text)) return;
            float top = -PixelFont.Measure(text).y * .5f;
            var lines = text.Split('\n');
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li]; int w = PixelFont.LineWidth(line);
                float x0 = align == Align.Centre ? -w * .5f : align == Align.Right ? -w : 0, y0 = top + li * PixelFont.LineHeight;
                for (int ci = 0; ci < line.Length; ci++)
                {
                    var rows = PixelFont.Rows(line[ci]);
                    if (rows == null) continue;
                    for (int y = 0; y < PixelFont.Height; y++)
                        for (int x = 0; x < PixelFont.Width; x++)
                            if (((rows[y] >> (PixelFont.Width - 1 - x)) & 1) != 0)
                                Dot(at + right * ((x0 + ci * PixelFont.Advance + x) * pixel) + down * ((y0 + y) * pixel), right * pixel, down * pixel, colour);
                }
            }
        }

        /// <summary>One Solid-mode dot: a quad from p along r and d whose uv1 says one metre square, so the shader draws one dot
        /// (with its gap) whatever the quad's size.</summary>
        public void Dot(Vector3 p, Vector3 r, Vector3 d, Color colour)
        {
            int k = Count;
            Vertex(p, colour, new Vector2(0, 0), Vector2.one); Vertex(p + r, colour, new Vector2(1, 0), Vector2.one);
            Vertex(p + r + d, colour, new Vector2(1, 1), Vector2.one); Vertex(p + d, colour, new Vector2(0, 1), Vector2.one);
            Quad(k, k + 1, k + 2, k + 3);
        }

        /// <summary>A dashed ring of dots on a horizontal plane: `count` dots of `size` metres round `c` at `radius`; every `gap`-th
        /// position is left out (0 = none), which breaks the ring into dashes.</summary>
        public void DotRing(Vector3 c, float radius, int count, float size, Color colour, int gap = 0)
        {
            for (int i = 0; i < count; i++)
            {
                if (gap > 0 && i % gap == 0) continue;
                float a = i * Mathf.PI * 2 / count;
                Vector3 p = c + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius;
                Dot(p - new Vector3(size, 0, size) * .5f, Vector3.right * size, Vector3.forward * size, colour);
            }
        }

        /// <summary>A flat quad on a horizontal plane at height y, centred on c, `width` along `right` and `length` along `forward`
        /// (both unit). uv0 runs 0..1 across and along; uv1 = (width, length) for the modes that read the size from it.</summary>
        public void Flat(Vector3 c, Vector3 right, Vector3 forward, float width, float length, Color colour)
        {
            Vector3 r = right * width * .5f, f = forward * length * .5f; var size = new Vector2(width, length);
            int k = Count;
            Vertex(c - r - f, colour, new Vector2(0, 0), size); Vertex(c + r - f, colour, new Vector2(1, 0), size);
            Vertex(c + r + f, colour, new Vector2(1, 1), size); Vertex(c - r + f, colour, new Vector2(0, 1), size);
            Quad(k, k + 1, k + 2, k + 3);
        }

        public void Fade(int from, int to) { for (int i = from; i < to && i < c.Count; i++) c[i] = new Color(c[i].r, c[i].g, c[i].b, 0); }
        public void Upload(MeshRenderer r) { if (r != null) r.GetComponent<MeshFilter>().sharedMesh.SetColors(c); }
        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv0); m.SetUVs(1, uv1); m.SetUVs(2, uv2); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            if (glowPad > 0) { var bounds = m.bounds; bounds.Expand(glowPad * 2); m.bounds = bounds; }
            return m;
        }
    }
}
