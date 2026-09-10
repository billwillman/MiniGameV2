using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh
{
    /// <summary>
    /// Per-asset GPU resources. Curves are evaluated once per bone/object on CPU in v1,
    /// then uploaded as a float palette texture; the vertex shader only performs VTF/LBS.
    /// </summary>
    public sealed class ClusterSkinnedMeshDrawContext : IDisposable
    {
        static readonly int ClustersId = Shader.PropertyToID("_Clusters");
        static readonly int GroupsId = Shader.PropertyToID("_Groups");
        static readonly int OwningGroupsId = Shader.PropertyToID("_OwningGroups");
        static readonly int GroupCountId = Shader.PropertyToID("_GroupCount");
        static readonly int VerticesId = Shader.PropertyToID("_Vertices");
        static readonly int IndicesId = Shader.PropertyToID("_Indices");
        static readonly int SkinWeightsId = Shader.PropertyToID("_SkinWeights");
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
        static readonly int PlanesId = Shader.PropertyToID("_Planes");
        static readonly int WorldCameraPosId = Shader.PropertyToID("_WorldCameraPos");
        static readonly int ObjectLocalToWorldId = Shader.PropertyToID("_ObjectLocalToWorld");
        static readonly int ObjectWorldToLocalId = Shader.PropertyToID("_ObjectWorldToLocal");
        static readonly int SkinPaletteTexId = Shader.PropertyToID("_SkinPaletteTex");
        static readonly int PaletteWidthId = Shader.PropertyToID("_SkinPaletteWidth");

        readonly ClusterSkinnedMeshAsset _asset;
        readonly ComputeShader _cullShader;
        readonly int _kernel;
        readonly Mesh _template;
        readonly GraphicsBuffer _clusters;
        readonly GraphicsBuffer _groups;
        readonly GraphicsBuffer _owningGroups;
        readonly GraphicsBuffer _vertices;
        readonly GraphicsBuffer _indices;
        readonly GraphicsBuffer _weights;
        readonly GraphicsBuffer _cullFrames;
        readonly GraphicsBuffer _segments;
        readonly GraphicsBuffer _cameraCull;
        readonly GraphicsBuffer[] _visible;
        readonly GraphicsBuffer[] _shadowVisible;
        readonly GraphicsBuffer[] _args;
        readonly GraphicsBuffer[] _shadowArgs;
        readonly Material[] _materials;
        readonly Material[] _shadowMaterials;
        readonly Texture2D _paletteTexture;
        readonly Color[] _palettePixels;
        readonly Matrix4x4[] _paletteScratch;
        readonly Matrix4x4[] _l2w = new Matrix4x4[ClusterMeshLimits.MaxBatchedObjects];
        readonly Matrix4x4[] _w2l = new Matrix4x4[ClusterMeshLimits.MaxBatchedObjects];
        readonly uint[] _segmentData = new uint[ClusterMeshLimits.MaxBatchedObjects];
        readonly uint[] _cameraCullData = new uint[ClusterMeshLimits.MaxBatchedObjects];
        readonly uint[] _noCameraCullData = new uint[ClusterMeshLimits.MaxBatchedObjects];
        readonly Vector4[] _planes = new Vector4[6];
        readonly Plane[] _planeScratch = new Plane[6];
        readonly uint[] _argsSeed = new uint[5];
        readonly Bounds _localAnimationBounds;
        readonly int _boneCount;
        readonly int _paletteWidth;
        bool _disposed;

        public bool IsReady { get; private set; }
        public string Error { get; private set; }

        public ClusterSkinnedMeshDrawContext(ClusterSkinnedMeshAsset asset, ComputeShader cullShader, Shader litShader)
        {
            _asset = asset;
            _cullShader = cullShader;
            if (asset == null || asset.geometry == null || asset.geometry.clusters == null || asset.geometry.clusters.Length == 0)
            { Error = "ClusterSkinnedMesh asset is missing geometry."; return; }
            if (asset.bindPoses == null || asset.bindPoses.Length == 0)
            { Error = "ClusterSkinnedMesh asset has no bind poses."; return; }
            if (asset.clips == null || asset.clips.Length == 0 || asset.cullFrames == null)
            { Error = "ClusterSkinnedMesh asset has no baked clip bounds."; return; }
            if (!ClusterMeshGeometry.TryReadGpuGeometry(asset.geometry, out ClusterPackedVertex[] verts, out uint[] indices, out string error))
            { Error = error; return; }
            if (!asset.TryReadSkinWeights(out ClusterPackedSkinWeight[] skinWeights, out string skinError))
            { Error = skinError; return; }
            if (cullShader == null || litShader == null)
            { Error = "ClusterSkinnedMesh cull/lit shader is missing."; return; }
            if (!SystemInfo.supportsComputeShaders)
            { Error = "ClusterSkinnedMesh requires compute shaders and vertex texture fetch."; return; }

            _boneCount = asset.bindPoses.Length;
            _paletteWidth = _boneCount * 3;
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
            _vertices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, verts.Length), ClusterMeshLimits.ClusterVertexStride);
            _vertices.SetData(verts);
            _indices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, indices.Length), 4);
            _indices.SetData(indices);
            _weights = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, verts.Length), 16);
            _weights.SetData(skinWeights);
            _cullFrames = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, asset.cullFrames.Length), 64);
            _cullFrames.SetData(asset.cullFrames);
            _segments = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ClusterMeshLimits.MaxBatchedObjects, 4);
            _cameraCull = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ClusterMeshLimits.MaxBatchedObjects, 4);

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
            _paletteTexture = new Texture2D(_paletteWidth, ClusterMeshLimits.MaxBatchedObjects, TextureFormat.RGBAFloat, false, true)
            { name = "ClusterSkinnedMesh Palette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            _palettePixels = new Color[_paletteWidth * ClusterMeshLimits.MaxBatchedObjects];
            _paletteScratch = new Matrix4x4[_boneCount];
            _localAnimationBounds = BuildAnimationBounds(asset);
            IsReady = true;
        }

        public void Draw(IList<Matrix4x4> matrices, IList<bool> cpuCull, IList<bool> cameraCull, IList<float> times,
            int clipIndex, bool enableConeCull, float lodErrorThreshold, Camera cullingCamera, Camera drawCamera,
            bool castShadows, bool receiveShadows, int drawLayer)
        {
            if (!IsReady || matrices == null || cullingCamera == null || drawCamera == null) return;
            int sourceCount = Mathf.Min(ClusterMeshLimits.MaxBatchedObjects, matrices.Count);
            if (sourceCount <= 0) return;
            int clip = Mathf.Clamp(clipIndex, 0, _asset.clips.Length - 1);
            ClusterSkinnedClip clipData = _asset.clips[clip];
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
                int segmentCount = Mathf.Max(1, clipData.segmentCount);
                _segmentData[count] = (uint)Mathf.Clamp(
                    Mathf.FloorToInt(Mathf.Repeat(t, 1f) * segmentCount), 0, segmentCount - 1);
                UploadPaletteRow(count, clip, t);
                count++;
            }
            if (count <= 0) return;
            _paletteTexture.SetPixels(_palettePixels); _paletteTexture.Apply(false, false);
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
                Bind(_materials[material], _visible[material]);
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
                    Bind(_shadowMaterials[material], _shadowVisible[material]);
                    Graphics.DrawMeshInstancedIndirect(_template, 0, _shadowMaterials[material], worldBounds,
                        _shadowArgs[material], 0, null, ShadowCastingMode.ShadowsOnly, false, drawLayer, drawCamera);
                }
            }
        }

        void UploadPaletteRow(int objectIndex, int clip, float time)
        {
            ClusterSkinnedClip clipData = _asset.clips[clip];
            ClusterSkinnedAnimation.EvaluatePaletteAtTime(
                _asset, clip, Mathf.Repeat(time, 1f) * Mathf.Max(0f, clipData.duration), _paletteScratch);
            int basePixel = objectIndex * _paletteWidth;
            for (int bone = 0; bone < _boneCount; bone++)
            {
                Matrix4x4 m = _paletteScratch[bone]; int x = basePixel + bone * 3;
                _palettePixels[x] = new Color(m.m00, m.m01, m.m02, m.m03);
                _palettePixels[x + 1] = new Color(m.m10, m.m11, m.m12, m.m13);
                _palettePixels[x + 2] = new Color(m.m20, m.m21, m.m22, m.m23);
            }
        }

        void Bind(Material m, GraphicsBuffer visible)
        {
            m.SetBuffer(ClustersId, _clusters); m.SetBuffer(VerticesId, _vertices); m.SetBuffer(IndicesId, _indices);
            m.SetBuffer(SkinWeightsId, _weights); m.SetBuffer(VisibleId, visible);
            m.SetMatrixArray(ObjectLocalToWorldId, _l2w); m.SetMatrixArray(ObjectWorldToLocalId, _w2l);
            m.SetTexture(SkinPaletteTexId, _paletteTexture); m.SetInt(PaletteWidthId, _paletteWidth);
        }

        static Bounds BuildAnimationBounds(ClusterSkinnedMeshAsset a)
        {
            if (a.cullFrames == null || a.cullFrames.Length == 0) return ClusterMeshFrustum.AssetLocalBounds(a.geometry);
            Bounds b = new Bounds(a.cullFrames[0].aabbCenter, a.cullFrames[0].aabbExtents * 2f);
            for (int i = 1; i < a.cullFrames.Length; i++) b.Encapsulate(new Bounds(a.cullFrames[i].aabbCenter, a.cullFrames[i].aabbExtents * 2f));
            return b;
        }
        void CopyPlane(int i, Plane p) => _planes[i] = new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance);
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; IsReady = false;
            _clusters?.Dispose(); _groups?.Dispose(); _owningGroups?.Dispose(); _vertices?.Dispose(); _indices?.Dispose();
            _weights?.Dispose(); _cullFrames?.Dispose(); _segments?.Dispose(); _cameraCull?.Dispose();
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
