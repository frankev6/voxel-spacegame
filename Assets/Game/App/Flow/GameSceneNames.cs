using Game.Core.Flow;

namespace Game.App.Flow
{
    /// <summary>Scene names of the four game scenes (architecture §4.1; build order Boot, MainMenu, World, BlueprintLab). Implements SPEC-01
    /// §5.4.</summary>
    public static class GameSceneNames
    {
        public const string Boot = "Boot", MainMenu = "MainMenu", World = "World", BlueprintLab = "BlueprintLab";

        public static string ToSceneName(this GameScene scene)
        {
            switch (scene)
            {
                case GameScene.Boot: return Boot;
                case GameScene.MainMenu: return MainMenu;
                case GameScene.World: return World;
                case GameScene.BlueprintLab: return BlueprintLab;
                default: throw new System.ArgumentOutOfRangeException(nameof(scene), scene, "not a loadable game scene");
            }
        }

        /// <summary>GameScene.None for any scene that is not one of the four (test scenes, look-dev scenes).</summary>
        public static GameScene FromSceneName(string name)
        {
            switch (name)
            {
                case Boot: return GameScene.Boot;
                case MainMenu: return GameScene.MainMenu;
                case World: return GameScene.World;
                case BlueprintLab: return GameScene.BlueprintLab;
                default: return GameScene.None;
            }
        }
    }
}
