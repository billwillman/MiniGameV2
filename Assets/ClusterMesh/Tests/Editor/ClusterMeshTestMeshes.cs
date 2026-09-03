using UnityEngine;

namespace ClusterMesh.Tests
{
    public static class ClusterMeshTestMeshes
    {
        public static Mesh Triangle()
        {
            var mesh = new Mesh { name = "CMTestTriangle" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f)
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        public static Mesh TwoSubmeshTriangles()
        {
            var mesh = new Mesh { name = "CMTestTwoSubmesh" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f),
                new Vector3(2f, 0f, 0f), new Vector3(3f, 0f, 0f), new Vector3(2f, 1f, 0f)
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 3, 4, 5 }, 1);
            mesh.uv = new[]
            {
                Vector2.zero, Vector2.right, Vector2.up,
                Vector2.zero, Vector2.right, Vector2.up
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        public static Mesh DegeneratePlusReal()
        {
            var mesh = new Mesh { name = "CMTestDegenerate" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f)
            };
            mesh.triangles = new[] { 0, 0, 0, 0, 1, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Grid(int cols, int rows)
        {
            int vertsX = cols + 1;
            int vertsY = rows + 1;
            var positions = new Vector3[vertsX * vertsY];
            var uvs = new Vector2[positions.Length];
            for (int y = 0; y < vertsY; y++)
            {
                for (int x = 0; x < vertsX; x++)
                {
                    int i = y * vertsX + x;
                    positions[i] = new Vector3(x, y, 0f);
                    uvs[i] = new Vector2(x / (float)cols, y / (float)rows);
                }
            }

            var tris = new int[cols * rows * 6];
            int t = 0;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int i = y * vertsX + x;
                    tris[t++] = i;
                    tris[t++] = i + vertsX;
                    tris[t++] = i + 1;
                    tris[t++] = i + 1;
                    tris[t++] = i + vertsX;
                    tris[t++] = i + vertsX + 1;
                }
            }

            var mesh = new Mesh { name = "CMTestGrid" };
            mesh.vertices = positions;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
