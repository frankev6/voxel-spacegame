using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// The project's required Unity settings in one place. <see cref="ApplyAll"/> is idempotent;
    /// <see cref="Check"/> lists deviations and is asserted by ProjectSettingsGuardTests so settings can't drift.
    /// Change a value here only together with an ADR (docs/decisions).
    /// </summary>
    public static class ProjectSetup
    {
        public const string CompanyName = "Koala Studios";            // ADR-0012 (working values)
        public const string ProductName = "Spaceship Game V2";
        public const string BundleId = "com.koalastudios.spaceshipgame.v2";
        public const float FixedTimestep = 1f / 60f;                  // docs/01-architecture.md §2
        public const float MaximumDeltaTime = 0.1f;                   // §5.1 catch-up cap: at most 6 fixed ticks per frame
        public const string ScenesFolder = "Assets/Game/Scenes";
        public const string RenderPipelineAsset = "Assets/Game/Settings/Rendering/URP_Default.asset";

        /// <summary>Build order is binding: docs/01-architecture.md §4.1.</summary>
        public static readonly string[] Scenes = { "Boot", "MainMenu", "World", "BlueprintLab" };

        [MenuItem("Game/Project Setup/Apply Required Settings")]
        public static void ApplyAll()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleId);
            PlayerSettings.runInBackground = true;              // the CLI harness drives the game while unfocused
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.defaultCursor = null;

            EditorSettings.enterPlayModeOptionsEnabled = true;  // no domain reload: statics must reset in SubsystemRegistration
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            Time.fixedDeltaTime = FixedTimestep; // "Fixed Timestep" in ProjectSettings/TimeManager.asset must read 0.016666668
            Time.maximumDeltaTime = MaximumDeltaTime;
            SetProjectSetting("ProjectSettings/ProjectSettings.asset", "activeInputHandler", 1); // Input System only
            Physics.simulationMode = SimulationMode.Script;    // PhysX never simulates; Unity Physics is the only gameplay physics

            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(RenderPipelineAsset);
            if (rp != null && GraphicsSettings.defaultRenderPipeline != rp) GraphicsSettings.defaultRenderPipeline = rp;

            EnsureInputSettings();
            EnsureScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("ProjectSetup.ApplyAll: done");
        }

        /// <summary>Every deviation from the required settings, as human-readable lines. Empty = OK.</summary>
        public static List<string> Check()
        {
            var issues = new List<string>();
            void Expect(bool ok, string what) { if (!ok) issues.Add(what); }

            Expect(PlayerSettings.companyName == CompanyName, $"companyName should be '{CompanyName}'");
            Expect(PlayerSettings.productName == ProductName, $"productName should be '{ProductName}'");
            Expect(PlayerSettings.runInBackground, "runInBackground should be true");
            Expect(PlayerSettings.colorSpace == ColorSpace.Linear, "color space should be Linear");
            Expect(EditorSettings.enterPlayModeOptionsEnabled &&
                   (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0,
                   "Enter Play Mode Options should be on with domain reload disabled");
            Expect(Mathf.Abs(Time.fixedDeltaTime - FixedTimestep) < 1e-6f, $"fixed timestep should be 1/60 (is {Time.fixedDeltaTime})");
            Expect(Mathf.Abs(Time.maximumDeltaTime - MaximumDeltaTime) < 1e-6f, $"maximum allowed timestep should be {MaximumDeltaTime} (is {Time.maximumDeltaTime})");
            Expect(GetProjectSettingInt("ProjectSettings/ProjectSettings.asset", "activeInputHandler") == 1, "active input handling should be Input System only");
            Expect(Physics.simulationMode == SimulationMode.Script, "Physics.simulationMode should be Script (PhysX must not simulate)");
            Expect(GraphicsSettings.defaultRenderPipeline != null &&
                   AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline) == RenderPipelineAsset,
                   $"default render pipeline should be {RenderPipelineAsset}");
            Expect(UnityEditor.EditorBuildSettings.TryGetConfigObject(InputSettingsConfigKey, out UnityEngine.InputSystem.InputSettings input) &&
                   AssetDatabase.GetAssetPath(input) == InputSettingsAsset && input.shortcutKeysConsumeInput,
                   $"Input System settings should be {InputSettingsAsset} with shortcutKeysConsumeInput = true (SPEC-03 §4.2)");
            var build = EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path)).ToArray();
            Expect(build.SequenceEqual(Scenes), $"build scenes should be [{string.Join(", ", Scenes)}] (are [{string.Join(", ", build)}])");
            return issues;
        }

        const string InputSettingsConfigKey = "com.unity.input.settings";   // InputSettingsProvider.kEditorBuildSettingsConfigKey
        const string InputSettingsAsset = "Assets/Game/Input/InputSystem.inputsettings.asset";

        /// <summary>SPEC-03 §4.2: shortcut keys (Ctrl+Z) consume input so plain-key neighbours (Z) do not also fire.</summary>
        static void EnsureInputSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputSettings>(InputSettingsAsset);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<UnityEngine.InputSystem.InputSettings>();
                AssetDatabase.CreateAsset(settings, InputSettingsAsset);
            }
            settings.shortcutKeysConsumeInput = true;
            EditorUtility.SetDirty(settings);
            EditorBuildSettings.AddConfigObject(InputSettingsConfigKey, settings, true);
            UnityEngine.InputSystem.InputSystem.settings = settings;
        }

        static void EnsureScenes()
        {
            Directory.CreateDirectory(ScenesFolder);
            var list = new List<EditorBuildSettingsScene>();
            foreach (var name in Scenes)
            {
                var path = $"{ScenesFolder}/{name}.unity";
                if (!File.Exists(path))
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    EditorSceneManager.SaveScene(scene, path);
                }
                list.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void SetProjectSetting(string assetPath, string property, float value)
        {
            var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(assetPath)[0]);
            var p = so.FindProperty(property);
            if (p == null) { Debug.LogError($"ProjectSetup: {assetPath} has no '{property}'"); return; }
            if (p.propertyType == SerializedPropertyType.Integer) p.intValue = (int)value; else p.doubleValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static int GetProjectSettingInt(string assetPath, string property)
        {
            var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(assetPath)[0]);
            return so.FindProperty(property)?.intValue ?? -1;
        }

        // ---- batch-mode entry points (tools/compile.sh, first-time setup) ----

        /// <summary>Batch: the project compiled if this method can run at all. Prints the marker compile.sh looks for.</summary>
        public static void VerifyCompileAndQuit()
        {
            var issues = Check();
            foreach (var i in issues) Debug.LogWarning("ProjectSetup.Check: " + i);
            Debug.Log("VerifyCompileAndQuit: OK");
            EditorApplication.Exit(0);
        }

        public static void ApplyAllAndQuit()
        {
            ApplyAll();
            var issues = Check();
            foreach (var i in issues) Debug.LogError("ProjectSetup.Check: " + i);
            Debug.Log(issues.Count == 0 ? "ApplyAllAndQuit: OK" : $"ApplyAllAndQuit: {issues.Count} issue(s)");
            EditorApplication.Exit(issues.Count == 0 ? 0 : 1);
        }
    }
}
