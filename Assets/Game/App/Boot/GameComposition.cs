using System;
using System.Collections.Generic;
using Game.App.Services;
using Game.App.Settings;
using Game.Core;
using Game.Core.Settings;
using Game.Input;
using Game.Persistence.Container;

namespace Game.App.Boot
{
    /// <summary>Builds every service in the order of SPEC-01 §5.2 and disposes them in reverse. Implements SPEC-01 §4.5, §5.2. A step whose
    /// type does not exist yet is a comment naming the task that adds it; each task adds exactly its own line.</summary>
    internal sealed class GameComposition : IDisposable
    {
        internal readonly List<IDisposable> DisposeStack = new List<IDisposable>();

        internal SettingsService Settings { get; private set; }
        internal InputService Input { get; private set; }
        internal PauseService Pause { get; private set; }

        GameComposition() { }

        internal static GameComposition Build(GameRoot root, GameRootConfig config, SettingsPaths settingsPaths)
        {
            var c = new GameComposition();
            try
            {
                c.BuildSteps(root, config, settingsPaths);
                return c;
            }
            catch (Exception e)
            {
                c.Dispose();
                GameLog.Exception(LogCategories.Boot, e, "GameComposition.Build");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
                throw;
            }
        }

        void BuildSteps(GameRoot root, GameRootConfig config, SettingsPaths settingsPaths)
        {
            // 1. Settings (FND-004).
            Settings = Push(new SettingsService(new NewtonsoftSaveJsonCodec(), settingsPaths));
            Settings.Load();
            Game.Core.Services.Register(Settings);
            Game.Core.Services.Register<ISettingsService>(Settings);

            // 2. TODO(FND-005): content = new ContentService(config.ContentDatabase); content.CreateSingletons(em);
            //    BootSingletons.CreateAll(em, content.Catalog); blueprints; VoxelStructureRegistry; VoxelStructureRegistryRef.

            // 3. Input (INP-002).
            Input = Push(new InputService(Settings.Current.Controls));
            Game.Core.Services.Register(Input);

            // 4. TODO(AUD-003): audio = new AudioService(...); audio.BindTo(world).

            // 5. Pause (FND-003). TODO(AUD-003): audio.BindPause(pause).
            Pause = Push(new PauseService());
            Game.Core.Services.Register(Pause);
            Game.Core.Services.Register<IPauseService>(Pause);

            // 6. TODO(PERS-005): save = new SaveService(savePaths, content, pause, new SaveServiceConfig()).
            // 7. TODO(FND-006): flow = new SceneFlow(world, pause, save, gameConfig).
            // 8–13: session, galaxy, travel, spawner, factions, map, economy, narrative, UI, camera, FX and dialog are added by their
            //       tasks, in the rows of SPEC-01 §5.2 (FND-009, GAL-008, TRV-006, FND-012, ENC-007, GAL-011, ECO-008, UI-003, …).

            // 14. Settings applier (FND-004): follows SettingsChanged, then pushes every section once.
            Push(new SettingsApplier(Settings));
            SettingsApplier.ApplyAll(Settings.Current);

            // 15. TODO(FND-006): flow.SnapshotBootEntities().
            // 16. TODO(DEV-003): dev = DevRuntimeHost.Create(root.transform) (#if UNITY_EDITOR || DEVELOPMENT_BUILD).
        }

        T Push<T>(T service) where T : IDisposable
        {
            DisposeStack.Add(service);
            return service;
        }

        /// <summary>Disposes DisposeStack in reverse (one failure cannot leak the rest), then Services.Reset.</summary>
        public void Dispose()
        {
            for (int i = DisposeStack.Count - 1; i >= 0; i--)
            {
                try { DisposeStack[i].Dispose(); }
                catch (Exception e) { GameLog.Exception(LogCategories.Boot, e, "GameComposition.Dispose " + DisposeStack[i].GetType().Name); }
            }
            DisposeStack.Clear();
            Settings = null; Input = null; Pause = null;
            Game.Core.Services.Reset();
        }
    }
}
