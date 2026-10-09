using System.Collections;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Smoke
{
    /// <summary>Proves the PlayMode runner and the build scene list work. SPEC-01 extends this into the real boot test.</summary>
    [Category("Smoke")]
    public class BootSmokeTests
    {
        [UnityTest]
        public IEnumerator BootScene_Loads_WithoutErrors()
        {
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            yield return null;
            Assert.AreEqual("Boot", SceneManager.GetActiveScene().name);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
