using System;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Data;
using Unity.Mathematics;

namespace Game.Galaxy.Generation
{
    /// <summary>Applies saved state to fresh generated content in SPEC-30 §5.6 order; never draws RNG.</summary>
    internal static class StarSystemDeltaApplier
    {
        internal static void Apply(StarSystemContent content, StarSystemDelta delta, GalaxyDef galaxy, ContentCatalog catalog)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (delta == null) throw new ArgumentNullException(nameof(delta));
            if (galaxy == null) throw new ArgumentNullException(nameof(galaxy));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!content.Id.Equals(delta.StarSystem)) throw new ArgumentException("Delta belongs to another star system.", nameof(delta));
            foreach (var replacement in delta.Replacements)
            {
                if (!content.TryFind(replacement.BodyKey, out int index))
                {
                    GameLog.WarnOnce("GAL.Delta.MissingBody", "Galaxy", "Saved replacement refers to a body that is no longer generated.");
                    continue;
                }
                if (!Resolve(replacement.Template, out _, out ushort templateIndex)) continue;
                var body = content.Bodies[index]; body.TemplateIndex = templateIndex;
                body.State |= BodyStateFlags.Replaced; content.Bodies[index] = body;
            }
            for (int slot = 0; slot < delta.StorySpawns.Count; slot++)
            {
                var spawn = delta.StorySpawns[slot];
                if (spawn.Removed || !Resolve(spawn.Template, out var template, out ushort templateIndex)) continue;
                ulong parentKey = spawn.AnchorBodyKey;
                OrbitElements parentOrbit = default;
                if (parentKey != 0 && content.TryFind(parentKey, out int anchorIndex))
                {
                    var anchor = content.Bodies[anchorIndex];
                    if (anchor.Id.Role == BodyRole.Moon)
                    {
                        GameLog.WarnOnce("GAL.StorySpawn.MoonAnchor", "Galaxy", "Story spawn anchored to a moon uses its parent planet.");
                        parentKey = anchor.ParentKey;
                        if (parentKey != 0 && content.TryFind(parentKey, out int planetIndex)) parentOrbit = content.Bodies[planetIndex].Orbit;
                    }
                    else parentOrbit = anchor.Orbit;
                }
                var id = new BodyId(content.Id, BodyRole.StorySpawn, checked((ushort)slot), 0);
                UnityEngine.Assertions.Assert.IsTrue(content.Bodies.Length == 0 || content.Bodies[content.Bodies.Length - 1].Id.LocalKey < id.LocalKey);
                content.Bodies.Add(new BodySpec
                {
                    Id = id, ParentKey = parentKey, ParentOrbit = parentOrbit,
                    TemplateIndex = templateIndex, LaneIndex = GalaxyConstants.NoIndex,
                    Orbit = new OrbitElements { Mode = OrbitMode.Fixed, RadiusM = math.length(spawn.OffsetM.xz), PhaseRad = math.atan2(spawn.OffsetM.z, spawn.OffsetM.x) },
                    YawRad = spawn.YawRad, RadiusM = template.RadiusMaxM,
                    Seed = StableHash.Combine(galaxy.Get(content.Id).Seed, id.LocalKey), State = BodyStateFlags.StorySpawn
                });
            }
            foreach (ulong key in delta.Interacted) Mark(key, BodyStateFlags.Interacted);
            foreach (ulong key in delta.Destroyed) Mark(key, BodyStateFlags.Destroyed);
            foreach (var record in delta.StructureDiffs) Mark(record.BodyKey, BodyStateFlags.HasStructure);

            bool Resolve(ContentId id, out BodyTemplateDefinition template, out ushort index)
            {
                index = GalaxyConstants.NoIndex;
                if (!catalog.TryResolve(id, "Galaxy delta " + content.Id, out template)) return false;
                index = galaxy.BodyTemplateIndexOf(template);
                if (index == GalaxyConstants.NoIndex) throw new InvalidOperationException("Resolved delta template is absent from this galaxy's catalog snapshot.");
                return true;
            }
            void Mark(ulong key, BodyStateFlags state)
            {
                if (!content.TryFind(key, out int index)) return;
                var body = content.Bodies[index]; body.State |= state; content.Bodies[index] = body;
            }
        }
    }
}
