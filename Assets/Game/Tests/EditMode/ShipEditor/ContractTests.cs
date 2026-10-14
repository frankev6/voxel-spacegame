using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Game.ShipEditor.Data;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.ShipEditor
{
    /// <summary>The four required EDT-001 contract checks: unmanaged layouts, issue range and authored config defaults.</summary>
    [Category("ShipEditor")]
    public sealed class ContractTests : GameTestBase
    {
        [Test]
        public void BlockRecord_Is20Bytes()
        {
            Assert.AreEqual(20, UnsafeUtility.SizeOf<BlockRecord>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<BlockRecord>());
            Assert.AreEqual(0, Marshal.OffsetOf<BlockRecord>(nameof(BlockRecord.Origin)).ToInt32());
            Assert.AreEqual(12, Marshal.OffsetOf<BlockRecord>(nameof(BlockRecord.Block)).ToInt32());
            Assert.AreEqual(14, Marshal.OffsetOf<BlockRecord>(nameof(BlockRecord.Orient)).ToInt32());
            Assert.AreEqual(15, Marshal.OffsetOf<BlockRecord>(nameof(BlockRecord.Color)).ToInt32());
            Assert.AreEqual(16, Marshal.OffsetOf<BlockRecord>(nameof(BlockRecord.DamageOffset)).ToInt32());
            var pristine = new BlockRecord { Origin = new Unity.Mathematics.int3(-3, 4, 5), Block = 7, Orient = 12, Color = 2, DamageOffset = -1 };
            var damaged = pristine; damaged.DamageOffset = 8;
            Assert.IsTrue(pristine.Equals(damaged), "Placed-block identity excludes damage.");
            Assert.IsTrue(pristine.Equals((object)damaged));
            Assert.AreEqual(pristine.GetHashCode(), damaged.GetHashCode());
        }

        [Test]
        public void DiffOp_Is24Bytes()
        {
            Assert.AreEqual(24, UnsafeUtility.SizeOf<DiffOp>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<DiffOp>());
            Assert.AreEqual(0, Marshal.OffsetOf<DiffOp>(nameof(DiffOp.Record)).ToInt32());
            Assert.AreEqual(20, Marshal.OffsetOf<DiffOp>(nameof(DiffOp.Kind)).ToInt32());
            Assert.AreEqual(21, Marshal.OffsetOf<DiffOp>(nameof(DiffOp.Pad0)).ToInt32());
            Assert.AreEqual(22, Marshal.OffsetOf<DiffOp>(nameof(DiffOp.Pad1)).ToInt32());
        }

        [Test]
        public void EditIssueKind_WarningsAt100Plus()
        {
            Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(EditIssueKind)));
            var errors = new[] { EditIssueKind.NoCore, EditIssueKind.MultipleCores, EditIssueKind.Disconnected,
                EditIssueKind.OutOfBounds, EditIssueKind.OverCellBudget, EditIssueKind.DamageInvariant, EditIssueKind.UnbuildableBlock };
            var warnings = new[] { EditIssueKind.PowerDeficit, EditIssueKind.NoThrust, EditIssueKind.NoWeapons };
            Assert.AreEqual(errors.Length + warnings.Length, Enum.GetValues(typeof(EditIssueKind)).Length);
            for (int i = 0; i < errors.Length; i++)
            {
                Assert.AreEqual(i, (byte)errors[i]);
                Assert.IsTrue(new EditIssue { Kind = errors[i] }.IsError);
            }
            for (int i = 0; i < warnings.Length; i++)
            {
                Assert.AreEqual(100 + i, (byte)warnings[i]);
                Assert.IsFalse(new EditIssue { Kind = warnings[i] }.IsError);
            }
        }

        [Test]
        public void Config_Defaults_MatchSpec()
        {
            var defaults = ScriptableObject.CreateInstance<ShipEditorConfig>();
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<ShipEditorConfig>("Assets/Game/ShipEditor/Data/ShipEditorConfig.asset");
                Assert.IsNotNull(asset, "EDT-001 supplies the canonical config asset.");
                (string Field, object Value)[] expected =
                {
                    (nameof(ShipEditorConfig.MaxCells), 1_000_000),
                    (nameof(ShipEditorConfig.MaxExtentCells), new Vector3Int(256, 128, 256)),
                    (nameof(ShipEditorConfig.BuildLimitMin), new Vector3Int(-512, -256, -512)),
                    (nameof(ShipEditorConfig.BuildLimitMax), new Vector3Int(511, 255, 511)),
                    (nameof(ShipEditorConfig.HistoryBudgetBytes), 64L * 1024 * 1024),
                    (nameof(ShipEditorConfig.HistoryMaxEntries), 4096),
                    (nameof(ShipEditorConfig.SplitCheckBudgetCells), 8192),
                    (nameof(ShipEditorConfig.MaxBatchCells), 262_144),
                    (nameof(ShipEditorConfig.FillMaxCells), 65_536),
                    (nameof(ShipEditorConfig.LineMaxCells), 256),
                    (nameof(ShipEditorConfig.SyncRemeshBricksPerFrame), 8),
                    (nameof(ShipEditorConfig.AsyncRemeshBricksPerFrame), 64),
                    (nameof(ShipEditorConfig.OverlayMaxInstances), 65_536),
                    (nameof(ShipEditorConfig.PreviewOutlineThreshold), 4096),
                    (nameof(ShipEditorConfig.PresenterRefreshSeconds), 0.1f),
                    (nameof(ShipEditorConfig.MaxPickSteps), 4096),
                };
                foreach (var row in expected)
                {
                    var field = typeof(ShipEditorConfig).GetField(row.Field, BindingFlags.Public | BindingFlags.Instance);
                    Assert.IsNotNull(field, row.Field);
                    Assert.AreEqual(row.Value, field.GetValue(defaults), "constructor: " + row.Field);
                    Assert.AreEqual(row.Value, field.GetValue(asset), "asset: " + row.Field);
                }
                Assert.IsNull(defaults.BrickMaterial); Assert.IsNull(defaults.BrickXRayMaterial); Assert.IsNull(defaults.OverlayMaterial);
                // EDT-018/019 supply the real draft and overlay materials.
                Assert.IsNotNull(asset.BrickMaterial); Assert.IsNotNull(asset.BrickXRayMaterial); Assert.IsNotNull(asset.OverlayMaterial);
                // Provisional asset colours come from the approved theme; no new art or C# style constants (Q25).
                string tokens = File.ReadAllText(Path.Combine(Application.dataPath, "Game/UI/Theme/tokens.uss"));
                (string Field, string Token)[] colours =
                {
                    (nameof(ShipEditorConfig.ValidColor), "--color-positive"),
                    (nameof(ShipEditorConfig.InvalidColor), "--color-danger"),
                    (nameof(ShipEditorConfig.ClearanceColor), "--color-warning"),
                    (nameof(ShipEditorConfig.MirrorGhostColor), "--color-info"),
                    (nameof(ShipEditorConfig.SelectionColor), "--color-accent"),
                    (nameof(ShipEditorConfig.DisconnectedColor), "--color-warning"),
                    (nameof(ShipEditorConfig.DamagedColor), "--color-danger"),
                    (nameof(ShipEditorConfig.MirrorPlaneColor), "--color-accent-strong"),
                    (nameof(ShipEditorConfig.GridColor), "--color-border"),
                };
                foreach (var row in colours)
                {
                    var match = Regex.Match(tokens, Regex.Escape(row.Token) + @"\s*:\s*(#[0-9A-Fa-f]{6})\s*;");
                    Assert.IsTrue(match.Success, row.Token);
                    Assert.IsTrue(ColorUtility.TryParseHtmlString(match.Groups[1].Value, out var expectedColour));
                    var actual = (Color)typeof(ShipEditorConfig).GetField(row.Field).GetValue(asset);
                    Assert.AreEqual(expectedColour.r, actual.r, 1e-6, row.Field);
                    Assert.AreEqual(expectedColour.g, actual.g, 1e-6, row.Field);
                    Assert.AreEqual(expectedColour.b, actual.b, 1e-6, row.Field);
                    Assert.AreEqual(expectedColour.a, actual.a, 1e-6, row.Field);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(defaults); }
        }
    }
}
