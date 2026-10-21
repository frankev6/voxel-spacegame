// Feel prototype (2026-10-21, docs/design/exploration/world-and-scale.md): a planet below the flight plane. The mesh is a cap of a
// sphere in metres round the planet's centre (object space), so every pixel can rebuild the surface direction exactly. Continents,
// clouds, night-side city lights and a haze towards the horizon are procedural; one material per planet sets the palette.
Shader "Game/Orbit/Planet"
{
    Properties
    {
        _Ocean ("Ocean", Color) = (0.03, 0.10, 0.20, 1)
        _Shallow ("Shallows", Color) = (0.05, 0.28, 0.36, 1)
        _Lowland ("Lowland", Color) = (0.23, 0.30, 0.14, 1)
        _Highland ("Highland", Color) = (0.42, 0.33, 0.22, 1)
        _Peak ("Peaks", Color) = (0.82, 0.80, 0.76, 1)
        _Haze ("Haze", Color) = (0.30, 0.48, 0.80, 1)
        [HDR] _City ("City lights", Color) = (2.4, 1.5, 0.6, 1)
        _Sea ("Sea level", Range(0, 1)) = 0.47
        _Scale ("Feature scale", Float) = 9
        _Clouds ("Cloud cover", Range(0, 1)) = 0.40
        _Seed ("Seed", Float) = 3.7
        _SunFallback ("Sun direction if no light", Vector) = (0.4, 0.6, 0.3, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Ocean, _Shallow, _Lowland, _Highland, _Peak, _Haze, _City, _SunFallback;
                float _Sea, _Scale, _Clouds, _Seed;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dirOS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.dirOS = input.positionOS;
                return o;
            }

            float Hash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float Noise(float3 x)
            {
                float3 i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1,0,0)), f.x), lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), f.x), lerp(Hash(i + float3(0,1,1)), Hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }
            float Fbm(float3 p, int octaves)
            {
                float sum = 0, amp = 0.5, norm = 0;
                for (int i = 0; i < octaves; i++) { sum += Noise(p) * amp; norm += amp; p = p * 2.03 + float3(1.7, 9.2, 4.1); amp *= 0.5; }
                return sum / norm;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.dirOS);
                float3 p = n * _Scale + _Seed;
                // the visible cap is tiny on the unit sphere; domain-warp so coasts read as coasts at any zoom
                float3 warp = float3(Fbm(p * 1.3 + 11.0, 4), Fbm(p * 1.3 + 23.0, 4), Fbm(p * 1.3 + 37.0, 4)) - 0.5;
                float h = Fbm(p + warp * 1.6, 9);
                float3 nWS = normalize(TransformObjectToWorldDir(n));
                Light sun = GetMainLight();
                float3 L = dot(sun.direction, sun.direction) > 0.5 ? normalize(sun.direction) : normalize(_SunFallback.xyz);
                float3 V = normalize(_WorldSpaceCameraPos - input.positionWS);

                bool water = h < _Sea;
                float3 albedo;
                if (water) albedo = lerp(_Ocean.rgb, _Shallow.rgb, smoothstep(_Sea - 0.06, _Sea, h));
                else
                {
                    float t = saturate((h - _Sea) / (1 - _Sea));
                    float moisture = Fbm(p * 2.1 + 51.0, 5);
                    albedo = lerp(_Lowland.rgb, _Highland.rgb, smoothstep(0.05, 0.45, t) * (0.6 + moisture * 0.6));
                    albedo = lerp(albedo, _Peak.rgb, smoothstep(0.55, 0.75, t));
                }
                // clouds drift slowly; their shadow sits a little sunward
                float3 cp = n * _Scale * 2.2 + float3(_Time.y * 0.004, 0, _Time.y * 0.0025) + _Seed * 3;
                float cloud = smoothstep(1 - _Clouds, 1 - _Clouds + 0.22, Fbm(cp, 7));
                float shadow = smoothstep(1 - _Clouds, 1 - _Clouds + 0.22, Fbm(cp + mul((float3x3)UNITY_MATRIX_I_M, L) * 0.02, 5));

                float diffuse = saturate(dot(nWS, L) * 1.1 + 0.08);
                float3 lit = albedo * (diffuse * (1 - shadow * 0.45) * sun.color + 0.015);
                if (water)
                {
                    float3 hlf = normalize(L + V);
                    lit += pow(saturate(dot(nWS, hlf)), 180) * 1.6 * sun.color * diffuse;
                }
                // night side: city lights on low land, in clusters
                float night = saturate(1 - diffuse * 4);
                if (!water)
                {
                    float cities = smoothstep(0.62, 0.8, Fbm(p * 14 + 77.0, 4)) * (1 - smoothstep(0.1, 0.4, (h - _Sea) / (1 - _Sea)));
                    lit += _City.rgb * cities * night * (1 - cloud);
                }
                lit = lerp(lit, float3(0.92, 0.94, 0.97) * (diffuse * sun.color + 0.02), cloud);
                // haze thickens towards the horizon, lit by the sun
                float grazing = 1 - saturate(dot(nWS, V));
                float haze = pow(grazing, 6) * 0.75;
                lit = lerp(lit, _Haze.rgb * (saturate(dot(nWS, L) + 0.25) * sun.color), haze);
                return half4(lit, 1);
            }
            ENDHLSL
        }
    }
}
