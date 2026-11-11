#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using Game.App.DevRuntime.Extraction;
using Game.Simulation.Extraction;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>SPEC-93 §6: the extraction loop from the console (EXT-007…011). Every verb acts on the playground's den and Lantern.</summary>
    public sealed class ExtractionDevCommand : IDevCommand
    {
        public string Name => "extraction";
        public string Usage => "extraction status | goto=den|lantern|<poi>[@x,y,z] | job board|accept=lantern|<lead> | transponder off|trader|work | hold t=<tonnes> | routes | magnet | cut [n=<0-5>|all] | stow | dig | build plate|gun|lamp|socket | ore add=<n> | heist alarm|dimming|status | den attack | interact";
        public string Summary => "The extraction loop: the Rock (den), Tollhouse Nine (heist), the gate pair, jobs, the tow, tethers, digging and building";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            var playground = ctx.Host.Playground;
            if (playground == null || !playground.Active) return DevCommandResult.Fail("enter the playground first (playground enter)");
            var x = playground.Extraction;
            if (x == null) return DevCommandResult.Fail("extraction runtime unavailable");
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            var ship = ctx.PlayerShipRoot;
            string verb = args.Positional.Count > 0 ? args.Positional[0] : (args.Has("goto") ? "goto" : "status");
            switch (verb)
            {
                case "status": return DevCommandResult.Ok(x.Status(em));
                case "goto":
                {
                    string where = args.GetString("goto", args.Positional.Count > 1 ? args.Positional[1] : "");
                    x.Build();
                    float3 local; string place = "lantern"; float yaw = 0;
                    if (where == "den") { place = "den"; local = new float3(DenPlace.CaveMouth.x + 260, 0, DenPlace.CaveMouth.z); yaw = -math.PI / 2; }
                    else if (where == "lantern") local = LanternLayout.Poi("arrival");
                    else if (where == "dengate") { place = "den"; local = new float3(ExtractionRuntime.DenGateLocal.x - 160, 0, ExtractionRuntime.DenGateLocal.y); yaw = math.PI / 2; }
                    else if (where == "table") { place = "den"; local = DenPlace.Table + new float3(30, 4, 0); yaw = -math.PI / 2; }
                    // the Cradle: in the core road east of the core and clear of the Yards post (44, 0), facing it, within the magnet's 90 m (the Cradle itself is 96 m
                    // square around a 40 m core: a 53 m hauler facing the core from inside it clipped the walls, EXT-015)
                    else if (where == "cradle") { local = LanternLayout.Core + new float3(80, 0, 4); yaw = -math.PI / 2; }
                    else if (where.StartsWith("den@") || where.StartsWith("lantern@"))
                    {
                        place = where.StartsWith("den@") ? "den" : "lantern";
                        var p = where.Substring(where.IndexOf('@') + 1).Split(',');
                        if (p.Length < 3) return DevCommandResult.Fail("goto=lantern@x,y,z[,yaw]");
                        var ci = CultureInfo.InvariantCulture;
                        local = new float3(float.Parse(p[0], ci), float.Parse(p[1], ci), float.Parse(p[2], ci));
                        if (p.Length > 3) yaw = math.radians(float.Parse(p[3], ci));
                    }
                    else
                    {
                        local = LanternLayout.Poi(where);
                        if (math.all(local == 0)) return DevCommandResult.Fail("goto=den|lantern|table|dengate|<poi>|lantern@x,y,z: unknown " + where);
                    }
                    if (!ctx.TryGetFrame(out var frame)) return DevCommandResult.Fail("no frame");
                    var at = (float3)(x.UniverseOf(place, local) - frame.OriginInStarSystem);
                    DevEcs.TeleportRoot(em, ship, at, quaternion.RotateY(yaw));
                    return DevCommandResult.Ok($"at {place} {where} local={DevEcs.Format(local)}");
                }
                case "job":
                    if (args.Has("accept")) return DevCommandResult.Ok(x.Accept(args.GetString("accept", "")));
                    return DevCommandResult.Ok(x.Board());
                case "transponder":
                {
                    string code = args.Positional.Count > 1 ? args.Positional[1] : "trader";
                    if (code == "off") TransponderApi.Set(em, ship, false, AccessCode.None);
                    else if (code == "work") TransponderApi.Set(em, ship, true, AccessCode.Work);
                    else if (code == "garrison") TransponderApi.Set(em, ship, true, AccessCode.Garrison);
                    else TransponderApi.Set(em, ship, true, AccessCode.Trader);
                    return DevCommandResult.Ok("transponder " + code);
                }
                case "magnet": return DevCommandResult.Ok(x.Magnet(em, ship));
                case "cut":
                {
                    if (args.Has("n") || (args.Positional.Count > 1 && args.Positional[1] == "all"))
                    {
                        using var q = em.CreateEntityQuery(ComponentType.ReadWrite<TetherCutRequests>());
                        var pending = q.GetSingleton<TetherCutRequests>().Pending;
                        int i = 0, n = (int)args.GetDouble("n", -1);
                        foreach (var t in x.Lantern.TetherEntities()) { if (n < 0 || i == n) pending.Add(t); i++; }
                        return DevCommandResult.Ok(n < 0 ? "cutting every tether" : "cutting tether " + n);
                    }
                    return DevCommandResult.Ok(x.Cut(em, ship));
                }
                case "routes": return DevCommandResult.Ok(x.Lantern.CheckRoutes(em));
                case "stow": return DevCommandResult.Ok(x.StowToggle(em, ship));
                case "dig": return DevCommandResult.Ok(x.Den.Dig());
                case "build": return DevCommandResult.Ok(x.Den.Place(args.Positional.Count > 1 ? args.Positional[1] : null));
                case "ore": x.Den.Ore += (int)args.GetDouble("add", 50); return DevCommandResult.Ok("ore " + x.Den.Ore);
                case "hold":
                {   // test lever: rate the crew ship's hold (t) without a refit; the real rating comes from its cargo blocks
                    x.HoldOverride = (float)args.GetDouble("t", 200); x.Crew();
                    var hold = em.HasComponent<CargoHold>(ship) ? em.GetComponentData<CargoHold>(ship) : default;
                    return DevCommandResult.Ok($"hold {hold.CapacityT:0} t ({hold.StowedT:0} t stowed)");
                }
                case "interact": return DevCommandResult.Ok(x.Interact() ?? "nothing here");
                case "heist":
                {
                    string what = args.Positional.Count > 1 ? args.Positional[1] : "status";
                    if (what == "dimming") { x.Lantern.ScheduleDimming(UnityEngine.Time.timeAsDouble - 150); return DevCommandResult.Ok("the Dimming in 30 s"); }
                    if (what == "alarm")
                    {
                        if (ship != Entity.Null) em.AddComponentData(ship, new Wanted { FactionMask = 1ul << x.Lantern.AllianceIndex });
                        return DevCommandResult.Ok("identified: the alarm starts");
                    }
                    return DevCommandResult.Ok(x.Lantern.Status(em));
                }
                case "den":
                    if (args.Positional.Count > 1 && args.Positional[1] == "attack")
                    {
                        if (!ctx.TryGetService<Game.Data.Services.ContentService>(out var content) || !ctx.TryGetFrame(out var f)) return DevCommandResult.Fail("not ready");
                        x.Den.Attack(content.Catalog, f); return DevCommandResult.Ok("den attack");
                    }
                    return DevCommandResult.Ok(x.Den.Status(em));
            }
            return DevCommandResult.Fail(Usage);
        }
    }
}
#endif
