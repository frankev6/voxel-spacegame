namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.2. Runs every ILegacyConverter in order.</summary>
    public static class LegacyMigrationRunner
    {
        [UnityEditor.MenuItem("Game/Legacy Migration/Run All")] public static void RunAllMenu() { throw new System.NotImplementedException("MIG-004"); }
        [UnityEditor.MenuItem("Game/Legacy Migration/Run All (Force)")] public static void RunAllForceMenu() { throw new System.NotImplementedException("MIG-004"); }
        public static Game.Data.Validation.ValidationReport RunAll(bool force) { throw new System.NotImplementedException("MIG-004"); }
        public static void RunCli() { throw new System.NotImplementedException("MIG-004"); }                                       // -executeMethod; exit code 1 on errors
    }
}
