// DEV-023 the Hollow's rock (owner: "the texture of the asteroid should be rock not the ship hull texture"; "like subtle large moon
// craters"; then "bring back the detail, and make the rocks a bit colored and darker and ideally multiple colors on the rocky surface,
// also the 'craters' are circles in a voxel game, make them look blocky and voxely"): layer style 6. Everything is decided per voxel
// cell (2 m on the Hollow) so the surface reads as voxels: mineral patches and strata in several colours, crater fields stepped into
// terraces with a lit and a shadowed edge on each step, and a 1 m grain inside each cell. Solid in grid space, so it runs on across
// block and brick edges; the face normal stays flat (the blocks are the relief).
#ifndef GAME_ROCK_SURFACE_INCLUDED
#define GAME_ROCK_SURFACE_INCLUDED

struct RockResult { half3 albedo; float3 normalWS; half smoothness; };

float3 RockHash33(float3 p)
{
    p = frac(p * float3(.1031, .1030, .0973)); p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}
float RockNoise(float3 p)
{
    float3 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
    float a = lerp(lerp(RockHash33(i).x, RockHash33(i + float3(1, 0, 0)).x, f.x), lerp(RockHash33(i + float3(0, 1, 0)).x, RockHash33(i + float3(1, 1, 0)).x, f.x), f.y);
    float b = lerp(lerp(RockHash33(i + float3(0, 0, 1)).x, RockHash33(i + float3(1, 0, 1)).x, f.x), lerp(RockHash33(i + float3(0, 1, 1)).x, RockHash33(i + float3(1, 1, 1)).x, f.x), f.y);
    return lerp(a, b, f.z);
}
// Craters on a jittered lattice (one per lattice cell, some absent): < 0 in a bowl, a raised rim, 0 elsewhere.
float RockCraters(float3 p, float density)
{
    float3 i = floor(p), f = frac(p);
    float h = 0;
    for (int z = -1; z <= 1; z++) for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
    {
        float3 o = float3(x, y, z), r = RockHash33(i + o);
        if (r.z > density) continue;
        float3 c = o + .2 + .6 * r;
        float radius = .28 + .3 * frac(r.x * 7.13);
        float d = length(f - c) / radius;
        float bowl = d < 1 ? -(1 - d * d) : 0;
        float rim = exp(-(d - 1) * (d - 1) * 12) * .4;
        h = min(h, bowl) + rim * (1 + h);
    }
    return h;
}
// The crater height at a cell, stepped into terraces (five levels from rim to floor).
float RockTerrace(float3 cell)
{
    float h = RockCraters(cell * .05, .65) + RockCraters(cell * .17 + 7.3, .4) * .6;
    return floor(h * 5 + .5) / 5;
}

// cells: grid-space position in cells; face: the grid-space outward face normal; tint: the paint colour (its hue tints the stone);
// relief: the terrace edges' strength.
RockResult RockShade(float3 cells, float3 face, float3 normalWS, half3 tint, half relief)
{
    RockResult r;
    // the solid cell behind this face, and the face's own two axes
    float3 cell = floor(cells - face * .5) + .5;
    float3 a = abs(face);
    float3 t1 = a.x > .5 ? float3(0, 1, 0) : float3(1, 0, 0), t2 = a.z > .5 ? float3(0, 1, 0) : float3(0, 0, 1);
    // minerals: slate and pale stone patches tens of metres across, ochre seams, rust strata, basalt blotches (linear colours)
    float m = RockNoise(cell * .028), m2 = RockNoise(cell * .061 + 5.1), m3 = RockNoise(cell * .12 + 9.7);
    float strata = frac(cell.y * .09 + m * 1.7);
    half3 slate = half3(.13, .145, .17), pale = half3(.29, .27, .245), ochre = half3(.30, .2, .1), rust = half3(.25, .11, .065), basalt = half3(.06, .06, .07);
    half3 stone = lerp(slate, pale, smoothstep(.3, .7, m));
    stone = lerp(stone, ochre, smoothstep(.58, .82, m2) * .85);
    stone = lerp(stone, rust, step(.84, strata) * smoothstep(.35, .7, m2) * .7);
    stone = lerp(stone, basalt, smoothstep(.72, .9, m3) * .8);
    // craters, stepped: bowls darker, and each terrace step lit on one side and shadowed on the other
    float level = RockTerrace(cell);
    float edge = level - RockTerrace(cell + t1 + t2);
    half shade = 1 + level * .18 + clamp(edge, -.4, .4) * .9 * relief;
    // detail: a 1 m grain inside each cell, and a little tone per cell
    half grain = RockHash33(floor(cells * 2) + 17).x - .5;
    half per = RockHash33(cell).y - .5;
    r.albedo = max(0, stone * shade * (1 + grain * .16 + per * .12) * (tint / max(Luminance(tint), .05)));
    r.smoothness = .1 + (RockHash33(cell).z > .93 ? .25 : 0);   // a few glassy cells
    r.normalWS = normalWS;
    return r;
}
#endif
