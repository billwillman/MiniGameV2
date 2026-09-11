using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh
{
    /// <summary>
    /// Per-asset GPU resources. GPU Texture mode reads a baked palette atlas directly;
    /// CPU Curves mode evaluates curves and uploads the current float palette row.
    /// Both modes share the same vertex VTF/LBS path.
    /// </summary>
    public sealed class ClusterSkinnedMeshDrawContext : IDisposable
    {
        static readonly int ClustersId = Shader.PropertyToID("_Clusters");
        static readonly int GroupsId = Shader.PropertyToID("_Groups");
        static readonly int OwningGroupsId = Shader.PropertyToID("_OwningGroups");
        static readonly int GroupCountId = Shader.PropertyToID("_GroupCount");
        static readonly int VerticesId = Shader.PropertyToID("_Vertices");
        static readonly int VerticesTightId = Shader.PropertyToID("_VerticesTight");
        static readonly int IndicesId = Shader.PropertyToID("_Indices");
        static readonly int SkinWeightsId = Shader.PropertyToID("_SkinWeights");
        static readonly int SkinWeights8Id = Shader.PropertyToID("_SkinWeights8");
        static readonly int VisibleId = Shader.PropertyToID("_VisibleClusterIds");
        static readonly int CullFramesId = Shader.PropertyToID("_CullFrames");
        static readonly int ObjectSegmentsId = Shader.PropertyToID("_ObjectCullSegments");
        static readonly int ObjectCameraCullId = Shader.PropertyToID("_ObjectCameraCullFlags");
        static readonly int ObjectCountId = Shader.PropertyToID("_ObjectCount");
        static readonly int ClusterCountId = Shader.PropertyToID("_ClusterCount");
        static readonly int MaterialIndexId = Shader.PropertyToID("_MaterialIndex");
        static readonly int CullFrameOffsetId = Shader.PropertyToID("_CullFrameOffset");
        static readonly int EnableConeCullId = Shader.PropertyToID("_EnableConeCull");
        static readonly int HierarchyVersionId = Shader.PropertyToID("_HierarchyVersion");
        static readonly int LodPerspectiveId = Shader.PropertyToID("_LodPerspective");
        static readonly int LodErrorThresholdId = Shader.PropertyToID("_LodErrorThreshold");
        static readonly int LodProjectionScaleId = Shader.PropertyToID("_LodProjectionScale");
        static readonly int EnableClusterColorId = Shader.PropertyToID("_EnableClusterColor");
        static readonly int PlanesId = Shader.PropertyToID("_Planes");
        static readonly int WorldCameraPosId = Shader.PropertyToID("_WorldCameraPos");
        static readonly int ObjectLocalToWorldId = Shader.PropertyToID("_ObjectLocalToWorld");
        static readonly int ObjectWorldToLocalId = Shader.PropertyToID("_ObjectWorldToLocal");
        static readonly int SkinPaletteTexId = Shader.PropertyToID("_SkinPaletteTex");
        static readonly int SkinAnimationTexId = Shader.PropertyToID("_SkinAnimationTex");
        static readonly int PaletteWidthId = Shader.PropertyToID("_SkinPaletteWidth");
        static readonly int UseGpuAnimationTextureId = Shader.PropertyToID("_UseGpuAnimationTexture");
        static readonly int SkinAnimationFrameCountId = Shader.PropertyToID("_SkinAnimationFrameCount");
        static readonly int SkinWeightPacked8Id = Shader.PropertyToID("_SkinWeightPacked8");
        static readonly int RestVertexTightId = Shader.PropertyToID("_RestVertexTight");
        static readonly int GpuPalettePixelsPerBoneId = Shader.PropertyToID("_GpuPalettePixelsPerBone");
        static readonly int ObjectAnimationTimesId = Shader.PropertyToID("_ObjectAnimationTimes");

        readonly ClusterSkinnedMeshAsset _asset;
        readonly ComputeShader _cullShader;
        readonly int _kernel;
        readonly Mesh _template;
        readonly GraphicsBuffer _clusters;
        readonly GraphicsBuffer _groups;
        readonly GraphicsBuffer _owningGroups;
        readonly GraphicsBuffer _vertices;
        readonly GraphicsBuffer _verticesTight;
        readonly GraphicsBuffer _indices;
        readonly GraphicsBuffer _weights;
        readonly GraphicsBuffer _weights8;
        readonly GraphicsBuffer _cullFrames;
        readonly GraphicsBuffer _segments;
        readonly GraphicsBuffer _cameraCull;
        readonly GraphicsBuffer _animationTimes;
        readonly GraphicsBuffer[] _visible;
        readonly GraphicsBuffer[] _shadowVisible;
        readonly GraphicsBuffer[] _args;
        readonly GraphicsBuffer[] _shadowArgs;
        readonly Material[] _materials;
        readonly Material[] _shadowMaterials;
        readonly Texture2D _paletteTexture;
        readonly Matrix4x4[] _l2w = new Matrix4x4[ClusterMeshLimits.MaxBatchedObjects];
        readonly Matrix4x4[] _w2l = new Matrix4x4[ClusterMeshLimits.MaxBatchedObjects];
        readonly uint[] _segmentData = new uint[ClusterMeshLimits.MaxBatchedObjects];
        readonly uint[] _cameraCullData = new uint[ClusterMeshLimits.MaxBatchedObjects];
        readonly uint[] _noCameraCullData = new uint[ClusterMeshLimits.MaxBatchedObjects];
        readonly float[] _animationTimeData = new float[ClusterMeshLimits.MaxBatchedObjects];
        readonly Vector4[] _planes = new Vector4[6];
        readonly Plane[] _planeScratch = new Plane[6];
        readonly uint[] _argsSeed = new uint[5];
        readonly Bounds _localAnimationBounds;
        readonly int _boneCount;
        readonly int _paletteWidth;
        readonly int _gpuPalettePixelsPerBone;
        readonly bool _skinWeightPacked8;
        readonly bool _restVertexTight;
        NativeArray<ClusterSkinnedCurveHeader> _burstCurveHeaders;
        NativeArray<ClusterSkinnedCurveSegment> _burstCurveSegments;
        NativeArray<int> _burstParents;
        NativeArray<int> _burstEvaluationOrder;
        NativeArray<float4x4> _burstBindPoses;
        NativeArray<float> _burstTimes;
        NativeArray<float4x4> _burstGlobalScratch;
        NativeArray<float4> _burstPalettePixels;
        NativeArray<float4x4> _burstPrefixScratch;
        NativeArray<int> _burstAncestorsA;
        NativeArray<int> _burstAncestorsB;
        int _burstMaxBoneDepth;
        bool _hasBurstCpuData;
        bool _disposed;

        public bool IsReady { get; private set; }
        public string Error { get; private set; }
        public bool EnableClusterColor { get; set; }

        public bool CanDraw
        {
            get
            {
                if (!IsReady || _disposed)
                    return false;
                if (_paletteTexture == null || _template == null)
                    return false;
                return MaterialsAlive(_materials) && MaterialsAlive(_shadowMaterials);
            }
        }

        public ClusterSkinnedMeshDrawContext(ClusterSkinnedMeshAsset asset, ComputeShader cullShader, Shader litShader)
        {
            _asset = asset;
            _cullShader = cullShader;
            _gpuPalettePixelsPerBone = 3;
            _skinWeightPacked8 = false;
            _restVertexTight = false;
            if (asset == null || asset.geometry == null || asset.geometry.clusters == null || asset.geometry.clusters.Length == 0)
            { Error = "ClusterSkinnedMesh asset is missing geometry."; return; }
            if (asset.bindPoses == null || asset.bindPoses.Length == 0)
            { Error = "ClusterSkinnedMesh asset has no bind poses."; return; }
            string cullError = null;
            if (asset.clips == null || asset.clips.Length == 0 ||
                !asset.TryGetCullFrames(out ClusterSkinnedCullFrame[] cullFrames, out cullError))
            { Error = cullError ?? "ClusterSkinnedMesh asset has no baked clip bounds."; return; }
            bool tightRest = asset.geometry.ResolvedVertexStride == ClusterMeshLimits.TightVertexStride;
            ClusterPackedVertex[] verts = Array.Empty<ClusterPackedVertex>();
            ClusterPackedVertexTight[] tightVerts = Array.Empty<ClusterPackedVertexTight>();
            uint[] indices;
            if (tightRest)
            {
                if (!ClusterMeshGeometry.TryReadTightVertices(asset.geometry, out tightVerts, out string tightError))
                { Error = tightError; return; }
                if (!ClusterMeshGeometry.TryReadPackedIndices(asset.geometry, out indices, out string indexError))
                { Error = indexError; return; }
            }
            else if (!ClusterMeshGeometry.TryReadGpuGeometry(asset.geometry, out verts, out indices, out string error))
            { Error = error; return; }

            ClusterPackedSkinWeight[] skinWeights = Array.Empty<ClusterPackedSkinWeight>();
            ClusterPackedSkinWeight8[] skinWeights8 = Array.Empty<ClusterPackedSkinWeight8>();
            bool packed8 = asset.ResolvedSkinWeightStride == ClusterSkinnedMeshAsset.PackedSkinWeightStride8;
            if (packed8)
            {
                if (!asset.TryReadSkinWeights8(out skinWeights8, out string skinError8))
                { Error = skinError8; return; }
            }
            else if (!asset.TryReadSkinWeights(out skinWeights, out string skinError))
            { Error = skinError; return; }
            if (cullShader == null || litShader == null)
            { Error = "ClusterSkinnedMesh cull/lit shader is missing."; return; }
            if (!SystemInfo.supportsComputeShaders)
            { Error = "ClusterSkinnedMesh requires compute shaders and vertex texture fetch."; return; }

            _boneCount = asset.bindPoses.Length;
            _paletteWidth = _boneCount * 3;
            _gpuPalettePixelsPerBone = asset.GpuPalettePixelsPerBone;
            _skinWeightPacked8 = packed8;
            _restVertexTight = tightRest;
            if (_paletteWidth > SystemInfo.maxTextureSize)
            { Error = "ClusterSkinnedMesh has too many bones for the palette texture."; return; }
            _kernel = cullShader.FindKernel("CullSkinnedClusters");
            _template = ClusterMeshTemplate.Create();
            _clusters = new GraphicsBuffer(GraphicsBuffer.Target.Structured, asset.geometry.clusters.Length, ClusterMeshLimits.ClusterHeaderStride);
            _clusters.SetData(asset.geometry.clusters);
            int groupCount = asset.geometry.groups != null ? asset.geometry.groups.Length : 0;
            _groups = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, groupCount), ClusterMeshLimits.ClusterGroupStride);
            _groups.SetData(groupCount > 0 ? asset.geometry.groups : new ClusterGroup[1]);
            int[] owning = ClusterMeshLod.BuildOwningGroupIndices(asset.geometry.clusters.Length, asset.geometry.groups);
            _owningGroups = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, owning.Length), 4);
            _owningGroups.SetData(owning.Length > 0 ? owning : new[] { ClusterMeshLod.NoParent });
            int vertexCount = tightRest ? tightVerts.Length : verts.Length;
            if (tightRest)
            {
                _vertices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, ClusterMeshLimits.ClusterVertexStride);
                _vertices.SetData(new ClusterPackedVertex[1]);
                _verticesTight = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, tightVerts.Length), ClusterMeshLimits.TightVertexStride);
                _verticesTight.SetData(tightVerts);
            }
            else
            {
                _vertices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, verts.Length), ClusterMeshLimits.ClusterVertexStride);
                _vertices.SetData(verts);
                _verticesTight = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, ClusterMeshLimits.TightVertexStride);
                _verticesTight.SetData(new ClusterPackedVertexTight[1]);
            }
            _indices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, indices.Length), 4);
            _indices.SetData(indices);
            if (packed8)
            {
                _weights = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, ClusterSkinnedMeshAsset.PackedSkinWeightStride);
                _weights.SetData(new ClusterPackedSkinWeight[1]);
                var weightUints = new uint[Mathf.Max(2, skinWeights8.Length * 2)];
                for (int i = 0; i < skinWeights8.Length; i++)
                {
                    weightUints[i * 2] = skinWeights8[i].boneIndices;
                    weightUints[i * 2 + 1] = skinWeights8[i].boneWeights;
                }
                _weights8 = new GraphicsBuffer(GraphicsBuffer.Target.Structured, weightUints.Length, 4);
                _weights8.SetData(weightUints);
            }
            else
            {
                _weights = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, vertexCount), ClusterSkinnedMeshAsset.PackedSkinWeightStride);
                _weights.SetData(skinWeights);
                _weights8 = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, 4);
                _weights8.SetData(new uint[2]);
            }
            _cullFrames = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, cullFrames.Length), 64);
            _cullFrames.SetData(cullFrames);
            _segments = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ClusterMeshLimits.MaxBatchedObjects, 4);
            _cameraCull = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ClusterMeshLimits.MaxBatchedObjects, 4);
            _animationTimes = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ClusterMeshLimits.MaxBatchedObjects, 4);

            int materialCount = Mathf.Max(1, asset.geometry.materials != null ? asset.geometry.materials.Length : 1);
            int capacity = Mathf.Max(1, asset.geometry.clusters.Length * ClusterMeshLimits.MaxBatchedObjects);
            _visible = new GraphicsBuffer[materialCount]; _shadowVisible = new GraphicsBuffer[materialCount];
            _args = new GraphicsBuffer[materialCount]; _shadowArgs = new GraphicsBuffer[materialCount];
            _materials = new Material[materialCount]; _shadowMaterials = new Material[materialCount];
            _argsSeed[0] = ClusterMeshLimits.TemplateVertexCount;
            for (int i = 0; i < materialCount; i++)
            {
                Material source = asset.geometry.materials != null && i < asset.geometry.materials.Length ? asset.geometry.materials[i] : null;
                _materials[i] = ClusterMeshMaterialUtil.CreateRuntimeMaterial(source, litShader);
                _shadowMaterials[i] = ClusterMeshMaterialUtil.CreateRuntimeMaterial(source, litShader);
                _visible[i] = new GraphicsBuffer(GraphicsBuffer.Target.Append | GraphicsBuffer.Target.Structured, capacity, 4);
                _shadowVisible[i] = new GraphicsBuffer(GraphicsBuffer.Target.Append | GraphicsBuffer.Target.Structured, capacity, 4);
                _args[i] = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 20);
                _shadowArgs[i] = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 20);
                _args[i].SetData(_argsSeed);
                _shadowArgs[i].SetData(_argsSeed);
            }
            bool needsCpuPalette = asset.AllowsCpuAnimation &&
                SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat);
            int cpuPaletteWidth = needsCpuPalette ? _paletteWidth : 1;
            int cpuPaletteHeight = needsCpuPalette ? ClusterMeshLimits.MaxBatchedObjects : 1;
            TextureFormat cpuPaletteFormat = needsCpuPalette ? TextureFormat.RGBAFloat : TextureFormat.RGBA32;
            _paletteTexture = new Texture2D(cpuPaletteWidth, cpuPaletteHeight, cpuPaletteFormat, false, true)
            { name = "ClusterSkinnedMesh Palette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            if (needsCpuPalette)
                InitializeBurstCpuData(asset);
            _localAnimationBounds = BuildAnimationBounds(asset);
            IsReady = true;
        }

        public void Draw(IList<Matrix4x4> matrices, IList<bool> cpuCull, IList<bool> cameraCull, IList<float> times,
            int clipIndex, ClusterSkinnedAnimationEvaluation animationEvaluation,
            bool enableParallelBonePrefix, bool enableConeCull, float lodErrorThreshold,
            Camera cullingCamera, Camera drawCamera,
            bool castShadows, bool receiveShadows, int drawLayer)
        {
            if (!CanDraw || matrices == null || cullingCamera == null || drawCamera == null) return;
            int sourceCount = Mathf.Min(ClusterMeshLimits.MaxBatchedObjects, matrices.Count);
            if (sourceCount <= 0) return;
            int clip = Mathf.Clamp(clipIndex, 0, _asset.clips.Length - 1);
            ClusterSkinnedClip clipData = _asset.clips[clip];
            Texture2D availableGpuTexture = _asset.HasGpuPalette(clip) &&
                SystemInfo.SupportsTextureFormat(_asset.gpuPaletteTextures[clip].format)
                    ? _asset.gpuPaletteTextures[clip] : null;
            bool cpuAvailable = _asset.HasCpuBurstCurves(clip) && _hasBurstCpuData;
            bool requestGpu = animationEvaluation == ClusterSkinnedAnimationEvaluation.GpuTexture;
            bool useGpuAnimationTexture = requestGpu && availableGpuTexture != null;
            bool useBurstCpu = !requestGpu && cpuAvailable;

            if (_asset.animationDataMode == ClusterSkinnedAnimationDataMode.GpuOnly)
            {
                if (availableGpuTexture == null)
                    return;
                useGpuAnimationTexture = true;
                useBurstCpu = false;
            }
            else if (_asset.animationDataMode == ClusterSkinnedAnimationDataMode.CpuOnly)
            {
                if (!cpuAvailable)
                    return;
                useGpuAnimationTexture = false;
                useBurstCpu = true;
            }
            else if (!useGpuAnimationTexture && !useBurstCpu)
            {
                // Only dual-data assets may fall back to the other baked representation.
                if (requestGpu && cpuAvailable)
                    useBurstCpu = true;
                else if (!requestGpu && availableGpuTexture != null)
                    useGpuAnimationTexture = true;
                else
                    return;
            }
            Texture2D gpuAnimationTexture = useGpuAnimationTexture ? availableGpuTexture : null;
            bool useParallelPrefix = useBurstCpu && enableParallelBonePrefix;
            ClusterMeshFrustum.WorldPlanes(cullingCamera, _planeScratch);
            for (int i = 0; i < _planeScratch.Length; i++)
                CopyPlane(i, _planeScratch[i]);
            int count = 0;
            for (int i = 0; i < sourceCount; i++)
            {
                Matrix4x4 m = matrices[i];
                bool useCpuCull = cpuCull == null || i >= cpuCull.Count || cpuCull[i];
                if (useCpuCull && !castShadows && !GeometryUtility.TestPlanesAABB(
                        _planeScratch, ClusterMeshFrustum.TransformLocalBounds(_localAnimationBounds, m)))
                    continue;
                _l2w[count] = m;
                _w2l[count] = m.inverse;
                _cameraCullData[count] = cameraCull == null || i >= cameraCull.Count || cameraCull[i] ? 1u : 0u;
                float t = times != null && i < times.Count ? times[i] : 0f;
                _animationTimeData[count] = Mathf.Repeat(t, 1f);
                if (useBurstCpu)
                    _burstTimes[count] = _animationTimeData[count];
                int segmentCount = Mathf.Max(1, clipData.segmentCount);
                _segmentData[count] = (uint)Mathf.Clamp(
                    Mathf.FloorToInt(_animationTimeData[count] * segmentCount), 0, segmentCount - 1);
                count++;
            }
            if (count <= 0) return;
            if (useBurstCpu)
            {
                if (useParallelPrefix)
                    EvaluateParallelPrefixPalette(count, clipData);
                else
                {
                    ClusterSkinnedPaletteJob paletteJob = new ClusterSkinnedPaletteJob
                    {
                        headers = _burstCurveHeaders,
                        segments = _burstCurveSegments,
                        parents = _burstParents,
                        evaluationOrder = _burstEvaluationOrder,
                        bindPoses = _burstBindPoses,
                        normalizedTimes = _burstTimes,
                        globalScratch = _burstGlobalScratch,
                        palettePixels = _burstPalettePixels,
                        curveHeaderOffset = clipData.cpuCurveHeaderOffset,
                        boneCount = _boneCount,
                        paletteWidth = _paletteWidth,
                        duration = clipData.duration
                    };
                    paletteJob.Schedule(count, 1).Complete();
                }
                _paletteTexture.SetPixelData(_burstPalettePixels, 0);
                _paletteTexture.Apply(false, false);
            }
            _animationTimes.SetData(_animationTimeData, 0, 0, count);
            _segments.SetData(_segmentData, 0, 0, count); _cameraCull.SetData(_cameraCullData, 0, 0, count);
            Bounds worldBounds = ClusterMeshFrustum.TransformLocalBounds(_localAnimationBounds, _l2w[0]);
            for (int i = 1; i < count; i++) worldBounds.Encapsulate(ClusterMeshFrustum.TransformLocalBounds(_localAnimationBounds, _l2w[i]));
            int groups = Mathf.Max(1, Mathf.CeilToInt(count * _asset.geometry.clusters.Length / 64f));
            for (int material = 0; material < _materials.Length; material++)
            {
                _cameraCull.SetData(_cameraCullData, 0, 0, count);
                _visible[material].SetCounterValue(0);
                _cullShader.SetBuffer(_kernel, ClustersId, _clusters);
                _cullShader.SetBuffer(_kernel, GroupsId, _groups);
                _cullShader.SetBuffer(_kernel, OwningGroupsId, _owningGroups);
                _cullShader.SetBuffer(_kernel, CullFramesId, _cullFrames);
                _cullShader.SetBuffer(_kernel, ObjectSegmentsId, _segments);
                _cullShader.SetBuffer(_kernel, ObjectCameraCullId, _cameraCull);
                _cullShader.SetBuffer(_kernel, VisibleId, _visible[material]);
                _cullShader.SetInt(ObjectCountId, count); _cullShader.SetInt(ClusterCountId, _asset.geometry.clusters.Length);
                _cullShader.SetInt(GroupCountId, _asset.geometry.groups != null ? _asset.geometry.groups.Length : 0);
                _cullShader.SetInt(MaterialIndexId, material); _cullShader.SetInt(CullFrameOffsetId, clipData.cullFrameOffset);
                _cullShader.SetInt(EnableConeCullId, enableConeCull ? 1 : 0);
                _cullShader.SetInt(HierarchyVersionId, _asset.geometry.hierarchyVersion);
                _cullShader.SetInt(LodPerspectiveId, cullingCamera.orthographic ? 0 : 1);
                _cullShader.SetFloat(LodErrorThresholdId, Mathf.Max(0f, lodErrorThreshold));
                _cullShader.SetFloat(LodProjectionScaleId, ClusterMeshLod.ProjectionScale(cullingCamera));
                _cullShader.SetVectorArray(PlanesId, _planes); _cullShader.SetVector(WorldCameraPosId, cullingCamera.transform.position);
                _cullShader.SetMatrixArray(ObjectLocalToWorldId, _l2w);
                _cullShader.Dispatch(_kernel, groups, 1, 1);
                _args[material].SetData(_argsSeed); GraphicsBuffer.CopyCount(_visible[material], _args[material], 4);
                Bind(_materials[material], _visible[material], gpuAnimationTexture, useGpuAnimationTexture);
                Graphics.DrawMeshInstancedIndirect(_template, 0, _materials[material], worldBounds, _args[material], 0, null,
                    ShadowCastingMode.Off, receiveShadows, drawLayer, drawCamera);
                if (castShadows)
                {
                    _shadowVisible[material].SetCounterValue(0);
                    _cameraCull.SetData(_noCameraCullData, 0, 0, count);
                    _cullShader.SetBuffer(_kernel, ObjectCameraCullId, _cameraCull);
                    _cullShader.SetBuffer(_kernel, VisibleId, _shadowVisible[material]);
                    _cullShader.SetInt(EnableConeCullId, 0);
                    _cullShader.Dispatch(_kernel, groups, 1, 1);
                    _shadowArgs[material].SetData(_argsSeed);
                    GraphicsBuffer.CopyCount(_shadowVisible[material], _shadowArgs[material], 4);
                    Bind(_shadowMaterials[material], _shadowVisible[material], gpuAnimationTexture, useGpuAnimationTexture);
                    Graphics.DrawMeshInstancedIndirect(_template, 0, _shadowMaterials[material], worldBounds,
                        _shadowArgs[material], 0, null, ShadowCastingMode.ShadowsOnly, false, drawLayer, drawCamera);
                }
            }
        }

        void InitializeBurstCpuData(ClusterSkinnedMeshAsset asset)
        {
            if (!ValidateBurstCpuData(asset))
                return;

            _burstCurveHeaders = new NativeArray<ClusterSkinnedCurveHeader>(asset.cpuCurveHeaders, Allocator.Persistent);
            _burstCurveSegments = new NativeArray<ClusterSkinnedCurveSegment>(asset.cpuCurveSegments, Allocator.Persistent);
            _burstParents = new NativeArray<int>(asset.boneParentIndices, Allocator.Persistent);
            _burstEvaluationOrder = new NativeArray<int>(asset.boneEvaluationOrder, Allocator.Persistent);
            _burstBindPoses = new NativeArray<float4x4>(_boneCount, Allocator.Persistent);
            for (int i = 0; i < _boneCount; i++)
                _burstBindPoses[i] = ToFloat4x4(asset.bindPoses[i]);
            _burstTimes = new NativeArray<float>(ClusterMeshLimits.MaxBatchedObjects, Allocator.Persistent);
            _burstGlobalScratch = new NativeArray<float4x4>(
                _boneCount * ClusterMeshLimits.MaxBatchedObjects, Allocator.Persistent);
            _burstPalettePixels = new NativeArray<float4>(
                _paletteWidth * ClusterMeshLimits.MaxBatchedObjects, Allocator.Persistent);
            var depths = new int[_boneCount];
            for (int i = 0; i < _boneCount; i++)
            {
                int bone = asset.boneEvaluationOrder[i];
                int parent = asset.boneParentIndices[bone];
                depths[bone] = parent >= 0 ? depths[parent] + 1 : 0;
                _burstMaxBoneDepth = Mathf.Max(_burstMaxBoneDepth, depths[bone]);
            }
            _hasBurstCpuData = true;
        }

        void EvaluateParallelPrefixPalette(int objectCount, ClusterSkinnedClip clip)
        {
            EnsureParallelPrefixBuffers();
            int itemCount = objectCount * _boneCount;
            JobHandle dependency = new ClusterSkinnedLocalPoseJob
            {
                headers = _burstCurveHeaders,
                segments = _burstCurveSegments,
                parents = _burstParents,
                normalizedTimes = _burstTimes,
                matrices = _burstGlobalScratch,
                ancestors = _burstAncestorsA,
                curveHeaderOffset = clip.cpuCurveHeaderOffset,
                boneCount = _boneCount,
                duration = clip.duration
            }.Schedule(itemCount, 32);

            NativeArray<float4x4> currentMatrices = _burstGlobalScratch;
            NativeArray<float4x4> nextMatrices = _burstPrefixScratch;
            NativeArray<int> currentAncestors = _burstAncestorsA;
            NativeArray<int> nextAncestors = _burstAncestorsB;
            for (int span = 1; span <= _burstMaxBoneDepth; span <<= 1)
            {
                dependency = new ClusterSkinnedPrefixStepJob
                {
                    inputMatrices = currentMatrices,
                    inputAncestors = currentAncestors,
                    outputMatrices = nextMatrices,
                    outputAncestors = nextAncestors,
                    boneCount = _boneCount
                }.Schedule(itemCount, 32, dependency);

                NativeArray<float4x4> matrixSwap = currentMatrices;
                currentMatrices = nextMatrices;
                nextMatrices = matrixSwap;
                NativeArray<int> ancestorSwap = currentAncestors;
                currentAncestors = nextAncestors;
                nextAncestors = ancestorSwap;
            }

            dependency = new ClusterSkinnedPaletteRowsJob
            {
                globalMatrices = currentMatrices,
                bindPoses = _burstBindPoses,
                palettePixels = _burstPalettePixels,
                boneCount = _boneCount,
                paletteWidth = _paletteWidth
            }.Schedule(itemCount, 32, dependency);
            dependency.Complete();
        }

        void EnsureParallelPrefixBuffers()
        {
            if (_burstPrefixScratch.IsCreated)
                return;
            int capacity = _boneCount * ClusterMeshLimits.MaxBatchedObjects;
            _burstPrefixScratch = new NativeArray<float4x4>(capacity, Allocator.Persistent);
            _burstAncestorsA = new NativeArray<int>(capacity, Allocator.Persistent);
            _burstAncestorsB = new NativeArray<int>(capacity, Allocator.Persistent);
        }

        static bool ValidateBurstCpuData(ClusterSkinnedMeshAsset asset)
        {
            if (asset == null || asset.cpuBurstAnimationVersion != ClusterSkinnedMeshAsset.CurrentCpuBurstAnimationVersion ||
                asset.bindPoses == null || asset.boneParentIndices == null || asset.boneEvaluationOrder == null ||
                asset.cpuCurveHeaders == null || asset.cpuCurveSegments == null)
                return false;
            int boneCount = asset.bindPoses.Length;
            if (boneCount <= 0 || asset.boneParentIndices.Length != boneCount ||
                asset.boneEvaluationOrder.Length != boneCount || asset.cpuCurveSegments.Length == 0)
                return false;

            bool[] seen = new bool[boneCount];
            for (int i = 0; i < boneCount; i++)
            {
                int bone = asset.boneEvaluationOrder[i];
                if (bone < 0 || bone >= boneCount || seen[bone])
                    return false;
                int parent = asset.boneParentIndices[bone];
                if (parent < -1 || parent >= boneCount || (parent >= 0 && !seen[parent]))
                    return false;
                seen[bone] = true;
            }
            for (int i = 0; i < asset.cpuCurveHeaders.Length; i++)
            {
                ClusterSkinnedCurveHeader header = asset.cpuCurveHeaders[i];
                if (header.segmentCount <= 0 || header.segmentOffset < 0 ||
                    header.segmentOffset > asset.cpuCurveSegments.Length - header.segmentCount)
                    return false;
            }
            return true;
        }

        static float4x4 ToFloat4x4(Matrix4x4 matrix)
        {
            Vector4 c0 = matrix.GetColumn(0);
            Vector4 c1 = matrix.GetColumn(1);
            Vector4 c2 = matrix.GetColumn(2);
            Vector4 c3 = matrix.GetColumn(3);
            return new float4x4(
                new float4(c0.x, c0.y, c0.z, c0.w),
                new float4(c1.x, c1.y, c1.z, c1.w),
                new float4(c2.x, c2.y, c2.z, c2.w),
                new float4(c3.x, c3.y, c3.z, c3.w));
        }

        void Bind(Material m, GraphicsBuffer visible, Texture2D gpuAnimationTexture,
            bool useGpuAnimationTexture)
        {
            if (m == null || visible == null)
                return;
            m.SetBuffer(ClustersId, _clusters); m.SetBuffer(VerticesId, _vertices); m.SetBuffer(VerticesTightId, _verticesTight);
            m.SetBuffer(IndicesId, _indices);
            m.SetBuffer(SkinWeightsId, _weights); m.SetBuffer(SkinWeights8Id, _weights8); m.SetBuffer(VisibleId, visible);
            m.SetBuffer(ObjectAnimationTimesId, _animationTimes);
            m.SetMatrixArray(ObjectLocalToWorldId, _l2w); m.SetMatrixArray(ObjectWorldToLocalId, _w2l);
            m.SetTexture(SkinPaletteTexId, _paletteTexture); m.SetInt(PaletteWidthId, _paletteWidth);
            m.SetTexture(SkinAnimationTexId, gpuAnimationTexture != null ? gpuAnimationTexture : _paletteTexture);
            m.SetInt(UseGpuAnimationTextureId, useGpuAnimationTexture ? 1 : 0);
            m.SetInt(SkinAnimationFrameCountId, gpuAnimationTexture != null ? gpuAnimationTexture.height : 1);
            m.SetInt(SkinWeightPacked8Id, _skinWeightPacked8 ? 1 : 0);
            m.SetInt(RestVertexTightId, _restVertexTight ? 1 : 0);
            m.SetInt(GpuPalettePixelsPerBoneId, _gpuPalettePixelsPerBone);
            m.SetFloat(EnableClusterColorId, EnableClusterColor ? 1f : 0f);
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

        static Bounds BuildAnimationBounds(ClusterSkinnedMeshAsset a)
        {
            if (!a.TryGetCullFrames(out ClusterSkinnedCullFrame[] frames, out _) || frames == null || frames.Length == 0)
                return ClusterMeshFrustum.AssetLocalBounds(a.geometry);
            Bounds b = new Bounds(frames[0].aabbCenter, frames[0].aabbExtents * 2f);
            for (int i = 1; i < frames.Length; i++) b.Encapsulate(new Bounds(frames[i].aabbCenter, frames[i].aabbExtents * 2f));
            return b;
        }
        void CopyPlane(int i, Plane p) => _planes[i] = new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance);
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; IsReady = false;
            _clusters?.Dispose(); _groups?.Dispose(); _owningGroups?.Dispose(); _vertices?.Dispose(); _verticesTight?.Dispose(); _indices?.Dispose();
            _weights?.Dispose(); _weights8?.Dispose(); _cullFrames?.Dispose(); _segments?.Dispose(); _cameraCull?.Dispose();
            _animationTimes?.Dispose();
            if (_burstCurveHeaders.IsCreated) _burstCurveHeaders.Dispose();
            if (_burstCurveSegments.IsCreated) _burstCurveSegments.Dispose();
            if (_burstParents.IsCreated) _burstParents.Dispose();
            if (_burstEvaluationOrder.IsCreated) _burstEvaluationOrder.Dispose();
            if (_burstBindPoses.IsCreated) _burstBindPoses.Dispose();
            if (_burstTimes.IsCreated) _burstTimes.Dispose();
            if (_burstGlobalScratch.IsCreated) _burstGlobalScratch.Dispose();
            if (_burstPalettePixels.IsCreated) _burstPalettePixels.Dispose();
            if (_burstPrefixScratch.IsCreated) _burstPrefixScratch.Dispose();
            if (_burstAncestorsA.IsCreated) _burstAncestorsA.Dispose();
            if (_burstAncestorsB.IsCreated) _burstAncestorsB.Dispose();
            if (_visible != null) foreach (var b in _visible) b?.Dispose();
            if (_shadowVisible != null) foreach (var b in _shadowVisible) b?.Dispose();
            if (_args != null) foreach (var b in _args) b?.Dispose();
            if (_shadowArgs != null) foreach (var b in _shadowArgs) b?.Dispose();
            if (_materials != null) foreach (var m in _materials) DestroyObject(m); if (_shadowMaterials != null) foreach (var m in _shadowMaterials) DestroyObject(m);
            DestroyObject(_paletteTexture); DestroyObject(_template);
        }
        static void DestroyObject(UnityEngine.Object o) { if (o == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o); }
    }
}
