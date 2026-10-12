using System;
using Game.Core;
using Game.Input;
using UnityEngine.InputSystem;

namespace Game.UI.Services
{
    /// <summary>Implements SPEC-60 §6: owns the Back subscription and forwards the existing input service API.</summary>
    public sealed class InputServiceBridge : IInputContextTarget, IDisposable
    {
        readonly InputService input;
        Action back;
        bool subscribed, disposed;
        public InputServiceBridge(InputService input) => this.input = input ?? throw new ArgumentNullException(nameof(input));
        public void SetContext(InputContext context)
        { if (disposed) throw new ObjectDisposedException(nameof(InputServiceBridge)); input.SetContext(context); }
        public void BindBack(Action onBack)
        {
            if (disposed) throw new ObjectDisposedException(nameof(InputServiceBridge));
            if (subscribed) input.Controls.Global.Back.performed -= OnBack;
            back = onBack; subscribed = onBack != null;
            if (subscribed) input.Controls.Global.Back.performed += OnBack;
        }
        void OnBack(InputAction.CallbackContext _) => back?.Invoke();
        public string GetBindingDisplay(string actionPath) => input.GetBindingDisplay(actionPath);
        public void Dispose()
        {
            if (disposed) return;
            if (subscribed) input.Controls.Global.Back.performed -= OnBack;
            back = null; subscribed = false; disposed = true;
        }
    }
}
