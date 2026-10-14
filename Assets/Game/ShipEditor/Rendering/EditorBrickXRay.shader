// SPEC-51 EDT-018: packed brick geometry, Nocturne editor tint supplied by the material.
Shader "Game/ShipEditor/BrickXRay"
{
    Properties { _Tint("Editor tint", Color) = (1,1,1,1) _Opacity("Opacity", Range(0,1)) = 0.2 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "DraftXRay"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest Always Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Game/Voxel/Rendering/Shaders/BrickVertexDecode.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Opacity;
            CBUFFER_END
            struct Attributes { float4 position:POSITION; float4 attributes:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                BrickVertex vertex = DecodeBrickVertex(input.position,input.attributes);
                Varyings output;
                output.positionCS = TransformObjectToHClip(vertex.positionCells);
                output.normalWS = TransformObjectToWorldNormal(kBrickNormals[vertex.normalIndex]);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half shade = .55h + .45h * abs(normalize(input.normalWS).y);
                return half4(_Tint.rgb * shade, _Tint.a * _Opacity);
            }
            ENDHLSL
        }
    }
}
