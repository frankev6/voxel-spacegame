using Game.Core;
using Game.UI.Data;
using Game.UI.ViewModels;
using Game.UI.Views;
using NUnit.Framework;

namespace Game.Tests.EditMode.UI
{
    /// <summary>HUD-001: literal SPEC-61 §4.1 screen metadata, independent of composition and assets.</summary>
    [Category("UI")]
    public sealed class ScreenTableTests
    {
        // Views require their commands (HUD-002/018); the flag table never invokes them, so a null object stands in.
        public class NullCommands : System.Reflection.DispatchProxy
        {
            protected override object Invoke(System.Reflection.MethodInfo method, object[] args)
                => method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? System.Activator.CreateInstance(method.ReturnType) : null;
        }
        static T Null<T>() where T : class => System.Reflection.DispatchProxy.Create<T, NullCommands>();
        readonly struct Expected
        {
            public readonly string Key;
            public readonly UILayer Layer;
            public readonly InputContext Context;
            public readonly bool Pauses, BlocksPointer, HidesHud, IsRoot;

            public Expected(string key, UILayer layer, InputContext context, bool pauses, bool blocksPointer, bool hidesHud, bool isRoot)
            {
                Key = key; Layer = layer; Context = context;
                Pauses = pauses; BlocksPointer = blocksPointer; HidesHud = hidesHud; IsRoot = isRoot;
            }
        }

        [Test]
        public void EveryScreen_FlagsMatchSpecTable()
        {
            var screens = new UIScreen[]
            {
                new MainMenuView(new MainMenuViewModel(), Null<IMainMenuCommands>()),
                new SaveSlotsView(new SaveSlotsViewModel(), Null<ISaveSlotsCommands>()),
                new SettingsView(new SettingsViewModel(), Null<ISettingsCommands>()),
                new PauseView(new PauseViewModel(), Null<IPauseCommands>()),
                new FlightHudView(new HudViewModel(), null, null),
                new InventoryView(new InventoryViewModel(), Null<IInventoryCommands>()),
                new MerchantView(new MerchantViewModel(), Null<IMerchantCommands>()),
                new DialogView(new DialogViewModel(), Null<IDialogCommands>()),
                new ShipLogView(new ShipLogViewModel(), Null<IShipLogCommands>()),
                new GalaxyMapView(new GalaxyMapViewModel(), Null<IGalaxyMapCommands>()),
                new StarSystemMapView(new StarSystemMapViewModel(), Null<IStarSystemMapCommands>()),
                new FleetView(new FleetViewModel(), Null<IFleetCommands>()),
                new ShipEditorView(new ShipEditorViewModel(), Null<IShipEditorCommands>()),
                new DockView(new DockViewModel(), Null<IDockCommands>()),
                new LoadingView(new LoadingViewModel())
            };
            var expected = new[]
            {
                new Expected("main-menu", UILayer.Screens, InputContext.Menu, false, true, false, true),
                new Expected("save-slots", UILayer.Screens, InputContext.Menu, true, true, true, false),
                new Expected("settings", UILayer.Modals, InputContext.Menu, true, true, false, false),
                new Expected("pause", UILayer.Modals, InputContext.Menu, true, true, false, false),
                new Expected("hud", UILayer.Hud, InputContext.Flight, false, false, false, true),
                new Expected("inventory", UILayer.Screens, InputContext.Menu, true, true, false, false),
                new Expected("merchant", UILayer.Screens, InputContext.Menu, true, true, true, false),
                new Expected("dialog", UILayer.Screens, InputContext.Dialog, true, true, true, false),
                new Expected("ship-log", UILayer.Screens, InputContext.Menu, true, true, true, false),
                new Expected("galaxy-map", UILayer.Screens, InputContext.Menu, true, true, true, false),
                new Expected("system-map", UILayer.Screens, InputContext.Menu, true, true, true, false),
                new Expected("fleet", UILayer.Screens, InputContext.Menu, true, true, true, false),
                new Expected("ship-editor", UILayer.Hud, InputContext.Editor, false, false, false, true),
                new Expected("dock", UILayer.Screens, InputContext.Menu, false, true, true, false),
                new Expected("loading", UILayer.Modals, InputContext.None, true, true, true, true)
            };
            Assert.AreEqual(expected.Length, screens.Length);
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i]; var row = expected[i];
                Assert.AreEqual(row.Key, screen.Key, screen.GetType().Name);
                Assert.AreEqual(row.Layer, screen.Layer, row.Key);
                Assert.AreEqual(row.Context, screen.Context, row.Key);
                Assert.AreEqual(row.Pauses, screen.PausesSimulation, row.Key);
                Assert.AreEqual(row.BlocksPointer, screen.BlocksWorldPointer, row.Key);
                Assert.AreEqual(row.HidesHud, screen.HidesHud, row.Key);
                Assert.AreEqual(row.IsRoot, screen.IsRoot, row.Key);
            }
        }
    }
}
