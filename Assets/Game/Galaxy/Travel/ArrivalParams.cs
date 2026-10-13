namespace Game.Galaxy.Travel
{
    /// <summary>Implements SPEC-32 §4.6 analytic arrival clearance parameters.</summary>
    public struct ArrivalParams
    {
        public float StandoffM, StepM, ClearanceM;
        public int MaxSteps;
    }
}
