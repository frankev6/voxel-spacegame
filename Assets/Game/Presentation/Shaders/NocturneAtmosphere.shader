Shader "Game/NocturneAtmosphere"
{
    Properties
    {
        _AtmosphereColor ("Pale sand atmosphere", Color) = (0.79, 0.73, 0.54, 1)
        _SurfaceRatio ("Surface to atmosphere radius", Float) = 0.970874
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _AtmosphereColor;
            float _SurfaceRatio;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Mesh has radius one. Ray impact parameter yields a soft outer edge rather
                // than a solid Fresnel outline. Body depth occludes the far hemisphere.
                float3 view = normalize(TransformWorldToObject(GetCameraPositionWS()) - input.positionOS);
                float impact = length(cross(input.positionOS, view));
                float h = saturate((impact - _SurfaceRatio) / (1 - _SurfaceRatio));
                float outer = (1-h)*(1-h);
                float inner = .25 * pow(saturate(impact / _SurfaceRatio), 14);
                float density = impact >= _SurfaceRatio ? outer : inner;
                float3 limb = normalize(input.positionOS - view * dot(input.positionOS, view));
                float light = smoothstep(-.28, .3, dot(limb, normalize(float3(-.66, .64, -.38))));
                return half4(_AtmosphereColor.rgb * density * light * 1.4, 1);
            }
            ENDHLSL
        }
    }
}
