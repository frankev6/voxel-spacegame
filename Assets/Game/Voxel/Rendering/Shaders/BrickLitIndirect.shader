// SPEC-11 REN-003 / ADR-0041: global indices, vertex owners and the shared packed surface.
Shader "Game/Voxel/BrickLitIndirect"
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
        ByteAddressBuffer _BrickVertices;
        StructuredBuffer<float4x4> _BrickMatrices;
        ByteAddressBuffer _BrickOwners;
        struct BrickAttributes { uint vertexID:SV_VertexID; };
        struct BrickVaryings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            float3 positionCells:TEXCOORD1;
            half3 normalWS:TEXCOORD2;
            half3 tangentWS:TEXCOORD3;
            float2 uv:TEXCOORD5;
            nointerpolation uint attributes:TEXCOORD6;
            half ao:TEXCOORD7;
            nointerpolation half3 faceNormal:TEXCOORD8;
        };
        BrickVertex DecodePoolVertex(uint2 packed)
        {
            // Raw buffers already contain bytes: avoid the registered mesh's UNorm conversion/round trip.
            BrickVertex b;
            b.positionCells=float3(packed.x&255u,(packed.x>>8)&255u,(packed.x>>16)&255u)*.5;
            b.normalIndex=packed.y&255u; b.layer=(packed.y>>8)&255u; b.colour=(packed.y>>16)&255u;
            b.ao=((packed.y>>24)&3u)/3.0; b.damage=(packed.y>>26)&3u; b.scorched=(packed.y>>28)&1u;
            return b;
        }
        BrickVaryings BrickVert(BrickAttributes a)
        {
            BrickVaryings o = (BrickVaryings)0;
            uint slot=_BrickOwners.Load(a.vertexID*4);
            uint2 packed=_BrickVertices.Load2(a.vertexID*8);
            BrickVertex b=DecodePoolVertex(packed);
            float4x4 brickToWorld=_BrickMatrices[slot];
            // The matrix's bottom row carries the brick's grid cell offset (BrickPoolRenderSystem); positions use .xyz only.
            float3 gridCells=b.positionCells+brickToWorld[3].xyz;
            o.positionCells=gridCells; o.positionWS=mul(brickToWorld,float4(b.positionCells,1)).xyz;
            o.positionCS=TransformWorldToHClip(o.positionWS);
            float3 n=kBrickNormals[b.normalIndex], t=kBrickTangents[b.normalIndex];
            o.normalWS=normalize(mul((float3x3)brickToWorld,n)); o.tangentWS=normalize(mul((float3x3)brickToWorld,t));
            o.uv=BrickFaceUV(gridCells,b.normalIndex,1); o.faceNormal=n;
            o.attributes=packed.y>>8; o.ao=b.ao;
            return o;
        }
        BrickSurfaceInput BrickInput(BrickVaryings v)
        {
            BrickSurfaceInput i;
            i.positionCells=v.positionCells; i.positionWS=v.positionWS; i.positionCS=v.positionCS;
            i.normalWS=normalize(v.normalWS); i.tangentWS=normalize(v.tangentWS); i.bitangentWS=normalize(cross(i.normalWS,i.tangentWS));
            // Brick transforms have positive uniform scale; the orthonormal bitangent is derived, not interpolated again.
            i.uv=v.uv; i.layer=v.attributes&255u; i.colour=(v.attributes>>8)&255u; i.damage=(v.attributes>>18)&3u; i.scorched=(v.attributes>>20)&1u; i.ao=v.ao;
            i.faceNormal=v.faceNormal;
            return i;
        }
        half4 BrickFrag(BrickVaryings v):SV_Target
        {
            
            BrickSurfaceInput i=BrickInput(v); SurfaceData s=(SurfaceData)0; BrickSurface(i,s); return BrickLighting(i,s);
        }
        float3 _LightDirection, _LightPosition;
        struct BrickDepthVaryings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
        struct BrickNormalVaryings
        {
            float4 positionCS:SV_POSITION;
            float2 uv:TEXCOORD0;
            half3 normalWS:TEXCOORD1;
            half3 tangentWS:TEXCOORD2;
            nointerpolation uint layer:TEXCOORD3;
        };
        void BrickGeometry(uint vertexID, out BrickVertex b, out float4x4 brickToWorld)
        {
            uint slot=_BrickOwners.Load(vertexID*4);
            uint2 packed=_BrickVertices.Load2(vertexID*8);
            b=DecodePoolVertex(packed);
            brickToWorld=_BrickMatrices[slot];
        }
        BrickDepthVaryings BrickDepthVert(BrickAttributes a)
        {
            BrickDepthVaryings o;
            BrickVertex b; float4x4 brickToWorld; BrickGeometry(a.vertexID,b,brickToWorld);
            o.positionCS=TransformWorldToHClip(mul(brickToWorld,float4(b.positionCells,1)).xyz);
            o.uv=BrickFaceUV(b.positionCells+brickToWorld[3].xyz,b.normalIndex,1);
            return o;
        }
        BrickDepthVaryings BrickShadowVert(BrickAttributes a)
        {
            BrickDepthVaryings o;
            BrickVertex b; float4x4 brickToWorld; BrickGeometry(a.vertexID,b,brickToWorld);
            float3 positionWS=mul(brickToWorld,float4(b.positionCells,1)).xyz;
            float3 normalWS=normalize(mul((float3x3)brickToWorld,kBrickNormals[b.normalIndex]));
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
            float3 direction=normalize(_LightPosition-positionWS);
#else
            float3 direction=_LightDirection;
#endif
            o.positionCS=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS,normalWS,direction)));
            o.uv=BrickFaceUV(b.positionCells+brickToWorld[3].xyz,b.normalIndex,1);
            return o;
        }
        BrickNormalVaryings BrickNormalVert(BrickAttributes a)
        {
            BrickNormalVaryings o;
            BrickVertex b; float4x4 brickToWorld; BrickGeometry(a.vertexID,b,brickToWorld);
            o.positionCS=TransformWorldToHClip(mul(brickToWorld,float4(b.positionCells,1)).xyz);
            o.normalWS=normalize(mul((float3x3)brickToWorld,kBrickNormals[b.normalIndex]));
            o.tangentWS=normalize(mul((float3x3)brickToWorld,kBrickTangents[b.normalIndex]));
            o.uv=BrickFaceUV(b.positionCells+brickToWorld[3].xyz,b.normalIndex,1); o.layer=b.layer;
            return o;
        }
        half4 BrickDepthFrag(BrickDepthVaryings v):SV_Target
        { BrickDissolve(v.uv); return 0; }
        half4 BrickNormalsFrag(BrickNormalVaryings v):SV_Target
        {
            BrickDissolve(v.uv);
            half4 parameters=LOAD_TEXTURE2D(_BrickLayerParams,int2(v.layer,1));
            float2 uv=v.uv/max(1,round(parameters.r));
            half2 xy=SAMPLE_TEXTURE2D_ARRAY(_BrickNormalArray,sampler_BrickNormalArray,uv,v.layer).rg*2-1;
            half3 normalTS=half3(xy,sqrt(saturate(1-dot(xy,xy))));
            float3 normalWS=normalize(v.normalWS), tangentWS=normalize(v.tangentWS);
            float3 n=normalize(normalTS.x*tangentWS+normalTS.y*normalize(cross(normalWS,tangentWS))+normalTS.z*normalWS);
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
            #pragma vertex BrickDepthVert
            #pragma fragment BrickDepthFrag


            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex BrickNormalVert
            #pragma fragment BrickNormalsFrag

            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    FallBack Off
}
