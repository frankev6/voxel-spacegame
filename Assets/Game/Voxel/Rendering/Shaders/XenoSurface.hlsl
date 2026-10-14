// ADR-0115 / DESIGN-GUIDE §7: grown alien tissue in the NOCTURNE colourway "Obsidian & acid".
// Shell #121315 / #26272C, grooves #030304, veins #B8FF2E, glow #D4FF6A. The vein network is procedural in grid space, so
// any colourway is a palette swap (layer rows 0/3). Tissue exposed by damage (deep samples) glows as living flesh.
#ifndef GAME_XENO_SURFACE_INCLUDED
#define GAME_XENO_SURFACE_INCLUDED

float XenoHash(float3 p)
{
    p = frac(p * float3(.1031, .1030, .0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.x + p.y) * p.z);
}
float XenoNoise(float3 p)
{
    float3 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
    return lerp(lerp(lerp(XenoHash(i), XenoHash(i + float3(1, 0, 0)), f.x), lerp(XenoHash(i + float3(0, 1, 0)), XenoHash(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(XenoHash(i + float3(0, 0, 1)), XenoHash(i + float3(1, 0, 1)), f.x), lerp(XenoHash(i + float3(0, 1, 1)), XenoHash(i + 1), f.x), f.y), f.z);
}
float XenoFbm(float3 p)
{
    float sum = 0, amplitude = .5;
    [unroll] for (int i = 0; i < 4; i++) { sum += XenoNoise(p) * amplitude; p = p * 2.03 + 17.1; amplitude *= .5; }
    return sum / .9375;
}
// Ridged band: 1 on the zero crossing of a noise field, used for thin vein lines.
float XenoRidge(float3 p) { return 1 - abs(XenoFbm(p) * 2 - 1); }

struct XenoResult { half3 albedo; half3 emission; half smoothness; half metallic; };

// cells: grid-local position in cells. depth: cells below the grown surface (0 on the shell, > 1 on exposed flesh).
// glow: 0 shell tissue .. 1 sacs/organs. shell/vein/flesh colours come from the layer (linear).
XenoResult XenoShade(float3 cells, half depth, half glow, half3 shellA, half3 shellB, half3 vein, half3 flesh)
{
    XenoResult r;
    float3 p = cells * .5;                                    // metres
    float warp = XenoFbm(p * .21 + 3.7);
    float ridgeA = XenoRidge(p * .32 + warp * 1.7);
    float ridgeB = XenoRidge(p * .71 + 11.3 + warp);
    // Thin lines must not widen with distance: once a vein is narrower than a pixel its contrast fades instead.
    float aaA = max(fwidth(ridgeA), 1e-4), aaB = max(fwidth(ridgeB), 1e-4);
    half fadeA = (half)saturate(.012 / aaA), fadeB = (half)saturate(.008 / aaB);
    half veins = (half)max(smoothstep(.978, .993, ridgeA) * fadeA, smoothstep(.985, .996, ridgeB) * .5h * fadeB);
    half groove = (half)saturate(smoothstep(.9, .965, ridgeA) * fadeA - veins);
    half mottling = (half)XenoFbm(p * 1.3 + 5.1);
    r.albedo = lerp(shellA, shellB, mottling);
    r.albedo = lerp(r.albedo, half3(.0009, .001, .0012), groove * .85h);
    r.albedo = lerp(r.albedo, vein * .35h, veins);
    // Chitin is glossy: strong white highlights on black (look book GRAVID / BROODMOTHER).
    r.smoothness = lerp(.72h, .45h, groove);   // glossy, but broad enough that the key and rim don't blow out to white
    r.metallic = .05h;
    half pulse = .82h + .18h * (half)sin(_Time.y * 1.6 + p.x * .35 + p.z * .21);
    // The grown shell's shallowest tissue sits < 0.75 cells deep; anything deeper is a wound (or a sac) and glows.
    half exposed = saturate((depth - .8h) / .7h);
    r.emission = vein * veins * 1.7h * pulse + flesh * (exposed * 3.0h + glow * 3.2h) * pulse;
    r.albedo = lerp(r.albedo, flesh * .5h, max(exposed, glow) * .8h);
    return r;
}
#endif
