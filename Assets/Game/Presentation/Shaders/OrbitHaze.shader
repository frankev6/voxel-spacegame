// DEV-028 (exploration slice): one soft card of nebula or belt dust, drawn instanced near the camera. Noise in object space, faded at
// the card's rim and when the camera is close (so flying through a card never shows its edge). Additive, no depth write.
Shader "Game/Orbit/Haze"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (0.55, 0.24, 0.70, 1)
        [HDR] _Color2 ("Second colour", Color) = (0.20, 0.55, 0.85, 1)
        _Density ("Density", Range(0, 2)) = 0.35
        _NearFade ("Fade within (m)", Float) = 600
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _Color2; float _Density, _NearFade;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; float3 positionWS : TEXCOORD1; float seed : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.positionOS = input.positionOS;
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                o.seed = frac(dot(floor(origin / 997.0), float3(0.137, 0.719, 0.331)));
                return o;
            }
            float Hash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float Noise(float3 x)
            {
                float3 i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1,0,0)), f.x), lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), f.x), lerp(Hash(i + float3(0,1,1)), Hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.positionOS.xz;   // unit quad, -0.5..0.5
                float rim = saturate(1 - length(uv) * 2);
                float3 p = float3(uv * 3.5, input.seed * 17);
                float n = Noise(p) * 0.5 + Noise(p * 2.1 + 3.3) * 0.3 + Noise(p * 4.7 + 7.1) * 0.2;
                float d = saturate(n * 1.6 - 0.45) * rim * rim;
                float near = saturate(distance(_WorldSpaceCameraPos, input.positionWS) / max(1, _NearFade));
                float3 c = lerp(_Color.rgb, _Color2.rgb, saturate(Noise(p * 0.7 + 11) * 1.4 - 0.3));
                return half4(c * d * _Density * near, 0);
            }
            ENDHLSL
        }
    }
}
