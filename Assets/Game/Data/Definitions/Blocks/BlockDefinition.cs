namespace Game.Data.Definitions
{
    /// <summary>A placeable block. Implements SPEC-04 §4.5. Category is always ItemCategory.Block.</summary>
    [ContentIdPrefix("block_", "weapon_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/Block", fileName = "block_new")]
    public sealed class BlockDefinition : ItemDefinition
    {
        [UnityEngine.Header("Shape")]
        [UnityEngine.SerializeField] private Game.Data.Blocks.BlockKind kind;
        [UnityEngine.SerializeField] private Game.Data.Blocks.BlockShape shape;                  // Structural only; Cube otherwise
        [UnityEngine.SerializeField] private UnityEngine.Vector3Int[] cells = { UnityEngine.Vector3Int.zero }; // unrotated footprint, must contain (0,0,0)
        [UnityEngine.SerializeField] private UnityEngine.Vector3Int[] clearance = System.Array.Empty<UnityEngine.Vector3Int>(); // must-stay-empty cells, block-local
        [UnityEngine.SerializeField] private Game.Data.Blocks.OrientationMask allowedOrientations = Game.Data.Blocks.OrientationMask.All;
        [UnityEngine.SerializeField] private Game.Data.Blocks.BlockFaceMask attachFaces = Game.Data.Blocks.BlockFaceMask.All;
        [UnityEngine.SerializeField] private BlockDefinition mirrorPartner;                       // null = self (achiral)

        [UnityEngine.Header("Durability and mass")]
        [UnityEngine.SerializeField, UnityEngine.Min(1f)] private float cellHp = 100f;           // HP of ONE cell; Cell.Hp 255 == cellHp
        [UnityEngine.SerializeField, UnityEngine.Min(0f)] private float functionHp;              // whole-block function HP (Functional only)
        [UnityEngine.SerializeField, UnityEngine.Min(0.01f)] private float cellMass = 25f;       // kg per cell
        [UnityEngine.SerializeField] private byte armorClass;                                     // 0 = standard; meaning defined by SPEC-13

        [UnityEngine.Header("Presentation")]
        [UnityEngine.SerializeField] private byte materialLayer;                                   // 0..63, structural texture-array layer (SPEC-11)
        [UnityEngine.SerializeField] private UnityEngine.Mesh propMesh;                           // Prop/Functional: pivot at root cell centre

        [UnityEngine.Header("Function")]
        [UnityEngine.SerializeField] private Game.Data.Blocks.BlockCategory blockCategory;    // display only (§4.6); rules in §6.4
        [UnityEngine.SerializeReference] private Game.Data.Blocks.BlockModule[] modules = System.Array.Empty<Game.Data.Blocks.BlockModule>(); // §4.6; empty unless Functional

        // DATA-010 (M2) adds: [SerializeField] AbilityDefinition[] grantedAbilities; (the weapon link lives in WeaponMountModule)

        public Game.Data.Blocks.BlockKind Kind => kind;
        public Game.Data.Blocks.BlockShape Shape => shape;
        public System.Collections.Generic.IReadOnlyList<UnityEngine.Vector3Int> Cells => cells;
        public System.Collections.Generic.IReadOnlyList<UnityEngine.Vector3Int> Clearance => clearance;
        public Game.Data.Blocks.OrientationMask AllowedOrientations => allowedOrientations;
        public Game.Data.Blocks.BlockFaceMask AttachFaces => attachFaces;
        public BlockDefinition MirrorPartner => mirrorPartner != null ? mirrorPartner : this;
        public float CellHp => cellHp; public float FunctionHp => functionHp; public float CellMass => cellMass;
        public byte ArmorClass => armorClass; public byte MaterialLayer => materialLayer;
        public UnityEngine.Mesh PropMesh => propMesh;
        public Game.Data.Blocks.BlockCategory BlockCategory => blockCategory;
        public System.Collections.Generic.IReadOnlyList<Game.Data.Blocks.BlockModule> Modules => modules;
        /// <summary>First module of type T, else false.</summary>
        public bool TryGetModule<T>(out T module) where T : Game.Data.Blocks.BlockModule
        {
            foreach (var m in modules)
                if (m is T typed) { module = typed; return true; }
            module = null;
            return false;
        }
        public bool RequiresInstance => kind == Game.Data.Blocks.BlockKind.Functional || cells.Length > 1;

        /// <summary>DATA-E020…E024 (item rules), then DATA-E030…E045, E048 and E049 (SPEC-04 §5.8). Module params report DATA-E041 through
        /// BlockModule.Validate; their fields are prefixed with "modules[i].".</summary>
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            bool footprintOk = ValidateFootprint(scope);
            if ((allowedOrientations.Bits & Game.Data.Blocks.OrientationMask.AllBits) == 0 || (allowedOrientations.Bits & ~Game.Data.Blocks.OrientationMask.AllBits) != 0)
                scope.Error("DATA-E034", nameof(allowedOrientations), $"Allowed orientations 0x{allowedOrientations.Bits:X} must be non-empty and use only bits 0..23.");
            bool modulesOk = ValidateModules(scope);
            ValidateKind(scope, modulesOk);
            if (!(cellHp > 0f) || !(cellMass > 0f))
                scope.Error("DATA-E039", !(cellHp > 0f) ? nameof(cellHp) : nameof(cellMass), $"Cell HP ({cellHp}) and cell mass ({cellMass}) must be greater than 0.");
            if (materialLayer >= 64) scope.Error("DATA-E045", nameof(materialLayer), $"Material layer {materialLayer} is outside 0..63.");
            if (footprintOk) ValidateMirror(scope);
        }

        bool ValidateFootprint(Game.Data.Validation.ValidationScope scope)
        {
            bool ok = true;
            var seen = new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>();
            if (cells == null || cells.Length == 0 || System.Array.IndexOf(cells, UnityEngine.Vector3Int.zero) < 0)
            { scope.Error("DATA-E030", nameof(cells), "The footprint must contain the root cell (0,0,0)."); ok = false; }
            if (cells != null)
                for (int i = 0; i < cells.Length; i++)
                {
                    if (!seen.Add(cells[i])) { scope.Error("DATA-E031", $"{nameof(cells)}[{i}]", $"Footprint cell {cells[i]} is listed twice."); ok = false; }
                    if (!InRange(cells[i])) { scope.Error("DATA-E032", $"{nameof(cells)}[{i}]", $"Footprint cell {cells[i]} is outside [-32, 31]."); ok = false; }
                }
            var clear = new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>();
            if (clearance != null)
                for (int i = 0; i < clearance.Length; i++)
                {
                    if (!InRange(clearance[i])) { scope.Error("DATA-E032", $"{nameof(clearance)}[{i}]", $"Clearance cell {clearance[i]} is outside [-32, 31]."); ok = false; }
                    if (seen.Contains(clearance[i])) { scope.Error("DATA-E033", $"{nameof(clearance)}[{i}]", $"Clearance cell {clearance[i]} is inside the footprint."); ok = false; }
                    else if (!clear.Add(clearance[i])) { scope.Error("DATA-E033", $"{nameof(clearance)}[{i}]", $"Clearance cell {clearance[i]} is listed twice."); ok = false; }
                }
            return ok;
        }

        static bool InRange(UnityEngine.Vector3Int c)
            => c.x >= -32 && c.x <= 31 && c.y >= -32 && c.y <= 31 && c.z >= -32 && c.z <= 31;

        bool ValidateModules(Game.Data.Validation.ValidationScope scope)
        {
            bool ok = true;
            var ids = new System.Collections.Generic.HashSet<Game.Data.Blocks.BlockModuleId>();
            for (int i = 0; i < (modules?.Length ?? 0); i++)
            {
                var m = modules[i];
                if (m == null) { scope.Error("DATA-E048", $"{nameof(modules)}[{i}]", "Module entry is empty."); ok = false; continue; }
                if (!ids.Add(m.Id)) { scope.Error("DATA-E048", $"{nameof(modules)}[{i}]", $"Module {m.Id} appears more than once."); ok = false; }
                var moduleReport = new Game.Data.Validation.ValidationReport(scope.AssetPath);
                m.Validate(this, moduleReport);
                foreach (var e in moduleReport.Entries)
                    scope.Error(e.Code, $"{nameof(modules)}[{i}].{e.Field}", e.Message);
            }
            return ok;
        }

        void ValidateKind(Game.Data.Validation.ValidationScope scope, bool modulesOk)
        {
            int moduleCount = modules?.Length ?? 0;
            bool functional = kind == Game.Data.Blocks.BlockKind.Functional;
            if (functional ? moduleCount == 0 : moduleCount > 0)
                scope.Error("DATA-E035", nameof(modules), functional ? "A functional block needs at least one module." : $"A {kind} block must not have modules.");
            if (kind == Game.Data.Blocks.BlockKind.Structural && cells != null && cells.Length > 1)
                scope.Error("DATA-E036", nameof(cells), $"A structural block has exactly one cell, not {cells.Length}.");
            if (kind != Game.Data.Blocks.BlockKind.Structural && shape != Game.Data.Blocks.BlockShape.Cube)
                scope.Error("DATA-E037", nameof(shape), $"A {kind} block must use shape Cube, not {shape}.");
            if (kind != Game.Data.Blocks.BlockKind.Structural && propMesh == null)
                scope.Error("DATA-E038", nameof(propMesh), $"A {kind} block needs a prop mesh.");
            if (functional ? !(functionHp > 0f) : functionHp != 0f)
                scope.Error("DATA-E040", nameof(functionHp), functional ? $"Function HP is {functionHp}; a functional block needs more than 0." : $"Function HP must be 0 for a {kind} block.");
            bool categoryOk = kind switch
            {
                Game.Data.Blocks.BlockKind.Structural => blockCategory == Game.Data.Blocks.BlockCategory.Structure,
                Game.Data.Blocks.BlockKind.Prop => blockCategory == Game.Data.Blocks.BlockCategory.Detail,
                _ => blockCategory != Game.Data.Blocks.BlockCategory.Structure && blockCategory != Game.Data.Blocks.BlockCategory.Detail,
            };
            if (!categoryOk) scope.Error("DATA-E049", nameof(blockCategory), $"Category {blockCategory} does not fit a {kind} block.");
        }

        void ValidateMirror(Game.Data.Validation.ValidationScope scope)
        {
            bool hasPartner = mirrorPartner != null && mirrorPartner != this;
            var partner = hasPartner ? mirrorPartner : this;
            if (hasPartner)
            {
                bool sameModules = SameModuleIds(this, partner);
                if (partner.MirrorPartner != this || partner.kind != kind || !sameModules)
                {
                    scope.Error("DATA-E042", nameof(mirrorPartner), $"Mirror partner {partner.IdString} must name this block back and have the same kind and module ids.");
                    return;
                }
                if (partner.cells == null || partner.cells.Length == 0) return;   // the partner reports its own footprint errors
            }
            string code = hasPartner ? "DATA-E043" : "DATA-E044";
            var d = Game.Data.Blocks.BlockPlacementMath.ComputeMirrorOffset(ToInt3(cells), ToInt3(partner.cells), out bool footprintMirrors);
            bool clearanceMirrors = SetEquals(MirrorX(clearance, d), partner.clearance ?? System.Array.Empty<UnityEngine.Vector3Int>());
            bool facesMirror = SwapX(attachFaces) == partner.attachFaces;
            if (!footprintMirrors || !clearanceMirrors || !facesMirror)
            {
                string what = !footprintMirrors ? "footprint" : !clearanceMirrors ? "clearance" : "attach faces";
                scope.Error(code, hasPartner ? nameof(mirrorPartner) : nameof(cells),
                    hasPartner ? $"The partner's {what} is not the X mirror of this block's." : $"This block has no mirror partner, but its {what} is not X-symmetric.");
            }
        }

        static bool SameModuleIds(BlockDefinition a, BlockDefinition b)
        {
            var x = new System.Collections.Generic.HashSet<Game.Data.Blocks.BlockModuleId>();
            var y = new System.Collections.Generic.HashSet<Game.Data.Blocks.BlockModuleId>();
            foreach (var m in a.modules ?? System.Array.Empty<Game.Data.Blocks.BlockModule>()) if (m != null) x.Add(m.Id);
            foreach (var m in b.modules ?? System.Array.Empty<Game.Data.Blocks.BlockModule>()) if (m != null) y.Add(m.Id);
            return x.SetEquals(y);
        }

        static Unity.Mathematics.int3[] ToInt3(UnityEngine.Vector3Int[] v)
        {
            var r = new Unity.Mathematics.int3[v?.Length ?? 0];
            for (int i = 0; i < r.Length; i++) r[i] = new Unity.Mathematics.int3(v[i].x, v[i].y, v[i].z);
            return r;
        }

        static UnityEngine.Vector3Int[] MirrorX(UnityEngine.Vector3Int[] v, Unity.Mathematics.int3 d)
        {
            var r = new UnityEngine.Vector3Int[v?.Length ?? 0];
            for (int i = 0; i < r.Length; i++) r[i] = new UnityEngine.Vector3Int(-v[i].x + d.x, v[i].y + d.y, v[i].z + d.z);
            return r;
        }

        static bool SetEquals(UnityEngine.Vector3Int[] a, UnityEngine.Vector3Int[] b)
            => new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>(a).SetEquals(b);

        static Game.Data.Blocks.BlockFaceMask SwapX(Game.Data.Blocks.BlockFaceMask m)
        {
            const Game.Data.Blocks.BlockFaceMask X = Game.Data.Blocks.BlockFaceMask.PosX | Game.Data.Blocks.BlockFaceMask.NegX;
            var swapped = m & ~X;
            if ((m & Game.Data.Blocks.BlockFaceMask.PosX) != 0) swapped |= Game.Data.Blocks.BlockFaceMask.NegX;
            if ((m & Game.Data.Blocks.BlockFaceMask.NegX) != 0) swapped |= Game.Data.Blocks.BlockFaceMask.PosX;
            return swapped;
        }
    }
}
