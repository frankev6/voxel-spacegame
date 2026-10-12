// SPEC-11 §4.4 / ADR-0040. Art-facing surface, shared by registered and pooled brick drawing.
#ifndef GAME_BRICK_SURFACE_INCLUDED
#define GAME_BRICK_SURFACE_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "BrickMaterial.hlsl"

struct BrickSurfaceInput
{
    float3 positionCells, positionWS, normalWS, tangentWS, bitangentWS;
    float4 positionCS;
    float2 uv;
    uint layer, colour, damage, scorched;
    float ao;
};
float BrickHash(float2 p)
{
    float3 q = frac(p.xyx * .1031); q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}
half3 BrickPaint(uint paint)
{
    return paint == 0 || paint == 17 ? Brick_TeamPrimary().rgb
        : paint == 18 ? Brick_TeamSecondary().rgb
        : paint == 19 ? Brick_TeamAccent().rgb
        : LOAD_TEXTURE2D(_PaintPalette, int2(paint, 0)).rgb;
}
half BrickDissolve(float2 uv)
{
    float amount = Brick_Dissolve01();
    if (amount <= 0) return 0;
    half n = SAMPLE_TEXTURE2D(_MacroNoiseTex, sampler_MacroNoiseTex, uv * .37).r * .7 + BrickHash(floor(uv * 4)) * .3;
    clip(n - amount);
    return 1 - smoothstep(0, .06, n - amount);
}
void BrickSurface(BrickSurfaceInput i, inout SurfaceData s)
{
    half dissolve = BrickDissolve(i.uv);
    half4 p0 = LOAD_TEXTURE2D(_BrickLayerParams, int2(i.layer, 0));
    half4 p1 = LOAD_TEXTURE2D(_BrickLayerParams, int2(i.layer, 1));
    float2 uv = i.uv / max(1, round(p1.r));
    half3 albedo = SAMPLE_TEXTURE2D_ARRAY(_BrickAlbedoArray, sampler_BrickAlbedoArray, uv, i.layer).rgb;
    half4 mask = SAMPLE_TEXTURE2D_ARRAY(_BrickMaskArray, sampler_BrickMaskArray, uv, i.layer);
    half2 nxy = SAMPLE_TEXTURE2D_ARRAY(_BrickNormalArray, sampler_BrickNormalArray, uv, i.layer).rg * 2 - 1;
    s.normalTS = half3(nxy, sqrt(saturate(1 - dot(nxy, nxy))));
    half mean = Luminance(SAMPLE_TEXTURE2D_ARRAY_LOD(_BrickAlbedoArray, sampler_BrickAlbedoArray, uv, i.layer, 8).rgb);
    albedo = lerp(albedo, BrickPaint(i.colour) * Luminance(albedo) / max(mean, .02h), mask.b);
    half macro = SAMPLE_TEXTURE2D(_MacroNoiseTex, sampler_MacroNoiseTex, i.uv / max(_MacroScale, 1)).r;
    albedo *= 1 + (macro - .5h) * 2 * _MacroStrength;
    uint stage = min(i.damage, 3u);
    half scorch = max(stage / 3.0h, (half)i.scorched) * _ScorchStrength * p1.g;
    albedo = lerp(albedo, _ScorchColor.rgb, saturate(scorch));
    albedo *= SAMPLE_TEXTURE2D(_DamageRamp, sampler_DamageRamp, float2(stage / 3.0, .5)).rgb;
    s.metallic = mask.r; s.smoothness = saturate(mask.g - .1h * min(stage, 1u));
    float2 inCell = frac(i.uv);
    float edgeDistance = min(min(inCell.x, 1 - inCell.x), min(inCell.y, 1 - inCell.y));
    half wearWidth = .05h * p1.a * (stage > 0 ? 1.4h : 1);
    half wear = wearWidth > 0 ? 1 - smoothstep(wearWidth * .4h, wearWidth, edgeDistance) : 0;
    albedo = lerp(albedo, half3(.42, .43, .45), wear * .8h);
    s.metallic = lerp(s.metallic, 1, wear); s.smoothness = lerp(s.smoothness, .55h, wear);
    half crackAmount = stage > 1 ? p1.b * (stage - 1) * .5h : 0;
    half crack = 0;
    if (crackAmount > 0)
    {
        half field = SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, i.uv / max(_CrackTiling, .001)).r;
        half width = crackAmount * .12h, aa = max(fwidth(field), 1e-3);
        crack = 1 - smoothstep(width - aa, width + aa, field);
        albedo = lerp(albedo, _ScorchColor.rgb * (1 - .65h * _ScorchStrength), crack);
        s.smoothness = lerp(s.smoothness, .1h, crack);
    }
    if (_HalftoneScale > 0 && p1.g > 0 && stage > 0)
    {
        float2 px = i.positionCS.xy;
        float2 q = float2(px.x + px.y, px.y - px.x) * .70710678 / _HalftoneScale;
        float d = length(frac(q) - .5), radius = sqrt((stage == 1 ? .15 : stage == 2 ? .4 : .75) / PI);
        float aa = max(fwidth(d), 1e-3);
        albedo = lerp(albedo, _ScorchColor.rgb, 1 - smoothstep(radius - aa, radius + aa, d));
    }
    s.albedo = lerp(_AOColor.rgb, albedo, lerp(1, i.ao, _AOStrength));
    s.occlusion = 1; s.alpha = 1;
    float3 normalWS = normalize(s.normalTS.x * i.tangentWS + s.normalTS.y * i.bitangentWS + s.normalTS.z * i.normalWS);
    half fresnel = 1 - saturate(dot(normalWS, GetWorldSpaceNormalizeViewDir(i.positionWS)));
    half powered = Brick_GridPowered();
    half3 layerEmission = p0.a > .5 ? Brick_TeamAccent().rgb * max(p0.r, max(p0.g, p0.b)) : p0.rgb;
    s.emission = layerEmission * mask.a * _EmissiveIntensity * powered;
    if (_SeamEmission > 0 && i.layer < 16)
    {
        float2 panel = frac(uv); float d = min(min(panel.x, 1 - panel.x), min(panel.y, 1 - panel.y));
        half aa = max(fwidth(d), 1e-4), seam = 1 - smoothstep(2.0 / 256 - aa, 2.0 / 256 + aa, d);
        half on = stage == 0 ? 1 : stage == 1 ? step(.05, BrickHash(floor(uv) + floor(_Time.y * 8))) : 0;
        half fade = _SeamFadeEnd > _SeamFadeStart ? saturate((_SeamFadeEnd - distance(GetCameraPositionWS(), i.positionWS)) / (_SeamFadeEnd - _SeamFadeStart)) : 1;
        s.emission += Brick_TeamAccent().rgb * _SeamEmission * seam * on * fade * powered;
    }
    half fadeHit = _EmberFadeSeconds > 0 ? saturate(1 - (_Time.y - Brick_LastHitTime()) / _EmberFadeSeconds) : 0;
    half ember = stage == 3 ? 1 : stage == 2 ? .35h : 0;
    half blink = _HalftoneScale > 0 ? step(.5, frac(_Time.y * 2)) : 1;
    s.emission += _EmberColor.rgb * _EmberIntensity * (fadeHit * ember * crack * blink + dissolve);
    s.emission += _RimColor.rgb * _RimStrength * pow(fresnel, 4);
    half4 highlight = Brick_Highlight(); s.emission += highlight.rgb * highlight.a * fresnel * fresnel;
}
half3 BrickToonLight(Light light, half3 normalWS, half3 albedo, bool key)
{
    half n = saturate(dot(normalWS, light.direction)) * light.shadowAttenuation;
    half ramp = SAMPLE_TEXTURE2D_LOD(_ToonRamp, sampler_ToonRamp, float2(n, .5), 0).r;
    return (key ? lerp(_AOColor.rgb, albedo, ramp) : albedo * ramp) * light.color * light.distanceAttenuation;
}
half4 BrickLighting(BrickSurfaceInput i, SurfaceData s)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = i.positionWS; inputData.positionCS = i.positionCS;
    inputData.normalWS = normalize(s.normalTS.x * i.tangentWS + s.normalTS.y * i.bitangentWS + s.normalTS.z * i.normalWS);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
    inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS); inputData.shadowMask = half4(1, 1, 1, 1);
    inputData.bakedGI = SampleSH(inputData.normalWS); inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
    if (_ShadingMode < .5) return UniversalFragmentPBR(inputData, s);
    half3 lit = BrickToonLight(GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask), inputData.normalWS, s.albedo, true) + inputData.bakedGI * s.albedo;
#if defined(_ADDITIONAL_LIGHTS)
    uint count = GetAdditionalLightsCount();
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        lit += BrickToonLight(GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask), inputData.normalWS, s.albedo, false);
    }
#endif
    LIGHT_LOOP_BEGIN(count)
        lit += BrickToonLight(GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask), inputData.normalWS, s.albedo, false);
    LIGHT_LOOP_END
#endif
    return half4(lit + s.emission, s.alpha);
}
#endif
