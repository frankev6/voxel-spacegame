#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.Galaxy.Components;
using Game.Simulation.Flight;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>DEV-028, exploration slice phase 1 (docs/design/exploration/exploration-slice.md, the approved map
    /// img/hollow_system_slice.png): the Hollow's star system round the playground. The star and four planets lie below the flight plane,
    /// each at its own depth; the Scatter (an asteroid belt) slows cruise; the Haze is a nebula; the lane gate waits at the edge.
    /// Map coordinates are km with the star at the origin (x east, y north = Hollow-local +z); VESHA's tether stays where the feel
    /// prototype put it. Bodies far from the camera are drawn scaled toward it (angular size kept) so they fit inside the camera's far
    /// clip. Plain GameObjects, placed every frame from double-precision positions so they follow the floating origin, just before the
    /// frame renders (from the final camera pose: placed a frame early, they shook with the frame time while cruising).</summary>
    internal sealed class HollowSystem
    {
        internal enum Kind : byte { Ocean, Barren, Gas, Ice, Star }
        internal readonly struct Body
        {
            internal readonly string Name, Line; internal readonly Kind Kind; internal readonly float2 MapKm; internal readonly float RadiusKm, DepthKm;
            internal Body(string name, Kind kind, float2 mapKm, float radiusKm, float depthKm, string line)
            { Name = name; Kind = kind; MapKm = mapKm; RadiusKm = radiusKm; DepthKm = depthKm; Line = line; }
        }
        internal static readonly float2 VeshaMapKm = new float2(-185, -30);
        /// <summary>The approved map. VESHA's radius and depth follow HollowOrbit's live tuning.</summary>
        internal static readonly Body[] Bodies =
        {
            new Body("VESHA", Kind.Ocean, VeshaMapKm, 80, 2.5f, "LOW ORBIT"),
            new Body("CINDER", Kind.Barren, new float2(60, 45), 18, 4, "HIGH ORBIT"),
            new Body("ORRO", Kind.Gas, new float2(200, -112), 110, 6, "OVER THE CLOUD DECK"),
            new Body("PALE", Kind.Ice, new float2(-70, 196), 26, 3, "HIGH ORBIT"),
            new Body("THE STAR", Kind.Star, float2.zero, 20, 40, "HEAT ZONE  ·  HULLS BURN"),
        };
        /// <summary>Places to come (phase 3) that the edge markers and goto already know.</summary>
        internal static readonly (string id, string name, float2 mapKm)[] Sites =
        {
            ("gate", "LANE GATE", new float2(-292, 150)), ("hauler", "THE LOST HAULER", new float2(132, 64)),
            ("colony", "THE DEAD COLONY", new float2(60, 45)),
        };
        internal const float BeltInnerKm = 121, BeltOuterKm = 135, BeltCruiseCap = 800, HeatKm = 30;
        internal static readonly float2 HazeMapKm = new float2(205, 128); internal const float HazeRadiusKm = 118;

        /// <summary>Hollow-local metres of a map point (km, star at the origin).</summary>
        internal static float2 Local(float2 mapKm) => HollowOrbit.Tether + (mapKm - VeshaMapKm) * 1000;
        static float Radius(Body b) => b.Kind == Kind.Ocean ? HollowOrbit.Radius : b.RadiusKm * 1000;
        static float Depth(Body b) => b.Kind == Kind.Ocean ? HollowOrbit.Depth : b.DepthKm * 1000;

        // The camera's far clip is 1000 km (CameraConfig): this system, its rings and air included, draws at true scale. Anything
        // farther is scaled toward the eye (which keeps its look) and its distance compressed: true up to Knee, then easing toward
        // MaxView. The curve only rises, so a nearer body stays in front.
        const double Knee = 750000, MaxView = 900000, Ease = 2000000;
        const int Rings = 300, Segments = 288;
        readonly List<UnityEngine.Object> owned = new();
        readonly Transform[] surfaces = new Transform[Bodies.Length], airs = new Transform[Bodies.Length], rings = new Transform[Bodies.Length];
        readonly Material[] surfaceMaterials = new Material[Bodies.Length], airMaterials = new Material[Bodies.Length];
        GameObject root;
        double3 placeOrigin; FrameOrigin placeFrame; bool placeReady;
        Mesh cap, rock, card;
        Material rockMaterial, hazeMaterial, dustMaterial;
        Entity beltZone = Entity.Null;
        static readonly int WorldScale = Shader.PropertyToID("_WorldScale"), RadiusId = Shader.PropertyToID("_Radius"),
            ScaleHeightId = Shader.PropertyToID("_ScaleHeight");

        internal bool Built => root != null;

        internal void Build()
        {
            if (root != null) return;
            var surface = Shader.Find("Game/Orbit/Planet"); var glow = Shader.Find("Game/Orbit/Atmosphere");
            var ringShader = Shader.Find("Game/Orbit/Ring"); var haze = Shader.Find("Game/Orbit/Haze"); var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (surface == null || glow == null) { Debug.LogWarning("HollowSystem: orbit shaders missing; no bodies"); return; }
            root = new GameObject("Hollow system (bodies)");
            RenderPipelineManager.beginContextRendering += OnRender;
            cap = Own(Cap()); rock = Own(Rock()); card = Own(Card());
            for (int i = 0; i < Bodies.Length; i++)
            {
                var b = Bodies[i];
                if (b.Kind == Kind.Star)
                {
                    var core = Own(new Material(unlit != null ? unlit : surface) { name = "star core" });
                    core.SetColor("_BaseColor", new Color(9f, 6.2f, 3.2f));
                    surfaceMaterials[i] = core;
                    var corona = airMaterials[i] = Own(new Material(glow) { name = "star corona" });
                    corona.SetColor("_Glow", new Color(6f, 3.4f, 1.2f)); corona.SetColor("_Dusk", new Color(5f, 2.2f, .7f));
                    corona.SetFloat("_ScaleHeight", 2600);
                }
                else
                {
                    surfaceMaterials[i] = Own(new Material(surface) { name = b.Name + " surface" });
                    airMaterials[i] = Own(new Material(glow) { name = b.Name + " air" });
                    Palette(b, surfaceMaterials[i], airMaterials[i]);
                }
                surfaces[i] = Sphere(b.Name + " surface", surfaceMaterials[i]);
                airs[i] = Sphere(b.Name + " air", airMaterials[i]);
                if (b.Kind == Kind.Gas && ringShader != null)
                {
                    var ringMaterial = Own(new Material(ringShader) { name = b.Name + " ring" });
                    var go = new GameObject(b.Name + " ring"); go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = Own(Annulus(.72f));
                    var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = ringMaterial; Quiet(r);
                    rings[i] = go.transform;
                }
            }
            if (lit != null)
            {
                rockMaterial = Own(new Material(lit) { name = "scatter rock", enableInstancing = true });
                rockMaterial.SetColor("_BaseColor", new Color(.24f, .21f, .19f)); rockMaterial.SetFloat("_Smoothness", .15f);
            }
            if (haze != null)
            {
                hazeMaterial = Own(new Material(haze) { name = "haze", enableInstancing = true });
                hazeMaterial.SetFloat("_Density", 1.1f);
                dustMaterial = Own(new Material(haze) { name = "scatter dust", enableInstancing = true });
                dustMaterial.SetColor("_Color", new Color(.32f, .24f, .17f)); dustMaterial.SetColor("_Color2", new Color(.22f, .2f, .18f));
                dustMaterial.SetFloat("_Density", .5f);
            }
        }

        static void Palette(in Body b, Material m, Material air)
        {
            air.SetFloat("_Radius", Radius(b));
            switch (b.Kind)
            {
                case Kind.Barren:   // scorched rock close to the star: no sea, no cloud, no lights
                    m.SetFloat("_Sea", 0); m.SetFloat("_Clouds", 0); m.SetColor("_City", Color.black);
                    m.SetColor("_Beach", new Color(.30f, .17f, .10f)); m.SetColor("_Dry", new Color(.36f, .22f, .13f)); m.SetColor("_Lowland", new Color(.22f, .13f, .09f));
                    m.SetColor("_Highland", new Color(.16f, .12f, .10f)); m.SetColor("_Peak", new Color(.45f, .38f, .32f)); m.SetColor("_Haze", new Color(.5f, .28f, .14f));
                    m.SetFloat("_Scale", 9); m.SetFloat("_HazeDistance", 60000); m.SetFloat("_Seed", 11.3f);
                    air.SetColor("_Glow", new Color(.5f, .25f, .1f)); air.SetColor("_Dusk", new Color(.6f, .2f, .05f)); air.SetFloat("_ScaleHeight", 160);
                    break;
                case Kind.Gas:
                    m.SetFloat("_Bands", 1); m.SetFloat("_Clouds", 0); m.SetColor("_City", Color.black);
                    m.SetColor("_Beach", new Color(.62f, .48f, .31f)); m.SetColor("_Dry", new Color(.43f, .29f, .17f));
                    m.SetColor("_Highland", new Color(.74f, .64f, .50f)); m.SetColor("_Peak", new Color(.80f, .72f, .58f)); m.SetColor("_Haze", new Color(.62f, .48f, .32f));
                    m.SetFloat("_Scale", 18); m.SetFloat("_HazeDistance", 90000); m.SetFloat("_Seed", 4.2f);
                    air.SetColor("_Glow", new Color(1.1f, .75f, .4f)); air.SetColor("_Dusk", new Color(1.2f, .45f, .15f)); air.SetFloat("_ScaleHeight", 1400);
                    break;
                case Kind.Ice:
                    m.SetFloat("_Sea", .28f); m.SetFloat("_Clouds", .18f); m.SetColor("_City", Color.black);
                    m.SetColor("_Ocean", new Color(.32f, .42f, .52f)); m.SetColor("_Shallow", new Color(.55f, .66f, .75f));
                    m.SetColor("_Beach", new Color(.70f, .76f, .82f)); m.SetColor("_Dry", new Color(.62f, .70f, .78f)); m.SetColor("_Lowland", new Color(.74f, .80f, .86f));
                    m.SetColor("_Highland", new Color(.84f, .88f, .92f)); m.SetColor("_Peak", new Color(.95f, .97f, 1f)); m.SetColor("_Haze", new Color(.45f, .6f, .85f));
                    m.SetFloat("_Scale", 12); m.SetFloat("_HazeDistance", 70000); m.SetFloat("_Seed", 7.9f);
                    air.SetColor("_Glow", new Color(.35f, .6f, 1.2f)); air.SetFloat("_ScaleHeight", 300);
                    break;
            }
        }

        /// <summary>Every frame: the belt's rocks and dust and the Haze's cards round the camera; the bodies are placed when the frame
        /// renders (<see cref="Place"/>).</summary>
        internal void Tick(double3 hollowOrigin, FrameOrigin frame, Camera camera)
        {
            if (root == null || camera == null) return;
            placeOrigin = hollowOrigin; placeFrame = frame; placeReady = true;
            Place(camera);
            Scatter(hollowOrigin, frame, camera);
        }

        void OnRender(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (root != null && placeReady) Place(Camera.main);
        }

        /// <summary>Bodies at their places relative to the floating origin, from the camera's pose now; far ones scaled toward it.</summary>
        void Place(Camera camera)
        {
            if (camera == null) return;
            double3 hollowOrigin = placeOrigin; var frame = placeFrame;
            double3 eye = (double3)(float3)camera.transform.position;
            for (int i = 0; i < Bodies.Length; i++)
            {
                var b = Bodies[i];
                // the air reaches nine scale heights up, where its glow is gone, but stays under the flight plane: the shell draws its
                // outside only, so a camera inside it would lose the glow (the shader fades it out before the shell ends)
                float radius = Radius(b), depth = Depth(b);
                float air = b.Kind == Kind.Star ? radius * 1.8f
                    : radius + math.min(math.max(b.Kind == Kind.Gas ? 5000 : 1800, 9 * airMaterials[i].GetFloat(ScaleHeightId)), depth - 300);
                var local = Local(b.MapKm);
                double3 centre = hollowOrigin + new double3(local.x, -(radius + depth), local.y) - frame.OriginInStarSystem;
                double d = math.distance(centre, eye), tangent = math.sqrt(math.max(1, d * d - (double)radius * radius));
                float k = (float)(ViewDistance(tangent) / tangent);
                var at = (Vector3)(float3)(eye + (centre - eye) * k);
                surfaces[i].position = airs[i].position = at;
                surfaces[i].localScale = Vector3.one * (radius * k); airs[i].localScale = Vector3.one * (air * k);
                if (b.Kind != Kind.Star) surfaceMaterials[i].SetFloat(WorldScale, k);
                airMaterials[i].SetFloat(WorldScale, k); airMaterials[i].SetFloat(RadiusId, radius);
                if (rings[i] != null) { rings[i].position = at; rings[i].localScale = Vector3.one * (radius * 1.75f * k); }
            }
        }

        /// <summary>Where a body whose limb is <paramref name="tangent"/> metres away is drawn: true up to Knee, then easing toward
        /// MaxView. Monotonic, so depth order between bodies holds.</summary>
        internal static double ViewDistance(double tangent) =>
            tangent <= Knee ? tangent : Knee + (MaxView - Knee) * (1 - math.exp(-(tangent - Knee) / Ease));

        // ------------------------------------------------------------------------------------------------ belt rocks, dust and haze
        readonly List<Matrix4x4> rocks = new(), dusts = new(), hazes = new();
        readonly Matrix4x4[] batch = new Matrix4x4[1023];
        const float RockCell = 400, RockReach = 5000, CardCell = 1600, CardReach = 14000;

        void Scatter(double3 hollowOrigin, FrameOrigin frame, Camera camera)
        {
            rocks.Clear(); dusts.Clear(); hazes.Clear();
            double3 eyeU = (double3)(float3)camera.transform.position + frame.OriginInStarSystem;   // universe
            double2 eyeLocal = eyeU.xz - hollowOrigin.xz;                                             // Hollow-local metres
            double2 star = (double2)Local(float2.zero), haze = (double2)Local(HazeMapKm);
            double eyeFromStar = math.distance(eyeLocal, star) / 1000;
            if (rockMaterial != null && eyeFromStar > BeltInnerKm - 6 && eyeFromStar < BeltOuterKm + 6)
            {
                int2 c0 = (int2)math.floor(eyeLocal / RockCell); int n = (int)math.ceil(RockReach / RockCell);
                for (int x = -n; x <= n; x++) for (int z = -n; z <= n; z++)
                {
                    int2 cell = c0 + new int2(x, z);
                    double2 centre = ((double2)cell + .5) * RockCell;
                    double fromStar = math.distance(centre, star) / 1000;
                    if (fromStar < BeltInnerKm || fromStar > BeltOuterKm) continue;
                    uint h = math.hash(cell);
                    var random = new Unity.Mathematics.Random(h | 1);
                    int count = (int)(h % 4);
                    for (int i = 0; i < count; i++)
                    {
                        double2 p = centre + (double2)(random.NextFloat2() - .5f) * RockCell;
                        // above or below the flight plane: the belt surrounds you, it never sits in the plane (real rock grids, phase 3)
                        float y = (random.NextBool() ? 1 : -1) * random.NextFloat(60, 320), size = random.NextFloat(6, 42);
                        var world = (float3)(hollowOrigin + new double3(p.x, y, p.y) - frame.OriginInStarSystem);
                        rocks.Add(Matrix4x4.TRS(world, random.NextQuaternionRotation(), new Vector3(size, size * random.NextFloat(.6f, 1), size * random.NextFloat(.7f, 1.2f))));
                    }
                    if (dustMaterial != null && (h >> 8) % 3 == 0)
                        dusts.Add(Card(hollowOrigin, frame, camera, centre, (h >> 12) % 2 == 0 ? -120 : 140, 900 + (h >> 16) % 700));
                }
            }
            double fromHaze = math.distance(eyeLocal, haze) / 1000;
            if (hazeMaterial != null && fromHaze < HazeRadiusKm + CardReach / 1000)
            {
                int2 c0 = (int2)math.floor(eyeLocal / CardCell); int n = (int)math.ceil(CardReach / CardCell);
                for (int x = -n; x <= n; x++) for (int z = -n; z <= n; z++)
                {
                    int2 cell = c0 + new int2(x, z);
                    double2 centre = ((double2)cell + .5) * CardCell;
                    double inside = 1 - math.distance(centre, haze) / 1000 / HazeRadiusKm;   // 1 at the core, 0 at the edge
                    if (inside <= 0) continue;
                    uint h = math.hash(cell + 7);
                    if ((h % 100) / 100.0 > .25 + inside * .9) continue;   // thinner toward the edge
                    float y = (int)(h >> 8) % 900 - 450;
                    hazes.Add(Card(hollowOrigin, frame, camera, centre + new double2((h >> 4) % 600, (h >> 14) % 600) - 300, y, 2400 + (h >> 18) % 2600));
                }
            }
            Draw(rock, rockMaterial, rocks, true); Draw(card, dustMaterial, dusts, false); Draw(card, hazeMaterial, hazes, false);
        }

        /// <summary>A soft card facing the camera at a Hollow-local point and height.</summary>
        static Matrix4x4 Card(double3 hollowOrigin, FrameOrigin frame, Camera camera, double2 local, float y, float size)
        {
            var world = (float3)(hollowOrigin + new double3(local.x, y, local.y) - frame.OriginInStarSystem);
            var facing = Quaternion.LookRotation(camera.transform.forward, camera.transform.up) * Quaternion.Euler(-90, 0, 0);
            return Matrix4x4.TRS(world, facing, new Vector3(size, 1, size));
        }

        void Draw(Mesh mesh, Material material, List<Matrix4x4> list, bool shadows)
        {
            if (material == null || list.Count == 0) return;
            var p = new RenderParams(material) { shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows = shadows };
            for (int start = 0; start < list.Count; start += batch.Length)
            {
                int n = Math.Min(batch.Length, list.Count - start);
                list.CopyTo(start, batch, 0, n);
                Graphics.RenderMeshInstanced(p, mesh, 0, batch, n);
            }
        }

        // ------------------------------------------------------------------------------------------------ cruise zone, markers, places
        /// <summary>The Scatter's cruise zone (ADR-0122): a ring round the star that caps cruise. Created once per playground build.</summary>
        internal void SpawnZones(EntityManager em, double3 hollowOrigin, FrameOrigin frame)
        {
            if (beltZone != Entity.Null && em.Exists(beltZone)) return;
            var star = Local(float2.zero);
            beltZone = em.CreateEntity(typeof(LocalTransform), typeof(CruiseZone));
            em.SetComponentData(beltZone, LocalTransform.FromPosition((float3)(hollowOrigin + new double3(star.x, 0, star.y) - frame.OriginInStarSystem)));
            em.SetComponentData(beltZone, new CruiseZone { InnerRadius = BeltInnerKm * 1000, OuterRadius = BeltOuterKm * 1000, MaxSpeed = BeltCruiseCap });
        }

        readonly double3[] markerPositions = new double3[8];
        readonly string[] markerNames = new string[8];
        /// <summary>Edge markers for the bodies (not the one you are over) and the gate.</summary>
        internal int Markers(double3 hollowOrigin, float2 at, out ReadOnlySpan<double3> positions, out ReadOnlySpan<string> names)
        {
            int n = 0;
            foreach (var b in Bodies)
            {
                if (b.Kind == Kind.Star) continue;
                var local = Local(b.MapKm);
                if (math.distance(at, local) < Radius(b)) continue;
                markerPositions[n] = hollowOrigin + new double3(local.x, 0, local.y); markerNames[n++] = b.Name;
            }
            var gate = Local(Sites[0].mapKm);
            markerPositions[n] = hollowOrigin + new double3(gate.x, 0, gate.y); markerNames[n++] = Sites[0].name;
            positions = markerPositions.AsSpan(0, n); names = markerNames.AsSpan(0, n);
            return n;
        }

        /// <summary>Where a Hollow-local point is in the system, for the title card: -1 nowhere named, else an index into <see cref="Place"/>.</summary>
        internal static int Locate(float2 at)
        {
            float fromStar = math.distance(at, Local(float2.zero)) / 1000;
            if (fromStar < HeatKm) return Bodies.Length - 1;
            if (math.distance(at, Local(HazeMapKm)) / 1000 < HazeRadiusKm * .85f) return Bodies.Length;
            if (fromStar > BeltInnerKm && fromStar < BeltOuterKm) return Bodies.Length + 1;
            for (int i = 0; i < Bodies.Length - 1; i++) if (math.distance(at, Local(Bodies[i].MapKm)) < Radius(Bodies[i])) return i;
            return -1;
        }
        internal static string Place(int index) => index < 0 ? "" : index < Bodies.Length ? Bodies[index].Name + "  ·  " + Bodies[index].Line
            : index == Bodies.Length ? "THE HAZE  ·  NEBULA  ·  SENSORS HALVED" : "THE SCATTER  ·  ASTEROID BELT";

        /// <summary>The Hollow-local point of a body (its top) or a site, by goto name.</summary>
        internal static bool TryLocal(string id, out float2 local)
        {
            foreach (var b in Bodies) if (string.Equals(b.Name.Replace("THE ", ""), id, StringComparison.OrdinalIgnoreCase)) { local = Local(b.MapKm); return true; }
            foreach (var s in Sites) if (s.id == id) { local = Local(s.mapKm); return true; }
            local = default; return false;
        }

        /// <summary>A place to go: a body's top (from the south), a site, the belt or the Haze. Hollow-local metres and a yaw.</summary>
        internal static bool TryGoTo(string id, out float2 at, out float yaw)
        {
            at = default; yaw = 0;
            foreach (var b in Bodies)
                if (string.Equals(b.Name.Replace("THE ", ""), id, StringComparison.OrdinalIgnoreCase))
                {   // a few km short of the top heading north over it; for the star, far enough out that it sits ahead, not below
                    float back = b.Kind == Kind.Star ? 120000 : math.min(Radius(b) * .35f, 9000);
                    at = Local(b.MapKm) - new float2(0, back); return true;
                }
            foreach (var s in Sites)
                if (s.id == id) { at = Local(s.mapKm) - new float2(0, 3000); return true; }
            if (id == "scatter" || id == "belt") { at = Local(new float2(-118, -18)) ; yaw = math.radians(-60); return true; }
            if (id == "haze") { at = Local(HazeMapKm - new float2(HazeRadiusKm * .5f, 0)); yaw = math.PI / 2; return true; }
            return false;
        }

        // ------------------------------------------------------------------------------------------------ meshes
        Transform Sphere(string name, Material material)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = cap;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; Quiet(r);
            return go.transform;
        }
        static void Quiet(MeshRenderer r)
        {
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>A unit sphere from +Y, rings crowding toward the top where the camera is closest (the coast); closed at the bottom
        /// so a body seen side-on is whole.</summary>
        static Mesh Cap()
        {
            var vertices = new Vector3[1 + Rings * Segments];
            var triangles = new int[(Segments + (Rings - 1) * Segments * 2) * 3];
            vertices[0] = Vector3.up;
            for (int r = 0; r < Rings; r++)
            {
                float t = (r + 1f) / Rings, polar = math.PI * t * t;
                for (int s = 0; s < Segments; s++)
                {
                    float az = 2 * math.PI * s / Segments;
                    vertices[1 + r * Segments + s] = new Vector3(math.sin(polar) * math.cos(az), math.cos(polar), math.sin(polar) * math.sin(az));
                }
            }
            int k = 0;
            for (int s = 0; s < Segments; s++) { triangles[k++] = 0; triangles[k++] = 1 + (s + 1) % Segments; triangles[k++] = 1 + s; }
            for (int r = 0; r < Rings - 1; r++)
                for (int s = 0; s < Segments; s++)
                {
                    int a = 1 + r * Segments + s, b = 1 + r * Segments + (s + 1) % Segments, c = a + Segments, d = b + Segments;
                    triangles[k++] = a; triangles[k++] = b; triangles[k++] = c;
                    triangles[k++] = b; triangles[k++] = d; triangles[k++] = c;
                }
            var mesh = new Mesh { name = "system sphere", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices; mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2);
            return mesh;
        }

        /// <summary>A flat ring in XZ, outer radius 1.</summary>
        static Mesh Annulus(float inner)
        {
            const int n = 256;
            var vertices = new Vector3[n * 2]; var triangles = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                float a = 2 * math.PI * i / n; var dir = new Vector3(math.cos(a), 0, math.sin(a));
                vertices[i * 2] = dir * inner; vertices[i * 2 + 1] = dir;
                int j = (i + 1) % n, t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = j * 2; triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = j * 2; triangles[t + 4] = j * 2 + 1; triangles[t + 5] = i * 2 + 1;
            }
            var mesh = new Mesh { name = "ring" }; mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A lumpy rock: an icosphere pushed in and out by a few fixed waves.</summary>
        static Mesh Rock()
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var source = sphere.GetComponent<MeshFilter>().sharedMesh;
            var vertices = source.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i].normalized;
                float bump = 1 + .22f * math.sin(v.x * 5.1f + 1.3f) * math.cos(v.y * 4.3f) + .14f * math.sin(v.z * 7.7f + v.x * 3.1f);
                vertices[i] = v * .5f * bump;
            }
            var mesh = new Mesh { name = "scatter rock" }; mesh.vertices = vertices; mesh.triangles = source.triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            UnityEngine.Object.Destroy(sphere);
            return mesh;
        }

        /// <summary>A unit quad in XZ (the haze shader reads its object-space XZ).</summary>
        static Mesh Card()
        {
            var mesh = new Mesh { name = "haze card" };
            mesh.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(.5f, 0, -.5f), new Vector3(.5f, 0, .5f), new Vector3(-.5f, 0, .5f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        T Own<T>(T o) where T : UnityEngine.Object { owned.Add(o); return o; }

        internal void Dispose(EntityManager? em)
        {
            RenderPipelineManager.beginContextRendering -= OnRender; placeReady = false;
            if (root != null) UnityEngine.Object.Destroy(root);
            foreach (var o in owned) if (o != null) UnityEngine.Object.Destroy(o);
            owned.Clear(); root = null; rockMaterial = hazeMaterial = dustMaterial = null;
            Array.Clear(surfaces, 0, surfaces.Length); Array.Clear(airs, 0, airs.Length); Array.Clear(rings, 0, rings.Length);
            Array.Clear(surfaceMaterials, 0, surfaceMaterials.Length); Array.Clear(airMaterials, 0, airMaterials.Length);
            if (em.HasValue && beltZone != Entity.Null && em.Value.Exists(beltZone)) em.Value.DestroyEntity(beltZone);
            beltZone = Entity.Null;
        }
    }
}
#endif
