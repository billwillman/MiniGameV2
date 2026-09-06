# ClusterMesh QEM Simplify Implementation Plan

> **For agentic workers:** Implement inline. TDD. Do not commit unless asked. Do not change `TryCollapseShortest` / `Consider` semantics.

**Goal:** Optional offline QEM (position + normal + UV) for DAG group simplify; default on; off path identical to current shortest-edge.

**Architecture:** `useQemSimplify` on bake settings. `CollapseHalf` branches to `ClusterMeshQem.TryCollapseQem` or existing shortest-edge. Lock / flip / `lodError` stay shared.

**Tech Stack:** ClusterMesh Editor + EditMode tests. No new packages.

## Global Constraints

- Default `useQemSimplify = true`. Existing lock/topology tests must set `false`.
- Do not edit `Consider` / `TryCollapseShortest` bodies.
- `lodError` remains bidirectional surface deviation.
- Do not hide Renderer LOD threshold.
- Do not commit unless asked.

## File map

- `Runtime/ClusterMeshTypes.cs` — field
- `Editor/ClusterMeshQem.cs` — new
- `Editor/ClusterMeshLodBaker.cs` — CollapseHalf branch only
- `Editor/ClusterMeshBakerWindow.cs` — toggle
- `Tests/Editor/ClusterMeshQemTests.cs` — new
- Pin `useQemSimplify = false` on lock/topology Bake tests

---

### Task 1: Settings + QEM + wire-up + tests

See spec `2026-09-06-clustermesh-qem-simplify-design.md`.

- [x] Tests first (`ClusterMeshQemTests`)
- [x] `ClusterMeshQem.TryCollapseQem`
- [x] `CollapseHalf(..., useQem)`
- [x] Window toggle when hierarchy on
- [x] Pin existing LodOn lock tests
- [x] Run `ClusterMesh.Editor.Tests` if editor unlocked (`119/119` Passed)
