# ClusterMesh CPU Object Cull Implementation Plan

> **For agentic workers:** This session implements inline and runs EditMode tests last (user: 可以开始了 / 先落盘再修改再测试). Do not commit unless asked.

**Goal:** Drop instances that are outside the camera frustum and the directional shadow prism before dispatch; one compute, two lists so the color pass does not pay off-screen casters.

**Architecture:** Pure `ClusterMeshObjectCull` builds reused 6-plane sets. `Draw` compacts objects on the main thread, then `CullClusters` appends color (`_Planes` + cone) and shadow (`_ShadowPlanes`, no cone) in one dispatch.

**Tech Stack:** Tuanjie 2022.3, URP `RenderSettings.sun` + `UniversalRenderPipeline.asset.shadowDistance`, existing Indirect / ShadowCaster pass.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-09-06-clustermesh-cpu-object-cull-design.md`
- No Job/Burst. No `new Plane[]` per frame. No `FindObjects`. No mutate `camera.farClipPlane`.
- Header 96, group 48, four vertex streams unchanged. No rebake.
- `CountDrawCalls` stays color-only. Demo 10-in-view is not the perf bar.
- Do not invent passing tests. Verify with `ClusterMesh.Editor.Tests`.
- Do not commit unless asked.

## File map

- Create: `Assets/ClusterMesh/Runtime/ClusterMeshObjectCull.cs`
- Create: `Assets/ClusterMesh/Tests/Editor/ClusterMeshCpuCullTests.cs`
- Modify: `ClusterMeshRenderer.cs`, `ClusterMeshSceneBatcher.cs`, `ClusterMeshDrawContext.cs`, `ClusterMeshCull.compute`, `ClusterMeshBatcherTests.cs`
- Spec status → Implemented after tests

---

### Task 1: Object cull helpers + tests

- [x] `KeepObject` / extrude / receiver / Sun-only / compact 1-of-4
- [x] `CountIndirectDraws`
- [x] `enableCpuObjectCull` default true

### Task 2: Batcher + Draw + compute two lists

- [x] Flush collects per-object flags and OR `castShadows`
- [x] Compact then one Dispatch, color Off + ShadowsOnly when split
- [x] Fail-safe: no Sun → old single draw

### Task 3: Verify

- [x] `ClusterMesh.Editor.Tests` batchmode — 103/103 passed, 0 failed
- [x] 闪屏：同一 Material 上先主画再 `SetBuffer` 阴影 list；改为两份 Material。经验见 `docs/superpowers/lessons/2026-09-06-indirect-shared-material-flicker.md`
