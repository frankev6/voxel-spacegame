namespace Game.Data.Definitions
{
    /// <summary>Base of every content definition. Implements SPEC-04 §4.3. Data only; never written at runtime.</summary>
    public abstract class ContentDefinition : UnityEngine.ScriptableObject
    {
        [UnityEngine.SerializeField, UnityEngine.Delayed] private string id;          // e.g. "block_hull"
        [UnityEngine.SerializeField] private string[] formerIds = System.Array.Empty<string>(); // aliases for renamed released content

        [System.NonSerialized] private Game.Core.ContentId cachedId;                  // derived cache only (not data)
        [System.NonSerialized] private bool cacheValid;

        /// <summary>new ContentId(id), cached; returns default if id is null/empty.</summary>
        public Game.Core.ContentId Id
        {
            get
            {
                if (!cacheValid) { cachedId = new Game.Core.ContentId(id); cacheValid = true; }
                return cachedId;
            }
        }
        public string IdString => id;
        public System.Collections.Generic.IReadOnlyList<string> FormerIds => formerIds;

        /// <summary>Per-definition field checks. Base does nothing; override and call base.</summary>
        public virtual void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context) { }

        protected virtual void OnValidate() { cacheValid = false; }                  // editor only; resets the ID cache
    }
}
