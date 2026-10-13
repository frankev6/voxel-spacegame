namespace Game.Core.Events
{
    /// <summary>SPEC-50 §4.2: player-facing notice kinds; append only.</summary>
    public enum EconomyNoticeKind : byte { CargoFull = 0, Jettisoned = 1, SalvageDone = 2, SalvagePartial = 3, SalvageRefused = 4, TradeRefused = 5 }
}
