using System;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Narrative.Dialog;
namespace Game.App.Services
{
    /// <summary>App-owned dialog session lifetime. Loaded preserves the restored DLGS memory.</summary>
    internal sealed class DialogSessionBinding : ISaveBlocker, IDisposable
    {
        readonly DialogService dialog;
        public DialogSessionBinding(DialogService dialog) { this.dialog = dialog; EventBus<SessionReset>.Subscribe(Reset); }
        public SaveBlockReason GetSaveBlock() => dialog.IsActive ? SaveBlockReason.Transitioning : SaveBlockReason.None;
        void Reset(SessionReset value)
        { if (dialog.IsActive) dialog.Interrupt(); if (value.Reason != SessionResetReason.Loaded) dialog.Memory.Clear(); }
        public void Dispose() { EventBus<SessionReset>.Unsubscribe(Reset); if (dialog.IsActive) dialog.Interrupt(); }
    }
}
