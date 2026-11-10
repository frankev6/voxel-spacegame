// EXT-014 a shaft of light in dusty air (owner, 2026-11-10: "make the inside of the station feel truly spectacle in terms of
// atmosphere, lighting, it should feel iconic"): additive on a lathe mesh (a cone for a searchlight or the Lantern shining up the
// Well, a ball for the core's halo). Soft at the silhouette (view against normal), faded at the source and the far end (uv.y 0..1),
// stirred by slow dust, faded near the lens so flying through a beam does not white out the view. No depth write.
Shader "Game/Vfx/LightShaft"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1.0, 0.9, 0.7, 1)
        _Intensity ("Intensity (set per frame)", Range(0, 4)) = 1
        _Dust ("Dust", Range(0, 1)) = 0.55
        _DustScale ("Dust scale (1/m)", Float) = 0.06
        _NearFade ("Near fade (m)", Float) = 14
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
                float4 _Color; float _Intensity, _Dust, _DustScale, _NearFade;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float3 normalWS : TEXCOORD1; float along : TEXCOORD2; float3 local : TEXCOORD3; };
            float Hash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float Noise(float3 x)
            {
                float3 i = floor(x), f = frac(x); f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }
            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.world = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(o.world);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.along = input.uv.y;
                o.local = o.world - TransformObjectToWorld(float3(0, 0, 0));   // dust stays with the shaft through origin shifts
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 toEye = _WorldSpaceCameraPos - input.world;
                float dist = length(toEye);
                float edge = abs(dot(normalize(input.normalWS), toEye / max(dist, 1e-3)));
                edge = edge * edge;
                float fade = smoothstep(0.0, 0.07, input.along) * pow(saturate(1 - input.along), 1.4);
                float3 p = input.local * _DustScale;
                float t = _Time.y;
                float dust = Noise(p + float3(0, -t * 0.09, t * 0.03)) * 0.65 + Noise(p * 2.7 + float3(t * 0.05, -t * 0.21, 0)) * 0.35;
                float near = smoothstep(2.0, _NearFade, dist);
                float a = edge * fade * near * _Intensity * lerp(1, dust * 1.8, _Dust);
                return half4(_Color.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}
