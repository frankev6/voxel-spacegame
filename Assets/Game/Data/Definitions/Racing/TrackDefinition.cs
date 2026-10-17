using System;
using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>A race track (SPEC-92 §4.1): gates and their graph, pieces (wrecks, walls, obstacles), zones (boost strips, field walls),
    /// mine fields and the start grid, in metres on the flight plane relative to the track origin. TrackBuilder writes Track 01 from
    /// ArtSource/Racing/tracks/track_01.json; TrackSpawnService spawns it.</summary>
    [ContentIdPrefix("track_")]
    [CreateAssetMenu(menuName = "Game/Content/Racing/Track", fileName = "track_new")]
    public sealed class TrackDefinition : ContentDefinition
    {
        public string DisplayName;
        public int DefaultLaps = 3;
        public TrackGate[] Gates = Array.Empty<TrackGate>();          // index = gate id; gate 0 is start/finish
        public TrackPiece[] Pieces = Array.Empty<TrackPiece>();       // voxel walls, pylons, barricades, obstacles, wrecks
        public TrackZone[] Zones = Array.Empty<TrackZone>();          // boost strips, slow fields, field walls (SPEC-91 ZoneDefinition)
        public TrackMines[] MineFields = Array.Empty<TrackMines>();   // SPEC-91 MineFieldDefinition
        public Vector2[] Bounds = Array.Empty<Vector2>();             // closed polyline; becomes a Bounds zone (empty = none)
        public TrackSlot[] StartGrid = Array.Empty<TrackSlot>();      // ≥ 4
        public ZoneDefinition GateZone;                               // the gate zone every gate spawns (its effect Id is set per gate)
        public ZoneDefinition BoundsZone;
        public Texture2D LayoutMap;                                   // the approved top-down map (doc and loading screen)
        // RACE-016: what the track looks like. None of it is simulated; TrackDressing builds it on every machine.
        public TrackPath[] Paths = Array.Empty<TrackPath>();          // route centrelines: the lane surface and the corner boards
        public TrackLight[] Lights = Array.Empty<TrackLight>();       // wreck running lights, beacons and fires (track space, y up)
        public TrackLook Look;

        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            foreach (var error in TrackValidator.Check(this)) scope.Error("RACE-E001", nameof(Gates), error);
        }
    }

    [Serializable] public struct TrackGate
    {
        public Vector2 Position; public float Yaw; public float Width;   // gate line is Width across, perpendicular to Yaw (forward = (sin, cos))
        public bool Required;                                            // on every valid lap
        public int[] Next;                                               // ids reachable next (branches); validator: graph from 0 back to 0
        public Vector2 RespawnPosition; public float RespawnYaw;
        public bool Interior;                                            // inside a wreck: its hull is the barrier, no pylons (RACE-016)
    }
    [Serializable] public struct TrackPiece { public ShipBlueprintDefinition Blueprint; public Vector2 Position; public float Yaw; public TrackPieceKind Kind; }
    public enum TrackPieceKind : byte { Wall = 0, Pylon = 1, Barricade = 2, Obstacle = 3, Decor = 4 }   // Obstacle = dynamic (pushable)
    [Serializable] public struct TrackZone
    {
        public ZoneDefinition Zone; public Vector2[] Path; public Vector2 Position; public float Yaw;
        public Vector2 HalfExtents;   // box or circle size for this placement; zero = the definition's
        public float PathRadius;      // path capsule radius for this placement; zero = the definition's
        // RACE-016: a field-wall span held up by two voxel emitter posts (track space). While either post's emitter head is shot
        // away the span is down (WallEmitterSystem). No emitters = always up.
        public bool Emitters; public Vector2 EmitterA, EmitterB;
    }
    [Serializable] public struct TrackMines { public MineFieldDefinition Field; public Vector2 Position; public float Yaw; public uint Seed; }
    [Serializable] public struct TrackSlot { public Vector2 Position; public float Yaw; }
    [Serializable] public struct TrackPath { public string Name; public Vector2[] Points; public float Width; public bool Closed; public bool Interior; }
    public enum TrackLightKind : byte { Steady = 0, Blink = 1, Fire = 2 }
    [Serializable] public struct TrackLight { public Vector3 Position; public Color Color; public float Size; public TrackLightKind Kind; }
    /// <summary>The materials of the dressing: one Game/Race/Holo material per mode, and a lit metal for pylons and posts.</summary>
    [Serializable] public struct TrackLook
    {
        public Material Curtain, Fence, Pad, Halo, Glow, Ribbon, Solid, Surface, Sign, Dust;
        public Material Metal;
    }

    /// <summary>SPEC-92 §7: the gate graph must lead from gate 0 back to gate 0, and every required gate must lie on every lap.</summary>
    public static class TrackValidator
    {
        public static System.Collections.Generic.List<string> Check(TrackDefinition track)
        {
            var errors = new System.Collections.Generic.List<string>();
            var gates = track.Gates ?? Array.Empty<TrackGate>();
            if (gates.Length == 0) { errors.Add("A track needs gates."); return errors; }
            if (!gates[0].Required) errors.Add("Gate 0 (start and finish) must be required.");
            for (int i = 0; i < gates.Length; i++)
            {
                if (gates[i].Next == null || gates[i].Next.Length == 0) { errors.Add($"Gate {i} leads nowhere."); continue; }
                foreach (int n in gates[i].Next) if (n < 0 || n >= gates.Length) errors.Add($"Gate {i} leads to missing gate {n}.");
                if (!(gates[i].Width > 0)) errors.Add($"Gate {i} has no width.");
            }
            if (errors.Count > 0) return errors;
            // every gate reachable from 0, and 0 reachable from every gate
            var reach = Reach(gates, 0);
            for (int i = 0; i < gates.Length; i++)
            {
                if (!reach.Contains(i)) errors.Add($"Gate {i} cannot be reached from gate 0.");
                else if (!Reach(gates, i).Contains(0)) errors.Add($"Gate {i} has no path back to gate 0.");
            }
            // a required gate must be on every lap: removing it must disconnect 0 from itself
            for (int r = 1; r < gates.Length; r++)
            {
                if (!gates[r].Required) continue;
                if (Reach(gates, 0, r).Contains(0)) errors.Add($"Required gate {r} can be skipped.");
            }
            if (track.StartGrid == null || track.StartGrid.Length < 4) errors.Add("The start grid needs at least four slots.");
            return errors;
        }
        static System.Collections.Generic.HashSet<int> Reach(TrackGate[] gates, int from, int without = -1)
        {
            var seen = new System.Collections.Generic.HashSet<int>(); var stack = new System.Collections.Generic.Stack<int>();
            foreach (int n in gates[from].Next) if (n != without) stack.Push(n);
            while (stack.Count > 0)
            {
                int g = stack.Pop();
                if (!seen.Add(g) || g == 0) continue;
                foreach (int n in gates[g].Next) if (n != without) stack.Push(n);
            }
            return seen;
        }
    }
}
