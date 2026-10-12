// SPEC-11 REN-008 / ADR-0040: packed runtime brick shader.
Shader "Game/Voxel/BrickLit"
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
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "BrickVertexDecode.hlsl"
        #include "BrickSurface.hlsl"
        struct BrickAttributes { float4 position:POSITION; float4 attributes:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct BrickVaryings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            float3 positionCells:TEXCOORD1;
            float3 normalWS:TEXCOORD2;
            float3 tangentWS:TEXCOORD3;
            float3 bitangentWS:TEXCOORD4;
            float2 uv:TEXCOORD5;
            nointerpolation uint4 attributes:TEXCOORD6;
            float ao:TEXCOORD7;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        BrickVaryings BrickVert(BrickAttributes a)
        {
            BrickVaryings o = (BrickVaryings)0;
            UNITY_SETUP_INSTANCE_ID(a); UNITY_TRANSFER_INSTANCE_ID(a,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            BrickVertex b = DecodeBrickVertex(a.position,a.attributes);
            o.positionCells=b.positionCells; o.positionWS=TransformObjectToWorld(b.positionCells);
            o.positionCS=TransformWorldToHClip(o.positionWS);
            float3 n=kBrickNormals[b.normalIndex], t=kBrickTangents[b.normalIndex];
            o.normalWS=TransformObjectToWorldNormal(n); o.tangentWS=TransformObjectToWorldDir(t);
            o.bitangentWS=TransformObjectToWorldDir(cross(n,t));
            o.uv=BrickFaceUV(b.positionCells,b.normalIndex,1);
            o.attributes=uint4(b.layer,b.colour,b.damage,b.scorched); o.ao=b.ao;
            return o;
        }
        BrickSurfaceInput BrickInput(BrickVaryings v)
        {
            BrickSurfaceInput i;
            i.positionCells=v.positionCells; i.positionWS=v.positionWS; i.positionCS=v.positionCS;
            i.normalWS=normalize(v.normalWS); i.tangentWS=normalize(v.tangentWS); i.bitangentWS=normalize(v.bitangentWS);
            i.uv=v.uv; i.layer=v.attributes.x; i.colour=v.attributes.y; i.damage=v.attributes.z; i.scorched=v.attributes.w; i.ao=v.ao;
            return i;
        }
        half4 BrickFrag(BrickVaryings v):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(v); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
            BrickSurfaceInput i=BrickInput(v); SurfaceData s=(SurfaceData)0; BrickSurface(i,s); return BrickLighting(i,s);
        }
        float3 _LightDirection, _LightPosition;
        BrickVaryings BrickShadowVert(BrickAttributes a)
        {
            BrickVaryings o=BrickVert(a);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
            float3 direction=normalize(_LightPosition-o.positionWS);
#else
            float3 direction=_LightDirection;
#endif
            o.positionCS=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(o.positionWS,o.normalWS,direction)));
            return o;
        }
        half4 BrickDepthFrag(BrickVaryings v):SV_Target
        { UNITY_SETUP_INSTANCE_ID(v); BrickDissolve(v.uv); return 0; }
        half4 BrickNormalsFrag(BrickVaryings v):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(v); BrickSurfaceInput i=BrickInput(v); SurfaceData s=(SurfaceData)0; BrickSurface(i,s);
            float3 n=normalize(s.normalTS.x*i.tangentWS+s.normalTS.y*i.bitangentWS+s.normalTS.z*i.normalWS);
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
            #pragma vertex BrickVert
            #pragma fragment BrickFrag
            #pragma multi_compile_instancing

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
            #pragma vertex BrickShadowVert
            #pragma fragment BrickDepthFrag
            #pragma multi_compile_instancing

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
            #pragma vertex BrickVert
            #pragma fragment BrickDepthFrag
            #pragma multi_compile_instancing

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex BrickVert
            #pragma fragment BrickNormalsFrag
            #pragma multi_compile_instancing

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    FallBack Off
}
