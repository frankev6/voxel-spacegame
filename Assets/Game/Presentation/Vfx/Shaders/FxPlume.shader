Shader "Game/Vfx/FxPlume"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Instance { float3 Nozzle; float Radius; float3 Dir; float Length; float4 Color; };
            StructuredBuffer<Instance> _Instances;
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; uint id : SV_InstanceID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Instance e = _Instances[input.id]; float3 a = e.Nozzle, b = a + e.Dir * e.Length; float width = e.Radius * 2.0 * (1.0 - input.uv.y * 0.85); float4 color = e.Color;
                float3 delta = b - a;
                float3 direction = delta / max(length(delta), 0.0001);
                float3 side = cross(direction, _WorldSpaceCameraPos - (a + b) * 0.5);
                side = dot(side, side) > 0.000001 ? normalize(side) : float3(1,0,0);
                Varyings o; o.positionCS = TransformWorldToHClip(lerp(a,b,input.uv.y) + side * (input.uv.x - 0.5) * width);
                o.uv = input.uv; o.color = color; return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float core = pow(saturate(1.0 - abs(input.uv.x * 2.0 - 1.0)), 1.5);
                float fade = smoothstep(0.0, 0.03, input.uv.y) * (1.0 - smoothstep(0.65, 1.0, input.uv.y));
                return half4(input.color.rgb * (1.0 + core), input.color.a * core * fade);
            }
            ENDHLSL
        }
    }
}
