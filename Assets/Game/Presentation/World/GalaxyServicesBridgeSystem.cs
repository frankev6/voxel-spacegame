using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Galaxy.Services;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Presentation.Systems
{
    [UpdateInGroup(typeof(WorldGroup), OrderFirst = true)]
    public partial class GalaxyServicesBridgeSystem : SystemBase
    {
        protected override void OnCreate() { RequireForUpdate<GameplayActive>(); RequireForUpdate<SimClock>(); }
        protected override void OnUpdate()
        {
            if (!Services.TryGet<GalaxyFrameTicker>(out var ticker))
            { GameLog.WarnOnce("GAL.MissingTicker", "Galaxy", "Galaxy frame ticker is not registered."); return; }
            var clock = SystemAPI.GetSingleton<SimClock>();
            ticker.Tick(new GalaxyFrameContext(EntityManager, clock.UniversalTime, clock.Tick,
                math.clamp(UnityEngine.Time.unscaledDeltaTime, 0, .1f), UnityEngine.Time.frameCount));
        }
    }
}
