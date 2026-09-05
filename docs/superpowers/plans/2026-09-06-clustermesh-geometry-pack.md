# ClusterMesh Geometry Pack Implementation Plan

> **For agentic workers:** Implement inline in the approving session. Steps use checkbox (`- [ ]`) syntax. Do not commit unless the user asks.

**Goal:** Pack GPU vertices to 32 bytes (half nrm/tan/uv, reconstruct tan.z), pack indices as 2×ushort per uint, Deflate those blobs on the asset. BakeResult stays unpacked.

**Architecture:** `ClusterMeshGeometry` packs/unpacks and Deflates. `CopyFrom` writes `geometryVersion = 1` + `byte[]`. DrawContext inflates once onto stride-32 / packed-index buffers. HLSL `ClusterVertex` matches `ClusterPackedVertex`.

**Tech Stack:** ClusterMesh Runtime/Editor/Tests, `ClusterMeshLit.hlsl`, Tuanjie 2022.3.48t2 EditMode. `System.IO.Compression.DeflateStream`. No new packages.

## Global Constraints

- Namespace `ClusterMesh`. Tests in `ClusterMesh.Tests`.
- Write `Assets/ClusterMesh/**` and these docs only. Do not edit FogOfWar, Packages, ProjectSettings, or the 2026-09-04 spec body.
- Header 96. Group 48. GPU vertex stride 32. Four draw buffers unchanged.
- `BakeResult.vertices` / `indices` stay fat working arrays.
- `geometryVersion = 1`. Old assets fail load. No dual path.
- Reconstruct `tan.z` from T⊥N; `position.w = sign(tangent.z)` when `|n.z| < 1e-4`.
- Do not commit unless asked.
- Final verify: Tuanjie `2022.3.48t2` `-runTests` assembly `ClusterMesh.Editor.Tests`. Editor must be closed.

## File map

- Create: `Assets/ClusterMesh/Runtime/ClusterMeshGeometry.cs` — pack, reconstruct, Deflate
- Create: `Assets/ClusterMesh/Tests/Editor/ClusterMeshGeometryTests.cs`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshTypes.cs` — `ClusterPackedVertex`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshLimits.cs` — stride 32, `GeometryVersion = 1`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshAsset.cs` — packed fields, CopyFrom
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshDrawContext.cs` — inflate + stride 32
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshBuffers.hlsl` — packed `ClusterVertex`
- Modify: `Assets/ClusterMesh/Shaders/ClusterMeshLit.hlsl` — unpack + tan.z
- Modify: Viewer / AssetEditor counts; Demo persist via inflate
- Modify: DrawContext / BakerWindow / Demo tests for version and old-asset fail

---

### Task 1: Codec + types

**Produces:** `ClusterPackedVertex` 32 bytes. `ClusterMeshGeometry` pack/unpack/Deflate. `ClusterMeshLimits.ClusterVertexStride = 32`, `GeometryVersion = 1`.

- [x] Tests: stride, position/normal/tan.z (including n.z≈0), odd indices, Deflate
- [x] Implement types + `ClusterMeshGeometry`
- [x] EditMode: those tests pass

### Task 2: Asset + Draw + shader

**Produces:** Asset stores packed+Deflate. DrawContext rejects version≠1. Shader fetches packed vertex.

- [x] `CopyFrom` packs from BakeResult
- [x] DrawContext inflate / error string contains rebake
- [x] HLSL unpack matches C#
- [x] Inspector / Viewer / Demo persist

### Task 3: Assembly tests

- [x] WriteAsset lod-off: hierarchy 0, geometry 1
- [x] Old asset (version 0) DrawContext not ready
- [x] Full `ClusterMesh.Editor.Tests`

---

## Self-review

- Spec 32-byte layout / tan.z / BakeResult / geometryVersion / Deflate / no old path → Task 1–2.
- Existing baker assertions stay on `BakeResult`.
- No commit unless asked.
