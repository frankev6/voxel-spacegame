// DEV-028 (exploration slice): a gas giant's ring, a flat annulus below the flight plane (object space: unit radius = the outer edge).
// Ringlets and gaps from the radius, lit on the day side, shadowed where the planet blocks the sun. Alpha blended, no depth write.
Shader "Game/Orbit/Ring"
{
    Properties
    {
        _Color ("Ring colour", Color) = (0.86, 0.80, 0.68, 1)
        _Inner ("Inner edge (fraction of outer)", Range(0, 1)) = 0.72
        _Opacity ("Opacity", Range(0, 1)) = 0.55
        _Seed ("Seed", Float) = 5.1
        _SunFallback ("Sun direction if no light", Vector) = (0.4, 0.6, 0.3, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-30" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _SunFallback; float _Inner, _Opacity, _Seed;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS); o.positionOS = input.positionOS; return o;
            }
            float Hash(float x) { return frac(sin(x * 127.1 + _Seed) * 43758.5453); }
            float Noise(float x) { float i = floor(x), f = frac(x); f = f * f * (3 - 2 * f); return lerp(Hash(i), Hash(i + 1), f); }
            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.positionOS.xz);
                if (r > 1 || r < _Inner) discard;
                float t = (r - _Inner) / (1 - _Inner);
                float ringlets = Noise(t * 60) * 0.6 + Noise(t * 220) * 0.4;
                float gap = smoothstep(0.02, 0.0, abs(t - 0.63)) + smoothstep(0.012, 0.0, abs(t - 0.31));
                float edge = smoothstep(0, 0.04, t) * smoothstep(1, 0.93, t);
                float a = saturate(ringlets * 1.3 - 0.15) * edge * (1 - gap) * _Opacity;
                Light sun = GetMainLight();
                float3 L = dot(sun.direction, sun.direction) > 0.5 ? normalize(sun.direction) : normalize(_SunFallback.xyz);
                float lit = 0.35 + 0.65 * saturate(abs(L.y) * 1.2);
                return half4(_Color.rgb * lit * sun.color, a);
            }
            ENDHLSL
        }
    }
}
