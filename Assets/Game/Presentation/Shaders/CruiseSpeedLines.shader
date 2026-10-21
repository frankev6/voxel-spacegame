// ADR-0122 cruise (owner, 2026-10-21: "lets make it feel like you're actually going really fast"): speed lines rushing out from the
// middle of the view toward its edges, a quad over the whole view just past the near clip. Polar coordinates round the screen centre:
// each of many thin angular lanes carries a dash that races outward and repeats, faster the faster the ship goes. The centre stays
// clear for the ship; the lines thicken and brighten toward the edges. Additive, drawn over everything (bloom catches it).
Shader "Game/Vfx/CruiseSpeedLines"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1.4, 1.6, 2.0, 1)
        _Intensity ("Intensity (set per frame)", Range(0, 2)) = 0
        _Rate ("Outward rate (set per frame)", Float) = 2
        _Aspect ("Aspect (set per frame)", Float) = 1.78
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Intensity, _Rate, _Aspect;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.positionOS.xy * 2;   // -1..1 over the view
                return o;
            }
            float Hash(float x) { return frac(sin(x * 91.3458) * 47453.5453); }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = float2(input.uv.x * _Aspect, input.uv.y);
                float r = length(p) / _Aspect;            // 0 at the centre, 1 at the side edges
                float a = atan2(p.y, p.x) / (2 * PI) + 0.5;
                const float Lanes = 220;
                float lane = floor(a * Lanes), across = frac(a * Lanes);
                float h = Hash(lane), h2 = Hash(lane + 17.1), h3 = Hash(lane + 41.7);
                if (h < 0.42) return 0;
                // a dash racing outward: its head at `head`, its tail trailing toward the centre
                float head = frac(h2 + _Time.y * _Rate * (0.55 + h3)) * 1.9 + 0.1;
                float length = 0.18 + 0.35 * h3;
                float along = saturate(1 - (head - r) / length) * step(r, head);
                float width = 0.06 + 0.22 * saturate(r - 0.3);
                float thin = smoothstep(width, 0, abs(across - 0.5));
                float mask = smoothstep(0.32, 0.85, r);
                float a1 = along * along * thin * mask * (0.35 + 0.65 * h) * _Intensity;
                return half4(_Color.rgb * a1, 0);
            }
            ENDHLSL
        }
    }
}
