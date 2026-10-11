using System.Collections;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Voxel
{
    public sealed class DeferredDisposalTests
    {
        [UnityTest] public IEnumerator Mesh_UnregisteredAfterThreeFrames()
        {
            using var world = new World("DeferredMeshFrames"); var em = world.EntityManager;
            world.GetOrCreateSystemManaged<PresentationSystemGroup>();
            // The ordinary headless suite covers absent-renderer cleanup too. A GPU batch run verifies real registration.
            var graphics = EntitiesGraphicsSystem.EntitiesGraphicsEnabled ? world.GetOrCreateSystemManaged<EntitiesGraphicsSystem>() : null;
            world.CreateSystem<GridSpawnSystem>(); var system = world.CreateSystem<DeferredDisposalSystem>();
            using var query = em.CreateEntityQuery(typeof(DeferredDisposalQueue));
            var queue = query.GetSingleton<DeferredDisposalQueue>();
            var mesh = new Mesh { name = "DeferredDisposal test triangle" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }; mesh.triangles = new[] { 0, 1, 2 };
            BatchMeshID id = graphics != null ? graphics.RegisterMesh(mesh) : BatchMeshID.Null;
            try
            {
                if (graphics != null) { Assert.AreNotEqual(BatchMeshID.Null, id); Assert.AreSame(mesh, graphics.GetMesh(id)); }
                DeferredDisposal.RetireMesh(ref queue, mesh, id);
                for (int i = 0; i < 2; i++)
                {
                    system.Update(world.Unmanaged); Assert.AreEqual(1, queue.Meshes.Length); Assert.IsTrue(mesh != null);
                    if (graphics != null) Assert.AreSame(mesh, graphics.GetMesh(id));
                }
                system.Update(world.Unmanaged); Assert.AreEqual(0, queue.Meshes.Length);
                if (graphics != null) Assert.IsNull(graphics.GetMesh(id));
                yield return null; Assert.IsTrue(mesh == null, "Destroy completes at frame end");
                TestContext.WriteLine(graphics == null ? "No graphics: mesh lifetime passed; GPU registration requires the GPU batch run." : "Entities Graphics registration/unregistration verified.");
            }
            finally { if (mesh != null) Object.Destroy(mesh); }
        }
    }
}
