using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Galaxy.Services;
using Game.Galaxy.Travel;
using Game.Narrative;
using Game.Narrative.Dialog;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.UI.Data;
using Game.UI.Services;
using Game.Voxel.Damage;
using Unity.Entities;

namespace Game.Presentation.Systems
{
    /// <summary>SPEC-40 §5.3: one narrative tick after managed event delivery, including menu frames.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup), OrderLast = true)]
    [UpdateAfter(typeof(ManagedEventDrainSystem))]
    public partial class NarrativeTickBridgeSystem : SystemBase
    {
        EntityQuery players, combat;
        protected override void OnCreate()
        {
            players = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<PlayerShip>() },
                None = new[] { ComponentType.ReadOnly<Hulk>() } });
            combat = GetEntityQuery(ComponentType.ReadOnly<PlayerCombatStatus>());
        }
        protected override void OnUpdate()
        {
            if (!Game.Core.Services.TryGet<NarrativeService>(out var narrative)) return;
            narrative.SetWorldReady(SystemAPI.HasSingleton<GameplayActive>() && SystemAPI.HasSingleton<WorldSceneActive>());
            bool idle = !Game.Core.Services.TryGet<TravelService>(out var travel) || travel.Phase == TravelPhase.Idle;
            bool dialogFree = !Game.Core.Services.TryGet<DialogService>(out var dialog) || !dialog.IsActive;
            bool modal = false, screen = false;
            if (Game.Core.Services.TryGet<UINavigator>(out var navigation))
            {
                modal = navigation.HasOpenLayer(UILayer.Modals);
                screen = navigation.HasOpenLayer(UILayer.Screens);
            }
            narrative.SetPlayerCanTalk(!modal && dialogFree && idle);
            EntityManager.CompleteDependencyBeforeRO<PlayerCombatStatus>();
            int hostiles = combat.IsEmptyIgnoreFilter ? 0 : combat.GetSingleton<PlayerCombatStatus>().HostilesEngaging;
            narrative.SetPlayerFree(!modal && !screen && dialogFree && idle && !players.IsEmptyIgnoreFilter && hostiles == 0);
            narrative.Tick();
        }
    }
}
