// ADR-0122 cruise (owner, 2026-10-21: "a shader effect to make it feel like you're going really really fast"): a dash of dust
// running past the camera, an instanced quad stretched along the ship's velocity (object space: x across, z along). Soft at the sides,
// tapered at both ends, faded near the lens and far off. Additive, no depth write; CruiseEffects draws and scales it.
Shader "Game/Vfx/CruiseStreak"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1.1, 1.3, 1.7, 1)
        _Intensity ("Intensity (set per frame)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Intensity;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fade : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 world = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(world);
                o.uv = input.positionOS.xz * 2;   // -1..1 across, -1..1 along
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                float d = distance(origin, _WorldSpaceCameraPos);
                float flicker = 0.45 + 0.55 * frac(sin(dot(floor(origin), float3(12.9898, 78.233, 37.719))) * 43758.5453);
                o.fade = smoothstep(6, 18, d) * (1 - smoothstep(90, 170, d)) * flicker;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float across = 1 - abs(input.uv.x), along = 1 - abs(input.uv.y);
                float a = across * across * smoothstep(0, 0.6, along) * input.fade * _Intensity;
                return half4(_Color.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}
