using UnityEngine;
namespace Game.ShipEditor.Rendering
{
    internal static class EdgeCubeMesh
    {
        internal static Mesh Create()
        {
            var vertices = new Vector3[24]; var uv = new Vector2[24]; var triangles = new int[36];
            for (int face = 0; face < 6; face++)
            {
                int axis = face / 2, u = (axis + 1) % 3, v = (axis + 2) % 3, first = face * 4;
                for (int corner = 0; corner < 4; corner++)
                {
                    float x = corner == 1 || corner == 2 ? 1 : 0, y = corner >= 2 ? 1 : 0;
                    Vector3 p = default; p[axis] = face % 2; p[u] = x; p[v] = y;
                    vertices[first + corner] = p; uv[first + corner] = new Vector2(x, y);
                }
                int t = face * 6; triangles[t] = first; triangles[t+1] = first+1; triangles[t+2] = first+2;
                triangles[t+3] = first; triangles[t+4] = first+2; triangles[t+5] = first+3;
            }
            var mesh = new Mesh { name = "Editor overlay edge cube", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateBounds(); mesh.UploadMeshData(false); return mesh;
        }
    }
}
