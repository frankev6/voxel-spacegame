using UnityEngine;

namespace Game.Voxel.Rendering
{
    /// <summary>DMG-011: bounded, world-owned chunk draw resources; source materials are borrowed.</summary>
    internal sealed class ChunkRenderResources : ScriptableObject
    {
        internal readonly GraphicsBuffer[] Cells = new GraphicsBuffer[256];
        internal readonly GraphicsBuffer[] Matrices = new GraphicsBuffer[256];
        internal readonly MaterialPropertyBlock[] Properties = new MaterialPropertyBlock[256];
        internal readonly Matrix4x4[] Matrix = new Matrix4x4[1];
        internal Mesh Cube;
        internal void Ensure(int slot, int count)
        {
            if (Properties[slot] == null) Properties[slot] = new MaterialPropertyBlock();
            if (Matrices[slot] == null) Matrices[slot] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 64);
            if (Cells[slot] != null && Cells[slot].count >= count) return;
            Cells[slot]?.Dispose(); Cells[slot] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.NextPowerOfTwo(count), 16);
        }
        internal void Dispose()
        {
            for (int i = 0; i < Cells.Length; i++)
            { Cells[i]?.Dispose(); Cells[i] = null; Matrices[i]?.Dispose(); Matrices[i] = null; Properties[i] = null; }
            if (Cube) { if (Application.isPlaying) Destroy(Cube); else DestroyImmediate(Cube); Cube = null; }
        }
        void OnDestroy() => Dispose();
        internal static Mesh CreateCube()
        {
            var vertices = new Vector3[24]; var normals = new Vector3[24]; var uv = new Vector2[24]; var chunkFlag = new Vector2[24];
            var colors = new Color[24]; var triangles = new int[36];
            var directions = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int face = 0; face < 6; face++)
            {
                Vector3 n = directions[face], u = Mathf.Abs(n.y) > .5f ? Vector3.right : Vector3.up, v = Vector3.Cross(n, u);
                for (int corner = 0; corner < 4; corner++)
                {
                    float x = corner == 0 || corner == 3 ? -.5f : .5f;
                    float y = corner < 2 ? -.5f : .5f;
                    int i = face * 4 + corner; vertices[i] = n * .5f + u * x + v * y; normals[i] = n;
                    uv[i] = new Vector2(x + .5f, y + .5f); chunkFlag[i] = new Vector2(0, 8); colors[i] = new Color(1, 0, 0, 0);
                }
                int t = face * 6, b = face * 4;
                triangles[t] = b; triangles[t+1] = b+1; triangles[t+2] = b+2;
                triangles[t+3] = b; triangles[t+4] = b+2; triangles[t+5] = b+3;
            }
            var mesh = new Mesh { name = "Chunk unit cube", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.uv2 = chunkFlag; mesh.colors = colors; mesh.triangles = triangles;   // uv1.y = 8 (outside any lightmap range): chunk plating (QA-001)
            mesh.RecalculateBounds(); mesh.UploadMeshData(true); return mesh;
        }
    }
}
