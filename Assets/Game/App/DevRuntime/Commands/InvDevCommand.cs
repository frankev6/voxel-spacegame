#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using System.Text;
using Game.App.DevRuntime.Scenarios;
using Game.Economy;

namespace Game.App.DevRuntime.Commands
{
    public sealed class InvDevCommand : IDevCommand
    {
        public string Name => "inv";
        public string Usage => "inv [max=80]";
        public string Summary => "List occupied cargo and overflow slots, capacity and credits";
        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            string error = DevCommandRegistry.UnknownArgument(args, Name, "max");
            if (error != null || args.Positional.Count != 0) return DevCommandResult.Fail(error ?? Usage);
            long max = args.GetLong("max", 80);
            if (max < 0 || max > InventoryModel.MaxSlots) return DevCommandResult.Fail("max must be between 0 and 200");
            var economy = DevState.Economy(ctx); var inventory = economy.Inventory; var text = new StringBuilder();
            int shown = 0;
            for (int slot = 0; slot < InventoryModel.MaxSlots && shown < max; slot++)
            {
                var stack = inventory.Get(slot); if (stack.IsEmpty) continue;
                string id = stack.OriginalId.IsNone ? stack.Item.IdString : stack.OriginalId.Value;
                text.Append(slot.ToString(CultureInfo.InvariantCulture)).Append(": ").Append(id).Append('×')
                    .Append(stack.Count.ToString(CultureInfo.InvariantCulture));
                if (slot >= inventory.Capacity) text.Append(" [overflow]");
                text.Append('\n'); shown++;
            }
            if (shown < inventory.UsedSlots) text.Append("... ").Append((inventory.UsedSlots - shown).ToString(CultureInfo.InvariantCulture)).Append(" more occupied slots\n");
            text.Append("capacity ").Append(inventory.Capacity.ToString(CultureInfo.InvariantCulture)).Append('\n')
                .Append("credits ").Append(economy.Wallet.Credits.ToString(CultureInfo.InvariantCulture));
            return DevCommandResult.Ok(text.ToString());
        }
    }
}
#endif
