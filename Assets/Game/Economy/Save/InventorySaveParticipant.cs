using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Save;
using Game.Data.Services;
using Game.Data.Definitions;

namespace Game.Economy
{
    /// <summary>SPEC-50 §5.5: INVT order 60, version 1; capacity is derived, never saved.</summary>
    public sealed class InventorySaveParticipant : ISaveParticipant
    {
        readonly EconomyService economy;
        readonly ContentCatalog catalog;
        readonly List<ContentId> merchantIds = new List<ContentId>();
        public InventorySaveParticipant(EconomyService economy, ContentCatalog catalog)
        {
            this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }
        public string Key => "INVT";
        public int Version => 1;
        public void Capture(SaveWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            writer.WriteInt64(economy.Wallet.Credits);
            writer.WriteVarUInt32((uint)economy.Inventory.UsedSlots);
            for (int slot = 0; slot < InventoryModel.MaxSlots; slot++)
            {
                var stack = economy.Inventory.Get(slot); if (stack.IsEmpty) continue;
                writer.WriteUInt16((ushort)slot);
                writer.WriteContentId(stack.Item == catalog.MissingItem && !stack.OriginalId.IsNone ? stack.OriginalId : stack.Item.Id);
                writer.WriteInt32(stack.Count);
            }
            merchantIds.Clear();
            foreach (var pair in economy.Merchants.SavedStates) merchantIds.Add(pair.Key);
            merchantIds.Sort((a, b) => string.CompareOrdinal(a.Value, b.Value));
            writer.WriteVarUInt32((uint)merchantIds.Count);
            foreach (var id in merchantIds)
            {
                var state = economy.Merchants.SavedStates[id];
                bool unknownMerchant = !catalog.TryGet<MerchantDefinition>(id, out _);
                writer.WriteContentId(id); writer.WriteDouble(state.LastRestock);
                writer.WriteVarUInt32((uint)state.Rows.Count);
                foreach (var row in state.Rows)
                {
                    writer.WriteContentId((unknownMerchant || row.Item == catalog.MissingItem) && !row.OriginalId.IsNone ? row.OriginalId : row.Item.Id);
                    writer.WriteInt32(row.Count); writer.WriteSingle(row.PriceFactor); writer.WriteBool(row.Buyback);
                }
            }
        }
        public void Restore(SaveReader reader, int fromVersion)
        {
            if (fromVersion != 0 && fromVersion != Version) throw new SaveFormatException("Unsupported INVT version", 0);
            economy.ResetToEmpty();
            if (fromVersion == 0) return;
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            economy.Wallet.Set(reader.ReadInt64());
            uint slots = ReadCount(reader, 7); // ushort + shortest content ID + int
            var occupied = new bool[InventoryModel.MaxSlots];
            for (uint n = 0; n < slots; n++)
            {
                int slot = reader.ReadUInt16(); var id = reader.ReadContentId(); int count = reader.ReadInt32();
                if (slot >= InventoryModel.MaxSlots)
                { GameLog.Warn(LogCategories.Economy, $"INVT drops out-of-range slot {slot}."); continue; }
                if (occupied[slot]) throw new SaveFormatException("Duplicate INVT slot", reader.Position);
                occupied[slot] = true;
                var item = catalog.ResolveItemOrMissing(id, "INVT");
                int clamped = Math.Clamp(count, 1, item.MaxStack);
                if (clamped != count) GameLog.Warn(LogCategories.Economy, $"INVT clamps slot {slot} count {count} to {clamped}.");
                try { economy.Inventory.RestoreSlot(slot, item, clamped, id); }
                catch (ArgumentOutOfRangeException) { throw new SaveFormatException("INVT item count exceeds model bounds", reader.Position); }
            }
            uint merchants = ReadCount(reader, 10); // shortest content ID + double + count
            var seen = new HashSet<ContentId>();
            for (uint n = 0; n < merchants; n++)
            {
                var id = reader.ReadContentId(); double restock = reader.ReadDouble();
                bool knownMerchant = catalog.TryGet<MerchantDefinition>(id, out var merchant);
                if (knownMerchant) id = merchant.Id;
                if (id.IsNone || !seen.Add(id)) throw new SaveFormatException("Empty or duplicate INVT merchant", reader.Position);
                uint entries = ReadCount(reader, 10); // shortest content ID + int + float + bool
                var rows = new List<MerchantStockState>();
                for (uint i = 0; i < entries; i++)
                {
                    var itemId = reader.ReadContentId();
                    var item = catalog.ResolveItemOrMissing(itemId, "INVT");
                    int count = reader.ReadInt32();
                    if (knownMerchant && count < -1) throw new SaveFormatException("Invalid INVT stock count", reader.Position);
                    rows.Add(new MerchantStockState { Item = item, OriginalId = itemId,
                        Count = count, PriceFactor = reader.ReadSingle(), Buyback = reader.ReadBool() });
                }
                economy.Merchants.RestoreState(id, restock, rows);
            }
            if (!reader.IsAtEnd) throw new SaveFormatException("Trailing INVT data", reader.Position);
        }
        static uint ReadCount(SaveReader reader, int minimumBytes)
        {
            uint count = reader.ReadVarUInt32();
            if (count > (uint)(reader.Remaining / minimumBytes)) throw new SaveFormatException("INVT count exceeds payload", reader.Position);
            return count;
        }
    }
}
