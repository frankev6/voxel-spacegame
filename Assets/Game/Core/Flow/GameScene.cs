namespace Game.Core.Flow
{
    /// <summary>The four scenes (architecture §4.1). Build index = (int)scene - 1. Implements SPEC-02 §4.8.</summary>
    public enum GameScene : byte { None = 0, Boot = 1, MainMenu = 2, World = 3, BlueprintLab = 4 }
}
