using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Game.Presentation.Services
{
    /// <summary>ADR-0122 cruise, the feel of speed (owner, 2026-10-21: "increase FOV and have a shader effect to make it feel like you're
    /// going really really fast"; then "the lines get too strong, and it doesn't really feel like you're going fast"; then "i didn't mean
    /// just make them all dull, i meant lets make it feel like you're actually going really fast"). Three layers, all moving:
    /// - speed lines rushing out from the middle of the view to its edges (`Game/Vfx/CruiseSpeedLines`, over the whole view; the middle
    ///   stays clear for the ship), faster and brighter with speed and flaring when the drive engages;
    /// - bright dust dashes running past the ship in the world. Dust at the true speed crossed the view in a frame or two and read as a
    ///   still star tunnel, so it flows past at a felt speed the eye can follow, rising with the true one;
    /// - a vignette (a global volume).
    /// All of it scales with the cruise blend and the speed; the camera rig owns this and ticks it after it poses the camera.</summary>
    internal sealed class CruiseEffects : IDisposable
    {
        const float Cell = 22, ReachSide = 170, ReachUp = 60, Ahead = 70, NearSkip = 5, Wrap = Cell * 65536;
        readonly List<Matrix4x4> streaks = new();
        readonly Matrix4x4[] batch = new Matrix4x4[1023];
        readonly Material material, lines;
        readonly Mesh quad, screen;
        readonly GameObject volumeObject;
        readonly Volume volume;
        readonly VolumeProfile profile;
        float3 drift;   // the dust's frame: it travels with the ship at the true speed less the felt one
        static readonly int IntensityId = Shader.PropertyToID("_Intensity"), RateId = Shader.PropertyToID("_Rate"),
            AspectId = Shader.PropertyToID("_Aspect");

        internal CruiseEffects(Transform parent)
        {
            var shader = Shader.Find("Game/Vfx/CruiseStreak");
            if (shader != null) material = new Material(shader) { name = "cruise streaks", enableInstancing = true };
            var lineShader = Shader.Find("Game/Vfx/CruiseSpeedLines");
            if (lineShader != null) lines = new Material(lineShader) { name = "cruise speed lines" };
            quad = new Mesh { name = "cruise streak" };
            quad.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(.5f, 0, -.5f), new Vector3(.5f, 0, .5f), new Vector3(-.5f, 0, .5f) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.bounds = new Bounds(Vector3.zero, Vector3.one);
            screen = new Mesh { name = "cruise speed lines" };   // a unit quad in XY, facing the camera
            screen.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            screen.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            screen.bounds = new Bounds(Vector3.zero, Vector3.one * 1000);
            profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "cruise";
            // no lens distortion (it squashed the planets into ovals) and no chromatic aberration (it fringed ORRO's ring in rainbows)
            var vignette = profile.Add<Vignette>(); vignette.intensity.Override(.32f); vignette.smoothness.Override(.55f);
            volumeObject = new GameObject("Cruise volume"); volumeObject.transform.SetParent(parent, false);
            volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 100; volume.weight = 0; volume.sharedProfile = profile;
        }

        internal int StreakCount => streaks.Count;
        /// <summary>The dust's speed past the camera at a true speed: 320 m/s at 1 km/s, 920 at 6, 1160 at 8.</summary>
        internal static float FeltSpeed(float speed) => math.min(speed, 200 + speed * .12f);
        /// <summary>A floating-origin shift moves the dust with everything else.</summary>
        internal void Warp(float3 delta) => drift += delta;

        /// <summary>`blend` 0..1 into the cruise view, `speed` m/s (eased), `velocity` the ship's (render space), `punch` 0..1 the flare
        /// as the drive engages.</summary>
        internal void Tick(float blend, float speed, float3 velocity, Camera camera, float dt, float punch)
        {
            streaks.Clear();
            float pace = math.saturate((speed - 300) / 4700), amount = blend * math.saturate((speed - 300) / 1700);
            if (volume != null) volume.weight = amount;
            if (camera == null || amount < .01f && punch < .01f) return;
            float3 eye = camera.transform.position, forward = camera.transform.forward;
            if (lines != null)
            {
                // the quad just past the near clip, filling the view
                float z = camera.nearClipPlane * 1.5f, h = 2 * z * math.tan(math.radians(camera.fieldOfView * .5f));
                var m = camera.transform.localToWorldMatrix * Matrix4x4.TRS(new Vector3(0, 0, z), Quaternion.identity, new Vector3(h * camera.aspect, h, 1));
                lines.SetFloat(IntensityId, amount * (.55f + .6f * pace) + punch * 1.2f);
                lines.SetFloat(RateId, .7f + 2.3f * pace + punch * 1.5f);
                lines.SetFloat(AspectId, camera.aspect);
                Graphics.RenderMesh(new RenderParams(lines) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, camera = camera }, screen, 0, m);
            }
            if (material == null || amount < .01f) return;
            float3 dir = math.lengthsq(velocity) > 1 ? math.normalize(velocity) : forward;
            float felt = FeltSpeed(speed);
            drift += dir * (speed - felt) * dt;
            drift -= math.floor(drift / Wrap) * Wrap;   // a whole number of cells: the lattice stays put
            float length = math.clamp(felt * .06f, 4, 70);   // about four frames of the felt motion
            float3 centre = eye + new float3(forward.x, 0, forward.z) * Ahead - drift;
            int3 c0 = (int3)math.floor(centre / Cell);
            int nx = (int)math.ceil(ReachSide / Cell), ny = (int)math.ceil(ReachUp / Cell);
            for (int x = -nx; x <= nx; x++) for (int y = -ny; y <= ny; y++) for (int z = -nx; z <= nx; z++)
            {
                int3 cell = c0 + new int3(x, y, z);
                uint h = math.hash(cell);
                if (h % 2 != 0) continue;
                var random = new Unity.Mathematics.Random(h | 1);
                float3 p = ((float3)cell + random.NextFloat3()) * Cell + drift;
                float3 toEye = eye - p; float d = math.length(toEye);
                if (d < NearSkip) continue;
                float3 right = math.cross(dir, toEye);
                if (math.lengthsq(right) < 1e-4f) continue;
                right = math.normalize(right);
                float width = .16f + d * .003f;
                var m = new Matrix4x4();
                m.SetColumn(0, new Vector4(right.x * width, right.y * width, right.z * width, 0));
                float3 normal = math.cross(right, dir); m.SetColumn(1, new Vector4(normal.x, normal.y, normal.z, 0));
                float len = length * random.NextFloat(.6f, 1.3f);
                m.SetColumn(2, new Vector4(dir.x * len, dir.y * len, dir.z * len, 0));
                m.SetColumn(3, new Vector4(p.x, p.y, p.z, 1));
                streaks.Add(m);
            }
            material.SetFloat(IntensityId, amount * (.6f + .4f * pace));
            var rp = new RenderParams(material) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, camera = camera };
            for (int start = 0; start < streaks.Count; start += batch.Length)
            {
                int n = Math.Min(batch.Length, streaks.Count - start);
                streaks.CopyTo(start, batch, 0, n);
                Graphics.RenderMeshInstanced(rp, quad, 0, batch, n);
            }
        }

        public void Dispose()
        {
            Destroy(material); Destroy(lines); Destroy(quad); Destroy(screen); Destroy(profile); Destroy(volumeObject);
        }
        static void Destroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
