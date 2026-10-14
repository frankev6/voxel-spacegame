#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using Game.App.DevRuntime.Scenarios;
using Game.Core;

namespace Game.App.DevRuntime.Commands
{
    public sealed class GiveDevCommand : IDevCommand
    {
        public string Name => "give";
        public string Usage => "give item=<id> [count=1] | credits=<amount>";
        public string Summary => "Grant items to cargo or add credits through their owners";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "item", "count", "credits");
            if (error != null || args.Positional.Count != 0) return DevCommandResult.Fail(error ?? Usage);
            if (args.Has("item") == args.Has("credits") || args.Has("credits") && args.Has("count")) return DevCommandResult.Fail(Usage);
            var economy = DevState.Economy(ctx);
            if (args.Has("credits"))
            {
                long amount = args.GetLong("credits", 0);
                if (amount < 0) return DevCommandResult.Fail("credits must be nonnegative");
                DevState.CompleteWrites(ctx); economy.Wallet.Add(amount);
                return DevCommandResult.Ok("credits " + economy.Wallet.Credits.ToString(CultureInfo.InvariantCulture));
            }
            long count = args.GetLong("count", 1);
            if (count <= 0 || count > int.MaxValue) return DevCommandResult.Fail("count must be between 1 and 2147483647");
            var item = new ContentId(args.GetString("item", ""));
            DevState.CompleteWrites(ctx);
            if (!economy.Inventory.TryAdd(item, (int)count, out string reason)) return DevCommandResult.Fail(reason);
            return DevCommandResult.Ok(item.Value + "×" + count.ToString(CultureInfo.InvariantCulture));
        }
    }
}
#endif
