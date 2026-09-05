using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshGeometry
    {
        const float NormalZEpsilon = 1e-4f;

        public static uint PackHalf2(float x, float y)
        {
            return (uint)Mathf.FloatToHalf(x) | ((uint)Mathf.FloatToHalf(y) << 16);
        }

        public static void UnpackHalf2(uint packed, out float x, out float y)
        {
            x = Mathf.HalfToFloat((ushort)(packed & 0xFFFFu));
            y = Mathf.HalfToFloat((ushort)(packed >> 16));
        }

        public static ClusterPackedVertex PackVertex(in ClusterVertex v)
        {
            Vector3 n = ((Vector3)v.normal).normalized;
            if (n.sqrMagnitude < 1e-12f)
                n = Vector3.up;

            Vector3 t = (Vector3)v.tangent;
            t -= n * Vector3.Dot(n, t);
            if (t.sqrMagnitude < 1e-12f)
                t = Vector3.Cross(n, Mathf.Abs(n.y) < 0.99f ? Vector3.up : Vector3.right);
            t.Normalize();

            float tanW = v.tangent.w >= 0f ? 1f : -1f;
            return new ClusterPackedVertex
            {
                position = new Vector4(v.position.x, v.position.y, v.position.z, t.z >= 0f ? 1f : -1f),
                nrmXY = PackHalf2(n.x, n.y),
                nrmZ_tanW = PackHalf2(n.z, tanW),
                tanXY = PackHalf2(t.x, t.y),
                uv = PackHalf2(v.uv.x, v.uv.y)
            };
        }

        public static ClusterVertex UnpackVertex(in ClusterPackedVertex p)
        {
            UnpackHalf2(p.nrmXY, out float nx, out float ny);
            UnpackHalf2(p.nrmZ_tanW, out float nz, out float tanW);
            UnpackHalf2(p.tanXY, out float tx, out float ty);
            UnpackHalf2(p.uv, out float u, out float v);

            Vector3 n = new Vector3(nx, ny, nz);
            if (n.sqrMagnitude < 1e-12f)
                n = Vector3.up;
            else
                n.Normalize();

            ReconstructTangent(n, tx, ty, p.position.w, out Vector3 t);

            return new ClusterVertex
            {
                position = new Vector4(p.position.x, p.position.y, p.position.z, 0f),
                normal = n,
                tangent = new Vector4(t.x, t.y, t.z, tanW >= 0f ? 1f : -1f),
                uv = new Vector4(u, v, 0f, 0f)
            };
        }

        public static void ReconstructTangent(Vector3 n, float tx, float ty, float tzSign, out Vector3 t)
        {
            float tz = Mathf.Abs(n.z) >= NormalZEpsilon
                ? -(n.x * tx + n.y * ty) / n.z
                : tzSign * Mathf.Sqrt(Mathf.Max(0f, 1f - tx * tx - ty * ty));
            t = new Vector3(tx, ty, tz);
            t -= n * Vector3.Dot(n, t);
            if (t.sqrMagnitude < 1e-12f)
                t = Vector3.Cross(n, Mathf.Abs(n.y) < 0.99f ? Vector3.up : Vector3.right);
            t.Normalize();
        }

        public static uint[] PackIndices(uint[] indices)
        {
            if (indices == null || indices.Length == 0)
                return Array.Empty<uint>();

            var packed = new uint[(indices.Length + 1) / 2];
            for (int i = 0; i < indices.Length; i++)
            {
                int slot = i >> 1;
                if ((i & 1) == 0)
                    packed[slot] = indices[i] & 0xFFFFu;
                else
                    packed[slot] |= (indices[i] & 0xFFFFu) << 16;
            }

            return packed;
        }

        public static uint[] UnpackIndices(uint[] packed, int indexCount)
        {
            if (indexCount <= 0)
                return Array.Empty<uint>();
            if (packed == null || packed.Length < (indexCount + 1) / 2)
                throw new InvalidOperationException("Packed index buffer is shorter than indexCount.");

            var indices = new uint[indexCount];
            for (int i = 0; i < indexCount; i++)
            {
                uint raw = packed[i >> 1];
                indices[i] = ((i & 1) == 0) ? (raw & 0xFFFFu) : (raw >> 16);
            }

            return indices;
        }

        public static byte[] Deflate(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                    deflate.Write(data, 0, data.Length);
                return output.ToArray();
            }
        }

        public static bool TryInflate(byte[] data, out byte[] raw)
        {
            raw = Array.Empty<byte>();
            if (data == null)
                return false;
            if (data.Length == 0)
                return true;

            try
            {
                using (var input = new MemoryStream(data))
                using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    deflate.CopyTo(output);
                    raw = output.ToArray();
                    return true;
                }
            }
            catch (InvalidDataException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        public static void WritePacked(ClusterMeshAsset asset, ClusterMeshBakeResult result)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            ClusterVertex[] verts = result.vertices ?? Array.Empty<ClusterVertex>();
            uint[] inds = result.indices ?? Array.Empty<uint>();
            var packedVerts = new ClusterPackedVertex[verts.Length];
            for (int i = 0; i < verts.Length; i++)
                packedVerts[i] = PackVertex(verts[i]);

            uint[] packedInds = PackIndices(inds);
            asset.geometryVersion = ClusterMeshLimits.GeometryVersion;
            asset.vertexCount = verts.Length;
            asset.indexCount = inds.Length;
            asset.packedVertices = Deflate(StructsToBytes(packedVerts));
            asset.packedIndices = Deflate(StructsToBytes(packedInds));
        }

        public static bool TryReadGpuGeometry(
            ClusterMeshAsset asset,
            out ClusterPackedVertex[] vertices,
            out uint[] packedIndices,
            out string error)
        {
            vertices = Array.Empty<ClusterPackedVertex>();
            packedIndices = Array.Empty<uint>();
            error = null;

            if (asset == null)
            {
                error = "ClusterMesh asset is missing or empty.";
                return false;
            }

            if (asset.geometryVersion != ClusterMeshLimits.GeometryVersion)
            {
                error = "ClusterMesh asset needs a rebake (packed geometry).";
                return false;
            }

            if (!TryInflate(asset.packedVertices, out byte[] vertBytes) ||
                !TryInflate(asset.packedIndices, out byte[] indexBytes))
            {
                error = "ClusterMesh asset packed geometry is corrupt.";
                return false;
            }

            int packedIndexCount = (asset.indexCount + 1) / 2;
            int expectedVert = asset.vertexCount * ClusterMeshLimits.ClusterVertexStride;
            int expectedIndex = packedIndexCount * 4;
            if (asset.vertexCount < 0 || asset.indexCount < 0 ||
                vertBytes.Length != expectedVert || indexBytes.Length != expectedIndex)
            {
                error = "ClusterMesh asset packed geometry is corrupt.";
                return false;
            }

            vertices = BytesToStructs<ClusterPackedVertex>(vertBytes, asset.vertexCount);
            packedIndices = BytesToStructs<uint>(indexBytes, packedIndexCount);
            return true;
        }

        public static bool TryReadWorkingGeometry(
            ClusterMeshAsset asset,
            out ClusterVertex[] vertices,
            out uint[] indices,
            out string error)
        {
            vertices = Array.Empty<ClusterVertex>();
            indices = Array.Empty<uint>();
            if (!TryReadGpuGeometry(asset, out ClusterPackedVertex[] packedVerts, out uint[] packedInds, out error))
                return false;

            vertices = new ClusterVertex[packedVerts.Length];
            for (int i = 0; i < packedVerts.Length; i++)
                vertices[i] = UnpackVertex(packedVerts[i]);
            indices = UnpackIndices(packedInds, asset.indexCount);
            return true;
        }

        static byte[] StructsToBytes<T>(T[] items) where T : struct
        {
            if (items == null || items.Length == 0)
                return Array.Empty<byte>();

            int stride = Marshal.SizeOf<T>();
            var bytes = new byte[items.Length * stride];
            var handle = GCHandle.Alloc(items, GCHandleType.Pinned);
            try
            {
                Marshal.Copy(handle.AddrOfPinnedObject(), bytes, 0, bytes.Length);
            }
            finally
            {
                handle.Free();
            }

            return bytes;
        }

        static T[] BytesToStructs<T>(byte[] bytes, int count) where T : struct
        {
            if (count <= 0)
                return Array.Empty<T>();

            var items = new T[count];
            var handle = GCHandle.Alloc(items, GCHandleType.Pinned);
            try
            {
                Marshal.Copy(bytes, 0, handle.AddrOfPinnedObject(), count * Marshal.SizeOf<T>());
            }
            finally
            {
                handle.Free();
            }

            return items;
        }
    }
}
