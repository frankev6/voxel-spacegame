using Game.Core;
using Game.Data;
using Game.Data.Services;
using Game.Editor.Content;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Editor.Content
{
    /// <summary>DATA-009 gate: the real content database has zero validation errors and boots ContentService.</summary>
    public sealed class ProjectContentTests
    {
        [Test]
        public void ProjectContent_HasZeroValidationErrors()
        {
            var report = ContentValidatorRunner.Validate(ContentDatabaseBuilder.Rebuild());
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
        }

        [Test]
        public void RealDatabase_BootsContentService()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(ContentDatabaseBuilder.DatabasePath);
            Assert.IsNotNull(db, "Assets/Game/Content/ContentDatabase.asset exists");
            using var service = new ContentService(db);
            Assert.AreEqual("block_missing", service.Catalog.MissingBlock.IdString);
            Assert.AreEqual("item_missing", service.Catalog.MissingItem.IdString);
            Assert.AreEqual(63, service.Catalog.MissingBlock.MaterialLayer);
            Assert.AreEqual(service.Catalog.GetBlockIndex(service.Catalog.MissingBlock), service.BlockDefs.Value.MissingBlockIndex);
            Assert.IsTrue(service.Catalog.TryGet(new ContentId("item_missing"), out _));
        }
    }
}
