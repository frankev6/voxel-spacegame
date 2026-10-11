using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode.Voxel.Rendering
{
    [Category("Voxel")]
    public sealed class PackingTests
    {
        [Test] public void Vertex_RoundTrip()
        {
            // Sweep every legal value of every field, including half-cell positions and all 42 normals.
            for (int i = 0; i < 256; i++)
            {
                int x = i % 33, y = i * 7 % 33, z = i * 13 % 33, normal = i % 42, layer = i % 64;
                int ao = i % 4, damage = i / 4 % 4; bool scorched = (i & 16) != 0;
                var vertex = PackedBrickVertex.Pack(x,y,z,normal,layer,i,ao,damage,scorched);
                Assert.AreEqual(x, vertex.Lo & 255); Assert.AreEqual(y, (vertex.Lo >> 8) & 255); Assert.AreEqual(z, (vertex.Lo >> 16) & 255);
                Assert.AreEqual(0, vertex.Lo >> 24); Assert.AreEqual(normal, vertex.Hi & 255); Assert.AreEqual(layer, (vertex.Hi >> 8) & 255);
                Assert.AreEqual(i, (vertex.Hi >> 16) & 255); Assert.AreEqual(ao, (vertex.Hi >> 24) & 3); Assert.AreEqual(damage, (vertex.Hi >> 26) & 3);
                Assert.AreEqual(scorched, ((vertex.Hi >> 28) & 1) != 0); Assert.AreEqual(0, vertex.Hi >> 29);
                float3 unorm = new float3(x,y,z) / 255f;
                Assert.AreEqual(new float3(x,y,z) * .5f, math.round(unorm * 255f) * .5f, "shader position decode is exact");
            }
        }
        [Test] public void MeshCell_RoundTrip()
        {
            for (int i = 0; i < 256; i++)
            {
                uint value = MeshCell.Encode(i%4, i%8, i%32, i%64, i, i/4%4, (i&16)!=0);
                Assert.AreEqual(i%4, MeshCell.Class(value)); Assert.AreEqual(i%8, MeshCell.Shape(value)); Assert.AreEqual(i%32, MeshCell.Orient(value));
                Assert.AreEqual(i%64, MeshCell.Layer(value)); Assert.AreEqual(i, MeshCell.Colour(value)); Assert.AreEqual(i/4%4, MeshCell.Damage(value));
                Assert.AreEqual((i&16)!=0, MeshCell.Scorched(value)); Assert.AreEqual(0, value >> 27);
            }
        }
        static float3[] Table(string text, string name)
        {
            string body = Regex.Match(text, @"static const float3 " + name + @"\[42\]\s*=\s*\{(.*?)\};", RegexOptions.Singleline).Groups[1].Value;
            var rows = Regex.Matches(body, @"float3\(([^,]+),([^,]+),([^\)]+)\)"); var result = new float3[rows.Count];
            for (int i = 0; i < rows.Count; i++) result[i] = new float3(float.Parse(rows[i].Groups[1].Value, CultureInfo.InvariantCulture),
                float.Parse(rows[i].Groups[2].Value, CultureInfo.InvariantCulture), float.Parse(rows[i].Groups[3].Value, CultureInfo.InvariantCulture));
            return result;
        }
        [Test] public void Normals_MatchHlsl()
        {
            string text = File.ReadAllText(Path.Combine(Application.dataPath, "Game/Voxel/Rendering/Shaders/BrickVertexDecode.hlsl"));
            var normals = Table(text, "kBrickNormals"); var tangents = Table(text, "kBrickTangents");
            Assert.AreEqual(42, normals.Length); Assert.AreEqual(42, tangents.Length);
            for (int i = 0; i < 42; i++)
            {
                float3 normal = VoxelNormals.Normal(i), tangent = VoxelNormals.Tangent(i);
                Assert.Less(math.distance(normal, normals[i]), 1e-7f); Assert.AreEqual(tangent, tangents[i]);
                Assert.AreEqual(1f, math.length(normal), 1e-6); Assert.AreEqual(1f, math.length(tangent), 1e-6); Assert.AreEqual(0f, math.dot(normal,tangent), 1e-6);
                float scale = i < 6 ? 1 : i < 18 ? math.sqrt(2f) : math.sqrt(5f);
                Assert.AreEqual(i, VoxelNormals.IndexOf((int3)math.round(normal * scale)));
            }
            Assert.AreEqual(-1, VoxelNormals.IndexOf(int3.zero)); Assert.AreEqual(-1, VoxelNormals.IndexOf(new int3(1,1,1)));
        }
    }
}
