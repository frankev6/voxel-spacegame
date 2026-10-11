namespace Game.App.Boot
{
    /// <summary>The persistent root object (Assets/Game/App/Resources/GameRoot.prefab); builds the composition in Awake. Implements SPEC-01 §4.5
    /// (shell; FND-002 adds the behaviour).</summary>
    public sealed class GameRoot : UnityEngine.MonoBehaviour
    {
        [UnityEngine.SerializeField] private GameRootConfig config;
        internal static GameRoot Instance;                        // reset in SubsystemRegistration (FND-002)
        internal GameComposition Composition { get; private set; }
    }
}
