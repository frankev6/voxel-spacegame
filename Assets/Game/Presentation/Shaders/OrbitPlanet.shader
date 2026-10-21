// Feel prototype (2026-10-21, docs/design/exploration/world-and-scale.md): a planet below the flight plane. The mesh is a cap of a
// sphere in metres round the planet's centre (object space), so every pixel can rebuild the surface direction exactly. Continents,
// clouds, night-side city lights and a haze towards the horizon are procedural; one material per planet sets the palette.
Shader "Game/Orbit/Planet"
{
    Properties
    {
        _Ocean ("Ocean", Color) = (0.010, 0.040, 0.105, 1)
        _Shallow ("Shallows", Color) = (0.030, 0.185, 0.270, 1)
        _Beach ("Coast", Color) = (0.420, 0.390, 0.300, 1)
        _Lowland ("Lowland", Color) = (0.060, 0.120, 0.050, 1)
        _Dry ("Dry land", Color) = (0.300, 0.230, 0.130, 1)
        _Highland ("Highland", Color) = (0.250, 0.220, 0.190, 1)
        _Peak ("Peaks", Color) = (0.850, 0.860, 0.880, 1)
        _Haze ("Haze", Color) = (0.180, 0.360, 0.780, 1)
        [HDR] _City ("City lights", Color) = (2.4, 1.5, 0.6, 1)
        _Sea ("Sea level", Range(0, 1)) = 0.5
        _Scale ("Feature scale", Float) = 30
        _Clouds ("Cloud cover", Range(0, 1)) = 0.42
        _HazeDistance ("Haze distance (m)", Float) = 26000
        _Seed ("Seed", Float) = 3.7
        _SunFallback ("Sun direction if no light", Vector) = (0.4, 0.6, 0.3, 0)
        _Bands ("Gas giant bands (0 = rocky)", Range(0, 1)) = 0
        _WorldScale ("Scaled-space factor (set per frame)", Float) = 1
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
                float4 _Ocean, _Shallow, _Beach, _Lowland, _Dry, _Highland, _Peak, _Haze, _City, _SunFallback;
                float _Sea, _Scale, _Clouds, _HazeDistance, _Seed, _Bands, _WorldScale;
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
                // a far planet is drawn scaled toward the camera (DEV-028 scaled space): distances are true distances again
                float distanceM = distance(_WorldSpaceCameraPos, input.positionWS) / max(1e-4, _WorldScale);
                // fewer octaves far away: detail below a pixel only shimmers
                int octaves = distanceM < 6000 ? 9 : distanceM < 14000 ? 7 : 5;
                // domain-warp so coasts read as coasts, with archipelagos and long inlets
                float3 warp = float3(Fbm(p * 0.9 + 11.0, 4), Fbm(p * 0.9 + 23.0, 4), Fbm(p * 0.9 + 37.0, 4)) - 0.5;
                float h = Fbm(p + warp * 2.2, octaves);
                float3 nWS = normalize(TransformObjectToWorldDir(n));
                Light sun = GetMainLight();
                float3 L = dot(sun.direction, sun.direction) > 0.5 ? normalize(sun.direction) : normalize(_SunFallback.xyz);
                float3 V = normalize(_WorldSpaceCameraPos - input.positionWS);
                float sunUp = dot(nWS, L);

                bool water = h < _Sea && _Bands <= 0;
                float3 albedo;
                if (_Bands > 0)
                {   // gas giant: belts and zones along the spin axis (+Z, so from above they run as stripes), sheared by storms
                    float lat = n.z * _Scale * 0.35 + (warp.x * 1.6 + warp.y * 0.6) * _Bands;
                    float band = Fbm(float3(lat * 2.2, _Seed, lat * 0.7), 5);
                    float swirl = Fbm(p * float3(2.4, 2.4, 0.6) + 91.0, 5);
                    albedo = lerp(_Beach.rgb, _Dry.rgb, smoothstep(0.35, 0.55, band));
                    albedo = lerp(albedo, _Highland.rgb, smoothstep(0.5, 0.7, swirl) * 0.6);
                    albedo = lerp(albedo, _Peak.rgb, smoothstep(0.62, 0.8, band) * 0.7);
                }
                else if (water) albedo = lerp(_Ocean.rgb, _Shallow.rgb, pow(smoothstep(_Sea - 0.09, _Sea, h), 2));
                else
                {
                    float t = saturate((h - _Sea) / (1 - _Sea));
                    float moisture = Fbm(p * 1.7 + 51.0, 5);
                    float3 low = lerp(_Dry.rgb, _Lowland.rgb, smoothstep(0.38, 0.62, moisture));
                    albedo = lerp(_Beach.rgb, low, smoothstep(0.0, 0.05, t));
                    albedo = lerp(albedo, _Highland.rgb, smoothstep(0.25, 0.5, t));
                    albedo = lerp(albedo, _Peak.rgb, smoothstep(0.62, 0.78, t));
                    albedo *= 0.8 + 0.4 * Noise(p * 40);   // fields, forest and scree
                }
                // clouds drift slowly; their shadow sits a little sunward
                float3 cp = n * _Scale * 1.6 + float3(_Time.y * 0.004, 0, _Time.y * 0.0025) + _Seed * 3;
                float cloudField = Fbm(cp + warp * 0.6, octaves - 2);
                float cloud = smoothstep(1 - _Clouds, 1 - _Clouds + 0.18, cloudField);
                float shadow = smoothstep(1 - _Clouds, 1 - _Clouds + 0.18, Fbm(cp + warp * 0.6 + mul((float3x3)UNITY_MATRIX_I_M, L) * 0.03, 4));

                float diffuse = saturate(sunUp * 1.05 + 0.05);
                float3 lit = albedo * (diffuse * (1 - shadow * 0.55) * sun.color + 0.012);
                if (water)
                {
                    float3 hlf = normalize(L + V);
                    float glint = pow(saturate(dot(nWS, hlf)), 220) * 2.4 + pow(saturate(dot(nWS, hlf)), 24) * 0.08;
                    lit += glint * sun.color * diffuse * (1 - cloud);
                }
                // night side: city lights on low land, in clusters
                float night = saturate(1 - diffuse * 5);
                if (!water && _Bands <= 0)
                {
                    float cities = smoothstep(0.62, 0.8, Fbm(p * 14 + 77.0, 4)) * (1 - smoothstep(0.08, 0.3, (h - _Sea) / (1 - _Sea)));
                    lit += _City.rgb * cities * night * (1 - cloud);
                }
                float3 cloudLit = float3(0.95, 0.96, 0.98) * (saturate(sunUp * 1.2 + 0.15) * sun.color + 0.015);
                lit = lerp(lit, cloudLit, cloud * 0.92);
                // aerial perspective: distance buries the ground in blue, sunlit air; the terminator warms it
                float3 air = _Haze.rgb * (saturate(sunUp + 0.3) * sun.color) + float3(0.9, 0.35, 0.12) * saturate(1 - abs(sunUp) * 4) * 0.25;
                float fog = 1 - exp(-distanceM / max(1, _HazeDistance));
                float limb = pow(1 - saturate(dot(nWS, V)), 10) * 0.35;
                lit = lerp(lit, air, saturate(fog * 0.85 + limb));
                return half4(lit, 1);
            }
            ENDHLSL
        }
    }
}
