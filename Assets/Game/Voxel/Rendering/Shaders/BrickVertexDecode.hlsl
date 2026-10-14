// SPEC-11 §4.4 and ADR-0027. Keep the table order identical to VoxelNormals.
#ifndef GAME_BRICK_VERTEX_DECODE_INCLUDED
#define GAME_BRICK_VERTEX_DECODE_INCLUDED

static const float3 kBrickNormals[42] =
{
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(-1.000000000, 0.000000000, 0.000000000),
    float3(0.000000000, 1.000000000, 0.000000000),
    float3(0.000000000, -1.000000000, 0.000000000),
    float3(0.000000000, 0.000000000, 1.000000000),
    float3(0.000000000, 0.000000000, -1.000000000),
    float3(0.000000000, 0.707106781, 0.707106781),
    float3(0.000000000, -0.707106781, 0.707106781),
    float3(0.000000000, 0.707106781, -0.707106781),
    float3(0.000000000, -0.707106781, -0.707106781),
    float3(0.707106781, 0.000000000, 0.707106781),
    float3(-0.707106781, 0.000000000, 0.707106781),
    float3(0.707106781, 0.000000000, -0.707106781),
    float3(-0.707106781, 0.000000000, -0.707106781),
    float3(0.707106781, 0.707106781, 0.000000000),
    float3(-0.707106781, 0.707106781, 0.000000000),
    float3(0.707106781, -0.707106781, 0.000000000),
    float3(-0.707106781, -0.707106781, 0.000000000),
    float3(0.000000000, 0.447213595, 0.894427191),
    float3(0.000000000, -0.447213595, 0.894427191),
    float3(0.000000000, 0.447213595, -0.894427191),
    float3(0.000000000, -0.447213595, -0.894427191),
    float3(0.000000000, 0.894427191, 0.447213595),
    float3(0.000000000, -0.894427191, 0.447213595),
    float3(0.000000000, 0.894427191, -0.447213595),
    float3(0.000000000, -0.894427191, -0.447213595),
    float3(0.447213595, 0.000000000, 0.894427191),
    float3(-0.447213595, 0.000000000, 0.894427191),
    float3(0.447213595, 0.000000000, -0.894427191),
    float3(-0.447213595, 0.000000000, -0.894427191),
    float3(0.894427191, 0.000000000, 0.447213595),
    float3(-0.894427191, 0.000000000, 0.447213595),
    float3(0.894427191, 0.000000000, -0.447213595),
    float3(-0.894427191, 0.000000000, -0.447213595),
    float3(0.447213595, 0.894427191, 0.000000000),
    float3(-0.447213595, 0.894427191, 0.000000000),
    float3(0.447213595, -0.894427191, 0.000000000),
    float3(-0.447213595, -0.894427191, 0.000000000),
    float3(0.894427191, 0.447213595, 0.000000000),
    float3(-0.894427191, 0.447213595, 0.000000000),
    float3(0.894427191, -0.447213595, 0.000000000),
    float3(-0.894427191, -0.447213595, 0.000000000)
};

static const float3 kBrickTangents[42] =
{
    float3(0.000000000, 0.000000000, -1.000000000),
    float3(0.000000000, 0.000000000, 1.000000000),
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(-1.000000000, 0.000000000, 0.000000000),
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(0.000000000, 1.000000000, 0.000000000),
    float3(0.000000000, 1.000000000, 0.000000000),
    float3(0.000000000, 1.000000000, 0.000000000),
    float3(0.000000000, 1.000000000, 0.000000000),
    float3(0.000000000, 0.000000000, 1.000000000),
    float3(0.000000000, 0.000000000, 1.000000000),
    float3(0.000000000, 0.000000000, 1.000000000),
    float3(0.000000000, 0.000000000, 1.000000000),
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(1.000000000, 0.000000000, 0.000000000),
    float3(0.000000000, 1.000000000, 0.000000000),
    float3(0.000000000, 1.000000000, 0.000000000),
    float3(0.000000000, 1.000000000, 0.000000000),
    float3(0.000000000, 1.000000000, 0.000000000),
    float3(0.000000000, 1.000000000, 0.000000000),
    float3(0.000000000, 1.000000000, 0.000000000),
    float3(0.000000000, 1.000000000, 0.000000000),
    float3(0.000000000, 1.000000000, 0.000000000),
    float3(0.000000000, 0.000000000, 1.000000000),
    float3(0.000000000, 0.000000000, 1.000000000),
    float3(0.000000000, 0.000000000, 1.000000000),
    float3(0.000000000, 0.000000000, 1.000000000),
    float3(0.000000000, 0.000000000, 1.000000000),
    float3(0.000000000, 0.000000000, 1.000000000),
    float3(0.000000000, 0.000000000, 1.000000000),
    float3(0.000000000, 0.000000000, 1.000000000)
};

struct BrickVertex
{
    float3 positionCells;
    uint normalIndex;
    uint layer;
    uint colour;
    float ao;
    uint damage;
    uint scorched;
    float3 normal, tangent;   // face table normal/tangent, or the smooth Xeno normal (ADR-0115)
    uint smooth;
    float xenoDepth;          // cells below the grown surface of the deepest tissue sample (exposed flesh glows)
};

float3 BrickOctDecode(float2 e)
{
    e = e * 2 - 1;
    float3 n = float3(e.x, 1 - abs(e.x) - abs(e.y), e.y);
    if (n.y < 0) n.xz = (1 - abs(n.zx)) * (n.xz >= 0 ? 1 : -1);
    return normalize(n);
}
// Shared decode of the packed bytes (positions, normal byte, high Lo byte, flags) for both drawing paths.
void BrickDecodeShared(inout BrickVertex v, uint3 position, uint normalByte, uint loTop, uint flags)
{
    v.smooth = (flags >> 5u) & 1u;
    if (v.smooth != 0)
    {
        v.positionCells = position / 8.0 - .5;
        v.normal = BrickOctDecode(float2(normalByte, loTop) / 255.0);
        float3 helper = abs(v.normal.y) < .95 ? float3(0, 1, 0) : float3(1, 0, 0);
        v.tangent = normalize(cross(helper, v.normal));
        v.xenoDepth = ((flags & 3u) | (((flags >> 6u) & 3u) << 2u)) / 4.0;
        v.ao = 1; v.normalIndex = 2; v.scorched = 0;
    }
    else
    {
        v.positionCells = position * 0.5;
        v.normal = kBrickNormals[normalByte]; v.tangent = kBrickTangents[normalByte];
        v.xenoDepth = 0; v.ao = (flags & 3u) / 3.0; v.normalIndex = normalByte; v.scorched = (flags >> 4u) & 1u;
    }
    v.damage = (flags >> 2u) & 3u;
}

BrickVertex DecodeBrickVertex(float4 posRaw, float4 uvRaw)
{
    uint4 attributes = (uint4)round(uvRaw * 255.0);
    uint4 position = (uint4)round(posRaw * 255.0);
    BrickVertex v = (BrickVertex)0;
    v.layer = attributes.y;
    v.colour = attributes.z;
    BrickDecodeShared(v, position.xyz, attributes.x, position.w, attributes.w);
    return v;
}

// Smooth surfaces have no face plane: project on the dominant axis of the normal.
float2 BrickSmoothUV(float3 positionCells, float3 n)
{
    float3 a = abs(n);
    return a.x > a.y && a.x > a.z ? positionCells.zy : a.y > a.z ? positionCells.xz : positionCells.xy;
}

float2 BrickFaceUV(float3 positionCells, uint normalIndex, float tileCells)
{
    float3 tangent = kBrickTangents[normalIndex];
    float3 bitangent = cross(kBrickNormals[normalIndex], tangent);
    return float2(dot(positionCells, tangent), dot(positionCells, bitangent)) / tileCells;
}
#endif
