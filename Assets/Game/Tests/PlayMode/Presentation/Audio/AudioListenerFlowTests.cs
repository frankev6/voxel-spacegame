using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Flow;
using Game.App.Boot;
using Game.Tests.PlayMode.App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Presentation.Audio
{
    /// <summary>SPEC-64 AUD-004: production SceneFlow preserves exactly one enabled listener.</summary>
    public sealed class AudioListenerFlowTests : ProductionSessionFixture
    {
        [UnityTest] public IEnumerator SceneTransitions_ExactlyOneListener_NoWarnings()
        {
            var warnings = new List<string>();
            void Capture(string message, string stack, LogType type)
            {
                if (message.IndexOf("no audio listeners", StringComparison.OrdinalIgnoreCase) >= 0) warnings.Add(message);
            }
            Application.logMessageReceived += Capture;
            try
            {
                AudioListener listener = null;
                foreach (var step in new[]
                {
                    (GameScene.Boot, SceneEntryReason.Startup),
                    (GameScene.MainMenu, SceneEntryReason.Startup),
                    (GameScene.World, SceneEntryReason.NewGame),
                    (GameScene.BlueprintLab, SceneEntryReason.EnterBlueprintLab),
                    (GameScene.MainMenu, SceneEntryReason.ExitBlueprintLab),
                })
                {
                    yield return Run(Game.Flow.TransitionAsync(step.Item1, step.Item2));
                    yield return null;
                    Assert.AreEqual(step.Item1.ToString(), SceneManager.GetActiveScene().name);
                    var enabled = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include)
                        .Where(a => a.isActiveAndEnabled).ToArray();
                    Assert.AreEqual(1, enabled.Length, step.Item1 + " enabled listener count");
                    if (listener == null) listener = enabled[0];
                    else Assert.AreSame(listener, enabled[0], "Service listener survives " + step.Item1);
                    Assert.IsTrue(listener.transform.IsChildOf(GameRoot.Instance.transform));
                    Assert.IsEmpty(warnings, step.Item1 + " missing-listener warnings");
                }
            }
            finally { Application.logMessageReceived -= Capture; }
        }
    }
}
