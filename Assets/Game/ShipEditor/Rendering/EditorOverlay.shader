Shader "Game/EditorOverlay"
{
    Properties { _FillAlpha("Fill alpha", Range(0,1)) = 0.12 _EdgeWidth("Edge pixels", Range(0.5,4)) = 1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Instance { float3 Min; float3 Size; uint ColorRGBA8; uint Flags; };
            StructuredBuffer<Instance> _Instances;
            float4x4 _GridToWorld, _MeshRotation;
            float _CellSize, _FillAlpha, _EdgeWidth;
            struct Input { float3 positionOS : POSITION; float2 uv : TEXCOORD0; uint instanceID : SV_InstanceID; };
            struct Output { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; nointerpolation uint flags : TEXCOORD1; };
            Output Vert(Input input)
            {
                Instance data = _Instances[input.instanceID]; Output o;
                float3 grid = (data.Min + mul((float3x3)_MeshRotation, input.positionOS) * data.Size) * _CellSize;
                o.positionCS = TransformWorldToHClip(mul(_GridToWorld, float4(grid,1)).xyz);
                o.uv = input.uv; o.flags = data.Flags;
                o.color = float4(data.ColorRGBA8 & 255, (data.ColorRGBA8 >> 8) & 255, (data.ColorRGBA8 >> 16) & 255, data.ColorRGBA8 >> 24) / 255.0;
                return o;
            }
            half4 Frag(Output input) : SV_Target
            {
                float2 distanceToEdge = min(input.uv, 1-input.uv) / max(fwidth(input.uv), float2(0.00001,0.00001));
                float edge = 1-smoothstep(_EdgeWidth, _EdgeWidth+1, min(distanceToEdge.x,distanceToEdge.y));
                float fill = (input.flags & 1) != 0 ? 0 : _FillAlpha;
                return half4(input.color.rgb, input.color.a * lerp(fill,1,edge));
            }
            ENDHLSL
        }
    }
}
