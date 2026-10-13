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
            half4 Frag(Varyings input) : SV_Target
            {
                Instance e = _Instances[input.id];
                float rim = pow(1.0-saturate(dot(normalize(input.normal), normalize(input.view))), 3.0);
                float rings = Ring(input.surfacePoint,e.Ripple0)+Ring(input.surfacePoint,e.Ripple1)+Ring(input.surfacePoint,e.Ripple2)+Ring(input.surfacePoint,e.Ripple3)
                    +Ring(input.surfacePoint,e.Ripple4)+Ring(input.surfacePoint,e.Ripple5)+Ring(input.surfacePoint,e.Ripple6)+Ring(input.surfacePoint,e.Ripple7);
                float alpha = saturate(rim*.06 + e.Flash*rim*.8 + rings) * e.Color.a;
                return half4(e.Color.rgb * (1.0+e.Flash+rings), alpha * saturate(e.HpFraction));
            }
            ENDHLSL
        }
    }
}
