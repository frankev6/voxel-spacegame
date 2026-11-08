using Game.Data.Definitions;
using Game.Simulation.Weapons;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Presentation.Vfx
{
    /// <summary>SPEC-63 for SPEC-21 §4.6d (WPN-019): draws charge beams from ChargeBeamState into the beam batch.
    /// Charging: thin beams from the muzzle to a ring around the aim point that closes onto it and turns as it closes, and a pulsing
    /// lock ring with four notches on whatever the beam is on, so the target sees it coming. Converged: one thin beam. Firing: the
    /// wide main beam with a soft halo. Every segment is drawn solid (negative intensity) so it lands visibly on the hull.</summary>
    [BurstCompile] internal partial struct ChargeBeamGatherJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<LocalToWorld> Transforms;
        [ReadOnly] public BlobAssetReference<WeaponDefsBlob> Weapons;
        public NativeArray<BeamInstance> Output;
        public NativeReference<int> Count;
        public NativeList<FxEvent> Impacts;
        public int Cap;
        public uint Frame;
        public float Time;              // presentation seconds (lock-ring pulse only; never gameplay)
        public float4 FallbackColor;    // the beam FX slot colour when the weapon has no tracer colour

        const int RingSegments = 16;
        const float RingMargin = 1.2f, RingWidth = .12f, NotchLength = .9f;

        void Execute(Entity entity, in ChargeBeamState beam, in FunctionalBlock block)
        {
            if (beam.Phase == ChargeBeamPhase.Idle || beam.PhaseTicks == 0 || !Transforms.HasComponent(block.Grid)) return;
            ref var blob = ref Weapons.Value;
            if (beam.Weapon == 0 || beam.Weapon >= blob.Weapons.Length || beam.Weapon >= blob.ChargeBeams.Length) return;
            ref var weapon = ref blob.Weapons[beam.Weapon]; ref var charge = ref blob.ChargeBeams[beam.Weapon];
            float3 start = math.transform(Transforms[block.Grid].Value, beam.MuzzleLocal), end = beam.EndWorld;
            if (!math.all(math.isfinite(start)) || !math.all(math.isfinite(end))) return;
            float3 dir = math.normalizesafe(end - start, beam.DirWorld);
            float3 side = math.normalizesafe(math.cross(dir, math.abs(dir.y) < .9f ? new float3(0, 1, 0) : new float3(1, 0, 0)));
            float3 up = math.cross(side, dir);
            float4 color = weapon.TracerColor != 0 ? FxStateMath.Linear(weapon.TracerColor) : FallbackColor;
            float progress = 1f - (float)beam.TicksLeft / beam.PhaseTicks;

            if (beam.Phase == ChargeBeamPhase.Charging)
            {
                float closing = math.saturate(progress / math.max(.05f, 1f - charge.ConvergedHoldFraction));
                float radius = charge.ConvergeRadius * (1f - math.smoothstep(0f, 1f, closing));
                float intensity = .35f + .65f * progress;
                if (radius > .05f)
                {
                    float spin = progress * math.PI;
                    for (int k = 0; k < charge.ConvergeBeams; k++)
                    {
                        float angle = spin + 2 * math.PI * k / charge.ConvergeBeams;
                        Add(start, end + radius * (math.cos(angle) * side + math.sin(angle) * up), charge.ConvergeBeamWidth, intensity, color);
                    }
                }
                else Add(start, end, charge.ConvergeBeamWidth * 1.5f, 1f, color);
                if (beam.HitSomething != 0)
                {
                    // The lock ring pulses faster as the charge completes: 3 Hz at the trigger, 12 Hz at convergence.
                    float pulse = .55f + .45f * math.sin(Time * 2 * math.PI * math.lerp(3f, 12f, progress));
                    float ring = radius + RingMargin;
                    float3 lift = -dir * .05f;   // a hair in front of the hull so the ring is not cut by it
                    for (int k = 0; k < RingSegments; k++)
                    {
                        float a0 = 2 * math.PI * k / RingSegments, a1 = 2 * math.PI * (k + 1) / RingSegments;
                        Add(end + lift + ring * (math.cos(a0) * side + math.sin(a0) * up), end + lift + ring * (math.cos(a1) * side + math.sin(a1) * up),
                            RingWidth, pulse, color);
                    }
                    for (int k = 0; k < 4; k++)
                    {
                        float3 radial = math.cos(k * math.PI / 2) * side + math.sin(k * math.PI / 2) * up;
                        Add(end + lift + radial * (ring + NotchLength * .5f), end + lift + radial * math.max(.2f, ring - NotchLength), RingWidth, pulse, color);
                    }
                }
                return;
            }

            // Firing: the main beam swells from the converged width over six ticks and fades over its last twelve.
            int ticksIn = beam.PhaseTicks - beam.TicksLeft;
            float width = math.lerp(charge.ConvergeBeamWidth, weapon.BeamWidth, math.saturate(ticksIn / 6f));
            float fade = math.max(.3f, math.saturate(beam.TicksLeft / 12f));
            Add(start, end, width, fade, color);
            Add(start, end, width * 2.4f, .25f * fade, color);
            if (beam.HitSomething != 0 && (Frame + (uint)entity.Index) % 6 == 0 && Impacts.Length < Impacts.Capacity)
                Impacts.AddNoResize(new FxEvent { Kind = FxKind.HitEnergy, Pos = end, Dir = -dir, Scale = 1.5f });
        }

        void Add(float3 a, float3 b, float width, float intensity, float4 color)
        {
            if (Count.Value >= Cap) return;
            // Negative intensity: solid to the end (FxBeam.shader), so the beams and ring land on the target.
            Output[Count.Value++] = new BeamInstance { Start = a, End = b, Width = width, Intensity = -math.max(intensity, 1e-3f), Color = color };
        }
    }
}
