using System;
using System.Collections.Generic;
using Game.Core.Save;
using Game.Persistence.Container;
using Game.Persistence.Data;

namespace Game.Persistence.Services
{
    /// <summary>Implements SPEC-05 §4.5. Profiles, slots, save and load (managed service, architecture §4.2 service #6).</summary>
    public sealed class SaveService : IDisposable
    {
        public SaveService(SavePaths paths, Game.Data.Services.ContentService content, Game.Core.IPauseService pause,
                           SaveServiceConfig config, SaveChunkRegistry registry = null) { throw new System.NotImplementedException("PERS-005"); }
        public void AttachContext(ISaveContext context) { throw new System.NotImplementedException("PERS-005"); }
        public void Register(Game.Core.Save.ISaveParticipant participant) { throw new System.NotImplementedException("PERS-005"); }   // InvalidOperationException: unknown key, duplicate key, META/IDPL

        // Profiles
        public IReadOnlyList<ProfileInfo> ListProfiles() { throw new System.NotImplementedException("PERS-002"); }
        public ProfileInfo CreateProfile(string displayName) { throw new System.NotImplementedException("PERS-002"); }
        public bool DeleteProfile(ProfileId id) { throw new System.NotImplementedException("PERS-002"); }
        public ProfileId ActiveProfile => throw new System.NotImplementedException("PERS-002");
        public void SetActiveProfile(ProfileId id) { throw new System.NotImplementedException("PERS-002"); }

        // Slots
        public IReadOnlyList<SlotInfo> ListSlots(ProfileId profile) { throw new System.NotImplementedException("PERS-002"); }
        public bool TryCreateManualSlot(ProfileId profile, out SlotRef slot) { throw new System.NotImplementedException("PERS-002"); }
        public SlotRef QuickSlot(ProfileId profile) { throw new System.NotImplementedException("PERS-002"); }
        public SlotRef NextAutosaveSlot(ProfileId profile) { throw new System.NotImplementedException("PERS-002"); }
        public bool DeleteSlot(SlotRef slot) { throw new System.NotImplementedException("PERS-002"); }
        public bool HasCurrentSlot => throw new System.NotImplementedException("PERS-005");
        public SlotRef CurrentSlot => throw new System.NotImplementedException("PERS-005");       // last slot saved to or loaded from in this session

        // State and operations
        public bool IsBusy => throw new System.NotImplementedException("PERS-005");
        public bool CanSave(out SaveBlockReason reason) { throw new System.NotImplementedException("PERS-005"); }
        public void ResetForNewGame() { throw new System.NotImplementedException("PERS-005"); }                                  // clears preserved chunks, seeded palette, thumbnail cache, CurrentSlot
        public UnityEngine.Awaitable<SaveResult> SaveAsync(SlotRef slot, SaveReason reason, string displayName = null) { throw new System.NotImplementedException("PERS-005"); }
        public UnityEngine.Awaitable<LoadResult> ReadAsync(SlotRef slot) { throw new System.NotImplementedException("PERS-006"); }
        public Game.Data.Validation.ValidationReport Restore(LoadedSave save) { throw new System.NotImplementedException("PERS-006"); }   // main thread
        public Game.Data.Validation.ValidationReport LastLoadReport => throw new System.NotImplementedException("PERS-006");
#pragma warning disable CS0067 // raised by PERS-005/006
        public event Action<SaveOperationResult> OperationCompleted;
#pragma warning restore CS0067

        // Thumbnails
        public bool TryLoadThumbnail(SlotRef slot, string key, out UnityEngine.Texture2D texture) { throw new System.NotImplementedException("PERS-005"); }  // caller destroys texture
        public bool TryGetSessionThumbnail(string key, out byte[] png) { throw new System.NotImplementedException("PERS-005"); }

        public AutosaveScheduler Autosave => throw new System.NotImplementedException("PERS-009");
        public void Dispose() { throw new System.NotImplementedException("PERS-005"); }

        // Synchronous seams for tests (InternalsVisibleTo):
        internal CapturedSave CaptureNow(SlotRef slot, SaveReason reason, string displayName) { throw new System.NotImplementedException("PERS-005"); }
        internal SaveResult WriteCaptured(CapturedSave captured) { throw new System.NotImplementedException("PERS-005"); }          // thread-agnostic
        internal LoadResult ReadNow(SlotRef slot) { throw new System.NotImplementedException("PERS-006"); }                         // thread-agnostic
        internal bool HandleWantsToQuit() { throw new System.NotImplementedException("PERS-009"); }
    }
}
