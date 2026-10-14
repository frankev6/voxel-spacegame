#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Encounters;
using Game.Galaxy.Services;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>ENC-012: direct requests and diagnostics from the production encounter owner.</summary>
    public sealed class EncounterDevCommand : IDevCommand
    {
        public string Name => "encounter";
        public string Usage => "encounter spawn=<id> [count=1..10] [anchor=player|<tag>] | combat=<id> | director=on|off | list";
        public string Summary => "Spawn encounter instances, start combat, toggle automatic rolls or list definitions and active instances";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "spawn", "count", "anchor", "combat", "director");
            if (error != null) return DevCommandResult.Fail(error);
            bool list = args.Positional.Count == 1 && args.Positional[0] == "list";
            int actions = (args.Has("spawn") ? 1 : 0) + (args.Has("combat") ? 1 : 0) + (args.Has("director") ? 1 : 0) + (list ? 1 : 0);
            if (actions != 1 || args.Positional.Count != (list ? 1 : 0) ||
                !args.Has("spawn") && (args.Has("count") || args.Has("anchor"))) return DevCommandResult.Fail("expected " + Usage);
            if (!ctx.TryGetService<EncounterDirectorService>(out var director)) return DevCommandResult.Fail("encounter director not available");
            if (args.TryGetString("director", out string enabled))
            {
                if (!string.Equals(enabled, "on", StringComparison.OrdinalIgnoreCase) && !string.Equals(enabled, "off", StringComparison.OrdinalIgnoreCase))
                    return DevCommandResult.Fail("argument 'director' expects on|off");
                director.Enabled = string.Equals(enabled, "on", StringComparison.OrdinalIgnoreCase);
                return DevCommandResult.Ok("encounter director " + (director.Enabled ? "on" : "off") + "; explicit spawn and combat requests remain available");
            }
            if (!ctx.TryGetService<ContentService>(out var content)) return DevCommandResult.Fail("content service not available");
            if (list)
            {
                var text = new StringBuilder("encounter director ").Append(director.Enabled ? "on" : "off")
                    .Append("; active=").Append(director.ActiveInstanceCount).Append(" ships=").Append(director.AliveShipCount).AppendLine();
                text.AppendLine("definitions:");
                foreach (var definition in content.Catalog.All<EncounterDefinition>())
                    text.Append(definition.Id.Value).Append(" faction=").Append(definition.Faction?.Id.Value ?? "none")
                        .Append(" kind=").Append(definition.Kind).AppendLine();
                var instances = new List<EncounterInstanceInfo>(); director.GetActiveInstances(instances);
                text.AppendLine("active instances:");
                foreach (var instance in instances)
                    text.Append("id=").Append(instance.Id).Append(' ').Append(instance.Definition.Id.Value)
                        .Append(" source=").Append(instance.Source).Append(" state=").Append(instance.State)
                        .Append(" system=").Append(instance.StarSystem.Value).Append(" alive=").Append(instance.AliveMembers)
                        .Append('/').Append(instance.Members).Append(" persistent=").Append(instance.Persistent).AppendLine();
                return DevCommandResult.Ok(text.ToString().TrimEnd());
            }
            bool combat = args.Has("combat"); string requested = args.GetString(combat ? "combat" : "spawn", "");
            if (string.IsNullOrWhiteSpace(requested)) return DevCommandResult.Fail("argument '" + (combat ? "combat" : "spawn") + "' expects an encounter id");
            if (!content.Catalog.TryGet<EncounterDefinition>(new ContentId(requested), out var encounter))
                return DevCommandResult.Fail("unknown encounter '" + requested + "'");
            long count = args.GetLong("count", 1);
            if (count < 1 || count > 10) return DevCommandResult.Fail("argument 'count' expects an integer from 1 to 10 (encounter instances, not ships)");
            string tag = args.GetString("anchor", "player");
            if (string.IsNullOrWhiteSpace(tag)) return DevCommandResult.Fail("argument 'anchor' expects player or a body story tag");
            var anchor = tag == "player" ? EncounterAnchor.Player() : new EncounterAnchor { Kind = EncounterAnchorKind.Tag, Tag = tag };
            int created = combat ? director.StartCombat(encounter, EncounterAnchor.Player()) :
                director.SpawnEncounter(encounter, anchor, (int)count, EncounterSource.Dev);
            if (created > 0)
                return DevCommandResult.Ok("created " + created.ToString(CultureInfo.InvariantCulture) + " of " + count.ToString(CultureInfo.InvariantCulture) +
                    " encounter instances for " + encounter.Id.Value + "; active=" + director.ActiveInstanceCount + " ships=" + director.AliveShipCount);
            string context = ctx.TryGetService<GalaxyService>(out var galaxy) && galaxy.Galaxy == null
                ? " Sandbox has no galaxy; start a campaign with newgame." : " Check world/player readiness, travel and encounter caps.";
            return DevCommandResult.Fail(combat
                ? "created 0 instances; the director may have queued or refused the combat request." + context
                : "created 0 instances; the director refused the Dev spawn request." + context);
        }
    }
}
#endif
