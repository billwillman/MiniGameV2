# ClusterMesh 照明 P3 Implementation Plan

> **For agentic workers:** Implement inline. TDD. Do not commit unless asked. Run only after Motion Vector 时机计划完成。

**Goal:** 每物体 Light Probe SH 进 `bakedGI`；Forward 吃 URP 雾。

**Architecture:** `ClusterMeshLightProbes.Evaluate/Pack` → `StructuredBuffer<ClusterMeshObjectSH>` → `SampleSH9`。Forward `ComputeFogFactor`。不改 CBUFFER 宽度。

**Tech Stack:** Tuanjie 2022.3 / URP 14 `Lighting.hlsl` `SampleSH9`。

## Global Constraints

- 不改 64/124、header 96、组 48、合批键、UV pack、FogOfWar、Packages。
- 不做 lightmap、DepthNormals、APV、矩阵 GraphicsBuffer。
- GBuffer 不加雾。`UNITY_TUANJIEGI` 闸不改语义。
- 不提交 unless asked。

## File map

- Create: `Assets/ClusterMesh/Runtime/ClusterMeshLightProbes.cs`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshTypes.cs`（或 LightProbes 文件内 struct）
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshDrawContext.cs`
- Modify: `Assets/ClusterMesh/Runtime/ClusterSkinnedMeshDrawContext.cs`
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshLit.hlsl`
- Modify: `Assets/ClusterMesh/Shaders/ClusterSkinnedMeshLit.hlsl`
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshLit.shader`
- Modify: `Assets/ClusterMesh/Shaders/ClusterSkinnedMeshLit.shader`
- Create: `Assets/ClusterMesh/Tests/Editor/ClusterMeshLightProbesTests.cs`
- Modify: 现有 shader 源文件断言测试（`ClusterMeshUrpTests` 或新测试）

---

### Task 1: Evaluate / Pack + 单测

**Interfaces:**
- Produces: `ClusterMeshObjectSH`（7×Vector4，stride 112）
- Produces: `ClusterMeshLightProbes.Evaluate(Vector3)` → `SphericalHarmonicsL2`
- Produces: `ClusterMeshLightProbes.Pack(SphericalHarmonicsL2, out ClusterMeshObjectSH)`

- [ ] **Step 1: 写失败测试** `ClusterMeshLightProbesTests`
  - `Pack_L0Only_PositiveYMatchesHandEval`
  - `Evaluate_NoProbeGroup_EqualsAmbientProbe`
  - `ObjectSH_Stride_Is112`

- [ ] **Step 2: 跑测试，确认失败**

- [ ] **Step 3: 实现 struct + Evaluate + Pack**（URP `unity_SH*` 布局，与 `SampleSH9` 一致）

- [ ] **Step 4: 测试转绿**

---

### Task 2: DrawContext 上传 + shader 采样 + 雾

- [ ] **Step 1: 写失败测试（源文件）**
  - Lit / SkinnedLit hlsl 含 `_ObjectSH` 与 `SampleSH9`，不含 `bakedGI = SampleSH(`
  - Forward shader 含 `multi_compile_fog`；GBuffer pass 不含
  - Frag 含 `ComputeFogFactor`，不含 `fogCoord = 0`

- [ ] **Step 2: 跑测试，确认失败**

- [ ] **Step 3: Prepare 进槽 Evaluate；GraphicsBuffer `_ObjectSH` SetData；Bind SetBuffer。两边 hlsl/shader 改采样与雾**

- [ ] **Step 4: 跑 `ClusterMesh.Editor.Tests`。期望全绿**

- [ ] **Step 5: 不提交**
