// SPEC-11 REN-008 / ADR-0040.
Shader "Game/Voxel/PropInstanced"
{
    Properties
    {
        [Header(Baked by BrickMaterialBaker)]
        [NoScaleOffset] _BrickAlbedoArray ("Albedo array (BC7 sRGB)", 2DArray) = "" {}
        [NoScaleOffset] _BrickMaskArray ("Mask array (BC7 linear: metallic, smoothness, paint, emissive)", 2DArray) = "" {}
        [NoScaleOffset] _BrickNormalArray ("Normal array (BC5)", 2DArray) = "" {}
        [NoScaleOffset] _BrickLayerParams ("Layer params 64 x 2 (RGBAHalf)", 2D) = "black" {}
        [NoScaleOffset] _PaintPalette ("Paint palette 256 x 1", 2D) = "white" {}

        [Header(Damage)]
        [NoScaleOffset] _CrackTex ("Crack distance field", 2D) = "white" {}
        _CrackTiling ("Crack tiling (cells per tile)", Range(1, 4)) = 2
        [NoScaleOffset] _DamageRamp ("Damage ramp (x = damage 0-1)", 2D) = "white" {}
        _ScorchColor ("Scorch colour", Color) = (0.0392, 0.0431, 0.0549, 1)
        _ScorchStrength ("Scorch strength", Range(0, 1)) = 0.6
        [HDR] _EmberColor ("Ember colour", Color) = (1, 0.416, 0.165, 1)
        _EmberIntensity ("Ember intensity", Range(0, 16)) = 4
        _EmberFadeSeconds ("Ember fade (s)", Range(0, 30)) = 8
        _ExposedLayers ("Exposed layers (x, y, z; w = count)", Vector) = (16, 17, 18, 3)

        [Header(Form)]
        _AOStrength ("AO strength", Range(0, 1)) = 0.6
        _AOColor ("AO colour", Color) = (0, 0, 0, 1)
        [NoScaleOffset] _MacroNoiseTex ("Macro noise", 2D) = "gray" {}
        _MacroScale ("Macro scale (cells)", Range(16, 256)) = 64
        _MacroStrength ("Macro strength", Range(0, 0.3)) = 0.03

        [Header(Light)]
        _EmissiveIntensity ("Emissive intensity", Range(0, 16)) = 1
        _SeamEmission ("Seam emission", Range(0, 2)) = 0
        _SeamFadeStart ("Seam fade start (m)", Float) = 0
        _SeamFadeEnd ("Seam fade end (m)", Float) = 0
        _RimColor ("Rim colour", Color) = (0.561, 0.651, 0.784, 1)
        _RimStrength ("Rim strength", Range(0, 1)) = 0
        _ShadingMode ("Shading mode (0 PBR, 1 toon ramp)", Range(0, 1)) = 0
        [NoScaleOffset] _ToonRamp ("Toon ramp", 2D) = "white" {}
        _HalftoneScale ("Halftone scale (screen px per dot; 0 = off)", Float) = 0

        [Header(Per grid values set by Presentation at runtime)]
        _TeamPrimary ("Team primary (linear)", Vector) = (0.78, 0.74, 0.65, 1)
        _TeamSecondary ("Team secondary (linear)", Vector) = (0.05, 0.05, 0.06, 1)
        _TeamAccent ("Team accent (linear)", Vector) = (1, 0.45, 0.05, 1)
        _GridPowered ("Grid powered", Range(0, 1)) = 1
        _LastHitTime ("Last hit time (_Time.y)", Float) = -100000
        _Highlight ("Highlight (rgb, strength)", Vector) = (0, 0, 0, 0)
        _Dissolve01 ("Dissolve", Range(0, 1)) = 0

        [NoScaleOffset] _PropAlbedo("Prop albedo",2D)="white" {}
        [NoScaleOffset] _PropMask("Prop mask",2D)="white" {}
        [NoScaleOffset] _PropNormal("Prop normal (RG)",2D)="bump" {}
        _FunctionPaint("Function palette index",Float)=20
        _Damage01("Damage fraction",Range(0,1))=0
        _Powered("Powered",Range(0,1))=1
        _Throttle("Throttle",Range(0,1))=0
        [HideInInspector] _DraftPaintIndex("Draft paint index",Float)=0
        [HideInInspector] _DraftSliceEnabled("Draft slice enabled",Float)=0
        [HideInInspector] _DraftSliceTop("Draft slice top",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "BrickSurface.hlsl"
        struct PropInstanceGpu { float3 LocalCellCenter; uint Packed; };
        StructuredBuffer<PropInstanceGpu> _PropInstances;
        StructuredBuffer<float4x4> _GridMatrices;
        float4x4 _OrientMatrices[24];
        UNITY_INSTANCING_BUFFER_START(DraftProps)
            UNITY_DEFINE_INSTANCED_PROP(float, _DraftPaintIndex)
        UNITY_INSTANCING_BUFFER_END(DraftProps)
        float _DraftSliceEnabled, _DraftSliceTop;
        float4x4 _DraftWorldToCells;
        // Vertex pulling below uses the same instance ID; no per-instance material or object is created.
        void PropSetup() { }
        struct PropAttributes
        {
            float3 position:POSITION; float3 normal:NORMAL; float4 tangent:TANGENT;
            float2 uv:TEXCOORD0; float2 blink:TEXCOORD1; float4 paint:COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct PropVaryings
        {
            float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0;
            float3 normalWS:TEXCOORD1; float3 tangentWS:TEXCOORD2; float3 bitangentWS:TEXCOORD3;
            float2 uv:TEXCOORD4; float4 paint:TEXCOORD5; float blink:TEXCOORD6;
            nointerpolation uint colour:TEXCOORD7;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        PropVaryings PropVert(PropAttributes a)
        {
            PropVaryings o=(PropVaryings)0;
            UNITY_SETUP_INSTANCE_ID(a); UNITY_TRANSFER_INSTANCE_ID(a,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 tangent=a.tangent.xyz;
            if (dot(tangent,tangent)<.01) tangent=normalize(cross(abs(a.normal.y)<.9 ? float3(0,1,0) : float3(1,0,0),a.normal));
            float3 bitangent=cross(a.normal,tangent)*(a.tangent.w==0 ? 1 : a.tangent.w);
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            PropInstanceGpu instance=_PropInstances[unity_InstanceID];
            float3x3 rotation=(float3x3)_OrientMatrices[instance.Packed&31u];
            float4x4 grid=_GridMatrices[(instance.Packed>>13u)&65535u];
            o.positionWS=mul(grid,float4(instance.LocalCellCenter+mul(rotation,a.position),1)).xyz;
            o.normalWS=normalize(mul((float3x3)grid,mul(rotation,a.normal)));
            o.tangentWS=normalize(mul((float3x3)grid,mul(rotation,tangent)));
            o.bitangentWS=normalize(mul((float3x3)grid,mul(rotation,bitangent)));
            o.colour=(instance.Packed>>5u)&255u;
#else
            o.positionWS=TransformObjectToWorld(a.position); o.normalWS=TransformObjectToWorldNormal(a.normal);
            o.tangentWS=TransformObjectToWorldDir(tangent); o.bitangentWS=TransformObjectToWorldDir(bitangent);
            o.colour=(uint)UNITY_ACCESS_INSTANCED_PROP(DraftProps, _DraftPaintIndex);
#endif
            o.positionCS=TransformWorldToHClip(o.positionWS); o.uv=a.uv; o.paint=a.paint; o.blink=a.blink.x;
            return o;
        }
        half4 PropFrag(PropVaryings v):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(v); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
#if !defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            if (_DraftSliceEnabled > .5)
                clip(_DraftSliceTop - mul(_DraftWorldToCells, float4(v.positionWS, 1)).y);
#endif
            half dissolve=BrickDissolve(v.uv);
            half3 albedo=SAMPLE_TEXTURE2D(_PropAlbedo,sampler_PropAlbedo,v.uv).rgb;
            half4 mask=SAMPLE_TEXTURE2D(_PropMask,sampler_PropMask,v.uv);
            half2 nxy=SAMPLE_TEXTURE2D(_PropNormal,sampler_PropNormal,v.uv).rg*2-1;
            half3 paint=BrickPaint(v.colour)*v.paint.r+Brick_TeamSecondary().rgb*v.paint.g+Brick_TeamAccent().rgb*v.paint.b;
            paint+=LOAD_TEXTURE2D(_PaintPalette,int2((int)Brick_FunctionPaint(),0)).rgb*v.paint.a;
            half selected=saturate(dot(v.paint,1));
            half mean=Luminance(SAMPLE_TEXTURE2D_LOD(_PropAlbedo,sampler_PropAlbedo,v.uv,8).rgb);
            albedo=lerp(albedo,paint*Luminance(albedo)/max(mean,.02h),mask.b*selected);
            uint stage=(uint)min(3,floor(saturate(Brick_Damage01())*4));
            albedo=lerp(albedo,_ScorchColor.rgb,_ScorchStrength*stage/3.0h);
            albedo*=SAMPLE_TEXTURE2D(_DamageRamp,sampler_DamageRamp,float2(stage/3.0,.5)).rgb;
            half crack=0;
            if(stage>1)
            {
                half field=SAMPLE_TEXTURE2D(_CrackTex,sampler_CrackTex,v.uv/max(_CrackTiling,.001)).r;
                half width=.06h*(stage-1),aa=max(fwidth(field),1e-3);
                crack=1-smoothstep(width-aa,width+aa,field); albedo=lerp(albedo,_ScorchColor.rgb*.35h,crack);
            }
            SurfaceData s=(SurfaceData)0; s.albedo=albedo; s.metallic=mask.r; s.smoothness=saturate(mask.g-.1h*min(stage,1u));
            s.normalTS=half3(nxy,sqrt(saturate(1-dot(nxy,nxy)))); s.occlusion=1; s.alpha=1;
            half blink=v.blink<=0 ? 1 : step(frac(_Time.y-v.blink),.1);
            half powered=Brick_Powered();
            s.emission=Brick_TeamAccent().rgb*mask.a*_EmissiveIntensity*powered*blink*(1+Brick_Throttle());
            half fade=_EmberFadeSeconds>0 ? saturate(1-(_Time.y-Brick_LastHitTime())/_EmberFadeSeconds) : 0;
            s.emission+=_EmberColor.rgb*_EmberIntensity*(fade*crack*(stage==3 ? 1 : .35)+dissolve);
            float3 n=normalize(s.normalTS.x*v.tangentWS+s.normalTS.y*v.bitangentWS+s.normalTS.z*v.normalWS);
            half fresnel=1-saturate(dot(n,GetWorldSpaceNormalizeViewDir(v.positionWS)));
            half4 highlight=Brick_Highlight(); s.emission+=highlight.rgb*highlight.a*fresnel*fresnel+_RimColor.rgb*_RimStrength*pow(fresnel,4);
            BrickSurfaceInput i=(BrickSurfaceInput)0; i.positionWS=v.positionWS; i.positionCS=v.positionCS;
            i.normalWS=normalize(v.normalWS); i.tangentWS=normalize(v.tangentWS); i.bitangentWS=normalize(v.bitangentWS);
            return BrickLighting(i,s);
        }
        float3 _LightDirection, _LightPosition;
        PropVaryings PropShadowVert(PropAttributes a)
        {
            PropVaryings o=PropVert(a);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
            float3 direction=normalize(_LightPosition-o.positionWS);
#else
            float3 direction=_LightDirection;
#endif
            o.positionCS=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(o.positionWS,o.normalWS,direction)));
            return o;
        }
        half4 PropDepthFrag(PropVaryings v):SV_Target { UNITY_SETUP_INSTANCE_ID(v); BrickDissolve(v.uv); return 0; }
        half4 PropNormalsFrag(PropVaryings v):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(v); BrickDissolve(v.uv);
            half2 xy=SAMPLE_TEXTURE2D(_PropNormal,sampler_PropNormal,v.uv).rg*2-1;
            float3 n=normalize(xy.x*v.tangentWS+xy.y*v.bitangentWS+sqrt(saturate(1-dot(xy,xy)))*v.normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
            return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*.5+.5)),0);
#else
            return half4(n,0);
#endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit" Tags { "LightMode"="UniversalForward" }
            ZWrite On Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex PropVert
            #pragma fragment PropFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:PropSetup
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            ZWrite On Cull Back
            ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex PropShadowVert
            #pragma fragment PropDepthFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:PropSetup
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            ZWrite On Cull Back
            ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex PropVert
            #pragma fragment PropDepthFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:PropSetup
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex PropVert
            #pragma fragment PropNormalsFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:PropSetup
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    FallBack Off
}
