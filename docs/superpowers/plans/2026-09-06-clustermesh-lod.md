# ClusterMesh Two-Level LOD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. This session only落地 docs; do not implement until the user says to. Do not commit unless the user asks.

**Goal:** Two-level cluster LOD (leaves + one parent layer) selected in compute after AABB/cone, without changing Indirect, batching, or the 4-buffer vertex path.

**Architecture:** Reuse pad slots on `ClusterHeader` (still 96 bytes). `ClusterMeshLod` owns the C# selection formula. Baker appends simplified parents and writes `parentIndex`. Cull skips LOD when `hierarchyVersion < 1` or `threshold <= 0` (leaves only).

**Tech Stack:** Existing ClusterMesh Runtime/Editor/Tests, `ClusterMeshCull.compute`, Tuanjie 2022.3.48t2 EditMode.

## Global Constraints

- Namespace `ClusterMesh`. Tests in `ClusterMesh.Tests`.
- Write `Assets/ClusterMesh/**` and these docs only. Do not edit FogOfWar, Packages, ProjectSettings, or the 2026-09-04 spec body.
- Header stride stays 96. Vertex StructuredBuffers stay 4. `MaxBatchedObjects = 256`.
- Re-bake is required to **see** LOD. Old assets (`hierarchyVersion == 0`) must keep current pixels.
- Default `lodErrorThreshold = 0` → leaves only.
- No URP Renderer Feature, Hi-Z, 3+ LOD levels, lock borders.
- Final verify: Tuanjie `2022.3.48t2` `-runTests` assembly `ClusterMesh.Editor.Tests`. Editor must be closed.
- Do not invent passing test output.

## File map

- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshTypes.cs` — header pads → parent/error/flags; bake settings + result version
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshAsset.cs` — `hierarchyVersion`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshLimits.cs` — `ClusterHeaderStride`
- Create: `Assets/ClusterMesh/Runtime/ClusterMeshLod.cs`
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshBuffers.hlsl`
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshCull.compute`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshDrawContext.cs`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshRenderer.cs`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshSceneBatcher.cs`
- Create: `Assets/ClusterMesh/Editor/ClusterMeshLodBaker.cs`
- Modify: `Assets/ClusterMesh/Editor/ClusterMeshBaker.cs`
- Modify: `Assets/ClusterMesh/Editor/ClusterMeshViewerWindow.cs`
- Create: `Assets/ClusterMesh/Tests/Editor/ClusterMeshLodTests.cs`
- Modify: `Assets/ClusterMesh/Tests/Editor/ClusterMeshBakerTests.cs`

Spec: `docs/superpowers/specs/2026-09-06-clustermesh-lod-design.md`

---

### Task 1: Header + C# LOD formula

**Files:**
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshTypes.cs`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshAsset.cs`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshLimits.cs`
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshBuffers.hlsl`
- Create: `Assets/ClusterMesh/Runtime/ClusterMeshLod.cs`
- Create: `Assets/ClusterMesh/Tests/Editor/ClusterMeshLodTests.cs`

**Interfaces:**
- Produces: `ClusterHeader.parentIndex` (`int`, `-1` = none), `lodError` (`float`), `flags` (`uint`, bit0 parent). `ClusterMeshLimits.ClusterHeaderStride = 96`. `ClusterMeshLod.NoParent`, `FlagParent = 1u`, `IsParent`, `ProjectionScale(Camera)`, `ProjectError`, `IsVisible(...)`.
- `CopyFrom` writes `hierarchyVersion` from `result.hierarchyVersion`.

- [ ] **Step 1: Write failing tests first**

```csharp
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshLodTests
    {
        [Test]
        public void Header_StrideIs96()
        {
            Assert.That(Marshal.SizeOf<ClusterHeader>(), Is.EqualTo(96));
            Assert.That(ClusterMeshLimits.ClusterHeaderStride, Is.EqualTo(96));
        }

        [Test]
        public void ThresholdZero_HidesParents_ShowsLeaves()
        {
            var leaf = new ClusterHeader { parentIndex = 0, lodError = 0f, flags = 0 };
            var parent = new ClusterHeader { parentIndex = ClusterMeshLod.NoParent, lodError = 0.5f, flags = ClusterMeshLod.FlagParent };
            Assert.That(ClusterMeshLod.IsVisible(leaf, parent, 1f, 2f, 100f, 0f, 1), Is.True);
            Assert.That(ClusterMeshLod.IsVisible(parent, null, 2f, 0f, 100f, 0f, 1), Is.False);
        }

        [Test]
        public void LargeThreshold_ShowsParent_HidesChild()
        {
            var parent = new ClusterHeader { parentIndex = ClusterMeshLod.NoParent, lodError = 0.5f, flags = ClusterMeshLod.FlagParent };
            var leaf = new ClusterHeader { parentIndex = 0, lodError = 0f, flags = 0 };
            const float scale = 100f;
            const float t = 50f;
            Assert.That(ClusterMeshLod.IsVisible(leaf, parent, 1f, 1f, scale, t, 1), Is.False);
            Assert.That(ClusterMeshLod.IsVisible(parent, null, 1f, 0f, scale, t, 1), Is.True);
        }

        [Test]
        public void VersionZero_AlwaysVisibleRegardlessOfFlags()
        {
            var parent = new ClusterHeader { flags = ClusterMeshLod.FlagParent, lodError = 0.5f, parentIndex = ClusterMeshLod.NoParent };
            Assert.That(ClusterMeshLod.IsVisible(parent, null, 1f, 0f, 100f, 50f, 0), Is.True);
        }
    }
}
```

`IsVisible` 对 `hierarchyVersion < 1` 必须返回 `true`（cull 用它表示「LOD 不否决」；version 0 资产没有父块几何）。

Large-threshold 用例：`ProjectError(0.5, 1, 100) = 50`，`T=50` 时 `selfProjected < T` 对父是 `50 < 50` 为假。把父 `lodError` 调到 `0.4` 或 `T` 调到 `51`，使 `parentProjected < T` 且叶子因 `parentProjected < T` 被藏。实现时以测试里的数字为准，先算再写断言。

- [ ] **Step 2: Run tests — expect compile fail / missing `ClusterMeshLod`**

```
Tuanjie 2022.3.48t2 -batchmode -nographics -projectPath D:/MiniGameV2 -runTests -testPlatform EditMode -assemblyNames ClusterMesh.Editor.Tests
```

- [ ] **Step 3: Replace pads in C# header and HLSL; add limits + asset field**

`ClusterHeader`：删 `pad0/pad1/pad2`，改为 `parentIndex` / `lodError` / `flags`。默认 `parentIndex = -1` 不能靠 struct 默认，Baker 必须显式写。

`ClusterMeshBuffers.hlsl` 同步三字段（`int parentIndex; float lodError; uint flags;`）。

`ClusterMeshBakeResult.hierarchyVersion`、`ClusterMeshAsset.hierarchyVersion`、`CopyFrom` 赋值。

`ClusterMeshBakeSettings.buildLodHierarchy = true`。

- [ ] **Step 4: Implement `ClusterMeshLod.cs`**

```csharp
using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshLod
    {
        public const int NoParent = -1;
        public const uint FlagParent = 1u;

        public static bool IsParent(uint flags) => (flags & FlagParent) != 0;

        public static float ProjectionScale(Camera camera)
        {
            if (camera == null)
                return 0f;
            float h = camera.pixelHeight;
            if (camera.orthographic)
                return h / Mathf.Max(2f * camera.orthographicSize, 1e-4f);
            float tanHalf = Mathf.Tan(0.5f * camera.fieldOfView * Mathf.Deg2Rad);
            return (0.5f * h) / Mathf.Max(tanHalf, 1e-6f);
        }

        public static float ProjectError(float lodError, float distance, float scale)
        {
            return lodError * scale / Mathf.Max(distance, 1e-4f);
        }

        public static bool IsVisible(
            in ClusterHeader self,
            ClusterHeader? parent,
            float selfDistance,
            float parentDistance,
            float scale,
            float threshold,
            int hierarchyVersion)
        {
            if (hierarchyVersion < 1)
                return true;
            if (threshold <= 0f)
                return !IsParent(self.flags);
            float selfP = ProjectError(self.lodError, selfDistance, scale);
            if (selfP >= threshold)
                return false;
            if (!parent.HasValue || self.parentIndex < 0)
                return true;
            float parentP = ProjectError(parent.Value.lodError, parentDistance, scale);
            return parentP >= threshold;
        }
    }
}
```

`ClusterHeader?` 在无 `nullable` 上下文可用 `bool hasParent` + `ClusterHeader parent` 重载，避免改 asmdef。优先：

```csharp
public static bool IsVisible(in ClusterHeader self, in ClusterHeader parent, bool hasParent, ...)
```

测试跟着改，不要引入 `Nullable<ClusterHeader>` 如果 Runtime 未开 nullable。

- [ ] **Step 5: Recompute the large-threshold numbers so the test is correct, then run EditMode for `ClusterMeshLodTests`**

Expected: those tests pass. Existing baker tests still pass if they do not read pads.

---

### Task 2: Compute cull + DrawContext

**Files:**
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshCull.compute`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshDrawContext.cs`

**Interfaces:**
- Consumes: header fields + `ClusterMeshLod` formulas.
- Produces: `_HierarchyVersion`, `_LodErrorThreshold`, `_LodProjectionScale` on the cull shader. `DrawContext.LodErrorThreshold` (default 0), `HierarchyVersion` from asset.

- [ ] **Step 1: Add uniforms and `TestLod` after Cone, before Append**

```hlsl
int _HierarchyVersion;
float _LodErrorThreshold;
float _LodProjectionScale;

bool TestLod(ClusterHeader h, float3 worldCenter)
{
    if (_HierarchyVersion < 1)
        return true;
    if (_LodErrorThreshold <= 0.0f)
        return (h.flags & 1u) == 0u;
    float dist = max(length(_WorldCameraPos - worldCenter), 1e-4f);
    float selfP = h.lodError * _LodProjectionScale / dist;
    if (selfP >= _LodErrorThreshold)
        return false;
    if (h.parentIndex < 0)
        return true;
    uint pidx = (uint)h.parentIndex;
    if (pidx >= _ClusterCount)
        return true;
    ClusterHeader p = _Clusters[pidx];
    float3 pc, pe;
    // parent world center: caller already has object matrix — pass float4x4 m into TestLod
    return true; // replace with parent project >= threshold
}
```

实现时 `TestLod(h, m)`：对 self 与 parent 都 `TransformAabb` 取 `worldCenter`，公式与 C# `ProjectError` 相同。非法 `parentIndex` 当无父。

在现有 `if (!TestCone(...)) return;` 之后：`if (!TestLod(h, _ObjectLocalToWorld[objectIndex])) return;`

- [ ] **Step 2: DrawContext sets the three uniforms every dispatch**

```csharp
static readonly int HierarchyVersionId = Shader.PropertyToID("_HierarchyVersion");
static readonly int LodErrorThresholdId = Shader.PropertyToID("_LodErrorThreshold");
static readonly int LodProjectionScaleId = Shader.PropertyToID("_LodProjectionScale");

public float LodErrorThreshold { get; set; }
```

构造后只读：`HierarchyVersion => _asset != null ? _asset.hierarchyVersion : 0`。

Dispatch 前：

```csharp
_cullShader.SetInt(HierarchyVersionId, _asset.hierarchyVersion);
_cullShader.SetFloat(LodErrorThresholdId, LodErrorThreshold);
_cullShader.SetFloat(LodProjectionScaleId, ClusterMeshLod.ProjectionScale(camera));
```

- [ ] **Step 3: Run full `ClusterMesh.Editor.Tests`**

Expected: 仍绿。阈值默认 0，旧测试资产 `hierarchyVersion` 默认 0。

---

### Task 3: Baker 4-to-1 + simplify

**Files:**
- Create: `Assets/ClusterMesh/Editor/ClusterMeshLodBaker.cs`
- Modify: `Assets/ClusterMesh/Editor/ClusterMeshBaker.cs`
- Modify: `Assets/ClusterMesh/Tests/Editor/ClusterMeshBakerTests.cs`

**Interfaces:**
- Consumes: leaf clusters already emitted.
- Produces: extra parent clusters; leaf `parentIndex` filled; `result.hierarchyVersion = settings.buildLodHierarchy ? 1 : 0`.

- [ ] **Step 1: Failing baker tests**

```csharp
[Test]
public void Bake_LodOff_Grid_TriangleCountEqualsSource()
{
    var mesh = ClusterMeshTestMeshes.Grid(16, 16);
    var result = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings { buildLodHierarchy = false });
    int baked = 0;
    foreach (var c in result.clusters)
        baked += (int)c.triangleCount;
    Assert.That(baked, Is.EqualTo(mesh.triangles.Length / 3));
    Assert.That(result.hierarchyVersion, Is.EqualTo(0));
    Object.DestroyImmediate(mesh);
}

[Test]
public void Bake_LodOn_Grid_HasParent_LeafTrisEqualSource()
{
    var mesh = ClusterMeshTestMeshes.Grid(16, 16);
    var result = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
    int leafTris = 0;
    int parents = 0;
    foreach (var c in result.clusters)
    {
        if (ClusterMeshLod.IsParent(c.flags))
            parents++;
        else
            leafTris += (int)c.triangleCount;
    }
    Assert.That(parents, Is.GreaterThan(0));
    Assert.That(leafTris, Is.EqualTo(mesh.triangles.Length / 3));
    Assert.That(result.hierarchyVersion, Is.EqualTo(1));
    Object.DestroyImmediate(mesh);
}
```

把现有 `Bake_GridLargerThanBudget_SplitsAndCoversEveryTriangleOnce` 改为显式 `buildLodHierarchy = false`，或改为只数叶子，避免默认 true 时失败。

- [ ] **Step 2: `ClusterMeshLodBaker.BuildParents`**

输入：`List<ClusterHeader> clusters`, `List<ClusterVertex> vertices`, `List<uint> indices`, 本 submesh 叶子 `[leafStart, leafEnd)`，`materialIndex`，budgets。

算法：

1. `used[]` 标记叶子是否已进组。
2. 当剩余未用 `>= 4`：取最小未用下标 `i`，在未用叶子里按 `sqrMagnitude(aabbCenter_i - aabbCenter_j)` 取最近 3 个，组成 4 个下标。
3. 把 4 个叶子的三角展开成物体空间 `Vector3[]` + 局部三角（用 header 的 vertexOffset/indexOffset/triangleCount）。
4. 最短边折叠：当 `vertCount > 64` 或 `triCount > 124`，找最短边 `(a,b)`，把 `b` 焊到 `a`，重写索引，去掉退化三角。若无法再折（只剩独立点）则停。
5. 用现有 `Emit` 路径或复制 `EmitCluster` 的 AABB/Cone 写入新 header：`flags = FlagParent`，`parentIndex = -1`，`lodError = max over parent verts of min distance to child triangle soup`。
6. 四个叶子 `parentIndex = newParentIndex`。

`lodError` 下限：若计算结果 `< 1e-6`，存 `1e-6`，避免父被当成精确叶子。

- [ ] **Step 3: Call from `Bake` after each submesh（或全部叶子 emit 完按 material 分组）**

同一 submesh 即同一材质。`BakeSubmesh` 返回后记下 `leafStart = clusters.Count` 之前，emit 完 `leafEnd`，再 `BuildParents`。

`Bake` 结束：`hierarchyVersion = settings.buildLodHierarchy ? 1 : 0`。`buildLodHierarchy == false` 时不调用 BuildParents，叶子保持 `parentIndex = -1`。

- [ ] **Step 4: Run baker + lod tests**

Expected: 绿。单三角资产无父。

---

### Task 4: Viewer / Renderer / Batcher

**Files:**
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshRenderer.cs`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshSceneBatcher.cs`
- Modify: `Assets/ClusterMesh/Editor/ClusterMeshViewerWindow.cs`
- Modify: `Assets/ClusterMesh/Editor/ClusterMeshBakerWindow.cs` — 一行说明要重 Bake 才有层次

**Interfaces:**
- Produces: `ClusterMeshRenderer.lodErrorThreshold` default `0`. Viewer slider default `0`. Batcher `max` of batch.

- [ ] **Step 1: Renderer field + batcher max**

```csharp
[Tooltip("Screen-pixel LOD error. 0 = leaves only.")]
public float lodErrorThreshold;
```

Flush 里与 `clusterColors` 一起扫批：

```csharp
float lodT = seed.lodErrorThreshold;
// in the other-loop:
lodT = Mathf.Max(lodT, other.lodErrorThreshold);
// after GetOrCreate:
ctx.LodErrorThreshold = lodT;
```

Cone / shadows / colors 现有逻辑一字不改。

- [ ] **Step 2: Viewer**

`float _lodError = 0`。`EditorGUILayout.Slider("LOD Error Threshold", _lodError, 0f, 64f)`。赋 `_context.LodErrorThreshold = _lodError`。HelpBox：`0 = 只叶子；拉大才出父块。要层次需重新 Bake。`

- [ ] **Step 3: Baker window HelpBox 加一句**

「勾默认层次时重新 Bake。旧资产不重 Bake 则只有叶子。」

- [ ] **Step 4: Run full EditMode assembly**

Expected: 全绿。合批测试阈值默认 0。

---

### Task 5: 收尾验证

- [ ] **Step 1: Confirm Tuanjie is not locking the project**
- [ ] **Step 2: Run**

```
"C:\Program Files\Tuanjie\Hub\Editor\2022.3.48t2\Editor\Tuanjie.exe" -batchmode -nographics -projectPath D:/MiniGameV2 -runTests -testPlatform EditMode -assemblyNames ClusterMesh.Editor.Tests -testResults D:/MiniGameV2/.superpowers/sdd/lod-editmode-results.xml -logFile D:/MiniGameV2/.superpowers/sdd/lod-editmode.log
```

Expected: `failed="0"`。把实际 `total/passed` 写进结果，不要编造。

- [ ] **Step 3: Manual（实现后告诉用户）**

重 Bake Mia → Viewer 阈值 0 应与现在相同 → 拉大应看到大块换色（Cluster Colors 开着更明显）。场景 renderer 默认 0 不该变画面。

---

## Spec coverage

| Spec section | Task |
| --- | --- |
| Header 96 / fields | 1 |
| hierarchyVersion 0/1 | 1, 3 |
| Formula + T<=0 | 1, 2 |
| Cull uniforms | 2 |
| 4-to-1 + collapse | 3 |
| Viewer / renderer / max | 4 |
| Tests + Tuanjie | 1–5 |
| No Hi-Z / 3+ levels / lock border | 未做，符合 non-goals |

## Placeholder scan

无 TBD。`IsVisible` 在 Task 1 用 `hasParent` 重载，不用未定义的 nullable。Task 2 的 HLSL 草稿在实现时必须写完 parent 投影，不能留 `return true`。
