using Game.Core;
using Game.Tests.EditMode.Data;
using Game.Voxel.Data;
using NUnit.Framework;
using Unity.Collections;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>SPEC-05 PERS-007: arbitrary wire bytes cannot escape TryDecode as exceptions.</summary>
    public sealed class VoxsCodecFuzzTests
    {
        [TearDown] public void TearDown()=>TestContentFactory.DestroyAll();
        [Test] public void RandomFlips_NeverThrow()
        {
            var c=VoxsCodecTests.Catalog();using var s=VoxsTestStructures.Random(c,1);
            byte[] bytes=VoxsCodec.Encode(s.Source,VoxsBlockNames.FromCatalog(c),VoxsEncodeOptions.ForSaveChunk);
            var rng=RngStreams.For(711,RngPurpose.Test,0);
            for(int i=0;i<1000;i++)
            {
                byte[] changed=(byte[])bytes.Clone();int offset=rng.NextInt(changed.Length);changed[offset]^=(byte)(1<<rng.NextInt(8));
                VoxsDecoded d=null;
                try{Assert.DoesNotThrow(()=>VoxsCodec.TryDecode(changed,c,Allocator.Persistent,out d,out _),"flip "+i);}
                finally{d?.Dispose();}
            }
        }
    }
}
