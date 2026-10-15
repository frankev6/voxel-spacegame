// QA-001 / DESIGN-GUIDE §3 and §5.2: NOCTURNE hull plating from the look book's own generator.
// ArtSource/Bricks/bake_plating.py bakes ArtSource/LookDev/ld_plating.py (plates, bolts, grilles, hatches, stencils,
// chevrons, wear and window rows) into three tiling texture arrays; NocturnePlating composes them like the look book's
// ld_mats.plated_material, on grid-continuous face UVs (one 6 m tile = 12 cells). The detail is all texture, no polygons.
//   _PlatingA: R tint x0.5, G secondary paint, B black/stencil, A accent
//   _PlatingB: R height, G dark metal, B edge wear, A lit windows
//   _PlatingN: RG tangent normal (x along u, y along v), B cavity occlusion
// Slices: 0-2 hull plate (round-4 "ark"), 3-5 calm plate ("yard") for armour, machinery, cargo and props: three seeds each.
// Owner (QA-001): "how do we make the procedural pattern not look like repeated stamps". Each 6 m tile of each face plane
// picks its own variant and mirror (and a quarter turn on horizontal faces), and tiles meet at a structural frame seam.
#ifndef GAME_NOCTURNE_PLATING_INCLUDED
#define GAME_NOCTURNE_PLATING_INCLUDED

float PlatingHash(float3 p)
{
    p = frac(p * float3(.1031, .1030, .0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.x + p.y) * p.z);
}

struct PlatingResult
{
    half3 albedo;
    half3 normalTS;    // tangent space of the face (x along u, y along v)
    half metallic;
    half smoothness;
    half glow;         // lit windows (flickered) or machinery grille slots; multiplies the layer's row-0 emission
};

// uv: face-plane cell coordinates (v is world-up on vertical faces). cells: grid-local position. face: grid normal.
// style: 1 hull, 2 armour, 3 machinery, 4 cargo. windows: 0..1. relief: normal strength. metal: base metallic.
// paint/accent: linear livery colours.
PlatingResult NocturnePlating(float2 uv, float3 cells, float3 face, int style, half windows, half relief, half metal,
    half3 paint, half3 accent)
{
    PlatingResult r;
    float2 t = uv / 12.0, tile = floor(t), f = t - tile;
    float plane = round(dot(cells, face) * 2);                       // parallel faces of one hull differ too
    float3 h = float3(PlatingHash(float3(tile, plane + dot(abs(face), float3(1, 2, 3)) * 97)),
                      PlatingHash(float3(tile + 31.7, plane)), PlatingHash(float3(tile - 13.1, plane + 5)));
    float slice = (style == 1 ? 0 : 3) + min(floor(h.x * 3), 2);
    // Tile transform M (mirror, then a quarter turn on horizontal faces only, so windows and rain stay upright).
    float2x2 m = float2x2(h.y < .5 ? -1 : 1, 0, 0, 1);
    if (abs(face.y) > .7)
    {
        int k = (int)min(floor(h.z * 4), 3);
        float2x2 rot = k == 0 ? float2x2(1, 0, 0, 1) : k == 1 ? float2x2(0, -1, 1, 0) : k == 2 ? float2x2(-1, 0, 0, -1) : float2x2(0, 1, -1, 0);
        m = mul(rot, m);
    }
    float2 st = mul(m, f - .5) + .5, dx = mul(m, ddx(t)), dy = mul(m, ddy(t));
    half4 a = SAMPLE_TEXTURE2D_ARRAY_GRAD(_PlatingA, sampler_PlatingA, st, slice, dx, dy);
    half4 b = SAMPLE_TEXTURE2D_ARRAY_GRAD(_PlatingB, sampler_PlatingB, st, slice, dx, dy);
    half4 n = SAMPLE_TEXTURE2D_ARRAY_GRAD(_PlatingN, sampler_PlatingN, st, slice, dx, dy);
    // Structural frame seam where tiles meet (2.5 cm groove), so a variant change reads as a frame, not a cut.
    float edge = min(min(f.x, 1 - f.x), min(f.y, 1 - f.y)) * 12, aa = max(fwidth(edge), 1e-4);
    half frame = 1 - smoothstep(.05 - aa, .05 + aa, edge);

    // Livery: the plate tint with a slow drift between 3 m blocks so no two big panels match, accent stencils and chevrons,
    // black stencils, dark-metal grilles and bolts, then bare metal. The company livery leaves replacement plates (A.g) in
    // the hull paint (look book: plate_paint unset); secondary-painted blocks carry the ochre instead.
    // The plate tint is compressed to 45 %: the look book's swing reads as a camo patchwork on the light worn-grey hull.
    half3 c = paint;
    half drift = lerp(.96h, 1.04h, (half)PlatingHash(floor((cells - face * .04) / 6) + 17.3));
    c *= (1 + (a.r * 2 - .955h) * .45h) * drift;
    c = lerp(c, accent, a.a);
    c = lerp(c, half3(.0070, .0080, .0097), a.b);                     // #141518
    half darkMetal = b.g * .7h;
    c = lerp(c, half3(.023, .025, .028), darkMetal);                  // #2A2C2F
    half wear = b.b * .8h;
    c = lerp(c, half3(.39, .41, .42), wear);                          // #A7AAAD

    // Grime: baked cavity occlusion, broad dirt clouds, and NOCTURNE rain streaks running down vertical faces.
    half3 grimeColour = paint * half3(.3, .28, .25);
    half cavity = saturate(1 - n.b);
    half clouds = saturate((SAMPLE_TEXTURE2D(_MacroNoiseTex, sampler_MacroNoiseTex, uv / 23.0).r - .5h) * 3) * .35h;
    c = lerp(c, grimeColour, saturate(max(max(cavity * .9h, frame * .8h), clouds)));
    bool vertical = abs(face.y) < .3;
    half streak = 0;
    if (vertical)
    {
        half s = SAMPLE_TEXTURE2D(_MacroNoiseTex, sampler_MacroNoiseTex, float2(uv.x / 14.0, cells.y / 160.0)).r;
        streak = saturate((s - .55h) / .2h) * .16h;
        c = lerp(c, half3(.03, .027, .02), streak);
    }

    // Windows: lit ports on vertical faces only, glass in between, each window its own brightness (flicker .25..1).
    half lit = vertical ? b.a * windows : 0;
    c = lerp(c, half3(.008, .016, .025), saturate(lit));               // glass #16222C
    half flicker = lerp(.25h, 1, (half)PlatingHash(floor(cells * 1.25 + face * .3) + 5.1));
    r.glow = lit * flicker;
    // Machinery: the grille slots (full-strength dark metal) carry the block's function light, dimly.
    if (style == 3) r.glow = saturate((b.g - .85h) * 6.67h) * .35h;

    r.albedo = c;
    half2 nxy = mul(transpose(m), (float2)(n.rg * 2 - 1)) * relief;   // back from tile space to face space
    r.normalTS = normalize(half3(nxy, 1));
    half rough = lerp(lerp(.5h, .3h, wear), .45h, b.g);
    rough = lerp(rough, .28h, streak);
    r.smoothness = 1 - rough + lit * .25h;
    r.metallic = lerp(lerp(metal, .85h, wear), .7h, b.g * .8h);
    return r;
}
#endif
