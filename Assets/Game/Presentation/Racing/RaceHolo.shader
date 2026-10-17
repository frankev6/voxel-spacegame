// RACE-016: the race track's light work, additive and HDR so bloom picks it up. One shader, one material per mode
// (Game/Content/Racing/mat_race_*.mat). Vertex colour tints (rgb) and scales (a); _Color is the HDR base.
// Every hologram is a dot matrix (owner: "pixel perfect pixelated ... easy to distinguish each pixel"): the surface is cut into
// _Pixel-metre cells (the track's voxel cell), each pattern is sampled once per cell, and each cell lights as a square dot with a
// dark gap. uv0 / uv1 per mode are written next to each mode below; Scale() turns them into metres.
Shader "Game/Race/Holo"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _Mode ("Mode", Float) = 0
        _Checker ("Checker", Float) = 0
        _Box ("Dust box (m)", Float) = 240
        _Pixel ("Pixel (m)", Float) = 1
        _Fill ("Dot fill", Range(0.3, 1)) = 0.72
        _Brightness ("Brightness", Float) = 0.8
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
                float4 _Color; float _Mode; float _Checker; float _Box; float _Pixel; float _Fill; float _Brightness;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; float2 extra : TEXCOORD2; };
            // extra.x: fade (glow blink and flicker, dust edges); extra.y: size in metres (halo radius, glow radius)

            Varyings Vert(Attributes input)
            {
                Varyings o; o.color = input.color; o.uv = input.uv; o.uv1 = input.uv1; o.extra = float2(1, 1);
                float3 world = TransformObjectToWorld(input.positionOS);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float scale = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                if (_Mode > 2.5 && _Mode < 3.5) o.extra.y = scale;                       // 3 halo: a unit disc scaled to its radius
                if (_Mode > 3.5 && _Mode < 4.5)          // 4 glow: a camera-facing sprite; uv1 = corner offset (m); uv2 = (kind, phase)
                {
                    world += (right * input.uv1.x + up * input.uv1.y) * scale;
                    o.extra.y = abs(input.uv1.x) * scale;
                    float t = _Time.y, phase = input.uv2.y;
                    if (input.uv2.x > 0.5 && input.uv2.x < 1.5) o.extra.x = 0.12 + 0.88 * smoothstep(0.35, 0.5, frac(t * 0.55 + phase)) * (1 - smoothstep(0.6, 0.75, frac(t * 0.55 + phase)));
                    else if (input.uv2.x > 1.5) o.extra.x = 0.7 + 0.3 * sin(t * 9 + phase * 40) * sin(t * 5.3 + phase * 17);
                }
                else if (_Mode > 8.5)                    // 9 dust: positionOS in [0,1)^3 wraps round the camera; uv1 = corner offset (m)
                {
                    float3 cam = _WorldSpaceCameraPos; float3 box = float3(_Box, 60, _Box);
                    float3 centre = float3(cam.x, 0, cam.z);
                    float3 p = (frac(input.positionOS - centre / box) - 0.5) * box;
                    float3 edge = abs(p) / (box * 0.5);
                    o.extra.x = saturate((1 - max(edge.x, edge.z)) * 4) * saturate((1 - edge.y) * 3);
                    world = centre + p + right * input.uv1.x + up * input.uv1.y;
                }
                o.positionCS = TransformWorldToHClip(world);
                return o;
            }

            float Line(float x, float width) { return saturate(1 - abs(x) / width); }

            /// Metres per uv unit for each mode.
            float2 Scale(float2 uv1, float size)
            {
                if (_Mode < 0.5) return uv1;                          // curtain: (width, height)
                if (_Mode < 1.5) return float2(1, uv1.y);             // fence: uv.x is metres; height
                if (_Mode < 2.5) return float2(uv1.y, 1);             // pad: width; uv.y is metres
                if (_Mode < 4.5) return float2(size, size);           // halo, glow: radius
                if (_Mode < 5.5) return float2(1, uv1.y);             // ribbon: uv.x is metres; width
                if (_Mode < 6.5) return max(uv1, 0.5);                // solid: the strip's size
                if (_Mode < 7.5) return float2(uv1.x, 1);             // surface: width; uv.y is metres
                return uv1;                                           // board: (width, height)
            }

            /// The pattern's brightness at uv (sampled at a pixel's centre). uv1 and size as in Scale().
            float Pattern(float2 uv, float2 uv1, float size, float t)
            {
                if (_Mode < 0.5)
                {
                    // 0 gate curtain: uv.x 0..1 across, uv.y 0..1 up; uv1 = (width, height). Bright at the pylons and along the flight
                    // plane, scanlines rising, a checker on the start/finish gate.
                    float w = uv1.x, h = uv1.y, x = uv.x * w, y = uv.y * h;
                    float edge = exp(-min(x, w - x) * 0.35);
                    float plane = exp(-abs(y - h * 0.3) * 0.6);
                    float scan = 0.5 + 0.5 * sin((y - t * 9) * 0.9);
                    float fade = smoothstep(0, 0.12, uv.y) * (1 - smoothstep(0.55, 1, uv.y));
                    float body = 0.05 + 0.12 * scan;
                    if (_Checker > 0.5) body *= 0.35 + 1.3 * step(0.5, frac(floor(x / 10) * 0.5 + floor(y / 10) * 0.5));
                    return edge * 0.8 + plane * 0.45 + body * fade;
                }
                if (_Mode < 1.5)
                {
                    // 1 field fence: uv.x metres along (signed so it grows the way the race runs), uv.y 0..1 up; uv1.y = height. A
                    // one-pixel diagonal lattice (8 m) that a slow pulse runs along; bright base and top rows.
                    float h = uv1.y, x = uv.x, y = uv.y * h;
                    float lattice = max(Line(frac((x + y) / 8) - 0.5, 0.07), Line(frac((x - y) / 8) - 0.5, 0.07));
                    float pulse = 0.35 + 0.65 * pow(0.5 + 0.5 * sin(x * 0.045 - t * 2.2), 4);
                    float base = exp(-uv.y * 9), top = exp(-(1 - uv.y) * 22) * 0.7;
                    return base * 0.9 + top + lattice * 0.30 * pulse * (1 - uv.y * 0.6) + 0.03;
                }
                if (_Mode < 2.5)
                {
                    // 2 boost pad: uv.x 0..1 across, uv.y metres along; uv1 = (length, width). Chevrons racing forward between rails.
                    float xc = abs(uv.x * 2 - 1), len = uv1.x, half = uv1.y * 0.5;
                    float c = frac((uv.y + xc * 22) / 30 - t * 1.8);   // tips lead: the edges trail the centre
                    float chevron = smoothstep(0, 0.06, c) * (1 - smoothstep(0.22, 0.34, c));
                    float fromEdge = (1 - xc) * half;
                    float rails = (fromEdge < 1.5 ? 1 : 0) + exp(-fromEdge * 0.25) * 0.25;
                    float ends = smoothstep(0, 25, uv.y) * smoothstep(0, 25, len - uv.y);
                    return (chevron * (0.75 - xc * 0.35) + rails + 0.05) * ends;
                }
                if (_Mode < 3.5)
                {
                    // 3 halo: uv −1..1 over the disc; size = radius. A dashed one-pixel rim turning slowly over a breathing fill.
                    float r = length(uv) * size; if (r > size) return 0;
                    float angle = atan2(uv.y, uv.x) / 6.2831853;
                    float dash = step(0.45, frac(angle * 28 + t * 0.25));
                    float rim = Line(r - (size - 0.8), 0.9) * (0.45 + 0.55 * dash);
                    float inner = Line(r - size * 0.55, 0.7) * 0.3;
                    return rim + inner + (1 - r / size) * 0.10;
                }
                if (_Mode < 4.5)
                {
                    // 4 glow: uv −1..1. A hot core and a soft halo.
                    float r2 = dot(uv, uv); if (r2 > 1) return 0;
                    return exp(-r2 * 9) * 1.3 + exp(-r2 * 3) * 0.35 * (1 - r2);
                }
                if (_Mode < 5.5)
                {
                    // 5 ribbon on the flight plane: uv.x metres along, uv.y 0..1 across; uv1.y = width. Wall footprint: a bright core,
                    // travelling dashes.
                    float yc = abs(uv.y * 2 - 1);
                    float dash = step(0.55, frac(uv.x / 16 - t * 0.6));
                    return exp(-yc * 5) * (0.55 + 0.25 * dash) + exp(-yc * 2) * 0.08;
                }
                if (_Mode < 6.5) return 1;   // 6 solid light: bands, strips, frames
                if (_Mode < 7.5)
                {
                    // 7 track surface: uv.x 0..1 across, uv.y metres along; uv1.x = width. Edge lines, a dashed centre line and a
                    // faint cross row every 40 m.
                    float w = uv1.x, x = uv.x * w, xc = abs(uv.x * 2 - 1);
                    float edges = Line(x - 7, 1.2) + Line(w - 7 - x, 1.2);
                    float centre = Line(x - w * 0.5, 1.2) * step(0.5, frac(uv.y / 24));
                    float cross = Line(frac(uv.y / 40) * 40 - 20, 0.8) * 0.18;
                    return edges * 0.45 + centre * 0.3 + cross + 0.015 * (1 - xc * 0.5);
                }
                // 8 corner board: uv 0..1; uv1 = (width, height). Three chevrons sweeping forward in a frame.
                float2 m = uv * uv1;
                float frame = max(max(Line(m.x - 0.5, 0.9), Line(uv1.x - 0.5 - m.x, 0.9)), max(Line(m.y - 0.5, 0.9), Line(uv1.y - 0.5 - m.y, 0.9)));
                float c = frac((uv.x * 3 + abs(uv.y - 0.5) * 1.6) - t * 1.2);
                float chevron = smoothstep(0, 0.08, c) * (1 - smoothstep(0.32, 0.42, c));
                return frame * 0.9 + chevron * 0.8 + 0.05;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y; float a;
                float4 c = _Color * input.color;
                if (_Mode > 8.5)
                {
                    // 9 dust: one square mote
                    float2 q = abs(input.uv);
                    a = (max(q.x, q.y) < 0.6 ? 1 : 0) * input.extra.x;
                    return half4(c.rgb * _Brightness, saturate(a * c.a));
                }
                float2 scale = Scale(input.uv1, input.extra.y);
                float2 m = input.uv * scale / _Pixel;                 // in pixels
                float2 cell = floor(m);
                float2 centre = (cell + 0.5) / scale * _Pixel;        // the pixel centre, back in uv
                float2 f = abs(frac(m) - 0.5), w = max(fwidth(m), 1e-4);
                float2 dot2 = saturate((_Fill * 0.5 - f) / w + 0.5);
                float mask = dot2.x * dot2.y;
                mask = lerp(mask, _Fill * _Fill, saturate(max(w.x, w.y) * 2 - 0.6));   // far away: an even glow, no moiré
                a = Pattern(centre, input.uv1, input.extra.y, t) * mask;
                if (_Mode > 3.5 && _Mode < 4.5) a *= input.extra.x;
                return half4(c.rgb * _Brightness, saturate(a * c.a));
            }
            ENDHLSL
        }
    }
}
