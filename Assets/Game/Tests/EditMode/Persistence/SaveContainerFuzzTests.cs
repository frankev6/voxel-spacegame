using System;
using System.Collections.Generic;
using Game.Data.Validation;
using Game.Persistence.Container;
using NUnit.Framework;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>PERS-003: random damage never throws from Read and is never reported as Ok (CRC-32 catches every single-byte error).</summary>
    public sealed class SaveContainerFuzzTests
    {
        static ContainerReadStatus Read(byte[] file)
            => SaveContainerReader.Read(file, out _, new List<RawChunk>(), new ValidationReport("fuzz"), "fuzz");

        [Test]
        public void RandomFlips_NeverThrow()
        {
            var good = SaveContainerWriter.Write(SaveContainerTests.Header, SaveContainerTests.Sample(), 64);
            var rng = new Random(20261011);
            for (int i = 0; i < 1000; i++)
            {
                var bad = (byte[])good.Clone();
                int at = rng.Next(bad.Length);
                bad[at] ^= (byte)rng.Next(1, 256);
                ContainerReadStatus status = default;
                Assert.DoesNotThrow(() => status = Read(bad), $"flip {i} at {at}");
                Assert.AreNotEqual(ContainerReadStatus.Ok, status, $"flip {i} at {at} went unnoticed");
            }
        }

        [Test]
        public void RandomTruncations_NeverThrow()
        {
            var good = SaveContainerWriter.Write(SaveContainerTests.Header, SaveContainerTests.Sample(), 64);
            var rng = new Random(11);
            for (int i = 0; i < 200; i++)
            {
                int length = rng.Next(good.Length);
                var cut = good.AsSpan(0, length).ToArray();
                ContainerReadStatus status = default;
                Assert.DoesNotThrow(() => status = Read(cut), $"truncation to {length}");
                Assert.AreNotEqual(ContainerReadStatus.Ok, status, $"truncation to {length}");
            }
        }
    }
}
