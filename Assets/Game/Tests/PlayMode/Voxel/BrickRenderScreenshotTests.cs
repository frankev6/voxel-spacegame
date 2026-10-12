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
                // Union of expected/actual foreground catches both missing sections and unexpected geometry.
                const int tile = 64;
                int columns = (actual.width + tile - 1) / tile, rows = (actual.height + tile - 1) / tile;
                var regionDifference = new double[columns * rows]; var regionPixels = new int[columns * rows];
                double shipDifference = 0; int shipPixels = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    var a = pixels[i]; var e = expected[i];
                    if (a.r+a.g+a.b <= 12 && e.r+e.g+e.b <= 12) continue;
                    double delta = Math.Abs(a.r-e.r)+Math.Abs(a.g-e.g)+Math.Abs(a.b-e.b);
                    shipDifference += delta; shipPixels++;
                    int region = (i / actual.width / tile) * columns + (i % actual.width / tile);
                    regionDifference[region] += delta; regionPixels[region]++;
                }
                Assert.Greater(shipPixels, 0);
                double shipError = shipDifference / (shipPixels * 3.0 * 255.0), worstRegion = 0;
                for (int region = 0; region < regionPixels.Length; region++)
                    if (regionPixels[region] > 0) worstRegion = Math.Max(worstRegion, regionDifference[region] / (regionPixels[region] * 3.0 * 255.0));
                TestContext.WriteLine($"Ship foreground MAE {shipError:F8}; worst 64px region {worstRegion:F8}");
                Assert.LessOrEqual(shipError, 2.0/255, "Ship foreground screenshot regression");
                Assert.LessOrEqual(worstRegion, 8.0/255, "Localized ship section screenshot regression");
            }
            finally { Object.DestroyImmediate(actual); Object.DestroyImmediate(reference); }
        }
    }
}
