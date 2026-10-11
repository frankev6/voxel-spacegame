using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Editor.Content
{
    /// <summary>DATA-008: the editor validation pass (SPEC-04 §5.7). Temporary assets live in Assets/Game/Content/_Test/ and
    /// Assets/Game/_ContentOutsideTest/ and are deleted in TearDown.</summary>
    public sealed class ContentValidatorRunnerTests
    {
        const string TestDir = "Assets/Game/Content/_Test", OutsideDir = "Assets/Game/_ContentOutsideTest";

        // Extension validators: inert unless a test turns them on.
        internal static bool Active, Throw;
        internal static readonly List<string> Calls = new List<string>();
        public sealed class FirstValidator : IContentDatabaseValidator { public int Order => 1; public void Validate(ContentCatalog c, ValidationReport r) { if (Active) Calls.Add("first"); } }
        public sealed class ThrowingValidator : IContentDatabaseValidator { public int Order => 3; public void Validate(ContentCatalog c, ValidationReport r) { if (Active && Throw) throw new InvalidOperationException("validator bug"); } }
        public sealed class LastValidator : IContentDatabaseValidator { public int Order => 5; public void Validate(ContentCatalog c, ValidationReport r) { if (Active) { Calls.Add("last"); r.Warning("TEST-W001", "test", "", "ran"); } } }

        [SetUp] public void SetUp() { Active = false; Throw = false; Calls.Clear(); AssetDatabase.CreateFolder("Assets/Game/Content", "_Test"); AssetDatabase.CreateFolder("Assets/Game", "_ContentOutsideTest"); }

        [TearDown]
        public void TearDown()
        {
            Active = false; Throw = false;
            AssetDatabase.DeleteAsset(TestDir); AssetDatabase.DeleteAsset(OutsideDir);
            DestroyAll();
            ContentDatabaseBuilder.Rebuild();
        }

        static T Save<T>(T obj, string path) where T : UnityEngine.Object { AssetDatabase.CreateAsset(obj, path); return obj; }

        static ValidationReport ValidateProject() => ContentValidatorRunner.Validate(ContentDatabaseBuilder.Rebuild());

        [Test]
        public void ReportsReferenceOutsideDatabase()
        {
            var outside = Save(Block("block_outside", BlockKind.Structural), OutsideDir + "/block_outside.asset");
            var inside = Save(Block("block_inside", BlockKind.Structural), TestDir + "/block_inside.asset");
            Edit(inside, so => Set(so, "mirrorPartner", outside));
            var r = ValidateProject();
            var e010 = r.Entries.Where(e => e.Code == "DATA-E010").ToList();
            Assert.AreEqual(1, e010.Count, r.ToText());
            Assert.AreEqual(TestDir + "/block_inside.asset", e010[0].AssetPath);
            Assert.AreEqual("mirrorPartner", e010[0].Field);
        }

        [Test]
        public void ReportsAssetOutsideContentFolder()
        {
            Save(Item("item_outside"), OutsideDir + "/item_outside.asset");
            var r = ValidateProject();
            Assert.IsTrue(r.Entries.Any(e => e.Code == "DATA-E011" && e.AssetPath == OutsideDir + "/item_outside.asset"), r.ToText());
        }

        [Test]
        public void ReportsFileNameMismatch()
        {
            Save(Item("item_right_name"), TestDir + "/wrong_name.asset");
            var r = ValidateProject();
            var w = r.Entries.Where(e => e.Code == "DATA-W012").ToList();
            Assert.AreEqual(1, w.Count, r.ToText());
            Assert.AreEqual(TestDir + "/wrong_name.asset", w[0].AssetPath);
            Assert.IsTrue(ContentDatabaseBuilder.Rebuild().Definitions.Any(d => d.IdString == "item_right_name"), "the builder picked the asset up");
        }

        [Test]
        public void RunsExtensionValidators_InOrder()
        {
            Active = true;
            var r = ValidateProject();
            CollectionAssert.AreEqual(new[] { "first", "last" }, Calls);
            Assert.IsTrue(r.Entries.Any(e => e.Code == "TEST-W001"));
        }

        [Test]
        public void ThrowingExtension_BecomesE099()
        {
            Active = true; Throw = true;
            var r = ValidateProject();
            var e099 = r.Entries.Where(e => e.Code == "DATA-E099").ToList();
            Assert.AreEqual(1, e099.Count);
            StringAssert.Contains("validator bug", e099[0].Message);
            CollectionAssert.AreEqual(new[] { "first", "last" }, Calls, "the others still ran");
        }

        [Test]
        public void Cli_ExitCodeReflectsErrors()
        {
            var db = ScriptableObject.CreateInstance<ContentDatabase>();
            try
            {
                var good = new List<ContentDefinition> { Block("block_missing", BlockKind.Structural), Item("item_missing") };
                db.EditorSetDefinitions(good);
                Assert.AreEqual(0, ContentValidationCli.RunAndGetExitCode(db), "valid content");
                good.Add(Item("item_bad", so => Set(so, "maxStack", 0)));
                db.EditorSetDefinitions(good);
                Assert.AreEqual(1, ContentValidationCli.RunAndGetExitCode(db), "an error");
            }
            finally { UnityEngine.Object.DestroyImmediate(db); }
        }

        [Test]
        public void MirrorPartnerCreator_ProducesValidPair()
        {
            var cells = new[] { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(1, 1, 0), new Vector3Int(1, 1, 1) };
            var left = Save(Block("block_chiral", BlockKind.Prop, cells, edit: so => { Set(so, "attachFaces", (int)(BlockFaceMask.PosX | BlockFaceMask.PosY)); Set(so, "clearance", new[] { new Vector3Int(2, 0, 0) }); }),
                            TestDir + "/block_chiral.asset");
            var right = MirrorPartnerCreator.Create(left);
            Assert.AreEqual("block_chiral_mirrored", right.IdString);
            Assert.AreEqual(TestDir + "/block_chiral_mirrored.asset", AssetDatabase.GetAssetPath(right));
            Assert.AreSame(right, left.MirrorPartner); Assert.AreSame(left, right.MirrorPartner);
            Assert.AreEqual(BlockFaceMask.NegX | BlockFaceMask.PosY, right.AttachFaces);
            foreach (var d in new[] { left, right })
            {
                var r = Validate(d);
                Assert.IsFalse(r.Entries.Any(e => e.Code == "DATA-E042" || e.Code == "DATA-E043" || e.Code == "DATA-E044"), $"{d.IdString}: {r.ToText()}");
            }
        }
    }
}
