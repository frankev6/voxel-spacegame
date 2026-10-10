namespace Game.Core.Save
{
    /// <summary>Why a save happened (values defined with SPEC-05 §4.4; append only). Implements SPEC-02 §4.7.</summary>
    public enum SaveReason : byte { Manual = 0, Quick = 1, AutoTimer = 2, AutoSceneTransition = 3, AutoStarSystemEntered = 4, Quit = 5, NewGame = 6 }
}
