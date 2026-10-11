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
        public override void Validate(ValidationScope scope, ContentValidationContext context) { throw new System.NotImplementedException("VOX-010"); }  // VOX-E001..E003 (§7)
#if UNITY_EDITOR
        public void EditorSetPayload(byte[] bytes, int cells, Vector3Int min, Vector3Int max) { throw new System.NotImplementedException("VOX-010"); }
#endif
    }
}
