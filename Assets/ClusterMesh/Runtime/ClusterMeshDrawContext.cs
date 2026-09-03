using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh
{
    public sealed class ClusterMeshDrawContext : IDisposable
    {
        static readonly int ClustersId = Shader.PropertyToID("_Clusters");
        static readonly int VerticesId = Shader.PropertyToID("_Vertices");
        static readonly int IndicesId = Shader.PropertyToID("_Indices");
        static readonly int VisibleId = Shader.PropertyToID("_VisibleClusterIds");
        static readonly int ClusterCountId = Shader.PropertyToID("_ClusterCount");
        static readonly int MaterialIndexId = Shader.PropertyToID("_MaterialIndex");
        static readonly int IsolateIndexId = Shader.PropertyToID("_IsolateIndex");
        static readonly int PlanesId = Shader.PropertyToID("_Planes");
        static readonly int LocalCameraPosId = Shader.PropertyToID("_LocalCameraPos");
        static readonly int LocalToWorldId = Shader.PropertyToID("_ClusterLocalToWorld");
        static readonly int WorldToLocalId = Shader.PropertyToID("_ClusterWorldToLocal");

        readonly ClusterMeshAsset _asset;
        readonly ComputeShader _cullShader;
        readonly int _cullKernel;
        readonly Mesh _template;
        readonly GraphicsBuffer _clusterBuffer;
        readonly GraphicsBuffer _vertexBuffer;
        readonly GraphicsBuffer _indexBuffer;
        readonly GraphicsBuffer[] _visibleBuffers;
        readonly GraphicsBuffer[] _argsBuffers;
        readonly Material[] _materials;
        readonly Plane[] _planes = new Plane[6];
        readonly Vector4[] _planeVectors = new Vector4[6];
        readonly uint[] _argsSeed = new uint[5];
        bool _disposed;

        public bool IsReady { get; private set; }
        public string Error { get; }
        public int IsolateIndex { get; set; } = -1;

        public ClusterMeshDrawContext(ClusterMeshAsset asset, ComputeShader cullShader, Shader litShader)
        {
            _asset = asset;
            _cullShader = cullShader;
            if (asset == null || asset.clusters == null || asset.clusters.Length == 0)
            {
                Error = "ClusterMesh asset is missing or empty.";
                return;
            }

            string reason = ClusterMeshCapability.GetUnsupportedReason();
            if (reason != null)
            {
                Error = reason;
                return;
            }

            if (cullShader == null || litShader == null)
            {
                Error = "ClusterMesh shaders are not assigned.";
                return;
            }

            _cullKernel = cullShader.FindKernel("CullClusters");
            _template = ClusterMeshTemplate.Create();

            _clusterBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, asset.clusters.Length, 96);
            _clusterBuffer.SetData(asset.clusters);
            _vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, asset.vertices.Length), 64);
            if (asset.vertices.Length > 0)
                _vertexBuffer.SetData(asset.vertices);
            _indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, asset.indices.Length), 4);
            if (asset.indices.Length > 0)
                _indexBuffer.SetData(asset.indices);

            int materialCount = Mathf.Max(1, asset.materials != null ? asset.materials.Length : 1);
            _materials = new Material[materialCount];
            _argsBuffers = new GraphicsBuffer[materialCount];
            _visibleBuffers = new GraphicsBuffer[materialCount];
            _argsSeed[0] = (uint)ClusterMeshLimits.TemplateVertexCount;
            for (int i = 0; i < materialCount; i++)
            {
                Material source = asset.materials != null && i < asset.materials.Length ? asset.materials[i] : null;
                _materials[i] = ClusterMeshMaterialUtil.CreateRuntimeMaterial(source, litShader);
                _argsBuffers[i] = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 20);
                _argsBuffers[i].SetData(_argsSeed);
                _visibleBuffers[i] = new GraphicsBuffer(
                    GraphicsBuffer.Target.Append | GraphicsBuffer.Target.Structured,
                    asset.clusters.Length,
                    4);
            }

            IsReady = true;
        }

        public void Draw(Matrix4x4 localToWorld, Camera camera)
        {
            if (!IsReady || camera == null)
                return;

            Matrix4x4 worldToLocal = localToWorld.inverse;
            ClusterMeshFrustum.CameraToLocalPlanes(camera, localToWorld, _planes);
            for (int i = 0; i < 6; i++)
                _planeVectors[i] = new Vector4(_planes[i].normal.x, _planes[i].normal.y, _planes[i].normal.z, _planes[i].distance);

            Bounds worldBounds = TransformBounds(localToWorld);
            int groups = Mathf.CeilToInt(_asset.clusters.Length / 64f);

            for (int materialIndex = 0; materialIndex < _materials.Length; materialIndex++)
            {
                GraphicsBuffer visible = _visibleBuffers[materialIndex];
                visible.SetCounterValue(0);
                _cullShader.SetBuffer(_cullKernel, ClustersId, _clusterBuffer);
                _cullShader.SetBuffer(_cullKernel, VisibleId, visible);
                _cullShader.SetInt(ClusterCountId, _asset.clusters.Length);
                _cullShader.SetInt(MaterialIndexId, materialIndex);
                _cullShader.SetInt(IsolateIndexId, IsolateIndex);
                _cullShader.SetVectorArray(PlanesId, _planeVectors);
                _cullShader.SetVector(LocalCameraPosId, worldToLocal.MultiplyPoint3x4(camera.transform.position));
                _cullShader.Dispatch(_cullKernel, groups, 1, 1);

                _argsBuffers[materialIndex].SetData(_argsSeed);
                GraphicsBuffer.CopyCount(visible, _argsBuffers[materialIndex], 4);

                Material mat = _materials[materialIndex];
                mat.SetBuffer(ClustersId, _clusterBuffer);
                mat.SetBuffer(VerticesId, _vertexBuffer);
                mat.SetBuffer(IndicesId, _indexBuffer);
                mat.SetBuffer(VisibleId, visible);
                mat.SetMatrix(LocalToWorldId, localToWorld);
                mat.SetMatrix(WorldToLocalId, worldToLocal);

                Graphics.DrawMeshInstancedIndirect(
                    _template,
                    0,
                    mat,
                    worldBounds,
                    _argsBuffers[materialIndex],
                    0,
                    null,
                    ShadowCastingMode.On,
                    true,
                    0,
                    camera);
            }
        }

        Bounds TransformBounds(Matrix4x4 localToWorld)
        {
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < _asset.clusters.Length; i++)
            {
                Vector3 c = _asset.clusters[i].aabbCenter;
                Vector3 e = _asset.clusters[i].aabbExtents;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 w = localToWorld.MultiplyPoint3x4(c + Vector3.Scale(e, new Vector3(x, y, z)));
                    min = Vector3.Min(min, w);
                    max = Vector3.Max(max, w);
                }
            }

            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            IsReady = false;
            _clusterBuffer?.Dispose();
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
            if (_visibleBuffers != null)
            {
                for (int i = 0; i < _visibleBuffers.Length; i++)
                    _visibleBuffers[i]?.Dispose();
            }

            if (_argsBuffers != null)
            {
                for (int i = 0; i < _argsBuffers.Length; i++)
                    _argsBuffers[i]?.Dispose();
            }

            if (_materials != null)
            {
                for (int i = 0; i < _materials.Length; i++)
                {
                    if (_materials[i] != null)
                        DestroyUnityObject(_materials[i]);
                }
            }

            if (_template != null)
                DestroyUnityObject(_template);
        }

        static void DestroyUnityObject(UnityEngine.Object obj)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
