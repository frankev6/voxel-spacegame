Shader "Game/Vfx/FxShieldShell"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Instance { float4x4 EllipsoidToWorld; float4 Color; float HpFraction, Flash, Pad0, Pad1;
                float4 Ripple0, Ripple1, Ripple2, Ripple3, Ripple4, Ripple5, Ripple6, Ripple7; };
            StructuredBuffer<Instance> _Instances;
            float _RippleSeconds;
            struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; uint id : SV_InstanceID; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 surfacePoint : TEXCOORD0; float3 normal : TEXCOORD1;
                float3 view : TEXCOORD2; nointerpolation uint id : TEXCOORD3; };
            Varyings Vert(Attributes input)
            {
                Instance e = _Instances[input.id]; Varyings o;
                // Unity's built-in sphere has radius .5; normalize maps it onto the unit sphere contract.
                o.surfacePoint = normalize(input.positionOS); float3 world = mul(e.EllipsoidToWorld, float4(o.surfacePoint, 1)).xyz;
                float3x3 m = (float3x3)e.EllipsoidToWorld;
                float3 c0 = float3(m[0][0],m[1][0],m[2][0]), c1 = float3(m[0][1],m[1][1],m[2][1]), c2 = float3(m[0][2],m[1][2],m[2][2]);
                o.normal = normalize(cross(c1,c2)*o.surfacePoint.x + cross(c2,c0)*o.surfacePoint.y + cross(c0,c1)*o.surfacePoint.z);
                o.positionCS = TransformWorldToHClip(world); o.view = _WorldSpaceCameraPos - world; o.id = input.id; return o;
            }
            float Ring(float3 p, float4 r)
            {
                if (r.w < 0) return 0;
                float distance = acos(clamp(dot(normalize(p), r.xyz), -1.0, 1.0));
                return exp(-abs(distance-r.w*4.5)*32.0) * saturate(1.0-r.w/max(0.001,_RippleSeconds));
            }
            // QA-001 (owner: "shield needs to look better"): readable energy shell. A faint triplanar hex lattice and a
            // fresnel edge stay visible; impacts light up the lattice along their ripple. All scale with shield HP.
            float HexDistance(float2 p) { p = abs(p); return max(dot(p, float2(0.8660254, 0.5)), p.y); }
            float HexEdge(float2 uv)
            {
                const float2 r = float2(1.7320508, 1.0); const float2 h = r * 0.5;
                float2 a = uv - r * floor(uv / r) - h; float2 b = (uv - h) - r * floor((uv - h) / r) - h;
                return HexDistance(dot(a, a) < dot(b, b) ? a : b);    // 0 at a cell centre, 0.5 at its edge
            }
            float Lattice(float3 p, float3 n)
            {
                float3 w = pow(abs(n), 4.0); w /= max(1e-4, w.x + w.y + w.z);
                float3 q = p * 9.0;
                float e = HexEdge(q.yz) * w.x + HexEdge(q.xz) * w.y + HexEdge(q.xy) * w.z;
                return smoothstep(0.40, 0.49, e);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                Instance e = _Instances[input.id];
                float3 n = normalize(input.normal);
                float facing = saturate(dot(n, normalize(input.view)));
                float rim = pow(1.0 - facing, 2.5);
                float rings = Ring(input.surfacePoint,e.Ripple0)+Ring(input.surfacePoint,e.Ripple1)+Ring(input.surfacePoint,e.Ripple2)+Ring(input.surfacePoint,e.Ripple3)
                    +Ring(input.surfacePoint,e.Ripple4)+Ring(input.surfacePoint,e.Ripple5)+Ring(input.surfacePoint,e.Ripple6)+Ring(input.surfacePoint,e.Ripple7);
                float lines = Lattice(input.surfacePoint, input.surfacePoint);
                float shimmer = 0.75 + 0.25 * sin(_Time.y * 2.3 + input.surfacePoint.y * 6.0 + input.surfacePoint.x * 3.0);
                float alpha = rim * 0.22 + lines * (0.035 * shimmer + rim * 0.25 + rings * 1.4 + e.Flash * 0.5) + rings * 0.55 + e.Flash * (0.15 + rim * 0.8);
                float hp = saturate(e.HpFraction);
                return half4(e.Color.rgb * (1.0 + e.Flash * 1.5 + rings * 1.5 + lines * 0.5), saturate(alpha) * e.Color.a * (0.35 + 0.65 * hp) * step(0.001, hp));
            }
            ENDHLSL
        }
    }
}
