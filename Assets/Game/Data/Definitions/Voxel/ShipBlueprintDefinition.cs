using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Definitions
{

    /// <summary>Implements SPEC-10 §4.7. A ship blueprint: VOXS payload plus cached summary.</summary>
    [ContentIdPrefix("ship_")]
    [CreateAssetMenu(menuName = "Game/Content/Ship Blueprint", fileName = "ship_new")]
    public sealed class ShipBlueprintDefinition : ContentDefinition
    {
        [SerializeField] private string displayName;
        [SerializeField] private float cellSize = 0.5f;          // > 0
        [SerializeField] private int payloadVersion = 1;         // VOXS payload layout version
        [SerializeField, HideInInspector] private byte[] payload; // VOXS bytes (SPEC-05 VoxsCodec, VoxsEncodeOptions.ForAsset); written by SPEC-72 and SPEC-51 export
        [SerializeField] private int cellCount;                  // cached summary for UI and validation
        [SerializeField] private Vector3Int boundsMin, boundsMax;
        public string DisplayName => displayName; public float CellSize => cellSize; public int PayloadVersion => payloadVersion;
        public ReadOnlySpan<byte> Payload => payload; public int CellCount => cellCount;
        public Vector3Int BoundsMin => boundsMin; public Vector3Int BoundsMax => boundsMax;
        // SPEC-10 §7; brought forward for the M0 migration gate (SPEC-72 Q16).
        // Data cannot reference the Voxel codec assembly. This is its supported v1 asset contract.
        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            if (payload == null || payload.Length == 0) scope.Error("VOX-E001", nameof(payload), "Blueprint payload is missing or empty.");
            if (!(cellSize > 0) || float.IsInfinity(cellSize)) scope.Error("VOX-E002", nameof(cellSize), "Cell size must be finite and positive.");
            if (payloadVersion > 1) scope.Error("VOX-E003", nameof(payloadVersion), "Blueprint payload version is newer than supported v1.");
        }
#if UNITY_EDITOR
        public void EditorSetPayload(byte[] bytes, int cells, Vector3Int min, Vector3Int max)
        {
            payload = bytes == null ? null : (byte[])bytes.Clone();
            cellCount = cells; boundsMin = min; boundsMax = max;
        }
#endif
    }
}
