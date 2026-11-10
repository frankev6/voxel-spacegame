using System;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Art
{
    /// <summary>SPEC-93 EXT-014, Tollhouse Nine's own surfaces (owner 2026-11-10: "add new textures instead of reusing the same stuff all
    /// the time, make it have its own voice"). A lantern-festival tollhouse: glazed ivory tile, jade key-fret, pierced brass lattice
    /// with the light behind it, paper lantern panels, riveted turbine plate, corrugated shanty sheet, deck tread with hazard edges,
    /// the core's glowing conduit glass, lit window panes and girder truss. Each is a 256 px procedural tile written here (own work,
    /// nothing imported) into a free slice (16-25) of the brick arrays, drawn by the brick shader's layer path (style 0: the layer's
    /// albedo, mask and normal; mask r metallic, g smoothness, b paint, a emission), with its layer row in BrickLayerParams and a
    /// `block_lantern_*` block on it. Layers 16 and up skip the ships' seam glow.</summary>
    public static class LanternLook
    {
        const string Textures = "Assets/Game/Art/_Placeholder/Textures/";
        const string Blocks = "Assets/Game/Content/Blocks/";
        const int Size = 256;

        public enum Surface { Tile = 16, Jade = 17, Lattice = 18, Paper = 19, Plate = 20, Corrugated = 21, Tread = 22, Conduit = 23, Glass = 24, Truss = 25 }

        struct Look
        {
            public Surface Layer; public string Block, Name; public int TileCells; public string Emission; public float Glow;
        }

        [Game.Core.StaticStateOk("constant surface table")]
        static readonly Look[] Looks =
        {
            new Look { Layer = Surface.Tile, Block = "block_lantern_tile", Name = "Lantern glazed tile", TileCells = 2 },
            new Look { Layer = Surface.Jade, Block = "block_lantern_jade", Name = "Lantern jade fret", TileCells = 2 },
            new Look { Layer = Surface.Lattice, Block = "block_lantern_lattice", Name = "Lantern brass lattice", TileCells = 1, Emission = "FFB45A", Glow = 1.1f },
            new Look { Layer = Surface.Paper, Block = "block_lantern_paper", Name = "Lantern paper panel", TileCells = 1, Emission = "FF9A3C", Glow = 4.5f },
            new Look { Layer = Surface.Plate, Block = "block_lantern_plate", Name = "Lantern turbine plate", TileCells = 2 },
            new Look { Layer = Surface.Corrugated, Block = "block_lantern_corrugated", Name = "Undertown corrugated sheet", TileCells = 1 },
            new Look { Layer = Surface.Tread, Block = "block_lantern_tread", Name = "Lantern deck tread", TileCells = 1 },
            new Look { Layer = Surface.Conduit, Block = "block_lantern_conduit", Name = "Core conduit glass", TileCells = 1, Emission = "8CF2FF", Glow = 6 },
            new Look { Layer = Surface.Glass, Block = "block_lantern_glass", Name = "Lantern window panes", TileCells = 1, Emission = "FFC27A", Glow = 3.5f },
            new Look { Layer = Surface.Truss, Block = "block_lantern_truss", Name = "Lantern girder truss", TileCells = 1 },
        };

        /// <summary>The block for a surface (LanternBuilder skins the town with these).</summary>
        public static string BlockId(Surface s) => Array.Find(Looks, l => l.Layer == s).Block;

        [MenuItem("Game/Art/Bake Lantern Look")]
        public static void Bake()
        {
            var albedo = Load<Texture2DArray>("BrickAlbedoArray.asset"); var mask = Load<Texture2DArray>("BrickMaskArray.asset");
            var normal = Load<Texture2DArray>("BrickNormalArray.asset"); var parameters = Load<Texture2D>("BrickLayerParams.asset");
            if (parameters.height < 4) throw new InvalidOperationException("BrickLayerParams has no rows 2/3: run Game/Art/Bake NOCTURNE Brick Look first");
            foreach (var look in Looks)
            {
                var a = new Color[Size * Size]; var m = new Color[Size * Size]; var h = new float[Size * Size];
                for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                {
                    var s = Shade(look.Layer, x, y);
                    a[x + y * Size] = new Color(s.Albedo.x, s.Albedo.y, s.Albedo.z, 1);
                    m[x + y * Size] = new Color(s.Metal, s.Smooth, s.Paint, s.Emit);
                    h[x + y * Size] = s.Height;
                }
                int layer = (int)look.Layer;
                Copy(Texture(a, false), albedo, layer, TextureFormat.BC7);
                Copy(Texture(m, true), mask, layer, TextureFormat.BC7);
                Copy(Texture(Normals(h, look.Layer == Surface.Corrugated ? 3f : 2f), true), normal, layer, TextureFormat.BC5);
                Color glow = string.IsNullOrEmpty(look.Emission) ? Color.clear : Hex(look.Emission).linear * look.Glow;
                parameters.SetPixel(layer, 0, new Color(glow.r, glow.g, glow.b, 0));
                parameters.SetPixel(layer, 1, new Color(look.TileCells, 1, 1, .25f));   // tile size in cells, scorch, crack, edge wear
                parameters.SetPixel(layer, 2, Color.clear);                             // style 0: the layer's own maps
                parameters.SetPixel(layer, 3, Color.clear);
                EnsureBlock(look);
            }
            albedo.Apply(false, false); mask.Apply(false, false); normal.Apply(false, false); parameters.Apply(false, false);
            foreach (UnityEngine.Object o in new UnityEngine.Object[] { albedo, mask, normal, parameters }) EditorUtility.SetDirty(o);
            AssetDatabase.SaveAssets();
            Debug.Log($"Baked the Lantern look: {Looks.Length} surfaces in brick layers {(int)Looks[0].Layer}-{(int)Looks[Looks.Length - 1].Layer}.");
        }

        static T Load<T>(string name) where T : UnityEngine.Object
            => AssetDatabase.LoadAssetAtPath<T>(Textures + name) ?? throw new InvalidOperationException("Missing " + Textures + name);

        /// <summary>A `block_lantern_*` block: the hull block's stats on its own material layer.</summary>
        static void EnsureBlock(Look look)
        {
            string path = Blocks + look.Block + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(Blocks + "block_hull.asset", path))
                throw new InvalidOperationException("Could not create " + path);
            var block = AssetDatabase.LoadMainAssetAtPath(path);
            var so = new SerializedObject(block);
            so.FindProperty("id").stringValue = look.Block;
            so.FindProperty("displayName").stringValue = look.Name;
            var former = so.FindProperty("formerIds"); if (former != null) former.ClearArray();
            so.FindProperty("materialLayer").intValue = (int)look.Layer;
            so.ApplyModifiedPropertiesWithoutUndo();
            block.name = look.Block; EditorUtility.SetDirty(block);
        }

        // ------------------------------------------------------------------------------------------------------------------ shading
        struct Texel { public float3 Albedo; public float Height, Metal, Smooth, Paint, Emit; }

        static Texel Shade(Surface surface, int x, int y)
        {
            switch (surface)
            {
                case Surface.Tile: return Tile(x, y, new float3(.93f, .89f, .80f), .85f, false);
                case Surface.Jade: return Tile(x, y, new float3(.55f, .78f, .70f), .9f, true);
                case Surface.Lattice: return Lattice(x, y);
                case Surface.Paper: return Paper(x, y);
                case Surface.Plate: return Plate(x, y);
                case Surface.Corrugated: return Corrugated(x, y);
                case Surface.Tread: return Tread(x, y);
                case Surface.Conduit: return Conduit(x, y);
                case Surface.Glass: return Glass(x, y);
                default: return Truss(x, y);
            }
        }

        /// <summary>Glazed tile, 4 × 4 to a tile (2 m squares): grout, a bevel, a glaze crackle and a shade per tile. Jade adds a carved
        /// key-fret (a squared spiral) in every tile.</summary>
        static Texel Tile(int x, int y, float3 baseColour, float paint, bool fret)
        {
            const int n = 64; int tx = x / n, ty = y / n, lx = x % n, ly = y % n;
            float edge = math.min(math.min(lx, n - 1 - lx), math.min(ly, n - 1 - ly));
            bool grout = edge < 2.5f;
            float shade = .93f + .1f * Rand(tx * 7 + ty * 13, 3), crackle = Crackle(x, y, 23);
            var t = new Texel { Albedo = baseColour * shade * (1 - .25f * crackle), Smooth = .78f - .3f * crackle, Paint = paint, Height = math.saturate(edge / 7f) };
            if (fret && Fret(lx - 8, ly - 8, n - 16)) { t.Albedo *= .62f; t.Height -= .45f; t.Smooth = .4f; }
            if (grout) { t.Albedo = new float3(.30f, .28f, .25f); t.Smooth = .15f; t.Paint = 0; t.Height = 0; }
            return t;
        }

        /// <summary>A squared spiral (key-fret) inside a `size` box, drawn on a 6 × 6 grid.</summary>
        static bool Fret(int x, int y, int size)
        {
            if (x < 0 || y < 0 || x >= size || y >= size) return false;
            int s = size / 6, cx = x / s, cy = y / s;
            if (cx > 5 || cy > 5) return false;
            int[,] k = { { 1, 1, 1, 1, 1, 1 }, { 1, 0, 0, 0, 0, 1 }, { 1, 0, 1, 1, 0, 1 }, { 1, 0, 1, 0, 0, 1 }, { 1, 0, 1, 1, 1, 1 }, { 1, 0, 0, 0, 0, 0 } };
            return k[cy, cx] == 1;
        }

        /// <summary>Pierced brass lattice: squares with a diamond in each, the warm light behind showing through the holes.</summary>
        static Texel Lattice(int x, int y)
        {
            const int n = 64; float lx = x % n - n / 2f + .5f, ly = y % n - n / 2f + .5f;
            float frame = math.max(math.abs(lx), math.abs(ly)), diamond = math.abs(lx) + math.abs(ly);
            bool bar = frame > 26 || math.abs(diamond - 20) < 4.5f || math.abs(lx) < 2.5f && frame > 12 || math.abs(ly) < 2.5f && frame > 12;
            float grain = .9f + .1f * Noise(x * .15f, y * .02f, 5);
            if (bar) return new Texel { Albedo = new float3(.80f, .60f, .28f) * grain, Metal = .92f, Smooth = .55f, Paint = .15f, Height = 1 };
            return new Texel { Albedo = new float3(.10f, .06f, .03f), Metal = 0, Smooth = .2f, Paint = 0, Emit = .9f + .1f * Noise(x * .05f, y * .05f, 9), Height = 0 };
        }

        /// <summary>Paper lantern panels: lacquered ribs, glowing paper with fibres.</summary>
        static Texel Paper(int x, int y)
        {
            bool rib = x % 64 < 5 || y % 128 < 6;
            if (rib) return new Texel { Albedo = new float3(.32f, .05f, .04f), Metal = .1f, Smooth = .7f, Paint = .2f, Height = 1 };
            float fibre = .85f + .15f * Noise(x * .6f, y * .05f, 4);
            return new Texel { Albedo = new float3(.97f, .86f, .64f) * fibre, Smooth = .25f, Paint = .35f, Emit = .75f * fibre, Height = .6f };
        }

        /// <summary>Turbine plate: 4 m plates with rivet rows and welds, oil and verdigris streaks running down.</summary>
        static Texel Plate(int x, int y)
        {
            const int n = 128; int lx = x % n, ly = y % n;
            float edge = math.min(math.min(lx, n - 1 - lx), math.min(ly, n - 1 - ly));
            float streak = Noise(x * .08f, y * .006f, 11), stain = math.saturate((streak - .55f) * 4);
            var t = new Texel { Albedo = new float3(.62f, .63f, .62f) * (.92f + .08f * Noise(x * .03f, y * .03f, 2)), Metal = .75f, Smooth = .45f - .2f * stain, Paint = .7f, Height = .7f };
            t.Albedo = math.lerp(t.Albedo, new float3(.30f, .55f, .46f), stain * .45f);   // verdigris
            if (edge < 2) { t.Height = 0; t.Albedo *= .45f; t.Paint = .3f; }
            float rx = (lx - 8) % 16 - 8f, ry = math.min(ly, n - 1 - ly) - 7f, rcol = math.min(lx, n - 1 - lx) - 7f, ry2 = (ly - 8) % 16 - 8f;
            if ((edge >= 2 && math.length(new float2(rx, ry)) < 3.2f) || (edge >= 2 && math.length(new float2(rcol, ry2)) < 3.2f)) { t.Height = 1; t.Smooth = .6f; t.Albedo *= 1.08f; }
            return t;
        }

        /// <summary>Corrugated sheet: ribs, sheet overlaps, rust running from the bolt lines.</summary>
        static Texel Corrugated(int x, int y)
        {
            float rib = .5f + .5f * math.sin(x * math.PI * 2 / 16f);
            float rust = math.saturate((Noise(x * .05f, y * .012f, 7) - .45f) * 3 + (y % 128 < 24 ? .25f : 0));
            var t = new Texel { Albedo = math.lerp(new float3(.70f, .70f, .66f), new float3(.42f, .22f, .11f), rust), Metal = .5f - .3f * rust, Smooth = .35f - .2f * rust, Paint = .8f - .4f * rust, Height = rib };
            if (y % 128 < 3) { t.Height = 0; t.Albedo *= .5f; }
            if (y % 128 > 6 && y % 128 < 12 && x % 32 > 13 && x % 32 < 19) { t.Height = 1; t.Albedo = new float3(.35f, .2f, .1f); }
            return t;
        }

        /// <summary>Deck tread: raised lozenges in a herringbone, a yellow and black hazard band along one edge.</summary>
        static Texel Tread(int x, int y)
        {
            if (y < 28) return new Texel { Albedo = ((x + y) / 18) % 2 == 0 ? new float3(.92f, .70f, .10f) : new float3(.07f, .07f, .07f), Metal = .3f, Smooth = .4f, Paint = 0, Height = .8f };
            int cx = x / 16, cy = y / 16; float lx = x % 16 - 7.5f, ly = y % 16 - 7.5f;
            float2 d = (cx + cy) % 2 == 0 ? new float2(lx + ly, lx - ly) : new float2(lx - ly, lx + ly);
            bool lozenge = math.abs(d.x) < 9 && math.abs(d.y) < 2.2f;
            return new Texel { Albedo = new float3(.55f, .56f, .55f) * (.9f + .1f * Noise(x * .1f, y * .1f, 3)), Metal = .85f, Smooth = lozenge ? .6f : .35f, Paint = .5f, Height = lozenge ? 1 : .3f };
        }

        /// <summary>The core's conduit glass: dark panels with glowing traces and nodes.</summary>
        static Texel Conduit(int x, int y)
        {
            const int n = 32; int cx = x / n, cy = y / n; int lx = x % n, ly = y % n;
            uint h = math.hash(new int2(cx, cy));
            bool horizontal = (h & 1) == 0, trace = horizontal ? math.abs(ly - 16) < 2 : math.abs(lx - 16) < 2;
            bool node = math.length(new float2(lx - 16, ly - 16)) < 5 && (h & 6) == 0;
            bool frame = lx < 2 || ly < 2;
            if (frame) return new Texel { Albedo = new float3(.12f, .14f, .15f), Metal = .8f, Smooth = .5f, Height = 1 };
            if (trace || node) return new Texel { Albedo = new float3(.55f, .95f, 1f), Smooth = .9f, Emit = node ? 1 : .8f, Height = .55f };
            return new Texel { Albedo = new float3(.03f, .06f, .07f), Smooth = .95f, Metal = .2f, Paint = .1f, Height = .5f, Emit = .05f };
        }

        /// <summary>Window panes in bronze mullions, each pane lit a little differently (someone's room behind each).</summary>
        static Texel Glass(int x, int y)
        {
            const int w = 64, hgt = 85; int px = x / w, py = y / hgt;
            bool mullion = x % w < 5 || y % hgt < 5;
            if (mullion) return new Texel { Albedo = new float3(.30f, .22f, .13f), Metal = .8f, Smooth = .5f, Height = 1 };
            float lit = .35f + .65f * Rand(px * 31 + py * 17, 1);
            return new Texel { Albedo = new float3(.85f, .78f, .62f), Smooth = .95f, Paint = .1f, Emit = lit, Height = .2f };
        }

        /// <summary>Girder truss: a frame with an X brace and rivets, dark between.</summary>
        static Texel Truss(int x, int y)
        {
            float u = x % 128, v = y % 128;
            bool frame = u < 12 || v < 12 || u > 115 || v > 115, brace = math.abs(u - v) < 7 || math.abs(u + v - 127) < 7;
            if (frame || brace)
            {
                bool rivet = frame && ((u + v) % 16) < 3 && (u < 8 || v < 8);
                return new Texel { Albedo = new float3(.40f, .38f, .34f) * (rivet ? 1.2f : 1), Metal = .8f, Smooth = .35f, Paint = .6f, Height = rivet ? 1 : .8f };
            }
            return new Texel { Albedo = new float3(.07f, .07f, .07f), Metal = .2f, Smooth = .2f, Height = 0 };
        }

        // ------------------------------------------------------------------------------------------------------------------ helpers
        static float Rand(int i, uint seed) => (math.hash(new uint2((uint)i, seed)) & 0xFFFF) / 65535f;

        /// <summary>Value noise, four octaves (x, y already scaled to the feature size).</summary>
        static float Noise(float x, float y, uint seed)
        {
            float total = 0, amp = .5f, norm = 0;
            for (int o = 0; o < 4; o++)
            {
                float f = 1 << o;
                total += amp * Value(x * f, y * f, seed + (uint)o); norm += amp; amp *= .5f;
            }
            return total / norm;
        }

        static float Value(float x, float y, uint seed)
        {
            int ix = (int)math.floor(x), iy = (int)math.floor(y); float fx = x - ix, fy = y - iy;
            float a = Corner(ix, iy, seed), b = Corner(ix + 1, iy, seed), c = Corner(ix, iy + 1, seed), d = Corner(ix + 1, iy + 1, seed);
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            return math.lerp(math.lerp(a, b, fx), math.lerp(c, d, fx), fy);
        }

        static float Corner(int x, int y, uint seed) => (math.hash(new uint3((uint)x, (uint)y, seed)) & 0xFFFF) / 65535f;

        /// <summary>Glaze crackle: thin lines on the borders of a jittered cell pattern.</summary>
        static float Crackle(int x, int y, uint seed)
        {
            const float cell = 21; float2 p = new float2(x, y) / cell; int2 i = (int2)math.floor(p);
            float best = 9, second = 9;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                int2 c = i + new int2(dx, dy);
                uint h = math.hash(new uint3((uint)c.x, (uint)c.y, seed));
                float2 jitter = new float2((h & 0xFF) / 255f, ((h >> 8) & 0xFF) / 255f);
                float d = math.distance(p, c + jitter);
                if (d < best) { second = best; best = d; } else if (d < second) second = d;
            }
            return 1 - math.smoothstep(0, .06f, second - best);
        }

        static Color[] Normals(float[] h, float strength)
        {
            var n = new Color[Size * Size];
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                float l = h[(x + Size - 1) % Size + y * Size], r = h[(x + 1) % Size + y * Size];
                float d = h[x + (y + Size - 1) % Size * Size], u = h[x + (y + 1) % Size * Size];
                float3 v = math.normalize(new float3((l - r) * strength, (d - u) * strength, 1));
                n[x + y * Size] = new Color(v.x * .5f + .5f, v.y * .5f + .5f, 0, 1);
            }
            return n;
        }

        static Texture2D Texture(Color[] pixels, bool linear)
        {
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, true, linear);
            t.SetPixels(pixels); t.Apply(true, false);
            return t;
        }

        static void Copy(Texture2D source, Texture2DArray array, int layer, TextureFormat format)
        {
            try
            {
                EditorUtility.CompressTexture(source, format, TextureCompressionQuality.Normal);
                if (source.mipmapCount != array.mipmapCount) throw new InvalidOperationException($"mip count {source.mipmapCount} != {array.mipmapCount}");
                for (int mip = 0; mip < source.mipmapCount; mip++) array.SetPixelData(source.GetPixelData<byte>(mip), mip, layer);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        static Color Hex(string value) => ColorUtility.TryParseHtmlString("#" + value, out var colour) ? colour : throw new InvalidOperationException(value);
    }
}
