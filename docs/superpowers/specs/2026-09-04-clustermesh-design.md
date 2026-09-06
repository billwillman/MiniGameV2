# ClusterMesh 设计结论

Date: 2026-09-04  
Status: Approved for implementation planning  
Module: `Assets/ClusterMesh`  
Unity: 2022.3.62f3c1, URP 14.0.12

## 1. Purpose

Build a self-contained research prototype that:

1. Splits a static Unity `Mesh` into fixed-capacity meshlets (clusters).
2. Saves the result as a `ClusterMeshAsset`.
3. Lets an artist inspect that asset in Edit Mode without entering Play Mode.
4. Draws the asset at runtime (and in the viewer) with compute frustum + cone culling and `Graphics.DrawMeshInstancedIndirect`.

The on-screen result should look like the original static mesh. Clusters are the runtime draw unit. The module does not hook into game scenes or the existing URP Renderer assets.

## 2. Goals and non-goals

### Goals

- Offline Baker: `Mesh` or scene `MeshFilter` → `ClusterMeshAsset`.
- Edit Mode Viewer that uses the same InstancedIndirect path as runtime (no in-memory combined Mesh, no saved combined Mesh).
- Runtime `ClusterMeshRenderer` with compute AABB frustum cull + cluster cone cull.
- Multi-submesh / multi-material static meshes.
- Capability gate: if Compute or Indirect is missing (including GLES), log an error and do not draw.
- Keep source material albedo / color / normal / metallic / smoothness on a ClusterMesh Lit shader that still does URP PBR lighting.

### Non-goals (v1)

- Hi-Z / occlusion culling.
- LOD cluster hierarchy.
- Skinned meshes.
- URP Renderer Feature.
- GLES fallback draw (unsupported, not a soft degrade).
- Offline or runtime `CombineMeshes` / a baked display Mesh used for drawing.
- Hooking `Assets/Script` gameplay, FogOfWar, or shipping scenes.

## 3. Locked decisions

| Topic | Decision |
| --- | --- |
| Split | Greedy meshlet. Default 64 vertices and 124 triangles per cluster. |
| Padding | Asset stores the real triangle count. The vertex shader degenerates extra template vertices when `vertexId >= triangleCount * 3`. |
| Input | Static `MeshFilter` only. Any scene `MeshFilter` can be fed to the Baker. Multiple submeshes / materials allowed. |
| Combine | Do not bake a combined display Mesh. Do not `CombineMeshes` at runtime. |
| Draw | One shared template Mesh + `DrawMeshInstancedIndirect`. One visible cluster = one instance. |
| Shader | ClusterMesh Lit (URP PBR, metallic workflow). Copy properties from the source material. Enable GPU instancing. Stock URP/Lit cannot fetch cluster vertices. |
| Viewer | Same `ClusterMeshDrawContext` as runtime. Edit Mode. No Play Mode required. |
| Platform | Target D3D11/12, Vulkan, Metal. GLES / no-compute / no-indirect → error, no draw. |
| Shadows | Set `ShadowCastingMode.On` and `receiveShadows` on the draw. Full URP shadow-pass coverage is not guaranteed in v1. |
| Layout | `Assets/ClusterMesh/{Runtime,Editor,Shaders,Samples,Tests}`. |

## 4. Architecture

```text
Mesh / MeshFilter
        |
        v
 ClusterMeshBaker (Editor, greedy meshlet)
        |
        v
 ClusterMeshAsset (ScriptableObject, binary)
        |
        +-- ClusterMeshViewerWindow / Inspector  --+
        |                                          |
        +-- ClusterMeshRenderer (Play Mode) -------+
                                                   |
                                                   v
                                    ClusterMeshDrawContext
                                      cull compute
                                      DrawMeshInstancedIndirect
                                      ClusterMesh/Lit
```

`ClusterMeshDrawContext` owns GPU buffers, the template Mesh, cull dispatch, and the per-material indirect draws. The Viewer and the Renderer are thin hosts.

## 5. Data

Namespace: `ClusterMesh`.

Constants in `ClusterMeshLimits`:

- `MaxVerticesPerCluster = 64`
- `MaxTrianglesPerCluster = 124`
- `TemplateVertexCount = 372` (124 * 3)

GPU-facing structs are 16-byte aligned and stored on the asset so CPU and HLSL share one layout.

```csharp
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct ClusterHeader
{
    public uint vertexOffset;
    public uint vertexCount;
    public uint indexOffset;
    public uint triangleCount;
    public uint materialIndex;
    public uint pad0, pad1, pad2;
    public Vector4 aabbCenter;      // xyz = local AABB center
    public Vector4 aabbExtents;     // xyz = local AABB extents
    public Vector4 coneAxisCutoff;  // xyz = axis, w = cutoff. w < 0 disables cone cull
    public Vector4 coneApex;        // xyz = local cone apex
}

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct ClusterVertex
{
    public Vector4 position; // xyz
    public Vector4 normal;   // xyz
    public Vector4 tangent;  // xyzw Unity tangent
    public Vector4 uv;       // xy
}
```

`ClusterMeshAsset` (`ScriptableObject`, `[PreferBinarySerialization]`):

- `Mesh sourceMesh`
- `Material[] materials` — references to the source materials, one per submesh
- `int maxVerticesPerCluster`
- `int maxTrianglesPerCluster`
- `ClusterHeader[] clusters`
- `ClusterVertex[] vertices`
- `uint[] indices` — triangle list, three indices per triangle, local to the packed vertex range via `vertexOffset`

The asset does not contain a combined display Mesh.

HLSL mirrors these structs as `float4` fields in the same order.

## 6. Baker

Entry points:

- `ClusterMeshBaker.Bake(Mesh mesh, Material[] materials, ClusterMeshBakeSettings settings) -> ClusterMeshBakeResult`
- EditorWindow `ClusterMesh Baker` under `Tools/ClusterMesh/Baker`

`ClusterMeshBakeSettings`:

- `maxVerticesPerCluster` (default 64)
- `maxTrianglesPerCluster` (default 124)

Algorithm, per submesh, in order:

1. Read that submesh's indices as triangles. Skip degenerate triangles (duplicated vertex indices).
2. Build triangle adjacency: two triangles are adjacent when they share at least one vertex index in the source mesh.
3. While unused triangles remain:
   - Seed a cluster with the lowest unused triangle index.
   - Grow by repeatedly adding an unused adjacent triangle if the unique vertex count would stay `<= maxVertices` and triangle count `<= maxTriangles`.
   - If no adjacent triangle fits, add the lowest unused triangle that still fits the budgets.
   - If nothing fits, close the cluster.
4. Remap used source vertices into a packed `ClusterVertex` run. Preserve position, normal, tangent, uv0. Missing normals become `(0,1,0)`. Missing tangents are rebuilt from uv0 + position using a standard length-weighted average. Missing uv0 becomes `0`.
5. Write three `uint` indices per triangle, local to the cluster packed run (`0 .. vertexCount-1`). The vertex shader fetches `_Vertices[header.vertexOffset + local]`. Do not pre-add `vertexOffset` into the stored indices.
6. AABB: min/max of packed positions. `center = (min+max)/2`, `extents = (max-min)/2`. Empty cluster is invalid and must not be emitted.
7. Cone: area-weighted average of triangle normals → `axis`. `cutoff = min(dot(axis, triangleNormal))`. If `cutoff < 0` (cluster spans more than a hemisphere), store `cutoff = -1` and skip cone cull. Apex is the AABB center.

Validation before bake:

- `mesh` is not null and has at least one triangle.
- Skinned bake is rejected with a clear error (v1 is `MeshFilter` only).
- Extra channels (uv1+, colors) are dropped; the Baker window warns once.

Output path: user-chosen `.asset` under `Assets/`.

## 7. Viewer

- Menu: `Tools/ClusterMesh/Viewer`
- Edit Mode only is enough; Play Mode is not required.
- Object field for `ClusterMeshAsset`.
- Preview camera via `PreviewRenderUtility`.
- Draw through `ClusterMeshDrawContext` (InstancedIndirect + cull). Orbit / zoom in the preview.
- Sidebar: cluster count, vertex count, index count, material count, selected cluster index.
- Toggles: show AABB gizmos overlay in the preview, isolate one cluster (pass an isolate index into the cull constants so only that cluster is drawn).
- Inspector: custom editor on `ClusterMeshAsset` with a smaller `PreviewRenderUtility` that uses the same draw context.

If the editor device fails the capability gate, the window shows the unsupported reason and does not draw.

## 8. Runtime draw

`ClusterMeshRenderer` is a `MonoBehaviour`:

- `ClusterMeshAsset asset`
- `Camera targetCamera` (optional; default `Camera.main`)

`OnEnable` builds a `ClusterMeshDrawContext`. `OnDisable` disposes it. `LateUpdate` calls `Draw`.

`ClusterMeshDrawContext`:

1. `ClusterMeshCapability.IsSupported()`. If false, set `Error` and skip all GPU work.
2. Upload `clusters`, `vertices`, `indices` as `GraphicsBuffer` (structured / raw as required).
3. Create per-material visible-id buffers and indirect-args buffers (`5` uints: `indexCountPerInstance`, `instanceCount`, `startIndex`, `baseVertex`, `startInstance`).
4. Build runtime materials: `new Material(ClusterMeshLit)` + copy `_BaseMap`/`_MainTex`, `_BaseColor`/`_Color`, `_BumpMap`, `_BumpScale`, `_Metallic`, `_Smoothness`, `_Cutoff` from the source slot. `enableInstancing = true`.
5. Each draw:
   - Compute six frustum planes in object-local space via `ClusterMeshFrustum.CameraToLocalPlanes`: `GeometryUtility.CalculateFrustumPlanes(camera)`, then for each world plane pick `point = normal * -distance`, transform the point with `worldToLocal.MultiplyPoint3x4`, transform the normal with `localToWorld.transpose.MultiplyVector(normal)` and normalize, then `new Plane(localNormal, localPoint)`.
   - Local camera position = `worldToLocal.MultiplyPoint3x4(camera.transform.position)`.
   - Dispatch cull: one thread per cluster. A cluster is visible if its `materialIndex` matches the current material pass, AABB vs 6 planes succeeds, cone test succeeds (or cutoff `< 0`), and isolate index is `< 0` or equals the cluster index.
   - Visible cluster ids are appended; `instanceCount` is the append count. `indexCountPerInstance` is always `372`.
   - `Graphics.DrawMeshInstancedIndirect(template, 0, material, worldBounds, argsBuffer)` once per material.

Template Mesh (`ClusterMeshTemplate.Create()`):

- 372 vertices at the origin, dummy normals `(0,1,0)`, dummy uv `0`.
- Indices `0,1,2,...,371` (triangle list).
- `Mesh.MarkDynamic` is not required. HideFlags `HideAndDontSave` for the runtime instance.

Vertex shader (procedural instancing):

```text
clusterId = _VisibleClusterIds[unity_InstanceID]
header    = _Clusters[clusterId]
if (vertexId >= header.triangleCount * 3) output degenerate position
localIndex = _Indices[header.indexOffset + vertexId]
vertex     = _Vertices[header.vertexOffset + localIndex]
use vertex.position/normal/tangent/uv as object-space attributes
unity_ObjectToWorld comes from ClusterMeshSetup() as the renderer matrix
```

`ClusterMeshSetup` writes the same `localToWorld` / `worldToLocal` for every instance of that draw.

World bounds passed to `DrawMeshInstancedIndirect` are the transformed asset AABB (8 corners of the union of cluster AABBs, or a conservative bounds on the renderer transform).

## 9. Capability

`ClusterMeshCapability.IsSupported()` is false when any of these hold:

- `!SystemInfo.supportsComputeShaders`
- `!SystemInfo.supportsIndirectArgumentsBuffer`
- `SystemInfo.graphicsDeviceType` is `OpenGLES2` or `OpenGLES3`
- `SystemInfo.maxComputeBufferInputsVertex < 4`

`GetUnsupportedReason()` returns a single English sentence for logs and the Viewer.

On failure the Renderer logs `ClusterMesh: <reason>` once and never calls draw APIs.

## 10. Directory

```text
Assets/ClusterMesh/
  Runtime/
    ClusterMeshLimits.cs
    ClusterMeshTypes.cs
    ClusterMeshCapability.cs
    ClusterMeshFrustum.cs
    ClusterMeshTemplate.cs
    ClusterMeshMaterialUtil.cs
    ClusterMeshAsset.cs
    ClusterMeshDrawContext.cs
    ClusterMeshRenderer.cs
    ClusterMesh.Runtime.asmdef
  Editor/
    ClusterMeshBaker.cs
    ClusterMeshBakerWindow.cs
    ClusterMeshViewerWindow.cs
    ClusterMeshAssetEditor.cs
    ClusterMesh.Editor.asmdef
  Shaders/
    ClusterMeshCull.compute
    ClusterMeshLit.shader
    ClusterMeshLit.hlsl
    ClusterMeshBuffers.hlsl
  Samples/
    ClusterMeshDemo.unity
  Tests/
    Editor/
      ClusterMeshBakerTests.cs
      ClusterMeshFrustumTests.cs
      ClusterMeshCapabilityTests.cs
      ClusterMesh.Editor.Tests.asmdef
```

No changes to `Assets/Settings` URP renderer assets.

## 11. Testing

Edit Mode tests via Unity Test Framework (`com.unity.test-framework` 1.1.33), assembly `ClusterMesh.Editor.Tests`.

Required cases:

- Baker: one triangle → one cluster, counts 3/1, AABB contains the triangle, cone cutoff `>= 0` or disabled only when normals oppose.
- Baker: a mesh larger than 64 unique vertices in one connected region → more than one cluster, every cluster `vertexCount <= 64` and `triangleCount <= 124`, every source triangle appears in exactly one cluster.
- Baker: two submeshes → `materialIndex` 0 then 1, no mixing.
- Baker: degenerate triangles are skipped.
- Frustum: AABB at origin is inside an identity-sized view, outside when translated beyond the far plane.
- Cone: viewing from behind a forward-facing cone culls; viewing from the front does not; `cutoff < 0` never culls.
- Capability: method is deterministic for the current `SystemInfo` (assert `IsSupported()` equals the inverse of a non-empty reason when unsupported).

GPU draw, Baker window, and Viewer are verified by the manual steps in the implementation plan (bake a cube / a two-material mesh, open Viewer, enter Play Mode).

## 12. Error handling

- Null mesh / empty mesh / skinned-only input: Baker throws `InvalidOperationException` with a specific message; the window shows it in a help box and does not write an asset.
- Null asset on the Renderer: skip draw, no exception every frame.
- Context dispose is safe to call twice.
- Buffer sizes match array lengths; a zero-cluster asset draws nothing without dispatching a 0-thread compute.

## 13. Open items that are closed

- Draw API: `DrawMeshInstancedIndirect`, not `RenderMeshIndirect`, not index-compact DrawMesh, not runtime CombineMeshes.
- Viewer preview: InstancedIndirect, not a temporary combined Mesh.
- Spec location: this file. Implementation plan: `docs/superpowers/plans/2026-09-04-clustermesh.md`.
