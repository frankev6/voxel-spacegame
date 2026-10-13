using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.UI
{
    /// <summary>SPEC-60 §5.12: real themed runtime panel. Callers yield one frame after Load before auditing layout.</summary>
    internal sealed class UITestPanel : IDisposable
    {
        internal static IReadOnlyList<Vector2Int> LogicalSizes { get; } = Array.AsReadOnly(new[]
        {
            new Vector2Int(1920, 1080), new Vector2Int(1663, 1247), new Vector2Int(2226, 932),
            new Vector2Int(1821, 1138), new Vector2Int(1280, 720)
        });
        GameObject host;
        RenderTexture texture;
        bool disposed;
        internal PanelSettings Settings { get; private set; }
        internal UIDocument Document { get; private set; }
        internal VisualElement Root => Document.rootVisualElement;
        internal Rect Viewport { get; }

        internal UITestPanel(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Viewport = new Rect(0, 0, width, height);
            var source = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Game/UI/Theme/GamePanelSettings.asset");
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Game/UI/Theme/Game.tss");
            if (source == null || theme == null) throw new InvalidOperationException("UI test panel requires the production panel settings and theme.");
            try
            {
                Settings = Object.Instantiate(source);
                Settings.scaleMode = PanelScaleMode.ConstantPixelSize;
                Settings.scale = 1;
                Settings.themeStyleSheet = theme;
                texture = new RenderTexture(width, height, 0);
                Settings.targetTexture = texture;
                Settings.SetScreenToPanelSpaceFunction(position => position);
                host = new GameObject("UI audit test panel");
                Document = host.AddComponent<UIDocument>();
                Document.panelSettings = Settings;
            }
            catch { Dispose(); throw; }
        }
        internal VisualElement Load(VisualTreeAsset asset)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UITestPanel));
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            Root.Clear();
            var tree = asset.CloneTree();
            tree.style.flexGrow = 1;
            Root.Add(tree);
            return tree;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (host != null) host.SetActive(false);
            if (Document != null) Document.panelSettings = null;
            if (Settings != null) Settings.targetTexture = null;
            if (texture != null) { texture.Release(); Object.Destroy(texture); }
            if (host != null) Object.Destroy(host);
            if (Settings != null) Object.Destroy(Settings);
        }
    }
}
