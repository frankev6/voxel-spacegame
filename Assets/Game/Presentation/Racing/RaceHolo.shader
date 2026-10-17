// RACE-016: the race track's light work, additive and HDR so bloom picks it up. One shader, one material per mode
// (Game/Content/Racing/mat_race_*.mat). Vertex colour tints (rgb) and scales (a); _Color is the HDR base.
// uv0 / uv1 meaning per mode is written next to each mode below and in TrackDressing.cs.
Shader "Game/Race/Holo"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _Mode ("Mode", Float) = 0
        _Checker ("Checker", Float) = 0
        _Box ("Dust box (m)", Float) = 240
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Mode; float _Checker; float _Box;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; float fade : TEXCOORD2; };

            Varyings Vert(Attributes input)
            {
                Varyings o; o.color = input.color; o.uv = input.uv; o.uv1 = input.uv1; o.fade = 1;
                float3 world = TransformObjectToWorld(input.positionOS);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                if (_Mode > 3.5 && _Mode < 4.5)          // 4 glow: a camera-facing sprite; uv1 = corner offset (m); uv2 = (kind, phase)
                {
                    float scale = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                    world += (right * input.uv1.x + up * input.uv1.y) * scale;
                    float t = _Time.y, phase = input.uv2.y;
                    if (input.uv2.x > 0.5 && input.uv2.x < 1.5) o.fade = 0.12 + 0.88 * smoothstep(0.35, 0.5, frac(t * 0.55 + phase)) * (1 - smoothstep(0.6, 0.75, frac(t * 0.55 + phase)));
                    else if (input.uv2.x > 1.5) o.fade = 0.7 + 0.3 * sin(t * 9 + phase * 40) * sin(t * 5.3 + phase * 17);
                }
                else if (_Mode > 8.5)                    // 9 dust: positionOS in [0,1)^3 wraps round the camera; uv1 = corner offset (m)
                {
                    float3 cam = _WorldSpaceCameraPos; float3 box = float3(_Box, 60, _Box);
                    float3 centre = float3(cam.x, 0, cam.z);
                    float3 p = (frac(input.positionOS - centre / box) - 0.5) * box;
                    float3 edge = abs(p) / (box * 0.5);
                    o.fade = saturate((1 - max(edge.x, edge.z)) * 4) * saturate((1 - edge.y) * 3);
                    world = centre + p + right * input.uv1.x + up * input.uv1.y;
                }
                o.positionCS = TransformWorldToHClip(world);
                return o;
            }

            float Line(float x, float width) { return saturate(1 - abs(x) / width); }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y; float2 uv = input.uv; float a = 0; float3 tint = 1;
                if (_Mode < 0.5)
                {
                    // 0 gate curtain: uv.x 0..1 across, uv.y 0..1 up; uv1 = (width, height) in metres. Bright at the pylons and along
                    // the flight plane line, scanlines rising, a checker on the start/finish gate.
                    float w = input.uv1.x, h = input.uv1.y, x = uv.x * w, y = uv.y * h;
                    float edge = exp(-min(x, w - x) * 0.35);
                    float plane = exp(-abs(y - h * 0.3) * 0.6);
                    float scan = 0.5 + 0.5 * sin((y - t * 9) * 0.9);
                    float fade = smoothstep(0, 0.12, uv.y) * (1 - smoothstep(0.55, 1, uv.y));
                    float body = 0.05 + 0.12 * scan;
                    if (_Checker > 0.5) body *= 0.35 + 1.3 * step(0.5, frac(floor(x / 10) * 0.5 + floor(y / 10) * 0.5));
                    a = edge * 0.8 + plane * 0.45 + body * fade;
                }
                else if (_Mode < 1.5)
                {
                    // 1 field fence: uv.x metres along (signed so it grows the way the race runs), uv.y 0..1 up; uv1.y = height (m). A thin lattice that a slow pulse runs along.
                    float h = input.uv1.y, x = uv.x, y = uv.y * h;
                    float d1 = frac((x + y) / 7), d2 = frac((x - y) / 7);
                    float lattice = max(Line(d1 - 0.5, 0.06), Line(d2 - 0.5, 0.06));
                    float pulse = 0.35 + 0.65 * pow(0.5 + 0.5 * sin(x * 0.045 - t * 2.2), 4);
                    float base = exp(-uv.y * 9), top = exp(-(1 - uv.y) * 22) * 0.7;
                    a = base * 0.9 + top + lattice * 0.22 * pulse * (1 - uv.y * 0.6) + 0.025;
                }
                else if (_Mode < 2.5)
                {
                    // 2 boost pad: uv.x 0..1 across, uv.y metres along; uv1.x = length (m). Chevrons racing forward between rails.
                    float xc = abs(uv.x * 2 - 1), len = input.uv1.x;
                    float c = frac((uv.y + xc * 22) / 30 - t * 1.8);   // tips lead: the edges trail the centre
                    float chevron = smoothstep(0, 0.06, c) * (1 - smoothstep(0.22, 0.34, c));
                    float rails = exp(-(1 - xc) * 40) + exp(-(1 - xc) * 8) * 0.25;
                    float ends = smoothstep(0, 25, uv.y) * smoothstep(0, 25, len - uv.y);
                    float grid = Line(frac(uv.y / 6) - 0.5, 0.04) * 0.15;
                    a = (chevron * (0.75 - xc * 0.35) + rails + grid + 0.05) * ends;
                }
                else if (_Mode < 3.5)
                {
                    // 3 halo: uv −1..1 over the disc. A dashed rim turning slowly over a breathing fill (mines, grid slots' pads).
                    float r = length(uv); if (r > 1.02) discard;
                    float angle = atan2(uv.y, uv.x) / 6.2831853;
                    float dash = step(0.45, frac(angle * 28 + t * 0.25));
                    float rim = exp(-abs(r - 0.96) * 45) * (0.45 + 0.55 * dash);
                    float inner = exp(-abs(r - 0.55) * 60) * 0.25;
                    float fill = (1 - r) * 0.10;
                    a = rim + inner + fill;
                }
                else if (_Mode < 4.5)
                {
                    // 4 glow: uv −1..1. A hot core and a wide soft halo.
                    float r2 = dot(uv, uv); if (r2 > 1) discard;
                    a = (exp(-r2 * 18) * 1.4 + exp(-r2 * 4) * 0.35 * (1 - r2)) * input.fade;
                }
                else if (_Mode < 5.5)
                {
                    // 5 ribbon on the flight plane: uv.x metres along, uv.y 0..1 across. Wall footprint: a bright core, travelling dashes.
                    float yc = abs(uv.y * 2 - 1);
                    float dash = step(0.55, frac(uv.x / 16 - t * 0.6));
                    a = exp(-yc * 7) * (0.55 + 0.25 * dash) + exp(-yc * 2) * 0.08;
                }
                else if (_Mode < 6.5)
                {
                    // 6 solid light: uv.y 0..1 across a strip, soft at its edges.
                    a = smoothstep(0, 0.3, uv.y) * smoothstep(0, 0.3, 1 - uv.y);
                }
                else if (_Mode < 7.5)
                {
                    // 7 track surface: uv.x 0..1 across, uv.y metres along; uv1.x = width (m). Edge lines, a dashed centre line and
                    // faint cross lines every 40 m.
                    float w = input.uv1.x, x = uv.x * w, xc = abs(uv.x * 2 - 1);
                    float edges = exp(-abs(x - 7) * 1.6) + exp(-abs(w - 7 - x) * 1.6);
                    float centre = exp(-abs(x - w * 0.5) * 2.0) * step(0.5, frac(uv.y / 24));
                    float cross = Line(frac(uv.y / 40) - 0.5, 0.004) * 0.18;
                    float sheen = 0.03 * (1 - xc * 0.5);
                    a = edges * 0.45 + centre * 0.25 + cross + sheen;
                }
                else if (_Mode < 8.5)
                {
                    // 8 corner board: uv 0..1; uv1.x = +1 or −1, the way the corner turns. Three chevrons sweeping that way in a frame.
                    float2 p = uv; if (input.uv1.x < 0) p.x = 1 - p.x;
                    float frame = max(max(Line(p.x - 0.02, 0.025), Line(p.x - 0.98, 0.025)), max(Line(p.y - 0.04, 0.05), Line(p.y - 0.96, 0.05)));
                    float c = frac((p.x * 3 + abs(p.y - 0.5) * 1.6) - t * 1.2);
                    float chevron = smoothstep(0, 0.08, c) * (1 - smoothstep(0.32, 0.42, c));
                    a = frame * 0.9 + chevron * 0.8 + 0.06;
                }
                else
                {
                    // 9 dust: uv −1..1, a soft mote.
                    float r2 = dot(uv, uv); if (r2 > 1) discard;
                    a = exp(-r2 * 6) * input.fade;
                }
                float4 c = _Color * input.color;
                return half4(c.rgb * tint, saturate(a * c.a));
            }
            ENDHLSL
        }
    }
}
