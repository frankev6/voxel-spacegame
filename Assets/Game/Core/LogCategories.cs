namespace Game.Core
{
    /// <summary>Common log category names (SPEC-02 §4.4, §5.4). Specs may log with other strings.</summary>
    public static class LogCategories
    {
        public const string Core = "Core", Boot = "Boot", Flow = "Flow", Session = "Session", Settings = "Settings",
            Input = "Input", Content = "Content", Save = "Save", Ecs = "Ecs", Voxel = "Voxel", Render = "Render",
            Physics = "Physics", Damage = "Damage", Flight = "Flight", Weapons = "Weapons", Power = "Power", AI = "AI",
            Abilities = "Abilities", Galaxy = "Galaxy", StarSystem = "StarSystem", Travel = "Travel", Encounter = "Encounter",
            Narrative = "Narrative", Dialog = "Dialog", Quest = "Quest", Economy = "Economy", Editor = "Editor", UI = "UI",
            Hud = "Hud", Camera = "Camera", Vfx = "Vfx", Audio = "Audio", Dev = "Dev", Test = "Test";
    }
}
