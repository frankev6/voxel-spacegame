using System.Collections.Generic;
using Game.Core;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>IPauseService whose pause state the test sets directly.</summary>
    public sealed class FakePause : IPauseService
    {
        public bool Paused;
        public PauseToken Acquire(string reason) => default;
        public bool IsPaused => Paused;
        public int ActiveTokenCount => Paused ? 1 : 0;
        public void Release(PauseToken token) { }
        public void GetActiveReasons(List<string> results) { results.Clear(); }
    }
}
