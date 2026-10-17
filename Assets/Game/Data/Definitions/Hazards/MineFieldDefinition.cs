using Game.Data.Hazards;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>A scatter of one mine type in an area; HazardSpawn.MineField places it from a pose and a seed (SPEC-91 §4.4).</summary>
    [ContentIdPrefix("minefield_")]
    [CreateAssetMenu(menuName = "Game/Content/Hazards/Mine Field", fileName = "minefield_new")]
    public sealed class MineFieldDefinition : ContentDefinition
    {
        [SerializeField] private MineDefinition mine;
        [SerializeField, Min(1)] private int count = 8;
        [SerializeField] private ZoneShape area = ZoneShape.Circle;      // Box, Circle or Path band
        [SerializeField] private Vector2 halfExtents = new Vector2(120, 120);
        [SerializeField, Min(0)] private float minSpacing = 30f;

        public MineDefinition Mine => mine;
        public int Count => count;
        public ZoneShape Area => area;
        public Vector2 HalfExtents => halfExtents;
        public float MinSpacing => minSpacing;

        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (mine == null) scope.Error("HAZ-E003", nameof(mine), "A mine field needs a mine.");
            if (!(halfExtents.x > 0)) scope.Error("HAZ-E003", nameof(halfExtents), "Mine field area must be positive.");
        }
    }
}
