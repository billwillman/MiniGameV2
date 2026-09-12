using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh
{
    public sealed class ClusterMeshDrawContext : IDisposable
    {
        static readonly int ClustersId = Shader.PropertyToID("_Clusters");
        static readonly int GroupsId = Shader.PropertyToID("_Groups");
        static readonly int OwningGroupsId = Shader.PropertyToID("_OwningGroups");
        static readonly int GroupCountId = Shader.PropertyToID("_GroupCount");
        static readonly int VerticesId = Shader.PropertyToID("_Vertices");
        static readonly int VerticesTightId = Shader.PropertyToID("_VerticesTight");
        static readonly int RestVertexTightId = Shader.PropertyToID("_RestVertexTight");
        const string ReceiveShadowsOffKeyword = "_RECEIVE_SHADOWS_OFF";
        static readonly int IndicesId = Shader.PropertyToID("_Indices");
        static readonly int VisibleId = Shader.PropertyToID("_VisibleClusterIds");
        static readonly int ShadowVisibleId = Shader.PropertyToID("_ShadowClusterIds");
        static readonly int ShadowPlanesId = Shader.PropertyToID("_ShadowPlanes");
        static readonly int EnableShadowListId = Shader.PropertyToID("_EnableShadowList");
        static readonly int ClusterCountId = Shader.PropertyToID("_ClusterCount");
        static readonly int ObjectCountId = Shader.PropertyToID("_ObjectCount");
        static readonly int MaterialIndexId = Shader.PropertyToID("_MaterialIndex");
        static readonly int IsolateIndexId = Shader.PropertyToID("_IsolateIndex");
        static readonly int EnableConeCullId = Shader.PropertyToID("_EnableConeCull");
        static readonly int HierarchyVersionId = Shader.PropertyToID("_HierarchyVersion");
        static readonly int LodPerspectiveId = Shader.PropertyToID("_LodPerspective");
        static readonly int LodErrorThresholdId = Shader.PropertyToID("_LodErrorThreshold");
        static readonly int LodProjectionScaleId = Shader.PropertyToID("_LodProjectionScale");
        static readonly int EnableClusterColorId = Shader.PropertyToID("_EnableClusterColor");
        static readonly int PlanesId = Shader.PropertyToID("_Planes");
        static readonly int WorldCameraPosId = Shader.PropertyToID("_WorldCameraPos");
        static readonly int ObjectLocalToWorldId = Shader.PropertyToID("_ObjectLocalToWorld");
        static readonly int ObjectPreviousLocalToWorldId = Shader.PropertyToID("_ObjectPreviousLocalToWorld");
        static readonly int ObjectMotionVectorEnabledId = Shader.PropertyToID("_ObjectMotionVectorEnabled");
        static readonly int ObjectCameraCullFlagsId = Shader.PropertyToID("_ObjectCameraCullFlags");
        static readonly int ObjectWorldToLocalId = Shader.PropertyToID("_ObjectWorldToLocal");
        static readonly int ObjectSHId = Shader.PropertyToID("_ObjectSH");

        readonly ClusterMeshAsset _asset;
        readonly ComputeShader _cullShader;
        readonly int _cullKernel;
        readonly Mesh _template;
        readonly GraphicsBuffer _clusterBuffer;
        readonly GraphicsBuffer _groupBuffer;
        readonly GraphicsBuffer _owningGroupBuffer;
        readonly GraphicsBuffer _objectCameraCullFlagsBuffer;
        readonly GraphicsBuffer _objectSHBuffer;
        readonly Bounds _localBounds;
        readonly GraphicsBuffer _vertexBuffer;
        readonly GraphicsBuffer _vertexTightBuffer;
        readonly GraphicsBuffer _indexBuffer;
        readonly bool _restVertexTight;
        readonly GraphicsBuffer[] _visibleBuffers;
        readonly GraphicsBuffer[] _shadowVisibleBuffers;
        readonly GraphicsBuffer[] _argsBuffers;
        readonly GraphicsBuffer[] _shadowArgsBuffers;
        readonly Material[] _materials;
        readonly Material[] _shadowMaterials;
        readonly Plane[] _planes = new Plane[6];
        readonly Plane[] _receiverPlanes = new Plane[6];
        readonly Plane[] _shadowPlanes = new Plane[6];
        readonly Vector4[] _planeVectors = new Vector4[6];
        readonly Vector4[] _shadowPlaneVectors = new Vector4[6];
        readonly uint[] _argsSeed = new uint[5];
        readonly Matrix4x4[] _single = new Matrix4x4[1];
        readonly bool[] _singleCull = { true };
        readonly Matrix4x4[] _l2w = new Matrix4x4[ClusterMeshLimits.MaxBatchedObjects];
        readonly Matrix4x4[] _previousL2w = new Matrix4x4[ClusterMeshLimits.MaxBatchedObjects];
        readonly Matrix4x4[] _w2l = new Matrix4x4[ClusterMeshLimits.MaxBatchedObjects];
        readonly float[] _motionVectorEnabled = new float[ClusterMeshLimits.MaxBatchedObjects];
        readonly uint[] _objectCameraCullFlags = new uint[ClusterMeshLimits.MaxBatchedObjects];
        readonly ClusterMeshObjectSH[] _objectSH = new ClusterMeshObjectSH[ClusterMeshLimits.MaxBatchedObjects];
        readonly List<UrpChunk> _urpChunks = new List<UrpChunk>();
        readonly List<GraphicsBuffer> _extraArgs = new List<GraphicsBuffer>();
        readonly List<GraphicsBuffer> _extraVisible = new List<GraphicsBuffer>();
        int _visibleCapacity;
        bool _preparedSplit;
        bool _preparedCast;
        bool _preparedReceive;
        bool _preparedHasMotionVectors;
        bool _disposed;

        const int ForwardShaderPass = 0;
        const int DepthShaderPass = 2;
        const int GBufferShaderPass = 3;
        const int MotionVectorShaderPass = 4;

        sealed class UrpChunk
        {
            public int n;
            public bool hasMotionVectors;
            public Bounds bounds;
            public Matrix4x4[] l2w;
            public Matrix4x4[] previousL2w;
            public Matrix4x4[] w2l;
            public float[] motionVectorEnabled;
            public ClusterMeshObjectSH[] objectSH;
            public GraphicsBuffer[] colorArgs;
            public GraphicsBuffer[] shadowArgs;
            public GraphicsBuffer[] visible;
            public GraphicsBuffer[] shadowVisible;
        }

        public bool IsReady { get; private set; }
        public bool CanDraw
        {
            get
            {
                if (!IsReady || _disposed)
                    return false;
                if (_template == null)
                    return false;
                return MaterialsAlive(_materials) && MaterialsAlive(_shadowMaterials);
            }
        }
        public string Error { get; }
        public int IsolateIndex { get; set; } = -1;
        public bool EnableConeCull { get; set; } = true;
        public bool EnableClusterColor { get; set; }
        public float LodErrorThreshold { get; set; }
#if UNITY_EDITOR
        public int EditorDrawLayer { get; set; }
#endif

        public ClusterMeshDrawContext(ClusterMeshAsset asset, ComputeShader cullShader, Shader litShader)
        {
            _asset = asset;
            _cullShader = cullShader;
            if (asset == null || asset.clusters == null || asset.clusters.Length == 0)
            {
                Error = "ClusterMesh asset is missing or empty.";
                return;
            }

            bool tightRest = asset.ResolvedVertexStride == ClusterMeshLimits.TightVertexStride;
            ClusterPackedVertex[] packedVerts = Array.Empty<ClusterPackedVertex>();
            ClusterPackedVertexTight[] tightVerts = Array.Empty<ClusterPackedVertexTight>();
            uint[] packedIndices;
            if (tightRest)
            {
                if (!ClusterMeshGeometry.TryReadTightVertices(asset, out tightVerts, out string tightError))
                {
                    Error = tightError;
                    return;
                }

                if (!ClusterMeshGeometry.TryReadPackedIndices(asset, out packedIndices, out string indexError))
                {
                    Error = indexError;
                    return;
                }
            }
            else if (!ClusterMeshGeometry.TryReadGpuGeometry(asset, out packedVerts, out packedIndices, out string geoError))
            {
                Error = geoError;
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

            _clusterBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, asset.clusters.Length, ClusterMeshLimits.ClusterHeaderStride);
            _clusterBuffer.SetData(asset.clusters);
            int groupCount = asset.groups != null ? asset.groups.Length : 0;
            _groupBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, groupCount), ClusterMeshLimits.ClusterGroupStride);
            if (groupCount > 0)
                _groupBuffer.SetData(asset.groups);
            else
                _groupBuffer.SetData(new ClusterGroup[1]);
            int[] owning = ClusterMeshLod.BuildOwningGroupIndices(asset.clusters.Length, asset.groups);
            _owningGroupBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, owning.Length), 4);
            if (owning.Length > 0)
                _owningGroupBuffer.SetData(owning);
            else
                _owningGroupBuffer.SetData(new[] { ClusterMeshLod.NoParent });
            _objectCameraCullFlagsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured, ClusterMeshLimits.MaxBatchedObjects, 4);
            _objectSHBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured, ClusterMeshLimits.MaxBatchedObjects, ClusterMeshLimits.ObjectSHStride);
            _localBounds = ClusterMeshFrustum.AssetLocalBounds(asset);
            _restVertexTight = tightRest;
            if (tightRest)
            {
                _vertexBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured, 1, ClusterMeshLimits.ClusterVertexStride);
                _vertexBuffer.SetData(new ClusterPackedVertex[1]);
                _vertexTightBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    Mathf.Max(1, tightVerts.Length),
                    ClusterMeshLimits.TightVertexStride);
                _vertexTightBuffer.SetData(tightVerts);
            }
            else
            {
                _vertexBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    Mathf.Max(1, packedVerts.Length),
                    ClusterMeshLimits.ClusterVertexStride);
                if (packedVerts.Length > 0)
                    _vertexBuffer.SetData(packedVerts);
                _vertexTightBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured, 1, ClusterMeshLimits.TightVertexStride);
                _vertexTightBuffer.SetData(new ClusterPackedVertexTight[1]);
            }
            _indexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                Mathf.Max(1, packedIndices.Length),
                4);
            if (packedIndices.Length > 0)
                _indexBuffer.SetData(packedIndices);

            int materialCount = Mathf.Max(1, asset.materials != null ? asset.materials.Length : 1);
            int visibleCapacity = Mathf.Max(1, asset.clusters.Length) * ClusterMeshLimits.MaxBatchedObjects;
            _visibleCapacity = visibleCapacity;
            _materials = new Material[materialCount];
            _shadowMaterials = new Material[materialCount];
            _argsBuffers = new GraphicsBuffer[materialCount];
            _shadowArgsBuffers = new GraphicsBuffer[materialCount];
            _visibleBuffers = new GraphicsBuffer[materialCount];
            _shadowVisibleBuffers = new GraphicsBuffer[materialCount];
            _argsSeed[0] = (uint)ClusterMeshLimits.TemplateVertexCount;
            for (int i = 0; i < materialCount; i++)
            {
                Material source = asset.materials != null && i < asset.materials.Length ? asset.materials[i] : null;
                _materials[i] = ClusterMeshMaterialUtil.CreateRuntimeMaterial(source, litShader);
                _shadowMaterials[i] = ClusterMeshMaterialUtil.CreateRuntimeMaterial(source, litShader);
                _argsBuffers[i] = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 20);
                _argsBuffers[i].SetData(_argsSeed);
                _shadowArgsBuffers[i] = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 20);
                _shadowArgsBuffers[i].SetData(_argsSeed);
                _visibleBuffers[i] = new GraphicsBuffer(
                    GraphicsBuffer.Target.Append | GraphicsBuffer.Target.Structured,
                    visibleCapacity,
                    4);
                _shadowVisibleBuffers[i] = new GraphicsBuffer(
                    GraphicsBuffer.Target.Append | GraphicsBuffer.Target.Structured,
                    visibleCapacity,
                    4);
            }

            IsReady = true;
        }

        public void Draw(Matrix4x4 localToWorld, Camera camera, bool castShadows = true, bool receiveShadows = true)
        {
            _single[0] = localToWorld;
            Draw(_single, null, null, _singleCull, null, 1, camera, camera, castShadows, receiveShadows);
        }

        public void Draw(IList<Matrix4x4> localToWorld, Camera camera, bool castShadows = true, bool receiveShadows = true)
        {
            if (localToWorld == null)
                return;
            Draw(localToWorld, null, null, null, null, localToWorld.Count, camera, camera, castShadows, receiveShadows);
        }

        public void Draw(
            IList<Matrix4x4> localToWorld,
            IList<bool> enableCpuObjectCull,
            Camera camera,
            bool castShadows = true,
            bool receiveShadows = true)
        {
            if (localToWorld == null)
                return;
            Draw(localToWorld, null, null, enableCpuObjectCull, null, localToWorld.Count, camera, camera, castShadows, receiveShadows);
        }

        public void Draw(
            IList<Matrix4x4> localToWorld,
            IList<bool> enableCpuObjectCull,
            IList<bool> enableCameraCull,
            Camera camera,
            bool castShadows = true,
            bool receiveShadows = true)
        {
            if (localToWorld == null)
                return;
            Draw(localToWorld, null, null, enableCpuObjectCull, enableCameraCull, localToWorld.Count,
                camera, camera, castShadows, receiveShadows);
        }

        public void DrawMotion(
            IList<Matrix4x4> localToWorld,
            IList<Matrix4x4> previousLocalToWorld,
            IList<bool> enableMotionVectors,
            IList<bool> enableCpuObjectCull,
            IList<bool> enableCameraCull,
            Camera camera,
            bool castShadows = true,
            bool receiveShadows = true)
        {
            if (localToWorld == null)
                return;
            Draw(localToWorld, previousLocalToWorld, enableMotionVectors,
                enableCpuObjectCull, enableCameraCull, localToWorld.Count,
                camera, camera, castShadows, receiveShadows);
        }

#if UNITY_EDITOR
        public void DrawEditorPreview(
            IList<Matrix4x4> localToWorld,
            IList<bool> enableCpuObjectCull,
            IList<bool> enableCameraCull,
            Camera cullingCamera,
            Camera drawCamera,
            bool castShadows = true,
            bool receiveShadows = true)
        {
            if (localToWorld == null)
                return;
            Draw(
                localToWorld, null, null, enableCpuObjectCull, enableCameraCull, localToWorld.Count,
                cullingCamera, drawCamera, castShadows, receiveShadows);
        }

        public void DrawEditorPreviewMotion(
            IList<Matrix4x4> localToWorld,
            IList<Matrix4x4> previousLocalToWorld,
            IList<bool> enableMotionVectors,
            IList<bool> enableCpuObjectCull,
            IList<bool> enableCameraCull,
            Camera cullingCamera,
            Camera drawCamera,
            bool castShadows = true,
            bool receiveShadows = true)
        {
            if (localToWorld == null)
                return;
            Draw(
                localToWorld, previousLocalToWorld, enableMotionVectors,
                enableCpuObjectCull, enableCameraCull, localToWorld.Count,
                cullingCamera, drawCamera, castShadows, receiveShadows);
        }
#endif

        void Draw(
            IList<Matrix4x4> localToWorld,
            IList<Matrix4x4> previousLocalToWorld,
            IList<bool> enableMotionVectors,
            IList<bool> enableCpuObjectCull,
            IList<bool> enableCameraCull,
            int count,
            Camera cullingCamera,
            Camera drawCamera,
            bool castShadows,
            bool receiveShadows)
        {
            if (drawCamera == null || !CanDraw)
                return;
            if (!PrepareChunks(localToWorld, previousLocalToWorld, enableMotionVectors,
                    enableCpuObjectCull, enableCameraCull, count, cullingCamera, castShadows, receiveShadows))
                return;
            for (int i = 0; i < _urpChunks.Count; i++)
                SubmitLegacy(_urpChunks[i], drawCamera);
        }

        public bool PrepareUrp(
            IList<Matrix4x4> localToWorld,
            IList<bool> enableCpuObjectCull,
            Camera camera,
            bool castShadows,
            bool receiveShadows)
        {
            return PrepareUrp(localToWorld, enableCpuObjectCull, null, camera, castShadows, receiveShadows);
        }

        public bool PrepareUrp(
            IList<Matrix4x4> localToWorld,
            IList<bool> enableCpuObjectCull,
            IList<bool> enableCameraCull,
            Camera camera,
            bool castShadows,
            bool receiveShadows)
        {
            if (localToWorld == null)
                return false;
            if (!PrepareChunks(localToWorld, null, null, enableCpuObjectCull, enableCameraCull,
                    localToWorld.Count, camera, castShadows, receiveShadows))
                return false;
            for (int i = 0; i < _urpChunks.Count; i++)
                SubmitUrpShadows(_urpChunks[i], camera);
            return true;
        }

        public bool PrepareUrpMotion(
            IList<Matrix4x4> localToWorld,
            IList<Matrix4x4> previousLocalToWorld,
            IList<bool> enableMotionVectors,
            IList<bool> enableCpuObjectCull,
            IList<bool> enableCameraCull,
            Camera camera,
            bool castShadows,
            bool receiveShadows)
        {
            if (localToWorld == null)
                return false;
            if (!PrepareChunks(localToWorld, previousLocalToWorld, enableMotionVectors,
                    enableCpuObjectCull, enableCameraCull, localToWorld.Count,
                    camera, castShadows, receiveShadows))
                return false;
            for (int i = 0; i < _urpChunks.Count; i++)
                SubmitUrpShadows(_urpChunks[i], camera);
            return true;
        }

        public void SubmitUrpDepth(CommandBuffer cmd)
        {
            if (cmd == null)
                return;
            for (int i = 0; i < _urpChunks.Count; i++)
                SubmitCmd(_urpChunks[i], cmd, DepthShaderPass);
        }

        public void SubmitUrpColor(CommandBuffer cmd)
        {
            if (cmd == null)
                return;
            for (int i = 0; i < _urpChunks.Count; i++)
                SubmitCmd(_urpChunks[i], cmd, ForwardShaderPass);
        }

        public void SubmitUrpGBuffer(CommandBuffer cmd)
        {
            if (cmd == null)
                return;
            for (int i = 0; i < _urpChunks.Count; i++)
                SubmitCmd(_urpChunks[i], cmd, GBufferShaderPass);
        }

        public void SubmitUrpMotionVectors(CommandBuffer cmd)
        {
            if (cmd == null || !_preparedHasMotionVectors)
                return;
            for (int i = 0; i < _urpChunks.Count; i++)
            {
                if (_urpChunks[i].hasMotionVectors)
                    SubmitCmd(_urpChunks[i], cmd, MotionVectorShaderPass);
            }
        }

        bool PrepareChunks(
            IList<Matrix4x4> localToWorld,
            IList<Matrix4x4> previousLocalToWorld,
            IList<bool> enableMotionVectors,
            IList<bool> enableCpuObjectCull,
            IList<bool> enableCameraCull,
            int count,
            Camera camera,
            bool castShadows,
            bool receiveShadows)
        {
            ReleaseExtras();
            _urpChunks.Clear();
            if (!CanDraw || camera == null || count <= 0)
                return false;

            ClusterMeshFrustum.WorldPlanes(camera, _planes);
            CopyPlanes(_planes, _planeVectors);

            bool splitShadows = false;
            if (castShadows && ClusterMeshObjectCull.TryGetMainDirectionalShadowLight(out Light sun) && sun != null)
            {
                splitShadows = true;
                ClusterMeshObjectCull.BuildReceiverFrustumPlanes(
                    camera, ClusterMeshObjectCull.ShadowDistance(camera), _receiverPlanes);
                ClusterMeshObjectCull.ExtrudePlanesToward(_receiverPlanes, -sun.transform.forward, _shadowPlanes);
                CopyPlanes(_shadowPlanes, _shadowPlaneVectors);
            }

            _preparedSplit = splitShadows;
            _preparedCast = castShadows;
            _preparedReceive = receiveShadows;
            _preparedHasMotionVectors = false;
            bool failSafe = castShadows && !splitShadows;
            int chunkSize = ClusterMeshLimits.MaxBatchedObjects;
            int chunks = Mathf.CeilToInt(count / (float)chunkSize);
            for (int chunk = 0; chunk < chunks; chunk++)
            {
                int start = chunk * chunkSize;
                int raw = Mathf.Min(chunkSize, count - start);
                Bounds worldBounds = new Bounds();
                bool hasBounds = false;
                bool chunkHasMotionVectors = false;
                int n = 0;
                for (int i = 0; i < raw; i++)
                {
                    Matrix4x4 l2w = localToWorld[start + i];
                    Bounds b = TransformBounds(l2w);
                    bool cameraCull = enableCameraCull == null
                        || start + i >= enableCameraCull.Count
                        || enableCameraCull[start + i];
                    bool enableCull = cameraCull && (enableCpuObjectCull == null
                        || start + i >= enableCpuObjectCull.Count
                        || enableCpuObjectCull[start + i]);
                    if (!failSafe && !ClusterMeshObjectCull.KeepObject(b, _planes, enableCull, splitShadows, _shadowPlanes))
                        continue;
                    _l2w[n] = l2w;
                    _previousL2w[n] = previousLocalToWorld != null && start + i < previousLocalToWorld.Count
                        ? previousLocalToWorld[start + i]
                        : l2w;
                    _w2l[n] = l2w.inverse;
                    _motionVectorEnabled[n] = enableMotionVectors != null &&
                        start + i < enableMotionVectors.Count && enableMotionVectors[start + i] ? 1f : 0f;
                    _preparedHasMotionVectors |= _motionVectorEnabled[n] > 0.5f;
                    chunkHasMotionVectors |= _motionVectorEnabled[n] > 0.5f;
                    _objectCameraCullFlags[n] = cameraCull ? 1u : 0u;
                    ClusterMeshLightProbes.Pack(
                        ClusterMeshLightProbes.Evaluate(l2w.GetColumn(3)), out _objectSH[n]);
                    if (!hasBounds)
                    {
                        worldBounds = b;
                        hasBounds = true;
                    }
                    else
                        worldBounds.Encapsulate(b);
                    n++;
                }

                if (n <= 0)
                    continue;

                var stored = new UrpChunk
                {
                    n = n,
                    hasMotionVectors = chunkHasMotionVectors,
                    bounds = worldBounds,
                    l2w = new Matrix4x4[n],
                    previousL2w = chunkHasMotionVectors ? new Matrix4x4[n] : null,
                    w2l = new Matrix4x4[n],
                    motionVectorEnabled = chunkHasMotionVectors ? new float[n] : null,
                    objectSH = new ClusterMeshObjectSH[n],
                    colorArgs = new GraphicsBuffer[_materials.Length],
                    shadowArgs = new GraphicsBuffer[_materials.Length],
                    visible = new GraphicsBuffer[_materials.Length],
                    shadowVisible = new GraphicsBuffer[_materials.Length]
                };
                Array.Copy(_l2w, stored.l2w, n);
                Array.Copy(_w2l, stored.w2l, n);
                Array.Copy(_objectSH, stored.objectSH, n);
                if (chunkHasMotionVectors)
                {
                    Array.Copy(_previousL2w, stored.previousL2w, n);
                    Array.Copy(_motionVectorEnabled, stored.motionVectorEnabled, n);
                }
                bool primary = chunk == 0;
                for (int materialIndex = 0; materialIndex < _materials.Length; materialIndex++)
                {
                    stored.visible[materialIndex] = primary
                        ? _visibleBuffers[materialIndex]
                        : AllocVisible();
                    stored.shadowVisible[materialIndex] = primary
                        ? _shadowVisibleBuffers[materialIndex]
                        : AllocVisible();
                    stored.colorArgs[materialIndex] = primary
                        ? _argsBuffers[materialIndex]
                        : AllocExtraArgs();
                    stored.shadowArgs[materialIndex] = primary
                        ? _shadowArgsBuffers[materialIndex]
                        : AllocExtraArgs();
                }

                DispatchCull(n, camera, splitShadows, stored.visible, stored.shadowVisible);
                for (int materialIndex = 0; materialIndex < _materials.Length; materialIndex++)
                {
                    stored.colorArgs[materialIndex].SetData(_argsSeed);
                    GraphicsBuffer.CopyCount(stored.visible[materialIndex], stored.colorArgs[materialIndex], 4);
                    if (splitShadows)
                    {
                        stored.shadowArgs[materialIndex].SetData(_argsSeed);
                        GraphicsBuffer.CopyCount(stored.shadowVisible[materialIndex], stored.shadowArgs[materialIndex], 4);
                    }
                }

                _urpChunks.Add(stored);
            }

            return _urpChunks.Count > 0;
        }

        void DispatchCull(
            int n,
            Camera camera,
            bool splitShadows,
            GraphicsBuffer[] visibleBuffers,
            GraphicsBuffer[] shadowVisibleBuffers)
        {
            int groups = Mathf.CeilToInt((n * _asset.clusters.Length) / 64f);
            _cullShader.SetBuffer(_cullKernel, ClustersId, _clusterBuffer);
            _cullShader.SetBuffer(_cullKernel, GroupsId, _groupBuffer);
            _cullShader.SetBuffer(_cullKernel, OwningGroupsId, _owningGroupBuffer);
            _objectCameraCullFlagsBuffer.SetData(_objectCameraCullFlags, 0, 0, n);
            _cullShader.SetBuffer(_cullKernel, ObjectCameraCullFlagsId, _objectCameraCullFlagsBuffer);
            _cullShader.SetInt(ObjectCountId, n);
            _cullShader.SetInt(ClusterCountId, _asset.clusters.Length);
            _cullShader.SetInt(GroupCountId, _asset.groups != null ? _asset.groups.Length : 0);
            _cullShader.SetInt(IsolateIndexId, IsolateIndex);
            _cullShader.SetInt(EnableConeCullId, EnableConeCull ? 1 : 0);
            _cullShader.SetInt(EnableShadowListId, splitShadows ? 1 : 0);
            _cullShader.SetInt(HierarchyVersionId, _asset.hierarchyVersion);
            _cullShader.SetInt(LodPerspectiveId, camera.orthographic ? 0 : 1);
            _cullShader.SetFloat(LodErrorThresholdId, LodErrorThreshold);
            _cullShader.SetFloat(LodProjectionScaleId, ClusterMeshLod.ProjectionScale(camera));
            _cullShader.SetVectorArray(PlanesId, _planeVectors);
            _cullShader.SetVectorArray(ShadowPlanesId, _shadowPlaneVectors);
            _cullShader.SetVector(WorldCameraPosId, camera.transform.position);
            _cullShader.SetMatrixArray(ObjectLocalToWorldId, _l2w);

            for (int materialIndex = 0; materialIndex < _materials.Length; materialIndex++)
            {
                GraphicsBuffer visible = visibleBuffers[materialIndex];
                GraphicsBuffer shadowVisible = shadowVisibleBuffers[materialIndex];
                visible.SetCounterValue(0);
                shadowVisible.SetCounterValue(0);
                _cullShader.SetBuffer(_cullKernel, VisibleId, visible);
                _cullShader.SetBuffer(_cullKernel, ShadowVisibleId, shadowVisible);
                _cullShader.SetInt(MaterialIndexId, materialIndex);
                _cullShader.Dispatch(_cullKernel, Mathf.Max(1, groups), 1, 1);
            }
        }

        void SubmitLegacy(UrpChunk chunk, Camera camera)
        {
            if (!CanDraw)
                return;
            RestoreChunk(chunk);
            for (int materialIndex = 0; materialIndex < _materials.Length; materialIndex++)
            {
                Material colorMat = _materials[materialIndex];
                BindDrawMaterial(colorMat, chunk.visible[materialIndex]);
                if (_preparedSplit)
                {
                    Graphics.DrawMeshInstancedIndirect(
                        _template, 0, colorMat, chunk.bounds, chunk.colorArgs[materialIndex], 0, null,
                        ShadowCastingMode.Off, _preparedReceive, ResolveDrawLayer(), camera);
                    Material shadowMat = _shadowMaterials[materialIndex];
                    BindDrawMaterial(shadowMat, chunk.shadowVisible[materialIndex]);
                    Graphics.DrawMeshInstancedIndirect(
                        _template, 0, shadowMat, chunk.bounds, chunk.shadowArgs[materialIndex], 0, null,
                        ShadowCastingMode.ShadowsOnly, false, ResolveDrawLayer(), camera);
                }
                else
                {
                    Graphics.DrawMeshInstancedIndirect(
                        _template, 0, colorMat, chunk.bounds, chunk.colorArgs[materialIndex], 0, null,
                        _preparedCast ? ShadowCastingMode.On : ShadowCastingMode.Off,
                        _preparedReceive, ResolveDrawLayer(), camera);
                }
            }
        }

        void SubmitUrpShadows(UrpChunk chunk, Camera camera)
        {
            if (!CanDraw || !_preparedCast)
                return;
            RestoreChunk(chunk);
            for (int materialIndex = 0; materialIndex < _materials.Length; materialIndex++)
            {
                Material shadowMat = _shadowMaterials[materialIndex];
                GraphicsBuffer visible = _preparedSplit ? chunk.shadowVisible[materialIndex] : chunk.visible[materialIndex];
                GraphicsBuffer args = _preparedSplit ? chunk.shadowArgs[materialIndex] : chunk.colorArgs[materialIndex];
                BindDrawMaterial(shadowMat, visible);
                Graphics.DrawMeshInstancedIndirect(
                    _template, 0, shadowMat, chunk.bounds, args, 0, null,
                    ShadowCastingMode.ShadowsOnly, false, ResolveDrawLayer(), camera);
            }
        }

        void SubmitCmd(UrpChunk chunk, CommandBuffer cmd, int shaderPass)
        {
            if (!CanDraw)
                return;
            RestoreChunk(chunk);
            for (int materialIndex = 0; materialIndex < _materials.Length; materialIndex++)
            {
                Material colorMat = _materials[materialIndex];
                if (!ClusterMeshMaterialUtil.CanSubmitShaderPass(colorMat, shaderPass))
                    continue;
                BindDrawMaterial(colorMat, chunk.visible[materialIndex], shaderPass == MotionVectorShaderPass);
                cmd.DrawMeshInstancedIndirect(
                    _template, 0, colorMat, shaderPass, chunk.colorArgs[materialIndex]);
            }
        }

        int ResolveDrawLayer()
        {
#if UNITY_EDITOR
            return EditorDrawLayer;
#else
            return 0;
#endif
        }

        void RestoreChunk(UrpChunk chunk)
        {
            Array.Copy(chunk.l2w, _l2w, chunk.n);
            Array.Copy(chunk.w2l, _w2l, chunk.n);
            Array.Copy(chunk.objectSH, _objectSH, chunk.n);
            _objectSHBuffer.SetData(_objectSH, 0, 0, chunk.n);
            if (chunk.hasMotionVectors)
            {
                Array.Copy(chunk.previousL2w, _previousL2w, chunk.n);
                Array.Copy(chunk.motionVectorEnabled, _motionVectorEnabled, chunk.n);
            }
        }

        GraphicsBuffer AllocExtraArgs()
        {
            var buffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 20);
            buffer.SetData(_argsSeed);
            _extraArgs.Add(buffer);
            return buffer;
        }

        GraphicsBuffer AllocVisible()
        {
            var buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Append | GraphicsBuffer.Target.Structured,
                Mathf.Max(1, _visibleCapacity),
                4);
            _extraVisible.Add(buffer);
            return buffer;
        }

        void ReleaseExtras()
        {
            for (int i = 0; i < _extraArgs.Count; i++)
                _extraArgs[i]?.Dispose();
            _extraArgs.Clear();
            for (int i = 0; i < _extraVisible.Count; i++)
                _extraVisible[i]?.Dispose();
            _extraVisible.Clear();
        }

        void BindDrawMaterial(Material mat, GraphicsBuffer visible, bool bindMotion = false)
        {
            if (mat == null)
                return;
            mat.SetBuffer(ClustersId, _clusterBuffer);
            mat.SetBuffer(VerticesId, _vertexBuffer);
            mat.SetBuffer(VerticesTightId, _vertexTightBuffer);
            mat.SetInt(RestVertexTightId, _restVertexTight ? 1 : 0);
            mat.SetBuffer(IndicesId, _indexBuffer);
            mat.SetBuffer(VisibleId, visible);
            mat.SetMatrixArray(ObjectLocalToWorldId, _l2w);
            mat.SetMatrixArray(ObjectWorldToLocalId, _w2l);
            mat.SetBuffer(ObjectSHId, _objectSHBuffer);
            if (bindMotion)
            {
                mat.SetMatrixArray(ObjectPreviousLocalToWorldId, _previousL2w);
                mat.SetFloatArray(ObjectMotionVectorEnabledId, _motionVectorEnabled);
            }
            mat.SetFloat(EnableClusterColorId, EnableClusterColor ? 1f : 0f);
            if (_preparedReceive)
                mat.DisableKeyword(ReceiveShadowsOffKeyword);
            else
                mat.EnableKeyword(ReceiveShadowsOffKeyword);
        }

        static bool MaterialsAlive(Material[] materials)
        {
            if (materials == null || materials.Length == 0)
                return false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null)
                    return false;
            }

            return true;
        }

        static void CopyPlanes(Plane[] src, Vector4[] dest)
        {
            for (int i = 0; i < 6; i++)
                dest[i] = new Vector4(src[i].normal.x, src[i].normal.y, src[i].normal.z, src[i].distance);
        }

        Bounds TransformBounds(Matrix4x4 localToWorld)
        {
            return ClusterMeshFrustum.TransformLocalBounds(_localBounds, localToWorld);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            IsReady = false;
            _urpChunks.Clear();
            ReleaseExtras();
            _clusterBuffer?.Dispose();
            _groupBuffer?.Dispose();
            _owningGroupBuffer?.Dispose();
            _objectCameraCullFlagsBuffer?.Dispose();
            _objectSHBuffer?.Dispose();
            _vertexBuffer?.Dispose();
            _vertexTightBuffer?.Dispose();
            _indexBuffer?.Dispose();
            if (_visibleBuffers != null)
            {
                for (int i = 0; i < _visibleBuffers.Length; i++)
                    _visibleBuffers[i]?.Dispose();
            }

            if (_shadowVisibleBuffers != null)
            {
                for (int i = 0; i < _shadowVisibleBuffers.Length; i++)
                    _shadowVisibleBuffers[i]?.Dispose();
            }

            if (_argsBuffers != null)
            {
                for (int i = 0; i < _argsBuffers.Length; i++)
                    _argsBuffers[i]?.Dispose();
            }

            if (_shadowArgsBuffers != null)
            {
                for (int i = 0; i < _shadowArgsBuffers.Length; i++)
                    _shadowArgsBuffers[i]?.Dispose();
            }

            if (_materials != null)
            {
                for (int i = 0; i < _materials.Length; i++)
                {
                    if (_materials[i] != null)
                        DestroyUnityObject(_materials[i]);
                }
            }

            if (_shadowMaterials != null)
            {
                for (int i = 0; i < _shadowMaterials.Length; i++)
                {
                    if (_shadowMaterials[i] != null)
                        DestroyUnityObject(_shadowMaterials[i]);
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
