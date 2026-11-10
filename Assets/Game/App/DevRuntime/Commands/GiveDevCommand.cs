#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using Game.App.DevRuntime.Scenarios;
using Game.Core;
using Game.Data.Definitions;
using Game.Core.Events;

namespace Game.App.DevRuntime.Commands
{
    public sealed class GiveDevCommand : IDevCommand
    {
        public string Name => "give";
        public string Usage => "give item=<id> [count=1] | credits=<amount> | blocks=<count, 1-999>";
        public string Summary => "Grant items to cargo, every ship block (the editor's materials) or credits through their owners";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "item", "count", "credits", "blocks");
            if (error != null || args.Positional.Count != 0) return DevCommandResult.Fail(error ?? Usage);
            if (args.Has("blocks")) return args.Has("item") || args.Has("credits") || args.Has("count") ? DevCommandResult.Fail(Usage) : Blocks(args, ctx);
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
        /// <summary>DEV-033: <paramref name="args"/> blocks=N puts N of every ship block in cargo, the items the ship editor builds from.
        /// One stack each (N ≤ 999); stacks past the hold's capacity go to its overflow slots, as a dock's hand-over does.</summary>
        DevCommandResult Blocks(DevArgs args, DevContext ctx)
        {
            long count = args.GetLong("blocks", 0);
            if (count <= 0 || count > 999) return DevCommandResult.Fail("blocks must be between 1 and 999");
            var economy = DevState.Economy(ctx);
            var catalog = DevState.Content(ctx);
            DevState.CompleteWrites(ctx);
            int kinds = 0;
            foreach (var block in catalog.All<BlockDefinition>())
            {
                if (block == null || catalog.IsPlaceholder(block)) continue;
                economy.Inventory.Give(block, (int)count, AcquireSource.Dev); kinds++;
            }
            return DevCommandResult.Ok(kinds.ToString(CultureInfo.InvariantCulture) + " blocks ×" + count.ToString(CultureInfo.InvariantCulture)
                + " in cargo; overflow slots past capacity " + economy.Inventory.Capacity.ToString(CultureInfo.InvariantCulture));
        }
    }
}
#endif
