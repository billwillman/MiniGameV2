# ClusterMesh Cull Lookup Implementation Plan

> **For agentic workers:** This session implements the plan inline and runs EditMode tests last (user: 先落盘再修改再测试). Do not commit unless the user asks.

**Goal:** Shipping cull looks up owning group in O(1) and builds Indirect world bounds from one cached asset AABB.

**Architecture:** Derive `int[clusterCount]` from group ranges when `ClusterMeshDrawContext` is created. Upload as `StructuredBuffer<int>`. Replace the compute linear scan. Cache `AssetLocalBounds` and transform 8 corners per object.

**Tech Stack:** Unity / Tuanjie 2022.3, existing `ClusterMeshCull.compute`, `ClusterMeshLod`, `ClusterMeshFrustum`.

## Global Constraints

- Namespace `ClusterMesh`. Tests in `ClusterMesh.Tests`.
- Write only `Assets/ClusterMesh/**` and this spec/plan. Do not edit FogOfWar, Packages, ProjectSettings, UserSettings.
- Header 96 and group 48 stay. No rebake. No `.asset` field for owning indices.
- Do not invent passing test output. Final verification is Tuanjie `2022.3.48t2` `-runTests` assembly `ClusterMesh.Editor.Tests`.
- Do not commit unless asked.

## File map

- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshLod.cs` — `BuildOwningGroupIndices`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshFrustum.cs` — `TransformLocalBounds`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshDrawContext.cs` — cache bounds + owning buffer
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshCull.compute` — O(1) lookup
- Modify: `Assets/ClusterMesh/Tests/Editor/ClusterMeshLodTests.cs`
- Modify: `Assets/ClusterMesh/Tests/Editor/ClusterMeshFrustumTests.cs`
- Modify: `docs/superpowers/specs/2026-09-06-clustermesh-lod-dag-design.md` — one-line cull pointer

---

### Task 1: Owning-group table

**Files:**
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshLod.cs`
- Modify: `Assets/ClusterMesh/Tests/Editor/ClusterMeshLodTests.cs`
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshCull.compute`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshDrawContext.cs`

**Interfaces:**
- Produces: `ClusterMeshLod.BuildOwningGroupIndices(int clusterCount, IList<ClusterGroup> groups)` → `int[]`

- [x] Add tests for leaf `-1`, parent group index, null groups, zero count, out-of-range start, overlap = first group, baked grid matches `TryGetOwningGroup`.
- [x] Implement `BuildOwningGroupIndices` (fill `-1`, write in-range intervals, first write wins).
- [x] Upload buffer in `ClusterMeshDrawContext` ctor; bind `_OwningGroups`; dispose it.
- [x] Replace `FindOwningGroup` loop with `_OwningGroups[clusterIndex]`; treat `own >= _GroupCount` as none.

### Task 2: Asset AABB

**Files:**
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshFrustum.cs`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshDrawContext.cs`
- Modify: `Assets/ClusterMesh/Tests/Editor/ClusterMeshFrustumTests.cs`

**Interfaces:**
- Produces: `ClusterMeshFrustum.TransformLocalBounds(Bounds local, Matrix4x4 localToWorld)` → `Bounds`

- [x] Add tests: identity, translate, rotated/scaled union contains every cluster world corner.
- [x] Implement 8-corner transform.
- [x] Cache `AssetLocalBounds` on context; `TransformBounds` calls `TransformLocalBounds`.

### Task 3: Verify

- [x] Run `ClusterMesh.Editor.Tests` with Tuanjie batchmode. Result: 92/92 passed, 0 failed (`clustermesh-cull-lookup-results.xml`).
