// QA-001 / DESIGN-GUIDE §3 and §5.2: NOCTURNE hull plating generated per fragment from grid-local cell coordinates.
// A port of the look book's ld_plating.py idea to the brick grid: 1.5 m (3 cell) panels split recursively into plates,
// bevelled raised/inset relief, seams, rivet rows, the plate-tone mosaic, grime streaks and sodium slit windows on
// vertical faces. No textures, so it costs no memory and stays sharp at any hull size. Layer row 2/3 select the style.
#ifndef GAME_NOCTURNE_PLATING_INCLUDED
#define GAME_NOCTURNE_PLATING_INCLUDED

struct PlatingResult
{
    half3 tone;        // multiplies the paint colour
    half3 normalTS;    // relief normal, tangent space of the face
    half seam;         // 1 in a seam groove
    half bare;         // 1 on worn bare-metal edges
    half emission;     // window/slot emission mask (0..1)
    half glass;        // 1 on window glass (dark, smooth)
    half stripe;       // cargo hazard plate: 0 none, 1 dark band, 2 yellow band
    half smoothness;   // additive smoothness tweak
};

float PlatingHash(float3 p)
{
    p = frac(p * float3(.1031, .1030, .0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.x + p.y) * p.z);
}
float3 PlatingHash3(float3 p)
{
    p = frac(p * float3(.1031, .1030, .0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

// Antialiased band: 1 where x < w, filtered by the pixel footprint of x.
half PlatingBand(float x, float w)
{
    float aa = max(fwidth(x), 1e-4);
    return 1 - smoothstep(w - aa, w + aa, x);
}

// Plate tones from the guide's mosaic (worn grey 42 %, lighter 20 %, cooler 22 %, warm 10 %, olive 6 %) as ratios.
half3 PlatingTone(float h)
{
    return h < .42 ? half3(1, 1, 1) : h < .62 ? half3(1.045, 1.04, 1.035) : h < .84 ? half3(.94, .945, .95)
        : h < .94 ? half3(1.015, .985, .93) : half3(.91, .92, .89);
}

// style: 1 hull, 2 armour, 3 machinery, 4 cargo. face: unit normal in grid space. uv: face-plane cell coordinates.
PlatingResult NocturnePlating(float2 uv, float3 cells, float3 face, float faceId, int style, half windows, half relief, float seed)
{
    PlatingResult r;
    r.tone = 1; r.normalTS = half3(0, 0, 1); r.seam = 0; r.bare = 0; r.emission = 0; r.glass = 0; r.stripe = 0; r.smoothness = 0;
    float panel = style == 2 ? 2 : 3;                         // armour uses 1 m slabs, the rest the 1.5 m big-block grid
    float2 id0 = floor(uv / panel);
    float2 lo = id0 * panel, size = panel;
    float rectId = 1;
    int depth = style == 2 ? 1 : style == 3 ? 2 : 3;
    // Recursive split: stop early on some panels so big plates and fine plates mix (stop chances per depth).
    [unroll] for (int d = 0; d < 3; d++)
    {
        if (d >= depth) break;
        float3 h = PlatingHash3(float3(id0 + rectId * 17.31, faceId + seed + d * 7.7));
        float stopChance = d == 0 ? .12 : d == 1 ? .38 : .62;
        if (h.x < stopChance) break;
        bool alongU = size.x > size.y * 1.15 ? true : size.y > size.x * 1.15 ? false : h.y < .5;
        float length = alongU ? size.x : size.y;
        // Split position snapped to a quarter cell (12.5 cm), between 30 % and 70 % of the plate.
        float cut = clamp(round(lerp(.3, .7, h.z) * length * 4) / 4, .5, length - .5);
        if (length < 1.0) break;
        float coord = alongU ? uv.x - lo.x : uv.y - lo.y;
        bool second = coord >= cut;
        if (alongU) { if (second) { lo.x += cut; size.x -= cut; } else size.x = cut; }
        else { if (second) { lo.y += cut; size.y -= cut; } else size.y = cut; }
        rectId = rectId * 2 + (second ? 1 : 0);
    }
    float2 local = uv - lo;
    float4 edges = float4(local.x, size.x - local.x, local.y, size.y - local.y);   // left, right, bottom, top
    float dist = min(min(edges.x, edges.y), min(edges.z, edges.w));
    float2 inward = edges.x == dist ? float2(1, 0) : edges.y == dist ? float2(-1, 0) : edges.z == dist ? float2(0, 1) : float2(0, -1);
    float plate = PlatingHash(float3(lo * 1.37 + size, faceId * 3.1 + seed));
    float3 ph = PlatingHash3(float3(lo + size * .5, seed + 11.7));

    // Relief: flush, raised or inset plates with a 4 cm bevel; the slope gives the normal.
    float kind = ph.x;
    float raise = style == 2 ? 1 : kind < .55 ? 0 : kind < .85 ? 1 : -1;
    const float bevel = .09;
    float slope = abs(raise) * relief * .55 * (1 - smoothstep(0, bevel, dist)) / bevel * (raise >= 0 ? 1 : -1);
    float2 grad = inward * slope;

    // Seam groove at every plate edge; wider at the panel (big-block) border.
    float2 panelLocal = uv - id0 * panel;
    float panelDist = min(min(panelLocal.x, panel - panelLocal.x), min(panelLocal.y, panel - panelLocal.y));
    half seam = max(PlatingBand(dist, .022), PlatingBand(panelDist, .04));
    r.seam = seam;

    // Rivet rows along raised plates and armour edges.
    if ((raise > 0 || style == 2) && ph.y < .75)
    {
        float inset = style == 2 ? .16 : .11, pitch = style == 2 ? .42 : .3;
        float along = abs(inward.x) > 0 ? local.y : local.x;
        float across = dist - inset;
        float t = along - (round((along - pitch * .5) / pitch) * pitch + pitch * .5);
        float rd = length(float2(t, across));
        float rr = style == 2 ? .045 : .034;
        if (rd < rr * 1.6)
        {
            float cap = saturate(1 - rd / rr);
            float2 dir = rd > 1e-4 ? float2(t, across) / rd : 0;
            float2 rivetGrad = -dir * cap * 1.8;
            float2 alongAxis = abs(inward.x) > 0 ? float2(0, 1) : float2(1, 0);
            grad += alongAxis * rivetGrad.x + inward * rivetGrad.y;
            r.bare = max(r.bare, PlatingBand(rd, rr * .55) * .6);
        }
    }

    // Machinery: louvred vents on some plates and a recessed glowing slot along others.
    if (style == 3)
    {
        if (ph.z < .45)
        {
            float louvre = frac((abs(inward.x) > 0 ? local.x : local.y) * 5.0);
            grad += (abs(inward.x) > 0 ? float2(1, 0) : float2(0, 1)) * (louvre < .5 ? .9 : -.9) * step(.12, dist);
            r.tone *= .78;
        }
        else if (ph.z > .8)
        {
            float slot = abs((abs(inward.x) > 0 ? local.y - size.y * .5 : local.x - size.x * .5));
            r.emission = PlatingBand(slot, .05) * step(.18, dist);
        }
    }
    // Cargo: hazard chevrons on one plate in five.
    if (style == 4 && ph.z < .22)
    {
        float diag = frac((uv.x + uv.y) * 1.6);
        r.stripe = step(.1, dist) * (diag < .5 ? 1 : 2);          // 1 dark band, 2 hazard-yellow band
    }

    // Tone: the plate mosaic plus a little per-plate brightness, then grime that streaks downward on vertical faces.
    r.tone *= PlatingTone(plate) * (.96 + .08 * ph.y);
    half grime = 0;
    if (abs(face.y) < .5)
    {
        float streak = PlatingHash(float3(floor(uv.x * 6) + faceId, floor(cells.y / 6), seed));
        float fall = frac(-cells.y / 3 + streak);
        grime = streak > .7 ? (1 - fall) * .18 : 0;
    }
    grime += (1 - smoothstep(0, .35, local.y)) * .06;            // dirt settles at the lower edge of each plate
    r.tone *= 1 - grime;

    // Bare metal: chipped bevels on raised plates.
    float chips = PlatingHash(float3(floor(uv * 7), seed + 3));
    r.bare = max(r.bare, raise > 0 && chips > .55 ? PlatingBand(dist, .035) : 0);

    // Windows: sodium slits on vertical hull faces, in deck rows, lit in runs (guide §3.3 and §5.1).
    if (style == 1 && windows > 0 && abs(face.y) < .3)
    {
        float3 horizontal = normalize(cross(float3(0, 1, 0), face));
        float x = dot(cells, horizontal);
        float row = floor((cells.y - .5) / 4);
        float yIn = cells.y - (row * 4 + .5) - 1.5;               // window band centred 2 cells above each deck line
        const float pitch = .84, halfWidth = .13, halfHeight = .22;
        float k = floor(x / pitch);
        float q = x - (k + .5) * pitch;
        float lit = PlatingHash(float3(k, row, faceId + seed + 5));
        float run = PlatingHash(float3(floor(k / 6), row, faceId + seed + 9));
        float present = step(1 - windows, PlatingHash(float3(floor(k / 3), row, faceId + seed + 13)));
        half shape = PlatingBand(abs(q), halfWidth) * PlatingBand(abs(yIn), halfHeight) * present;
        r.glass = shape;
        r.emission = max(r.emission, shape * step(lit, .72) * step(run, .78));
    }
    r.normalTS = normalize(half3(-grad * .5, 1));
    r.smoothness = r.glass * .5 - r.seam * .2;
    return r;
}
#endif
