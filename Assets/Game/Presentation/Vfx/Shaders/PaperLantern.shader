// EXT-014 Tollhouse Nine's paper lanterns (owner, 2026-11-10: "make the inside of the station feel truly spectacle in terms of
// atmosphere, lighting, it should feel iconic"). An instanced, self-lit paper lantern on a lathe mesh (object space: y up, -0.5..0.5,
// radius <= 0.5): glowing paper between bamboo ribs and pleats, brightest where the flame sits behind it, lacquered collars top and
// bottom. Each lantern's hue and flicker come from its yaw (fixed per lantern), so nothing per instance is uploaded. A lantern below
// _DarkBelow (render-space y) is out: the blackout climbs the Well as a line.
Shader "Game/Vfx/PaperLantern"
{
    Properties
    {
        [HDR] _Paper ("Paper glow", Color) = (3.6, 1.35, 0.38, 1)
        [HDR] _Flame ("Flame behind the paper", Color) = (7.0, 3.6, 1.3, 1)
        [HDR] _Rose ("Rose paper", Color) = (3.4, 0.55, 0.42, 1)
        [HDR] _Jade ("Jade paper", Color) = (0.55, 2.6, 1.6, 1)
        _Cap ("Lacquer collars", Color) = (0.11, 0.025, 0.018, 1)
        _Ribs ("Ribs", Float) = 12
        _Lit ("Lit (set per frame)", Range(0, 1)) = 1
        _DarkBelow ("Out below this render-space y (set per frame)", Float) = -100000
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Paper, _Flame, _Rose, _Jade, _Cap; float _Ribs, _Lit, _DarkBelow;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 posOS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewWS : TEXCOORD2;
                float2 seedLit : TEXCOORD3;
            };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 world = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(world);
                o.posOS = input.positionOS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewWS = _WorldSpaceCameraPos - world;
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                float3 ax = TransformObjectToWorldDir(float3(1, 0, 0));
                float seed = frac(atan2(ax.z, ax.x) * 0.15915494 + 0.5);
                // the blackout's edge is ragged: each lantern goes out at its own height around the line (±10 m), one in twelve hangs on
                // 36 m longer, and in the eight metres above its own line a lantern sputters before it dies (owner, 2026-11-10: "a
                // gradient where not all lights turn off at the same time")
                float own = (frac(seed * 13.7) - 0.5) * 20 + (frac(seed * 29.3) < 0.085 ? 36 : 0);
                float edge = origin.y - (_DarkBelow + own);
                float sputter = frac(sin(floor(_Time.y * 13 + seed * 50) * 12.9898) * 43758.5453);
                float lit = edge <= 0 ? 0 : edge < 8 ? (sputter > (1 - edge / 8) * 0.9 ? 1 : 0.06) : 1;
                o.seedLit = float2(seed, _Lit * lit);
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float seed = input.seedLit.x, lit = input.seedLit.y;
                float y = input.posOS.y;
                float ang = atan2(input.posOS.z, input.posOS.x) * 0.15915494;
                float rib = abs(frac(ang * _Ribs) - 0.5) * 2;               // 0 mid panel, 1 on a rib
                float ribDark = smoothstep(0.8, 0.96, rib);
                float pleat = 0.86 + 0.14 * cos(y * 6.2831853 * 9);
                float facing = saturate(dot(normalize(input.normalWS), normalize(input.viewWS)));
                float hot = facing * facing * facing * saturate(1 - abs(y) * 2.4);   // the flame showing through
                float pick = frac(seed * 7.31);
                float3 paper = pick < 0.68 ? _Paper.rgb : pick < 0.9 ? _Rose.rgb : _Jade.rgb;
                float t = _Time.y;
                float flicker = 0.84 + 0.16 * sin(t * (6.0 + seed * 5.0) + seed * 40.0) * sin(t * (2.1 + seed * 2.7) + seed * 9.0);
                float3 glow = (paper * (0.55 + 0.45 * facing) + _Flame.rgb * hot) * pleat * (1 - ribDark * 0.8) * flicker;
                float3 colour = glow * lit + paper * 0.16 * (0.45 + 0.55 * facing) * (1 - lit);   // out: dim paper, a last ember
                float collar = smoothstep(0.405, 0.425, abs(y));
                colour = lerp(colour, _Cap.rgb * (0.55 + 0.45 * facing) + paper * 0.05 * lit, collar);
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }
}
