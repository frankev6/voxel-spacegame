// Feel prototype (2026-10-21): the glow of a below-plane planet's atmosphere, an additive shell just outside the surface cap. It
// brightens towards the limb and on the day side and warms at the terminator, so the horizon under the flight plane reads as a
// curve of light that thins into space.
Shader "Game/Orbit/Atmosphere"
{
    Properties
    {
        [HDR] _Glow ("Glow", Color) = (0.45, 0.80, 1.9, 1)
        [HDR] _Dusk ("Dusk", Color) = (1.6, 0.55, 0.20, 1)
        _Radius ("Planet radius (m)", Float) = 80000
        _ScaleHeight ("Scale height (m)", Float) = 420
        _SunFallback ("Sun direction if no light", Vector) = (0.4, 0.6, 0.3, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Glow, _Dusk, _SunFallback; float _Radius, _ScaleHeight;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldDir(normalize(input.positionOS));
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // The glow follows the air the view ray passes through: brightest where it grazes the surface, fading with the height of
                // its closest approach above it (scale height), and fading again as the ray looks down through the thin air onto the
                // ground. No hard edge where the shell itself ends.
                float3 centre = TransformObjectToWorld(float3(0, 0, 0));
                float3 eye = _WorldSpaceCameraPos - centre, ray = normalize(input.positionWS - _WorldSpaceCameraPos);
                float along = max(0, -dot(eye, ray));
                float3 closest = eye + ray * along;
                float altitude = length(closest) - _Radius;
                float glow = altitude >= 0 ? exp(-altitude / _ScaleHeight) : exp(altitude / (_ScaleHeight * 12));
                Light sun = GetMainLight();
                float3 L = dot(sun.direction, sun.direction) > 0.5 ? normalize(sun.direction) : normalize(_SunFallback.xyz);
                float sunUp = dot(normalize(closest), L);
                float day = saturate(sunUp * 1.4 + 0.35);
                float3 colour = lerp(_Dusk.rgb, _Glow.rgb, saturate(sunUp * 3 + 0.35));
                return half4(colour * glow * day, 0);
            }
            ENDHLSL
        }
    }
}
