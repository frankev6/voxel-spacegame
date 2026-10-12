Shader "Game/NocturneSky"
{
    Properties
    {
        _NebulaSpeed ("Nebula evolution speed", Range(0, 0.05)) = 0.012
        _Brightness ("Sky brightness", Range(0, 2)) = 1
        _NebulaPhase ("Nebula phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _NebulaSpeed, _Brightness, _NebulaPhase;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            float Hash(float3 p)
            {
                p = frac(p * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float Noise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(i), Hash(i+float3(1,0,0)),f.x),
                                 lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),
                            lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),
                                 lerp(Hash(i+float3(0,1,1)),Hash(i+1),f.x),f.y),f.z);
            }
            float Fbm(float3 p)
            {
                float value=0, weight=.52;
                [unroll] for(int octave=0;octave<5;octave++)
                {
                    value += weight * Noise(p);
                    p = float3(p.y+p.z*.21, p.z-p.x*.19, p.x+p.y*.17)*2.03+7.1;
                    weight *= .48;
                }
                return value;
            }
            float Stars(float3 direction, float scale, float threshold)
            {
                float3 p=direction*scale, cell=floor(p);
                float seed=Hash(cell+19.7);
                float3 centre=.2+.6*float3(Hash(cell+2.3), Hash(cell+6.7), Hash(cell+11.1));
                float distance=length(frac(p)-centre);
                float aa=max(fwidth(distance),.006);
                return (1-smoothstep(.035-aa,.055+aa,distance))*step(threshold,seed);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 d=normalize(input.direction);
                float time=_Time.y*_NebulaSpeed+_NebulaPhase;
                // Time deforms the smoke field, never the direction used by stars.
                float3 p=d*3.4;
                float warp=Fbm(p+float3(time*.3,0,time*.13));
                float smoke=Fbm(p*1.4 + warp*2 + float3(0,time*.22,-time*.16));
                float fine=Fbm(p*4.5 + warp*3 + float3(time*.12,0,0));
                float dust=Fbm(p*.8+float3(8,2,time*.09));
                float lower=1-smoothstep(.1,.65,d.y);
                float green=smoothstep(-.4,.85,dot(d,normalize(float3(.5,-.65,.6))));
                float violet=smoothstep(-.3,.85,dot(d,normalize(float3(-.7,-.45,.5))));
                float amber=pow(saturate(dot(d,normalize(float3(.8,-.55,-.1)))),3);
                float density=smoothstep(.28,.72,smoke)*( .35 + fine);
                // Linear equivalents of DESIGN-GUIDE's green, violet and low amber palette.
                float3 colour=lerp(float3(.023,.042,.03),float3(.068,.042,.016),fine)*green;
                colour+=lerp(float3(.068,.018,.138),float3(.016,.021,.112),fine)*violet*.8;
                colour+=float3(.254,.048,.013)*amber*.24;
                float ridges=pow(saturate(1-abs(fine-.49)*18),3)*smoothstep(.38,.65,smoke);
                float lanes=1-smoothstep(.44,.66,dust)*.92;
                float3 sky=float3(.0006,.0009,.0009) + lower*lanes*(colour*density + float3(.17,.22,.16)*ridges*.13);
                float stars=Stars(d,170,.88)*.65+Stars(d,420,.94)*.3+Stars(d,800,.98)*.12;
                sky+=stars*lerp(float3(.65,.78,1),float3(1,.83,.64),Hash(floor(d*170)))*(.25+.75*lanes);
                return half4(sky*_Brightness,1);
            }
            ENDHLSL
        }
    }
}
