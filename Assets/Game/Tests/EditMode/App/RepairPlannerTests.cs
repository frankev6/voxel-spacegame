using System.Collections.Generic;
using Game.App.Repair;
using Game.Tests.EditMode.Voxel;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.App
{
    /// <summary>ABL-006 planner: engines first, attached cells only, the repair fraction and the hull-item cap.</summary>
    public sealed class RepairPlannerTests
    {
        sealed class Live : RepairPlanner.ILive
        {
            public readonly Dictionary<int3, Cell> Cells = new(); public readonly List<BlockInstance> Instances = new();
            public Cell CellAt(int3 cell) => Cells.TryGetValue(cell, out var value) ? value : default;
            public int InstanceCount => Instances.Count;
            public BlockInstance Instance(int index) => Instances[index];
        }
        sealed class Fixture : System.IDisposable
        {
            public readonly VoxelTestContent Content = new VoxelTestContent();
            public readonly VoxelStructure Structure = new VoxelStructure();
            public RepairPlanner.Design Design; public Live Live;
            public int Thruster = -1;
            // A core cross at the origin (its cells reach z = -1), a hull spine along -z from z = -2 to -8, a hull slab off +x, and a thruster
            // on the spine's end.
            public Fixture()
            {
                ushort hull = Structure.FindOrAddPalette(new PaletteEntry { Block = Content.Index("block_hull"), Color = 1 });
                Place("block_core_cross", new int3(0));
                for (int z = -2; z >= -8; z--) Structure.SetCell(new int3(0, 1, z), Cell.Palette(hull));
                for (int x = 2; x <= 6; x++) for (int z = -2; z <= 2; z++) Structure.SetCell(new int3(x, 1, z), Cell.Palette(hull));
                Place("block_thruster_medium", new int3(-1, 1, -10));   // its cells reach z = -9, touching the spine
                for (int i = 0; i < Structure.Data.Instances.Length; i++)
                    if (Structure.Data.Instances[i].Block == Content.Index("block_thruster_medium")) Thruster = i;
                Design = RepairPlanner.FromStructure(Structure);
                Live = new Live();
                foreach (var (cell, value) in Design.Cells) Live.Cells[cell] = value;
                foreach (var instance in Design.Instances) Live.Instances.Add(instance);
            }
            void Place(string id, int3 origin) =>
                Assert.AreEqual(PlaceResult.Ok, Structure.TryPlaceBlock(new BlockPlacement { Block = Content.Index(id), Origin = origin }, Content.Content));
            public void LoseThruster()
            {
                var instance = Live.Instances[Thruster]; instance.Flags |= BlockInstanceFlags.Destroyed; instance.CellsAlive = 0; instance.FunctionHp = 0;
                Live.Instances[Thruster] = instance;
                foreach (var (cell, value) in Design.Cells) if (value.IsInstance && value.InstanceIndex == Thruster) Live.Cells.Remove(cell);
            }
            public RepairPlan Plan(float fraction, int cap = int.MaxValue) =>
                RepairPlanner.Plan(Design, Live, ref Content.Service.BlockDefs.Value, ref Content.Service.Orientations.Value, fraction, cap);
            public void Dispose() { Structure.Dispose(); Content.Dispose(); }
        }

        [Test] public void NothingMissing_EmptyPlan()
        {
            using var f = new Fixture(); var plan = f.Plan(.35f);
            Assert.AreEqual(0, plan.Missing); Assert.IsEmpty(plan.Steps);
        }
        [Test] public void EnginesFirst()
        {
            using var f = new Fixture(); f.LoseThruster();
            for (int x = 2; x <= 6; x++) f.Live.Cells.Remove(new int3(x, 1, 2));   // hull lost elsewhere too
            var plan = f.Plan(1);
            Assert.AreEqual(1, plan.Engines);
            int revive = plan.Steps.FindIndex(s => s.Type == RepairStep.Kind.Revive);
            Assert.GreaterOrEqual(revive, 0);
            for (int i = 0; i < revive; i++) Assert.AreEqual(RepairStep.Kind.InstanceCell, plan.Steps[i].Type, "the engine's cells come before any hull");
            Assert.AreEqual(f.Thruster, plan.Steps[revive].Instance);
        }
        [Test] public void CutOffEngine_BringsItsStrut()
        {
            using var f = new Fixture(); f.LoseThruster();
            for (int z = -6; z >= -8; z--) f.Live.Cells.Remove(new int3(0, 1, z));   // the spine's last three cells
            var plan = f.Plan(1);
            var strut = new HashSet<int3> { new(0, 1, -6), new(0, 1, -7), new(0, 1, -8) };
            int firstEngineCell = plan.Steps.FindIndex(s => s.Type == RepairStep.Kind.InstanceCell);
            Assert.Greater(firstEngineCell, 0);
            for (int i = 0; i < firstEngineCell; i++) Assert.IsTrue(strut.Contains(plan.Steps[i].Cell), "only the strut lands before the engine");
            Assert.AreEqual(new int3(0, 1, -6), plan.Steps[0].Cell, "the strut lands from the ship outwards");
        }
        [Test] public void Fraction_LimitsHull()
        {
            using var f = new Fixture();
            for (int x = 2; x <= 6; x++) for (int z = -2; z <= 2; z++) if (x > 2) f.Live.Cells.Remove(new int3(x, 1, z));   // 20 hull cells
            var plan = f.Plan(.35f);
            Assert.AreEqual(20, plan.Missing); Assert.AreEqual(7, plan.Cells); Assert.AreEqual(7, plan.Steps.Count);
            for (int i = 0; i < 5; i++) Assert.AreEqual(3, plan.Steps[i].Cell.x, "breadth-first: the column touching the ship first");
            for (int i = 5; i < 7; i++) Assert.AreEqual(4, plan.Steps[i].Cell.x, "then the next layer");
        }
        [Test] public void FirstEngine_AllowedPastTheFraction()
        {
            using var f = new Fixture(); f.LoseThruster();
            var plan = f.Plan(.01f);
            Assert.AreEqual(1, plan.Engines);
        }
        [Test] public void HullItems_CapThePlan()
        {
            using var f = new Fixture(); f.LoseThruster();
            Assert.IsEmpty(f.Plan(1, 0).Steps, "nothing to pay with");
            for (int x = 3; x <= 6; x++) f.Live.Cells.Remove(new int3(x, 1, 0));
            var plan = f.Plan(1, 2);
            Assert.AreEqual(0, plan.Engines, "the engine costs more than the cap"); Assert.AreEqual(2, plan.Cells);
        }
        [Test] public void Deterministic()
        {
            using var f = new Fixture(); f.LoseThruster();
            for (int x = 2; x <= 6; x++) for (int z = -2; z <= 2; z++) f.Live.Cells.Remove(new int3(x, 1, z));
            var a = f.Plan(.5f); var b = f.Plan(.5f);
            Assert.AreEqual(a.Steps.Count, b.Steps.Count);
            for (int i = 0; i < a.Steps.Count; i++) { Assert.AreEqual(a.Steps[i].Cell, b.Steps[i].Cell); Assert.AreEqual(a.Steps[i].Type, b.Steps[i].Type); }
        }
    }
}
