namespace Game.Economy
{
    /// <summary>SPEC-50 contract shell; ECO-003 owns behavior.</summary>
    public sealed class Wallet
    {
        public long Credits => throw new System.NotImplementedException("ECO-003");
        public void Add(long amount) => throw new System.NotImplementedException("ECO-003");                  // amount ≥ 0; saturates at MaxCredits; negative → ArgumentOutOfRange
        public bool TrySpend(long amount) => throw new System.NotImplementedException("ECO-003");             // amount ≥ 0; false and no change when Credits < amount
        public void Set(long credits) => throw new System.NotImplementedException("ECO-003");                 // restore and new game only; clamps to [0, MaxCredits]
        public const long MaxCredits = 1_000_000_000_000L;
        public event System.Action Changed { add => throw new System.NotImplementedException("ECO-003"); remove => throw new System.NotImplementedException("ECO-003"); }
    }
}
