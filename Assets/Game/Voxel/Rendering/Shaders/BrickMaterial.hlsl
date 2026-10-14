// SPEC-11 / ADR-0040: material layout and canonical DOTS property defaults.
#ifndef GAME_BRICK_MATERIAL_INCLUDED
#define GAME_BRICK_MATERIAL_INCLUDED
        CBUFFER_START(UnityPerMaterial)
            float _CrackTiling;
            half4 _ScorchColor;
            half _ScorchStrength;
            half4 _EmberColor;
            half _EmberIntensity;
            half _EmberFadeSeconds;
            float4 _ExposedLayers;
            half _AOStrength;
            half4 _AOColor;
            float _MacroScale;
            half _MacroStrength;
            half _EmissiveIntensity;
            half _SeamEmission;
            float _SeamFadeStart;
            float _SeamFadeEnd;
            half4 _RimColor;
            half _RimStrength;
            half _ShadingMode;
            float _HalftoneScale;
            float4 _TeamPrimary;
            float4 _TeamSecondary;
            float4 _TeamAccent;
            float _GridPowered;
            float _LastHitTime;
            float4 _Highlight;
            float _Dissolve01;
            float _FunctionPaint, _Damage01, _Powered, _Throttle;
            // Editor overrides must retain an SRP-compatible fallback for live Entities Graphics props.
            float _DraftPaintIndex, _DraftSliceEnabled, _DraftSliceTop;
            float4x4 _DraftWorldToCells;
        CBUFFER_END

        TEXTURE2D_ARRAY(_BrickAlbedoArray); SAMPLER(sampler_BrickAlbedoArray);
        TEXTURE2D_ARRAY(_BrickMaskArray);   SAMPLER(sampler_BrickMaskArray);
        TEXTURE2D_ARRAY(_BrickNormalArray); SAMPLER(sampler_BrickNormalArray);
        TEXTURE2D(_BrickLayerParams);
        TEXTURE2D(_PaintPalette);
        TEXTURE2D(_CrackTex);      SAMPLER(sampler_CrackTex);
        TEXTURE2D(_DamageRamp);    SAMPLER(sampler_DamageRamp);
        TEXTURE2D(_MacroNoiseTex); SAMPLER(sampler_MacroNoiseTex);
        TEXTURE2D(_ToonRamp);      SAMPLER(sampler_ToonRamp);


TEXTURE2D(_PropAlbedo); SAMPLER(sampler_PropAlbedo);
TEXTURE2D(_PropMask); SAMPLER(sampler_PropMask);
TEXTURE2D(_PropNormal); SAMPLER(sampler_PropNormal);
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _TeamPrimary)
    UNITY_DOTS_INSTANCED_PROP(float4, _TeamSecondary)
    UNITY_DOTS_INSTANCED_PROP(float4, _TeamAccent)
    UNITY_DOTS_INSTANCED_PROP(float, _GridPowered)
    UNITY_DOTS_INSTANCED_PROP(float, _LastHitTime)
    UNITY_DOTS_INSTANCED_PROP(float4, _Highlight)
    UNITY_DOTS_INSTANCED_PROP(float, _Dissolve01)
    UNITY_DOTS_INSTANCED_PROP(float, _FunctionPaint)
    UNITY_DOTS_INSTANCED_PROP(float, _Damage01)
    UNITY_DOTS_INSTANCED_PROP(float, _Powered)
    UNITY_DOTS_INSTANCED_PROP(float, _Throttle)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
#endif
float4 Brick_TeamPrimary()
{
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
    return UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _TeamPrimary);
#else
    return _TeamPrimary;
#endif
}
float4 Brick_TeamSecondary()
{
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
    return UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _TeamSecondary);
#else
    return _TeamSecondary;
#endif
}
float4 Brick_TeamAccent()
{
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
    return UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _TeamAccent);
#else
    return _TeamAccent;
#endif
}
float Brick_GridPowered()
{
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
    return UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _GridPowered);
#else
    return _GridPowered;
#endif
}
float Brick_LastHitTime()
{
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
    return UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _LastHitTime);
#else
    return _LastHitTime;
#endif
}
float4 Brick_Highlight()
{
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
    return UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _Highlight);
#else
    return _Highlight;
#endif
}
float Brick_Dissolve01()
{
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
    return UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Dissolve01);
#else
    return _Dissolve01;
#endif
}
float Brick_FunctionPaint()
{
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
    return UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _FunctionPaint);
#else
    return _FunctionPaint;
#endif
}
float Brick_Damage01()
{
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
    return UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Damage01);
#else
    return _Damage01;
#endif
}
float Brick_Powered()
{
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
    return UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Powered);
#else
    return _Powered;
#endif
}
float Brick_Throttle()
{
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
    return UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Throttle);
#else
    return _Throttle;
#endif
}

#endif
