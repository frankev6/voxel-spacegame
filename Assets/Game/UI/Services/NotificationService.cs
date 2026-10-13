using System;
using System.Collections.Generic;
using System.Globalization;
using Game.UI.Data;
using Unity.Profiling;
using UnityEngine.UIElements;

namespace Game.UI.Services
{
    /// <summary>Implements SPEC-60 §5.8/6: pooled, unscaled toasts with bounded FIFO overflow and coalescing.</summary>
    public sealed class NotificationService
    {
        sealed class ToastSlot
        {
            internal VisualElement Element;
            internal Label Icon, Text;
            internal Notification Data;
            internal float Life, LastBumpTime, ExitLeft, BumpLeft;
            internal int EnterFrame;
            internal bool Used, Entering, Exiting, Bumping;
        }
        struct QueuedToast { internal Notification Data; internal float LastBumpTime; internal string PreparedText; }
        static readonly ProfilerMarker TickMarker = new ProfilerMarker(ProfilerCategory.Scripts, "NotificationService.Tick");
        readonly UIRoot root;
        readonly IFrameClock clock;
        readonly VisualElement column;
        readonly ToastSlot[] pool;
        readonly List<ToastSlot> visible;
        readonly Queue<QueuedToast> queue;
        readonly int maxVisible, maxQueued;
        readonly float lifetime, coalesceWindow;
        readonly CustomStyleProperty<string> normalDuration = new CustomStyleProperty<string>("--duration-normal");
        readonly CustomStyleProperty<string> fastDuration = new CustomStyleProperty<string>("--duration-fast");
        float exitSeconds = .2f, bumpSeconds = .1f;
        bool reducedMotion, disposed;
        bool ReduceMotion => root != null ? root.ReduceMotion : reducedMotion;

        internal NotificationService(UIRoot root, UIConfig config, IFrameClock clock)
            : this(root.GetLayer(UILayer.Toasts), config, clock) { this.root = root; }
        // Detached tests use the same queue, pool, content and timers as the runtime service.
        internal NotificationService(VisualElement layer, UIConfig config, IFrameClock clock)
        {
            if (layer == null) throw new ArgumentNullException(nameof(layer));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (config == null || config.ToastUxml == null || config.MaxVisibleToasts < 1
                || config.MaxQueuedToasts < 0 || config.ToastPoolSize < config.MaxVisibleToasts
                || !float.IsFinite(config.ToastLifetimeSeconds) || config.ToastLifetimeSeconds <= 0
                || !float.IsFinite(config.ToastCoalesceWindowSeconds) || config.ToastCoalesceWindowSeconds < 0)
                throw new InvalidOperationException("UIConfig requires toast UXML, a sufficient pool, valid capacities and finite toast timers");
            maxVisible = config.MaxVisibleToasts; maxQueued = config.MaxQueuedToasts;
            lifetime = config.ToastLifetimeSeconds; coalesceWindow = config.ToastCoalesceWindowSeconds;
            pool = new ToastSlot[config.ToastPoolSize]; visible = new List<ToastSlot>(maxVisible);
            queue = new Queue<QueuedToast>(maxQueued);
            column = new VisualElement { name = "toast-column", pickingMode = PickingMode.Ignore };
            column.AddToClassList(UIStyleClasses.ToastColumn);
            for (int i = 0; i < pool.Length; i++)
            {
                var tree = config.ToastUxml.Instantiate();
                var element = Require<VisualElement>(tree, "toast");
                var icon = Require<Label>(element, "toast-icon");
                var text = Require<Label>(element, "toast-text");
                element.name = "toast-" + i; icon.name = element.name + "-icon"; text.name = element.name + "-text";
                element.AddToClassList(UIStyleClasses.IsHidden);
                column.Add(element); pool[i] = new ToastSlot { Element = element, Icon = icon, Text = text };
            }
            // Subscribe only once the entire template/pool has been validated.
            column.RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
            layer.Add(column);
        }
        static T Require<T>(VisualElement parent, string name) where T : VisualElement
            => parent.Q<T>(name) ?? throw new InvalidOperationException("Toast UXML missing '" + name + "'");
        void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(NotificationService)); }
        public int VisibleCount => visible.Count;
        public int QueuedCount => queue.Count;
        public void Post(in Notification notification)
        {
            CheckAlive();
            if (notification.Kind < NotificationKind.Info || notification.Kind > NotificationKind.Danger)
                throw new ArgumentOutOfRangeException(nameof(notification), "Unknown notification kind");
            float now = clock.UnscaledTime;
            if (!string.IsNullOrEmpty(notification.CoalesceKey))
            {
                for (int i = 0; i < visible.Count; i++)
                {
                    var slot = visible[i];
                    if (!Matches(slot.Data, slot.LastBumpTime, notification, now)) continue;
                    slot.Data.Amount = Sum(slot.Data.Amount, notification.Amount);
                    slot.LastBumpTime = now; slot.Life = lifetime; slot.Exiting = false; slot.ExitLeft = 0;
                    slot.Element.RemoveFromClassList(UIStyleClasses.ToastExit);
                    slot.Text.text = Text(slot.Data);
                    slot.Bumping = !ReduceMotion; slot.BumpLeft = bumpSeconds;
                    slot.Element.EnableInClassList(UIStyleClasses.ToastBump, slot.Bumping);
                    return;
                }
                if (CoalesceQueued(notification, now)) return;
            }
            if (visible.Count < maxVisible) { Show(notification, now, Text(notification)); return; }
            if (maxQueued == 0) return;
            if (queue.Count == maxQueued) DropOldestLowPriority();
            queue.Enqueue(new QueuedToast { Data = notification, LastBumpTime = now, PreparedText = Text(notification) });
        }
        bool Matches(in Notification existing, float lastBump, in Notification incoming, float now)
            => string.Equals(existing.CoalesceKey, incoming.CoalesceKey, StringComparison.Ordinal)
                && now >= lastBump && now - lastBump < coalesceWindow;
        bool CoalesceQueued(in Notification notification, float now)
        {
            bool matched = false;
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                var queued = queue.Dequeue();
                if (!matched && Matches(queued.Data, queued.LastBumpTime, notification, now))
                {
                    queued.Data.Amount = Sum(queued.Data.Amount, notification.Amount);
                    queued.LastBumpTime = now; queued.PreparedText = Text(queued.Data); matched = true;
                }
                queue.Enqueue(queued);
            }
            return matched;
        }
        void DropOldestLowPriority()
        {
            bool dropped = false;
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                var queued = queue.Dequeue();
                if (!dropped && (queued.Data.Kind == NotificationKind.Info || queued.Data.Kind == NotificationKind.Positive)) dropped = true;
                else queue.Enqueue(queued);
            }
            if (!dropped) queue.Dequeue();
        }
        static int Sum(int left, int right)
        {
            long sum = (long)left + right;
            return sum > int.MaxValue ? int.MaxValue : sum < int.MinValue ? int.MinValue : (int)sum;
        }
        static string Text(in Notification notification)
        {
            string label = notification.Label ?? "";
            if (notification.Amount == 0) return label;
            long magnitude = notification.Amount < 0 ? -(long)notification.Amount : notification.Amount;
            string number = magnitude <= int.MaxValue ? NumberStringCache.Get((int)magnitude) : magnitude.ToString(CultureInfo.InvariantCulture);
            return (notification.Amount > 0 ? "+" : "−") + number + " " + label;
        }
        void Show(in Notification notification, float lastBumpTime, string preparedText)
        {
            ToastSlot slot = null;
            for (int i = 0; i < pool.Length; i++) if (!pool[i].Used) { slot = pool[i]; break; }
            if (slot == null) throw new InvalidOperationException("Toast pool exhausted");
            slot.Data = notification; slot.Used = true; slot.Life = lifetime; slot.LastBumpTime = lastBumpTime;
            slot.EnterFrame = clock.FrameCount; slot.Entering = !ReduceMotion;
            slot.Element.EnableInClassList(UIStyleClasses.ToastInfo, notification.Kind == NotificationKind.Info);
            slot.Element.EnableInClassList(UIStyleClasses.ToastPositive, notification.Kind == NotificationKind.Positive);
            slot.Element.EnableInClassList(UIStyleClasses.ToastWarning, notification.Kind == NotificationKind.Warning);
            slot.Element.EnableInClassList(UIStyleClasses.ToastDanger, notification.Kind == NotificationKind.Danger);
            slot.Icon.text = notification.Kind == NotificationKind.Positive ? "✓" : notification.Kind == NotificationKind.Warning ? "⚠"
                : notification.Kind == NotificationKind.Danger ? "✕" : "ⓘ";
            slot.Text.text = preparedText;
            slot.Element.EnableInClassList(UIStyleClasses.ToastEnter, slot.Entering);
            slot.Element.RemoveFromClassList(UIStyleClasses.IsHidden);
            slot.Element.BringToFront(); visible.Add(slot);
        }
        internal void Tick(float unscaledDeltaTime, bool reduceMotion = false)
        {
            if (disposed) return;
            using (TickMarker.Auto())
            {
                reducedMotion = reduceMotion;
                float dt = float.IsFinite(unscaledDeltaTime) && unscaledDeltaTime > 0 ? unscaledDeltaTime : 0;
                bool motion = ReduceMotion;
                for (int i = 0; i < visible.Count;)
                {
                    var slot = visible[i];
                    if (slot.Entering && (motion || clock.FrameCount != slot.EnterFrame))
                    { slot.Entering = false; slot.Element.RemoveFromClassList(UIStyleClasses.ToastEnter); }
                    if (slot.Bumping)
                    {
                        slot.BumpLeft -= dt;
                        if (motion || slot.BumpLeft <= 0)
                        { slot.Bumping = false; slot.Element.RemoveFromClassList(UIStyleClasses.ToastBump); }
                    }
                    if (slot.Exiting) slot.ExitLeft -= dt;
                    else
                    {
                        slot.Life -= dt;
                        if (slot.Life <= 0)
                        {
                            slot.Exiting = true; slot.ExitLeft = (motion ? 0 : exitSeconds) + slot.Life;
                            slot.Element.RemoveFromClassList(UIStyleClasses.ToastBump); slot.Bumping = false;
                            slot.Element.AddToClassList(UIStyleClasses.ToastExit);
                        }
                    }
                    if (slot.Exiting && (motion || slot.ExitLeft <= 0))
                    { Release(slot); visible.RemoveAt(i); }
                    else i++;
                }
                while (queue.Count > 0 && visible.Count < maxVisible)
                {
                    var queued = queue.Dequeue(); Show(queued.Data, queued.LastBumpTime, queued.PreparedText);
                }
            }
        }
        void Release(ToastSlot slot)
        {
            slot.Used = slot.Entering = slot.Exiting = slot.Bumping = false;
            slot.Data = default; slot.Life = slot.LastBumpTime = slot.ExitLeft = slot.BumpLeft = 0;
            slot.Element.AddToClassList(UIStyleClasses.IsHidden);
            slot.Element.RemoveFromClassList(UIStyleClasses.ToastEnter);
            slot.Element.RemoveFromClassList(UIStyleClasses.ToastExit);
            slot.Element.RemoveFromClassList(UIStyleClasses.ToastBump);
            slot.Icon.text = ""; slot.Text.text = "";
        }
        public void ClearAll()
        {
            CheckAlive();
            for (int i = 0; i < visible.Count; i++) Release(visible[i]);
            visible.Clear(); queue.Clear();
        }
        void OnStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(normalDuration, out string normal)) exitSeconds = Seconds(normal, .2f);
            if (evt.customStyle.TryGetValue(fastDuration, out string fast)) bumpSeconds = Seconds(fast, .1f);
        }
        static float Seconds(string text, float fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            bool milliseconds = text.EndsWith("ms", StringComparison.Ordinal);
            int suffix = milliseconds ? 2 : text.EndsWith("s", StringComparison.Ordinal) ? 1 : 0;
            if (!float.TryParse(text.AsSpan(0, text.Length - suffix), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                || !float.IsFinite(value) || value < 0) return fallback;
            return milliseconds ? value * .001f : value;
        }
        internal void Dispose()
        {
            if (disposed) return;
            ClearAll(); disposed = true;
            column.UnregisterCallback<CustomStyleResolvedEvent>(OnStyleResolved); column.RemoveFromHierarchy();
        }
    }
}
