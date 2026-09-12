# ClusterMesh Motion Vector 时机 Implementation Plan

> **For agentic workers:** Implement inline. TDD. Do not commit unless asked.

**Goal:** ClusterMesh 物体 MV 写在 `AfterRenderingSkybox + 1`，早于 Radiant `+ 2`。

**Architecture:** `ClusterMeshUrpBridge.MotionVectorPassEvent` 为唯一挂点；Feature `Create` 引用它。历史矩阵与默认关开关不动。

**Tech Stack:** Tuanjie 2022.3 / URP 14，`ClusterMesh.Editor.Tests`。

## Global Constraints

- 不改 64/124、header 96、组 48、合批键、FogOfWar、Packages、Radiant、玩法 URP 资产。
- 不改 `enableMotionVectors` 默认值，不改场景勾选。
- 不实现 LOD 滞回、P3、Depth Prepass。
- 不提交 unless asked。

## File map

- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshUrpBridge.cs`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshUrpFeature.cs`
- Test: `Assets/ClusterMesh/Tests/Editor/ClusterMeshUrpTests.cs`

---

### Task 1: 挂点常量 + 单测

**Files:**
- Test: `Assets/ClusterMesh/Tests/Editor/ClusterMeshUrpTests.cs`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshUrpBridge.cs`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshUrpFeature.cs`

- [ ] **Step 1: 写失败测试**

在 `ClusterMeshUrpTests` 增加：

```csharp
[Test]
public void MotionVectorPassEvent_IsAfterSkyboxBeforeRadiant()
{
    Assert.That(
        ClusterMeshUrpBridge.MotionVectorPassEvent,
        Is.EqualTo((RenderPassEvent)((int)RenderPassEvent.AfterRenderingSkybox + 1)));
    string feature = System.IO.File.ReadAllText(
        "Assets/ClusterMesh/Runtime/ClusterMeshUrpFeature.cs");
    Assert.That(feature, Does.Contain("ClusterMeshUrpBridge.MotionVectorPassEvent"));
    Assert.That(feature, Does.Not.Contain("BeforeRenderingPostProcessing"));
}
```

- [ ] **Step 2: 跑该测试，确认失败**（缺常量或仍是 Post-1）

- [ ] **Step 3: 最小实现**

`ClusterMeshUrpBridge`：

```csharp
public static readonly RenderPassEvent MotionVectorPassEvent =
    (RenderPassEvent)((int)RenderPassEvent.AfterRenderingSkybox + 1);
```

`ClusterMeshUrpFeature.Create`：`_motionPass = new ClusterMeshUrpPass(ClusterMeshUrpBridge.MotionVectorPassEvent, ClusterMeshUrpPhase.Motion);`

- [ ] **Step 4: 跑 `ClusterMesh.Editor.Tests`（编辑器未占用时）。期望：新测试绿，旧 MV 默认关 / pass 索引仍绿**

- [ ] **Step 5: 不提交**
