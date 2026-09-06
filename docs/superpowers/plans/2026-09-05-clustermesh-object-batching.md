# ClusterMesh Object Batching Implementation Plan

> **For agentic workers:** This session implements the plan inline and runs EditMode tests last (user: 先落盘再实现最后测试). Do not commit unless the user asks.

**Goal:** Same `ClusterMeshAsset` + same Camera scene renderers share one Indirect draw per material (10 identical single-material objects = 1 DrawCall).

**Architecture:** Static `ClusterMeshSceneBatcher` groups enabled renderers, caches one `ClusterMeshDrawContext` per asset, and calls `Draw(IList<Matrix4x4>, Camera)`. Cull is world-space over `objectCount * clusterCount`. Object matrices live in a CBUFFER of size 256 so the vertex stage still uses 4 StructuredBuffers.

**Tech Stack:** Unity / Tuanjie 2022.3, URP, `Graphics.DrawMeshInstancedIndirect`, existing `ClusterMesh/Lit` + `ClusterMeshCull.compute`.

## Global Constraints

- Namespace `ClusterMesh`. Tests in `ClusterMesh.Tests`.
- Write only `Assets/ClusterMesh/**` and `.superpowers/sdd/` plus these docs. Do not edit FogOfWar, Packages, ProjectSettings, or the 2026-09-04 parent spec body except a one-line pointer if needed.
- `MaxBatchedObjects = 256`. Visible id: `(objectIndex << 16) | clusterIndex`.
- Vertex StructuredBuffers stay at 4. Capability gate unchanged.
- No URP Renderer Feature. Viewer keeps a private context.
- Demo `InstanceCount = 10`.
- Do not invent passing test output. Final verification is Tuanjie `2022.3.48t2` `-runTests` assembly `ClusterMesh.Editor.Tests`.

## File map

- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshLimits.cs` — cap + pack
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshFrustum.cs` — world AABB / world planes
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshDrawContext.cs` — batched `Draw`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshRenderer.cs` — register + flush
- Create: `Assets/ClusterMesh/Runtime/ClusterMeshSceneBatcher.cs`
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshCull.compute`
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshLit.hlsl`
- Modify: `Assets/ClusterMesh/Editor/ClusterMeshDemoSceneMenu.cs`
- Create: `Assets/ClusterMesh/Tests/Editor/ClusterMeshBatcherTests.cs`
- Modify: `Assets/ClusterMesh/Tests/Editor/ClusterMeshFrustumTests.cs`
- Modify: `Assets/ClusterMesh/Tests/Editor/ClusterMeshDemoSceneTests.cs`

---

### Task 1: Limits, packing, world frustum

**Files:**
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshLimits.cs`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshFrustum.cs`
- Create: `Assets/ClusterMesh/Tests/Editor/ClusterMeshBatcherTests.cs` (pack + draw-count cases only in this task)
- Modify: `Assets/ClusterMesh/Tests/Editor/ClusterMeshFrustumTests.cs`

**Interfaces:**
- Produces: `ClusterMeshLimits.MaxBatchedObjects`, `PackVisibleId`, `UnpackVisibleId`, `CountDrawCalls` lives on the batcher in Task 3 — this task only limits + frustum. `ClusterMeshFrustum.WorldPlanes(Camera, Plane[])`, `TransformAabb(in ClusterHeader, Matrix4x4, out Vector3, out Vector3)`, `TestAabbWorld(Vector3, Vector3, Plane[])`.

- [ ] **Step 1: Replace `ClusterMeshLimits.cs`**

```csharp
namespace ClusterMesh
{
    public static class ClusterMeshLimits
    {
        public const int MaxVerticesPerCluster = 64;
        public const int MaxTrianglesPerCluster = 124;
        public const int TemplateVertexCount = MaxTrianglesPerCluster * 3;
        public const int MaxBatchedObjects = 256;

        public static uint PackVisibleId(int objectIndex, int clusterIndex)
        {
            return ((uint)objectIndex << 16) | (uint)clusterIndex;
        }

        public static void UnpackVisibleId(uint packed, out int objectIndex, out int clusterIndex)
        {
            objectIndex = (int)(packed >> 16);
            clusterIndex = (int)(packed & 0xFFFFu);
        }
    }
}
```

- [ ] **Step 2: Add world helpers to `ClusterMeshFrustum.cs` (keep existing local methods)**

```csharp
public static void WorldPlanes(Camera camera, Plane[] dest)
{
    GeometryUtility.CalculateFrustumPlanes(camera, dest);
}

public static void TransformAabb(in ClusterHeader header, Matrix4x4 localToWorld, out Vector3 worldCenter, out Vector3 worldExtents)
{
    Vector3 c = header.aabbCenter;
    Vector3 e = header.aabbExtents;
    worldCenter = localToWorld.MultiplyPoint3x4(c);
    Vector3 axisX = localToWorld.MultiplyVector(new Vector3(e.x, 0f, 0f));
    Vector3 axisY = localToWorld.MultiplyVector(new Vector3(0f, e.y, 0f));
    Vector3 axisZ = localToWorld.MultiplyVector(new Vector3(0f, 0f, e.z));
    worldExtents = new Vector3(
        Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
        Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
        Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
}

public static bool TestAabbWorld(Vector3 worldCenter, Vector3 worldExtents, Plane[] planes)
{
    for (int i = 0; i < 6; i++)
    {
        Vector3 n = planes[i].normal;
        float r = worldExtents.x * Mathf.Abs(n.x) + worldExtents.y * Mathf.Abs(n.y) + worldExtents.z * Mathf.Abs(n.z);
        if (Vector3.Dot(n, worldCenter) + planes[i].distance + r < 0f)
            return false;
    }

    return true;
}
```

- [ ] **Step 3: Add tests** (full file created in Task 3; for this task append pack + frustum cases). Frustum tests:

```csharp
[Test]
public void TransformAabb_Identity_MatchesLocal()
{
    var h = new ClusterHeader { aabbCenter = new Vector4(1f, 2f, 3f, 0f), aabbExtents = new Vector4(0.5f, 1f, 1.5f, 0f) };
    ClusterMeshFrustum.TransformAabb(h, Matrix4x4.identity, out var c, out var e);
    Assert.That(c, Is.EqualTo(new Vector3(1f, 2f, 3f)));
    Assert.That(e, Is.EqualTo(new Vector3(0.5f, 1f, 1.5f)));
}

[Test]
public void TransformAabb_Translation_MovesCenterKeepsExtents()
{
    var h = new ClusterHeader { aabbCenter = new Vector4(0f, 0f, 0f, 0f), aabbExtents = new Vector4(1f, 2f, 3f, 0f) };
    ClusterMeshFrustum.TransformAabb(h, Matrix4x4.Translate(new Vector3(10f, 0f, 0f)), out var c, out var e);
    Assert.That(c, Is.EqualTo(new Vector3(10f, 0f, 0f)));
    Assert.That(e, Is.EqualTo(new Vector3(1f, 2f, 3f)));
}
```

Do not run Unity yet.

---

### Task 2: Shaders

**Files:**
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshCull.compute`
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshLit.hlsl`

**Interfaces:**
- Consumes: pack layout from Task 1; `MaxBatchedObjects = 256`
- Produces: cull kernel over `objectCount * clusterCount`; VS / `ClusterMeshSetup` decode packed id and index CBUFFER matrices

- [ ] **Step 1: Replace `ClusterMeshCull.compute`**

```hlsl
#pragma kernel CullClusters

#include "ClusterMeshBuffers.hlsl"

StructuredBuffer<ClusterHeader> _Clusters;
AppendStructuredBuffer<uint> _VisibleClusterIds;

uint _ClusterCount;
uint _ObjectCount;
uint _MaterialIndex;
int _IsolateIndex;
float4 _Planes[6];
float3 _WorldCameraPos;
float4x4 _ObjectLocalToWorld[256];

float3x3 Matrix3(float4x4 m)
{
    return float3x3(m[0].xyz, m[1].xyz, m[2].xyz);
}

void TransformAabb(ClusterHeader h, float4x4 m, out float3 worldCenter, out float3 worldExtents)
{
    float3 c = h.aabbCenter.xyz;
    float3 e = h.aabbExtents.xyz;
    worldCenter = mul(m, float4(c, 1.0f)).xyz;
    float3 x = mul((float3x3)m, float3(e.x, 0, 0));
    float3 y = mul((float3x3)m, float3(0, e.y, 0));
    float3 z = mul((float3x3)m, float3(0, 0, e.z));
    worldExtents = abs(x) + abs(y) + abs(z);
}

bool TestAabb(float3 c, float3 e)
{
    [unroll]
    for (int i = 0; i < 6; i++)
    {
        float3 n = _Planes[i].xyz;
        float r = e.x * abs(n.x) + e.y * abs(n.y) + e.z * abs(n.z);
        if (dot(n, c) + _Planes[i].w + r < 0.0f)
            return false;
    }
    return true;
}

bool TestCone(ClusterHeader h, float4x4 m)
{
    float cutoff = h.coneAxisCutoff.w;
    if (cutoff < 0.0f)
        return true;
    float3 apex = mul(m, float4(h.coneApex.xyz, 1.0f)).xyz;
    float3 axis = normalize(mul((float3x3)m, h.coneAxisCutoff.xyz));
    float3 view = _WorldCameraPos - apex;
    float len = length(view);
    if (len < 1e-6f)
        return true;
    return dot(view / len, axis) >= cutoff;
}

[numthreads(64, 1, 1)]
void CullClusters(uint3 id : SV_DispatchThreadID)
{
    uint total = _ObjectCount * _ClusterCount;
    uint i = id.x;
    if (i >= total)
        return;
    uint objectIndex = i / _ClusterCount;
    uint clusterIndex = i % _ClusterCount;
    ClusterHeader h = _Clusters[clusterIndex];
    if (h.materialIndex != _MaterialIndex)
        return;
    if (_IsolateIndex >= 0 && (int)clusterIndex != _IsolateIndex)
        return;
    float3 wc, we;
    TransformAabb(h, _ObjectLocalToWorld[objectIndex], wc, we);
    if (!TestAabb(wc, we) || !TestCone(h, _ObjectLocalToWorld[objectIndex]))
        return;
    _VisibleClusterIds.Append((objectIndex << 16) | clusterIndex);
}
```

Unity HLSL `float4x4` row vs mul: `mul(m, float4(c,1))` matches `Matrix4x4.MultiplyPoint3x4` if `m` was set with `SetMatrixArray` (Unity uploads column-major / its usual convention). Use the same `mul(M, float4(p,1))` as URP object-to-world.

- [ ] **Step 2: Update `ClusterMeshLit.hlsl` buffer + setup + fetch**

Replace the single-matrix uniforms and functions:

```hlsl
StructuredBuffer<ClusterHeader> _Clusters;
StructuredBuffer<ClusterVertex> _Vertices;
StructuredBuffer<uint> _Indices;
StructuredBuffer<uint> _VisibleClusterIds;
float4x4 _ObjectLocalToWorld[256];
float4x4 _ObjectWorldToLocal[256];

#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
void ClusterMeshSetup()
{
    uint packed = _VisibleClusterIds[unity_InstanceID];
    uint objectIndex = packed >> 16;
    unity_ObjectToWorld = _ObjectLocalToWorld[objectIndex];
    unity_WorldToObject = _ObjectWorldToLocal[objectIndex];
}
#endif

void FetchClusterVertex(uint vertexID, uint instanceID, out float3 positionOS, out float3 normalOS, out float4 tangentOS, out float2 uv)
{
    uint packed = _VisibleClusterIds[instanceID];
    uint clusterId = packed & 0xFFFFu;
    ClusterHeader h = _Clusters[clusterId];
    // ... remainder unchanged (degenerate + fetch)
}
```

Delete `_ClusterLocalToWorld` / `_ClusterWorldToLocal`.

---

### Task 3: DrawContext + SceneBatcher + Renderer

**Files:**
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshDrawContext.cs`
- Create: `Assets/ClusterMesh/Runtime/ClusterMeshSceneBatcher.cs`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshRenderer.cs`
- Create: `Assets/ClusterMesh/Tests/Editor/ClusterMeshBatcherTests.cs`

**Interfaces:**
- Consumes: limits, frustum world helpers, new shader uniforms
- Produces:
  - `ClusterMeshDrawContext.Draw(Matrix4x4, Camera)` still exists; adds `Draw(IList<Matrix4x4> localToWorld, Camera camera)`
  - `ClusterMeshSceneBatcher.Register/Unregister/Flush/ResetForTests/CountDrawCalls/CollectBatches`
  - `ClusterMeshBatchDesc` with `asset`, `camera`, `objectCount`, `materialCount`, `drawCallCount`

- [ ] **Step 1: DrawContext changes**

Property IDs: drop `LocalCameraPosId`; add `ObjectCountId`, `WorldCameraPosId`, `ObjectLocalToWorldId`, `ObjectWorldToLocalId` (`_ObjectCount`, `_WorldCameraPos`, `_ObjectLocalToWorld`, `_ObjectWorldToLocal`). Keep `LocalToWorldId` unused or remove.

Visible / args buffer length: `Mathf.Max(1, asset.clusters.Length) * ClusterMeshLimits.MaxBatchedObjects` for append.

Scratch arrays on the context: `Matrix4x4[] _l2w = new Matrix4x4[256]`, `_w2l = new Matrix4x4[256]`.

```csharp
public void Draw(Matrix4x4 localToWorld, Camera camera)
{
    _single[0] = localToWorld;
    Draw(_single, 1, camera);
}

public void Draw(IList<Matrix4x4> localToWorld, Camera camera)
{
    if (!IsReady || camera == null || localToWorld == null)
        return;
    Draw(localToWorld, localToWorld.Count, camera);
}

void Draw(IList<Matrix4x4> localToWorld, int count, Camera camera)
{
    if (count <= 0)
        return;

    ClusterMeshFrustum.WorldPlanes(camera, _planes);
    for (int i = 0; i < 6; i++)
        _planeVectors[i] = new Vector4(_planes[i].normal.x, _planes[i].normal.y, _planes[i].normal.z, _planes[i].distance);

    int chunkSize = ClusterMeshLimits.MaxBatchedObjects;
    int chunks = Mathf.CeilToInt(count / (float)chunkSize);
    for (int chunk = 0; chunk < chunks; chunk++)
    {
        int start = chunk * chunkSize;
        int n = Mathf.Min(chunkSize, count - start);
        Bounds worldBounds = new Bounds();
        bool hasBounds = false;
        for (int i = 0; i < n; i++)
        {
            Matrix4x4 l2w = localToWorld[start + i];
            _l2w[i] = l2w;
            _w2l[i] = l2w.inverse;
            Bounds b = TransformBounds(l2w);
            if (!hasBounds) { worldBounds = b; hasBounds = true; }
            else worldBounds.Encapsulate(b);
        }

        int groups = Mathf.CeilToInt((n * _asset.clusters.Length) / 64f);
        _cullShader.SetInt(ObjectCountId, n);
        _cullShader.SetInt(ClusterCountId, _asset.clusters.Length);
        _cullShader.SetInt(IsolateIndexId, IsolateIndex);
        _cullShader.SetVectorArray(PlanesId, _planeVectors);
        _cullShader.SetVector(WorldCameraPosId, camera.transform.position);
        _cullShader.SetMatrixArray(ObjectLocalToWorldId, _l2w);
        _cullShader.SetBuffer(_cullKernel, ClustersId, _clusterBuffer);

        for (int materialIndex = 0; materialIndex < _materials.Length; materialIndex++)
        {
            GraphicsBuffer visible = _visibleBuffers[materialIndex];
            visible.SetCounterValue(0);
            _cullShader.SetBuffer(_cullKernel, VisibleId, visible);
            _cullShader.SetInt(MaterialIndexId, materialIndex);
            _cullShader.Dispatch(_cullKernel, Mathf.Max(1, groups), 1, 1);

            _argsBuffers[materialIndex].SetData(_argsSeed);
            GraphicsBuffer.CopyCount(visible, _argsBuffers[materialIndex], 4);

            Material mat = _materials[materialIndex];
            mat.SetBuffer(ClustersId, _clusterBuffer);
            mat.SetBuffer(VerticesId, _vertexBuffer);
            mat.SetBuffer(IndicesId, _indexBuffer);
            mat.SetBuffer(VisibleId, visible);
            mat.SetMatrixArray(ObjectLocalToWorldId, _l2w);
            mat.SetMatrixArray(ObjectWorldToLocalId, _w2l);

            Graphics.DrawMeshInstancedIndirect(
                _template, 0, mat, worldBounds, _argsBuffers[materialIndex], 0, null,
                ShadowCastingMode.On, true, 0, camera);
        }
    }
}
```

Constructor: allocate `_single = new Matrix4x4[1]`. Append size `clusterCount * MaxBatchedObjects`.

`SetMatrixArray` requires the array length to match the shader array (256). Always pass the full 256-length scratch arrays.

- [ ] **Step 2: Write `ClusterMeshSceneBatcher.cs` in full**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ClusterMesh
{
    public readonly struct ClusterMeshBatchDesc
    {
        public readonly ClusterMeshAsset asset;
        public readonly Camera camera;
        public readonly int objectCount;
        public readonly int materialCount;
        public readonly int drawCallCount;

        public ClusterMeshBatchDesc(ClusterMeshAsset asset, Camera camera, int objectCount, int materialCount, int drawCallCount)
        {
            this.asset = asset;
            this.camera = camera;
            this.objectCount = objectCount;
            this.materialCount = materialCount;
            this.drawCallCount = drawCallCount;
        }
    }

    public static class ClusterMeshSceneBatcher
    {
        static readonly List<ClusterMeshRenderer> Renderers = new List<ClusterMeshRenderer>();
        static readonly Dictionary<ClusterMeshAsset, ClusterMeshDrawContext> Contexts = new Dictionary<ClusterMeshAsset, ClusterMeshDrawContext>();
        static readonly Dictionary<ClusterMeshAsset, int> Refs = new Dictionary<ClusterMeshAsset, int>();
        static readonly List<Matrix4x4> Matrices = new List<Matrix4x4>(64);
        static readonly List<ClusterMeshRenderer> Group = new List<ClusterMeshRenderer>(64);
        static int _flushedFrame = int.MinValue;
        static bool _loggedError;

        public static void Register(ClusterMeshRenderer renderer)
        {
            if (renderer == null || renderer.asset == null || renderer.asset.clusters == null || renderer.asset.clusters.Length == 0)
                return;
            if (!Renderers.Contains(renderer))
                Renderers.Add(renderer);
        }

        public static void Unregister(ClusterMeshRenderer renderer)
        {
            Renderers.Remove(renderer);
        }

        public static void Flush()
        {
            if (Time.frameCount == _flushedFrame)
                return;
            _flushedFrame = Time.frameCount;

            for (int i = Renderers.Count - 1; i >= 0; i--)
            {
                if (Renderers[i] == null)
                    Renderers.RemoveAt(i);
            }

            var seen = new HashSet<int>();
            for (int i = 0; i < Renderers.Count; i++)
            {
                if (seen.Contains(i))
                    continue;
                ClusterMeshRenderer seed = Renderers[i];
                Camera camera = ResolveCamera(seed);
                if (camera == null || seed.asset == null)
                    continue;

                Group.Clear();
                Matrices.Clear();
                for (int j = i; j < Renderers.Count; j++)
                {
                    ClusterMeshRenderer other = Renderers[j];
                    if (other == null || other.asset != seed.asset || ResolveCamera(other) != camera)
                        continue;
                    seen.Add(j);
                    Group.Add(other);
                    Matrices.Add(other.transform.localToWorldMatrix);
                }

                ClusterMeshDrawContext ctx = GetOrCreate(seed);
                if (ctx == null || !ctx.IsReady)
                    continue;
                ctx.Draw(Matrices, camera);
            }
        }

        public static int CountDrawCalls(int objectCount, int materialCount)
        {
            if (objectCount <= 0 || materialCount <= 0)
                return 0;
            int chunks = Mathf.CeilToInt(objectCount / (float)ClusterMeshLimits.MaxBatchedObjects);
            return chunks * materialCount;
        }

        public static void CollectBatches(IList<ClusterMeshRenderer> source, List<ClusterMeshBatchDesc> dest)
        {
            dest.Clear();
            if (source == null)
                return;
            var seen = new HashSet<int>();
            for (int i = 0; i < source.Count; i++)
            {
                if (seen.Contains(i))
                    continue;
                ClusterMeshRenderer seed = source[i];
                if (seed == null || seed.asset == null)
                    continue;
                Camera camera = ResolveCamera(seed);
                if (camera == null)
                    continue;
                int count = 0;
                for (int j = i; j < source.Count; j++)
                {
                    ClusterMeshRenderer other = source[j];
                    if (other == null || other.asset != seed.asset || ResolveCamera(other) != camera)
                        continue;
                    seen.Add(j);
                    count++;
                }

                int materials = seed.asset.materials != null && seed.asset.materials.Length > 0
                    ? seed.asset.materials.Length
                    : 1;
                dest.Add(new ClusterMeshBatchDesc(seed.asset, camera, count, materials, CountDrawCalls(count, materials)));
            }
        }

        public static void ResetForTests()
        {
            Renderers.Clear();
            foreach (var kv in Contexts)
                kv.Value?.Dispose();
            Contexts.Clear();
            Refs.Clear();
            _flushedFrame = int.MinValue;
            _loggedError = false;
        }

        static Camera ResolveCamera(ClusterMeshRenderer renderer)
        {
            return renderer.targetCamera != null ? renderer.targetCamera : Camera.main;
        }

        static ClusterMeshDrawContext GetOrCreate(ClusterMeshRenderer seed)
        {
            ClusterMeshAsset asset = seed.asset;
            if (Contexts.TryGetValue(asset, out var existing))
                return existing;

            var ctx = new ClusterMeshDrawContext(asset, seed.cullShader, seed.litShader);
            if (!ctx.IsReady)
            {
                if (!_loggedError && !string.IsNullOrEmpty(ctx.Error))
                {
                    Debug.LogError("ClusterMesh: " + ctx.Error);
                    _loggedError = true;
                }

                ctx.Dispose();
                return null;
            }

            Contexts[asset] = ctx;
            return ctx;
        }
    }
}
```

Note: `GetOrCreate` as written does not refcount dispose on last unregister. Spec requires dispose when the last renderer of that asset unregisters. Implement Unregister as:

```csharp
public static void Unregister(ClusterMeshRenderer renderer)
{
    bool removed = Renderers.Remove(renderer);
    if (!removed || renderer == null || renderer.asset == null)
        return;
    ClusterMeshAsset asset = renderer.asset;
    for (int i = 0; i < Renderers.Count; i++)
    {
        if (Renderers[i] != null && Renderers[i].asset == asset)
            return;
    }

    if (Contexts.TryGetValue(asset, out var ctx))
    {
        ctx.Dispose();
        Contexts.Remove(asset);
    }
}
```

- [ ] **Step 3: Replace renderer body**

```csharp
using UnityEngine;

namespace ClusterMesh
{
    [ExecuteAlways]
    public sealed class ClusterMeshRenderer : MonoBehaviour
    {
        public ClusterMeshAsset asset;
        public Camera targetCamera;
        public ComputeShader cullShader;
        public Shader litShader;

        bool _registered;

        public void EnsureInitialized()
        {
#if UNITY_EDITOR
            if (cullShader == null)
                cullShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
#endif
            if (litShader == null)
                litShader = Shader.Find("ClusterMesh/Lit");
            SyncRegistration();
        }

        void OnEnable()
        {
            EnsureInitialized();
        }

        void OnDisable()
        {
            ClusterMeshSceneBatcher.Unregister(this);
            _registered = false;
        }

        void OnValidate()
        {
            ClusterMeshSceneBatcher.Unregister(this);
            _registered = false;
            if (isActiveAndEnabled)
                EnsureInitialized();
        }

        void LateUpdate()
        {
            EnsureInitialized();
            ClusterMeshSceneBatcher.Flush();
        }

        void SyncRegistration()
        {
            bool has = asset != null && asset.clusters != null && asset.clusters.Length > 0;
            if (has && isActiveAndEnabled)
            {
                ClusterMeshSceneBatcher.Register(this);
                _registered = true;
            }
            else if (_registered)
            {
                ClusterMeshSceneBatcher.Unregister(this);
                _registered = false;
            }
        }
    }
}
```

- [ ] **Step 4: Write `ClusterMeshBatcherTests.cs` in full** (see Task 4 file in the implementation; include pack, CountDrawCalls, CollectBatches 10/2-asset/2-camera/unregister). Each test `TearDown` calls `ClusterMeshSceneBatcher.ResetForTests()` and destroys created objects.

Do not run Unity yet.

---

### Task 4: Demo 10 instances

**Files:**
- Modify: `Assets/ClusterMesh/Editor/ClusterMeshDemoSceneMenu.cs`
- Modify: `Assets/ClusterMesh/Tests/Editor/ClusterMeshDemoSceneTests.cs`

**Interfaces:**
- Produces: `InstanceCount = 10`; parent with 10 child renderers sharing one asset

`CreateDemoObjects`: bake once, parent `ClusterMeshDemo`, loop `i in [0,10)` create child, set position `x = (i - 4.5f) * 1.5f`, assign same asset / shaders / camera, activate after all assigned.

`CreateDemoScene`: persist once, assign `stored` to every child renderer.

Demo test: `GetComponentsInChildren<ClusterMeshRenderer>()` length 10, all `asset` references equal. Keep `LogAssert.Expect` for capability.

Do not run Unity yet.

---

### Task 5: Final EditMode run (only test gate)

Run:

```text
"C:\Program Files\Tuanjie\Hub\Editor\2022.3.48t2\Editor\Tuanjie.exe" -batchmode -nographics -projectPath D:/MiniGameV2 -runTests -testPlatform EditMode -assemblyNames ClusterMesh.Editor.Tests -testResults D:/MiniGameV2/.superpowers/sdd/batching-editmode-results.xml -logFile D:/MiniGameV2/.superpowers/sdd/batching-editmode.log
```

Expected: `test-run` `failed="0"`. Fix compile/assert failures against the spec, then re-run. Record the XML path in `.superpowers/sdd/progress.md`.

---

## Spec coverage

| Spec section | Task |
| --- | --- |
| Pack / Max 256 / 4 vertex buffers | 1, 2, 3 |
| World cull | 1, 2, 3 |
| Batcher key / flush / cache | 3 |
| Renderer no longer draws | 3 |
| Viewer private context | unchanged API `Draw(Matrix4x4, Camera)` |
| Demo 10 | 4 |
| Tests + final green | 3, 4, 5 |
