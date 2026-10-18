#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Mathematics;
using UnityEngine;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>DEV-023 option E, the Hollow: a pirate haven built into an asteroid, the Khuchad Clan's. Its data is generated
    /// (HollowLayout.g.cs, from the hub design by ArtSource/Hub/hollow_codegen.py); these are its types. Positions are metres
    /// relative to the Hollow's own origin (x east, z north, the flight plane at y = 0), which sits at <see cref="Offset"/> from the
    /// playground origin, clear of the ARK Proving Grounds.</summary>
    internal static partial class HollowLayout
    {
        internal static readonly float2 Offset = new float2(9000, 0);
        internal const string Faction = "faction_khuchad_clan";
        internal const float StationRadius = 18;
        /// <summary>Where the playground puts a pilot (owner: "load into the entrance of the asteroid"): outside the Maw, off the
        /// traffic lane through it, facing in.</summary>
        internal static readonly float2 Entrance = new float2(-90, -472);
        internal const float EntranceYaw = .55f;
        /// <summary>The race portal: in an alcove off the Ring south-east of the Maw junction (about 58 m wide, open to the south), 61 m
        /// from every traffic route (a portal takes any ship). A ship crosses it heading `RacePortalYaw`, into the alcove.</summary>
        internal static readonly float2 RacePortal = new float2(80, -150);
        internal const float RacePortalYaw = .21f, RacePortalWidth = 40;

        internal enum Layer : byte { Low, High, Struct, Fort }
        internal enum StationKind : byte { Merchant, Rearm, Reset }

        /// <summary>A blueprint whose grid origin (cell (0,0,0)'s corner) spawns at `Origin`, unrotated. High pieces are the rock above
        /// +24 m, hidden while the player is inside; a Fort piece spins about its origin.</summary>
        internal readonly struct Piece
        {
            internal readonly string Id; internal readonly float2 Origin; internal readonly Layer Layer; internal readonly int Fort;
            internal Piece(string id, float2 origin, Layer layer, int fort) { Id = id; Origin = origin; Layer = layer; Fort = fort; }
        }
        /// <summary>An outlying rock with its fort, turning steadily at `Rate` rad/s (owner: "the smaller asteroids arounds should
        /// rotate constantly so that the guns attached can't always face the necessary angle").</summary>
        internal readonly struct Fort
        {
            internal readonly float2 Centre; internal readonly float Rate;
            internal Fort(float2 centre, float rate) { Centre = centre; Rate = rate; }
        }
        internal readonly struct Gun
        {
            internal readonly float2 Position; internal readonly float Yaw; internal readonly bool Big; internal readonly string Group;
            internal Gun(float2 position, float yaw, bool big, string group) { Position = position; Yaw = yaw; Big = big; Group = group; }
        }
        internal readonly struct Light
        {
            internal readonly float3 Position; internal readonly Color Colour; internal readonly float Radius; internal readonly string Kind;
            internal Light(float3 position, Color colour, float radius, string kind) { Position = position; Colour = colour; Radius = radius; Kind = kind; }
        }
        internal readonly struct Neon
        {
            internal readonly float2 A, B; internal readonly float Y; internal readonly Color Colour;
            internal Neon(float2 a, float2 b, float y, Color colour) { A = a; B = b; Y = y; Colour = colour; }
        }
        internal readonly struct Sign
        {
            internal readonly string Text; internal readonly float3 Position; internal readonly float Yaw, Size; internal readonly Color Colour;
            internal Sign(string text, float3 position, float yaw, Color colour, float size) { Text = text; Position = position; Yaw = yaw; Colour = colour; Size = size; }
        }
        internal readonly struct Station
        {
            internal readonly string Id, Title, Merchant; internal readonly StationKind Kind; internal readonly float2 Position; internal readonly float Yaw;
            internal Station(string id, StationKind kind, float2 position, float yaw, string title, string merchant)
            { Id = id; Kind = kind; Position = position; Yaw = yaw; Title = title; Merchant = merchant; }
        }
        internal readonly struct Moored
        {
            internal readonly string Blueprint; internal readonly float2 Position; internal readonly float Yaw;
            internal Moored(string blueprint, float2 position, float yaw) { Blueprint = blueprint; Position = position; Yaw = yaw; }
        }
        internal readonly struct Route
        {
            internal readonly string Blueprint; internal readonly float2[] Points; internal readonly bool Loop;
            internal Route(string blueprint, float2[] points, bool loop) { Blueprint = blueprint; Points = points; Loop = loop; }
        }
        internal readonly struct Guard
        {
            internal readonly string Blueprint; internal readonly float2 Position; internal readonly float Radius;
            internal Guard(string blueprint, float2 position, float radius) { Blueprint = blueprint; Position = position; Radius = radius; }
        }
        /// <summary>A rock of the approach field: ship_hollow_boulder_&lt;Size&gt; (10, 20, 30 or 44 m), turned to `Yaw`.</summary>
        internal readonly struct Boulder
        {
            internal readonly float2 Position; internal readonly int Size; internal readonly float Yaw;
            internal Boulder(float2 position, int size, float yaw) { Position = position; Size = size; Yaw = yaw; }
        }
        /// <summary>A junction or tunnel mouth where a pilot chooses: for each station reachable from it, the heading of the first leg of
        /// the shortest way there (wayfinding holograms point along it).</summary>
        internal readonly struct Waypoint
        {
            internal readonly float2 Position; internal readonly string[] To; internal readonly float[] Yaw;
            internal Waypoint(float2 position, string[] to, float[] yaw) { Position = position; To = to; Yaw = yaw; }
        }
        internal readonly struct Beacon
        {
            internal readonly float2 Position; internal readonly Color Colour;
            internal Beacon(float2 position, Color colour) { Position = position; Colour = colour; }
        }

        /// <summary>Whether rock (or a structure) rises above +24 m over `p` (Hollow-relative): the cutaway hides the high pieces then.</summary>
        internal static bool Inside(float2 p)
        {
            int c = (int)math.floor((p.x - InsideX0) / InsideCell), r = (int)math.floor((p.y - InsideZ0) / InsideCell);
            return c >= 0 && r >= 0 && c < InsideW && r < InsideH && r < InsideRows.Length && c < InsideRows[r].Length && InsideRows[r][c] == '1';
        }
        internal static bool Near(float2 p, float margin) => p.x > Bounds.x - margin && p.y > Bounds.y - margin && p.x < Bounds.z + margin && p.y < Bounds.w + margin;
    }
}
#endif
