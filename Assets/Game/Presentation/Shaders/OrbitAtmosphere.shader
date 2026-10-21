// Feel prototype (2026-10-21): the glow of a below-plane planet's atmosphere, an additive shell just outside the surface cap. It
// brightens towards the limb and on the day side, so the horizon under the flight plane reads as a curve of light.
Shader "Game/Orbit/Atmosphere"
{
    Properties
    {
        [HDR] _Glow ("Glow", Color) = (0.55, 0.85, 1.7, 1)
        _Power ("Limb power", Float) = 7
        _SunFallback ("Sun direction if no light", Vector) = (0.4, 0.6, 0.3, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Glow, _SunFallback; float _Power;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldDir(normalize(input.positionOS));
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS), V = normalize(_WorldSpaceCameraPos - input.positionWS);
                Light sun = GetMainLight();
                float3 L = dot(sun.direction, sun.direction) > 0.5 ? normalize(sun.direction) : normalize(_SunFallback.xyz);
                float limb = pow(1 - saturate(dot(n, V)), _Power);
                float day = saturate(dot(n, L) * 0.8 + 0.35);
                return half4(_Glow.rgb * limb * day, 0);
            }
            ENDHLSL
        }
    }
}
