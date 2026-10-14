using System.Globalization;
using Game.Data.Logic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Editor.Narrative.Dialog
{
    /// <summary>Yarn's ysls.schema.json: command/function arrays, named typed parameters, and function returns.</summary>
    public static class YslsGenerator
    {
        public static string Build(LogicFunctionRegistry registry)
        {
            var commands = new JArray(); var functions = new JArray();
            foreach (var info in registry.Snapshot())
            {
                if ((info.AllowedUsage & (info.IsEffect ? LogicUsage.DialogCommand : LogicUsage.DialogCondition)) == 0) continue;
                var parameters = new JArray();
                foreach (var param in info.Params)
                {
                    string type = param.ArgKind == LogicArgKind.Int ? "number" : param.ArgKind == LogicArgKind.Bool ? "bool" : "string";
                    var item = new JObject { ["name"] = param.Name, ["type"] = type };
                    if (param.ArgKind == LogicArgKind.Ref) item["documentation"] = "Content reference: " + param.RefKind;
                    if (param.Optional) item["defaultValue"] = param.ArgKind == LogicArgKind.Tag ? "here" :
                        param.ArgKind == LogicArgKind.Bool ? (param.DefaultInt == 0 ? "false" : "true") : param.DefaultInt.ToString(CultureInfo.InvariantCulture);
                    parameters.Add(item);
                }
                var definition = new JObject { ["yarnName"] = info.Name, ["parameters"] = parameters };
                if (info.IsEffect) { definition["async"] = info.Flow == EffectFlow.Blocking; commands.Add(definition); }
                else { definition["return"] = new JObject { ["type"] = info.ReturnType == LogicType.Bool ? "bool" : "number" }; functions.Add(definition); }
            }
            return new JObject { ["version"] = 1, ["commands"] = commands, ["functions"] = functions }
                .ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n";
        }
    }
}
