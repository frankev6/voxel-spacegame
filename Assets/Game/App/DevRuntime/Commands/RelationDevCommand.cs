#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Services;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>ENC-012: relations are queued through FactionService, never written into the matrix.</summary>
    public sealed class RelationDevCommand : IDevCommand
    {
        public string Name => "relation";
        public string Usage => "relation faction=<id> [set=<Int32>|add=<Int32>]";
        public string Summary => "Read a faction relation and stance, or queue a set/add for the next fixed tick";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "faction", "set", "add");
            if (error != null) return DevCommandResult.Fail(error);
            if (args.Positional.Count != 0 || !args.TryGetString("faction", out string id) || string.IsNullOrWhiteSpace(id) || args.Has("set") && args.Has("add"))
                return DevCommandResult.Fail("expected " + Usage);
            if (!ctx.TryGetService<FactionService>(out var factions)) return DevCommandResult.Fail("faction service not available");
            if (!ctx.TryGetService<ContentService>(out var content)) return DevCommandResult.Fail("content service not available");
            if (!content.Catalog.TryGet<FactionDefinition>(new ContentId(id), out var faction) || factions.Indices.IndexOf(faction) == 255)
                return DevCommandResult.Fail("unknown faction '" + id + "'");
            bool write = args.Has("set") || args.Has("add");
            if (write)
            {
                string key = args.Has("set") ? "set" : "add";
                long value = args.GetLong(key, 0);
                if (value < int.MinValue || value > int.MaxValue) return DevCommandResult.Fail("argument '" + key + "' expects an Int32 integer");
                if (faction.RelationLocked || faction.IsPlayerFaction) return DevCommandResult.Fail("relation is locked for faction '" + faction.Id.Value + "'");
                if (key == "set") factions.SetRelation(faction, (int)value); else factions.AdjustRelation(faction, (int)value);
            }
            return DevCommandResult.Ok("faction=" + faction.Id.Value + " relation=" + factions.GetRelation(faction).ToString(CultureInfo.InvariantCulture) +
                " stance=" + factions.GetPlayerStance(faction) + (write ? "; queued for next fixed tick (values include pending requests)" : "; values include pending requests"));
        }
    }
}
#endif
