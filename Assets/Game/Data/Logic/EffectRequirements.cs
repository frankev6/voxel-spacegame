namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.5); behavior follows in the named implementation task.</summary>
    public sealed class EffectRequirements
    {
        // TODO(NAR-005): aggregate effect costs and test their summed requirements.
        public void Clear() => throw new System.NotImplementedException("NAR-005");
        public void AddItemCost(int itemIndex, int count) => throw new System.NotImplementedException("NAR-005");
        public void AddCreditCost(long amount) => throw new System.NotImplementedException("NAR-005");
        public bool IsSatisfied(ILogicContext ctx) => throw new System.NotImplementedException("NAR-005");       // every summed item cost ≤ count held; credit cost ≤ credits
    }
}
