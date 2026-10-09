Shader "Unlit/WeaponArcDashedLines"
{
    Properties
    {
        [MainColor] _Color ("Color", Color) = (1,1,1,1)
        _Angle ("Angle Degrees", Float) = 45
        
        // New Properties for Dashes
        _DashLength ("Dash Length", Range(0.01, 0.5)) = 0.05
        _GapLength ("Gap Length", Range(0.0, 0.5)) = 0.05
        _Thickness ("Line Thickness", Range(0.001, 0.1)) = 0.01
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

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
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Angle;
                float _DashLength;
                float _GapLength;
                float _Thickness;
            CBUFFER_END

            #ifdef DOTS_INSTANCING_ON
                UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float4, _Color)
                    UNITY_DOTS_INSTANCED_PROP(float, _Angle)
                    UNITY_DOTS_INSTANCED_PROP(float, _DashLength)
                    UNITY_DOTS_INSTANCED_PROP(float, _GapLength)
                    UNITY_DOTS_INSTANCED_PROP(float, _Thickness)
                UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

                #define _Color UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _Color)
                #define _Angle UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Angle)
                #define _DashLength UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _DashLength)
                #define _GapLength UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _GapLength)
                #define _Thickness UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Thickness)
            #endif

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // Adjust pivot to bottom center so (0,0,0) is at the feet
                float3 positionOS = input.positionOS.xyz;
                positionOS.y += 0.5; 

                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 color = _Color;
                float angleDeg = _Angle;
                
                // Ensure values are safe to prevent division errors
                float dashLen = max(0.001, _DashLength);
                float gapLen = max(0.0, _GapLength);
                float thickness = _Thickness;
                float period = dashLen + gapLen;

                // --- Coordinate Setup ---
                float2 coord = float2(input.uv.x - 0.5, input.uv.y); 
                float dist = length(coord); // Distance from origin (0 to ~1.0)
                float angleRad = atan2(coord.x, coord.y); 
                float halfAngleRad = radians(angleDeg * 0.5);

                // --- Line Logic ---

                // Determine which side (Left or Right) this pixel is closest to
                float sideSign = (angleRad >= 0) ? 1.0 : -1.0; 
                float targetAngle = sideSign * halfAngleRad;

                // 1. Calculate Perpendicular Distance (Width)
                // How far is this pixel from the ideal line?
                float perpDist = abs(dist * sin(angleRad - targetAngle));
                
                // Anti-aliased width check
                // If perpDist < thickness/2, we are inside the line
                float halfThickness = thickness * 0.5;
                float edgeFuzz = fwidth(perpDist); // AA factor
                float widthAlpha = 1.0 - smoothstep(halfThickness - edgeFuzz, halfThickness, perpDist);

                // 2. Calculate Dash Pattern (Length)
                // Where are we in the repeating pattern?
                float distInCycle = fmod(dist, period);
                
                // Anti-aliased dash check
                // We want alpha 1.0 if distInCycle < dashLen, else 0.0
                float dashFuzz = fwidth(dist);
                // Soft start of dash (0.0)
                float dashStart = smoothstep(0.0, dashFuzz, distInCycle);
                // Soft end of dash (dashLen)
                float dashEnd = 1.0 - smoothstep(dashLen - dashFuzz, dashLen, distInCycle);
                
                float dashAlpha = dashStart * dashEnd;

                // 3. Distance Fade (Attentuation)
                // Fade from opaque at origin (0.0) to transparent at tip (1.0)
                float fadeAlpha = saturate(1.0 - dist);

                // Combine everything
                float finalAlpha = widthAlpha * dashAlpha * fadeAlpha;

                // Optimization: Discard transparent pixels
                if (finalAlpha <= 0.001)
                    discard;

                return half4(color.rgb, color.a * finalAlpha);
            }
            ENDHLSL
        }
    }
}