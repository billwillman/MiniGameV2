using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh
{
    public static class ClusterMeshTemplate
    {
        public static Mesh Create()
        {
            int count = ClusterMeshLimits.TemplateVertexCount;
            var mesh = new Mesh
            {
                name = "ClusterMeshTemplate",
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = IndexFormat.UInt32
            };
            var positions = new Vector3[count];
            var normals = new Vector3[count];
            var uvs = new Vector2[count];
            var tris = new int[count];
            for (int i = 0; i < count; i++)
            {
                normals[i] = Vector3.up;
                tris[i] = i;
            }

            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
            return mesh;
        }
    }
}
