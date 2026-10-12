using System.Collections;
using Game.Presentation.Services;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Presentation.Environment
{
    /// <summary>M1 distant Nocturne placeholder and stationary, cosmetic flight-reference dust.
    /// SYS-011 replaces the scene-authored sphere and backdrop; these particles never enter simulation or saves.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class NocturneFlightEnvironment : MonoBehaviour
    {
        const int Count = 1400;
        const float HalfExtent = 96;
        [SerializeField] Material distantSky;
        [SerializeField] Material debrisMaterial;
        readonly Vector3[] anchors = new Vector3[Count];
        readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[Count];
        Camera camera;
        Skybox skybox;
        Material previousSky;
        bool previousSkyEnabled, ownsSkybox;
        CameraClearFlags previousClear;
        ParticleSystem dust;

        IEnumerator Start()
        {
            // A direct World reload can run Start before the prior environment's deferred Destroy(Skybox).
            // Bind after that destruction boundary, so this scene never borrows a component queued for removal (FND-019).
            yield return null;
            if (!Game.Core.Services.TryGet<CameraRigService>(out var rig)) yield break;
            camera = rig.Camera;
            previousClear = camera.clearFlags;
            skybox = camera.GetComponent<Skybox>();
            ownsSkybox = skybox == null;
            if (ownsSkybox) skybox = camera.gameObject.AddComponent<Skybox>();
            previousSky = skybox.material; previousSkyEnabled = skybox.enabled;
            skybox.material = distantSky; skybox.enabled = true;
            camera.clearFlags = CameraClearFlags.Skybox;

            var obj = new GameObject("Nocturne spatial debris");
            obj.transform.SetParent(transform, false);
            dust = obj.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = dust.main;
            main.playOnAwake = false; main.loop = false; main.maxParticles = Count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.simulationSpeed = 0; main.startSpeed = 0;
            var emission = dust.emission; emission.enabled = false;
            var shape = dust.shape; shape.enabled = false;
            dust.useAutoRandomSeed = false; dust.randomSeed = 418;
            var renderer = dust.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = debrisMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = .015f;
            var random = new Unity.Mathematics.Random(418);
            for (int i = 0; i < Count; i++)
            {
                anchors[i] = random.NextFloat3(-HalfExtent, HalfExtent);
                particles[i] = new ParticleSystem.Particle
                {
                    position = anchors[i], startSize = random.NextFloat(.055f, .19f),
                    startLifetime = 1000000, remainingLifetime = 1000000,
                    rotation = random.NextFloat(0, 360), randomSeed = (uint)i + 1
                };
            }
            dust.Play();
            LateUpdate();
        }

        void LateUpdate()
        {
            if (camera == null || dust == null) return;
            Vector3 origin = camera.transform.position;
            for (int i = 0; i < Count; i++)
            {
                // The lattice is fixed in world space. Only invisible boundary copies recycle;
                // camera motion never adds velocity to a particle or drags the field along.
                float3 delta = (float3)(anchors[i] - origin);
                delta -= math.floor((delta + HalfExtent) / (2 * HalfExtent)) * (2 * HalfExtent);
                particles[i].position = origin + (Vector3)delta;
                float edge = math.cmax(math.abs(delta));
                float fade = math.saturate((HalfExtent - edge) / 22) * math.saturate((math.length(delta) - 3) / 5);
                particles[i].startColor = new Color(.58f, .61f, .55f, fade * .75f);
            }
            dust.SetParticles(particles, Count);
        }

        void OnDestroy()
        {
            if (camera != null) camera.clearFlags = previousClear;
            if (skybox != null)
            {
                if (ownsSkybox) Destroy(skybox);
                else { skybox.material = previousSky; skybox.enabled = previousSkyEnabled; }
            }
            if (dust != null) Destroy(dust.gameObject);
        }
    }
}
