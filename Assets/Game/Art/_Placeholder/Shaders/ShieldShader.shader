Shader "Custom/ShieldShader"
{
    Properties
    {
        _MainTex ("Shield Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0, 0.5, 1, 0.3)
        _RimColor ("Rim Color", Color) = (0, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        _FlashAmount ("Flash Amount", Float) = 0
        _ShieldAlpha ("Shield Alpha", Float) = 1
        _FlashIntensity ("Flash Intensity Multiplier", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off  // Double-sided rendering

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // All per-material properties MUST be inside this cbuffer for SRP Batcher compatibility
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _RimColor;
                float _RimPower;
                float _FlashAmount;
                float _ShieldAlpha;
                float _FlashIntensity;
            CBUFFER_END

            // DOTS Instancing for Entities Graphics - only per-instance properties
            #ifdef DOTS_INSTANCING_ON
                UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float, _FlashAmount)
                    UNITY_DOTS_INSTANCED_PROP(float, _ShieldAlpha)
                UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

                #define _FlashAmount UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _FlashAmount)
                #define _ShieldAlpha UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ShieldAlpha)
            #endif

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return output;
            }

            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Early out if shield is invisible
                if (_ShieldAlpha <= 0)
                    discard;

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                // Flip normal for back faces
                float3 normal = normalize(input.normalWS) * (facing > 0 ? 1 : -1);
                float NdotV = max(0, dot(normal, viewDir));
                
                // Rim Effect
                float rim = pow(1.0 - NdotV, _RimPower);
                half4 rimColor = _RimColor * rim;

                // Flash Effect - direct from component value (set by system, decays over time)
                float flash = saturate(_FlashAmount) * _FlashIntensity;

                // Combine
                half4 baseColor = _Color * texColor;
                half4 finalColor = baseColor + rimColor;
                
                // Add flash to color (additive bright effect)
                finalColor.rgb += _RimColor.rgb * flash;
                finalColor.a = (baseColor.a + rim + flash * 0.3) * _ShieldAlpha;
                
                return saturate(finalColor);
            }
            ENDHLSL
        }
    }
}
