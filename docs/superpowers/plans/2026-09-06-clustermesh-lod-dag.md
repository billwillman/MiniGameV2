# ClusterMesh Lock-Border DAG Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans or implement inline in the approving session. Steps use checkbox (`- [ ]`) syntax. Do not commit unless the user asks.

**Goal:** Offline Nanite-style cluster DAG (group 4, leftover 2–3, lock borders, simplify ~50%, split, repeat toward a root) with runtime cut selection via `ClusterGroup`.

**Architecture:** `hierarchyVersion == 2` makes `ClusterHeader.parentIndex` a parent **group** index. Baker replaces 4-to-1 collapse with group → lock → half tris → recluster. Cull binds a groups StructuredBuffer. Version 0/1 keep existing TestLod.

**Tech Stack:** ClusterMesh Runtime/Editor/Tests, `ClusterMeshCull.compute`, Tuanjie 2022.3.48t2 EditMode.

## Global Constraints

- Namespace `ClusterMesh`. Tests in `ClusterMesh.Tests`.
- Write `Assets/ClusterMesh/**` and these docs only. Do not edit FogOfWar, Packages, ProjectSettings, or the 2026-09-04 spec body.
- Header stride 96. `ClusterGroup` stride 48. Four vertex StructuredBuffers. `MaxBatchedObjects = 256`.
- New bake with hierarchy → `hierarchyVersion = 2`. Old v1 assets keep cluster-parent TestLod.
- Default `lodErrorThreshold = 0` → leaves only.
- No Hi-Z, METIS, meshoptimizer, URP Renderer Feature, streaming, software raster.
- Do not unlock borders to force a single root.
- Final verify: Tuanjie `2022.3.48t2` `-runTests` assembly `ClusterMesh.Editor.Tests`. Editor must be closed.
- Do not commit unless asked.

## File map

- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshTypes.cs` — `ClusterGroup`, `BakeResult.groups`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshLimits.cs` — `ClusterGroupStride = 48`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshAsset.cs` — `groups`, CopyFrom
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshLod.cs` — version 2, PackFlags, TryGetParent, IsVisible
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshBuffers.hlsl` — `ClusterGroup`
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshCull.compute` — `_Groups` TestLod
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshDrawContext.cs` — groups buffer
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshRenderer.cs` — gizmo parent via groups
- Modify: `Assets/ClusterMesh/Editor/ClusterMeshRendererLodGizmos.cs` — labels L0..Ln
- Modify: `Assets/ClusterMesh/Editor/ClusterMeshBaker.cs` — soup entry + version 2 + BuildHierarchy
- Modify: `Assets/ClusterMesh/Editor/ClusterMeshLodBaker.cs` — DAG loop
- Modify: `Assets/ClusterMesh/Tests/Editor/ClusterMeshLodTests.cs`
- Modify: `Assets/ClusterMesh/Tests/Editor/ClusterMeshBakerTests.cs`
- Modify: `Assets/ClusterMesh/Editor/ClusterMeshBakerWindow.cs` — `buildLodHierarchy` 开关（已做）
- Modify: `Assets/ClusterMesh/Tests/Editor/ClusterMeshBakerWindowTests.cs` — `WriteAsset_LodOff_WritesVersionZero`（已做）
- Spec: `docs/superpowers/specs/2026-09-06-clustermesh-lod-dag-design.md`

---

### Task 1: Types + LOD formula

**Files:** Types, Limits, Asset, Lod, LodTests

**Produces:** `ClusterGroup` 48 bytes. `HierarchyVersionDag = 2`. `PackFlags` / `Level`. `TryGetParent`. `IsVisible` with parent error float. v1 overload unchanged.

- [ ] Write tests for group stride, PackFlags/Level, v2 T=0 / large T / v0 / v1
- [ ] Implement types + Lod helpers
- [ ] EditMode: those tests pass

### Task 2: Baker DAG

**Files:** Baker (extract `ClusterTriangles`), LodBaker.BuildHierarchy, BakerTests

**Produces:** `BuildHierarchy` as specified. `hierarchyVersion = 2`. Leaf tris still equal source.

- [ ] Tests: LodOn Grid version 2 + groups; lock positions; 4 children share group; leftover 2 can merge
- [ ] Implement lock collapse + 50% + split + leftover 2–3
- [ ] EditMode: baker + lod tests pass

### Task 3: Cull + Draw + gizmos

**Files:** hlsl, compute, DrawContext, Renderer, LodGizmos

**Produces:** GPU TestLod v2 reads groups. Empty groups → 1-element placeholder buffer. Gizmos use TryGetParent; colors by level.

- [ ] Bind `_Groups` / `_GroupCount`
- [ ] Branch TestLod on `_HierarchyVersion`
- [ ] Gizmos resolve parent group AABB center
- [ ] EditMode full assembly

---

## Self-review

- Spec 分组/锁边/50%/再切/ClusterGroup/v2/v0v1/超预算 → Task 1–3.
- No TBD. `parentIndex` v2 = group index everywhere in new bake path.
- `IsVisible` v1 tests keep cluster-parent overload.
