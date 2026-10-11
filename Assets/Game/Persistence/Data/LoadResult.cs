namespace Game.Persistence.Data
{
    /// <summary>Implements SPEC-05 §4.5. Outcome of ReadAsync.</summary>
    public readonly struct LoadResult
    {
        public readonly bool Success; public readonly SaveErrorKind Error; public readonly string Message;
        public readonly Game.Persistence.Container.LoadedSave Save;   // null unless Success
        public LoadResult(bool success, SaveErrorKind error, string message, Game.Persistence.Container.LoadedSave save)
        { Success = success; Error = error; Message = message; Save = save; }
    }
}
