using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Voxel
{
    public sealed class BrickRenderScreenshotTests
    {
        [UnityTest] public IEnumerator Ship20k_MatchesReference()
        {
            using var f = new BrickRenderFixture("ship"); yield return f.Ready(); yield return f.WaitFrames(60);
            var actual = f.ReadPixels(); var reference = new Texture2D(2,2,TextureFormat.RGB24,false,true);
            try
            {
                const string output = "Logs/playtest/ren013/Ship20k.png";
                const string baseline = "Assets/Game/Tests/PlayMode/Voxel/Reference/Ship20k.png";
                Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllBytes(output,actual.EncodeToPNG());
                var pixels = actual.GetPixels32(); int nonblack = 0; foreach (var p in pixels) if (p.r+p.g+p.b > 12) nonblack++;
                Assert.Greater(nonblack,10000,"Reference must contain visible rendered geometry");
                Assert.IsTrue(File.Exists(baseline),"Inspect candidate " + output + " and store the initial reference after the Editor exits.");
                Assert.IsTrue(reference.LoadImage(File.ReadAllBytes(baseline))); Assert.AreEqual(actual.width,reference.width); Assert.AreEqual(actual.height,reference.height);
                var expected = reference.GetPixels32(); double difference = 0;
                for (int i=0;i<pixels.Length;i++) difference += Math.Abs(pixels[i].r-expected[i].r)+Math.Abs(pixels[i].g-expected[i].g)+Math.Abs(pixels[i].b-expected[i].b);
                double error = difference/(pixels.Length*3.0*255.0);
                TestContext.WriteLine($"Ship20k reference MAE {error:F8}; {nonblack} nonblack pixels, {f.BrickCount} applied bricks");
                Assert.LessOrEqual(error,2.0/255,"Shader/mesher screenshot regression");
            }
            finally { Object.DestroyImmediate(actual); Object.DestroyImmediate(reference); }
        }
    }
}
