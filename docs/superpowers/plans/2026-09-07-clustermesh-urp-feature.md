# ClusterMesh URP Feature R1 Implementation Plan

> **For agentic workers:** Implement inline. TDD. Do not commit unless asked. R2 is out of scope.

**Goal:** Keep LateUpdate Flush; when the camera’s URP Renderer Data has an active `ClusterMeshUrpFeature`, skip Flush and submit Depth/Forward at URP pass events; shadows stay `Graphics.ShadowsOnly`.

**Architecture:** `ClusterMeshUrpBridge.HasActiveFeature` / `ShouldSkipLegacyFlush` (renderer-data list, not AddRenderPasses). Batcher `ForEachBatch`. DrawContext `PrepareUrp` once then cmd depth/color. Menu Enable/Disable only the current URP Asset default renderer.

**Tech Stack:** Tuanjie 2022.3 / URP 14, `ScriptableRendererFeature`, EditMode tests.

## Global Constraints

- Do not change 64/124, header 96, group 48, batch key, FogOfWar, Packages, game URP assets (unless user runs the menu).
- Do not implement R2 / P3 / LOD hysteresis.
- `T<=0` and cull/LOD semantics unchanged.
- Do not commit unless asked.

## File map

- `Runtime/ClusterMeshUrpBridge.cs` — detect + skip
- `Runtime/ClusterMeshUrpFeature.cs` — Feature + two passes
- `Runtime/ClusterMeshDrawContext.cs` — PrepareUrp / SubmitUrp*
- `Runtime/ClusterMeshSceneBatcher.cs` — ForEachBatch, Flush skip, URP entry
- `Editor/ClusterMeshUrpFeatureMenu.cs` — T1 menu
- `Editor/ClusterMesh.Editor.asmdef` — URP reference
- `Tests/Editor/ClusterMeshUrpTests.cs`

---

### Task 1: Bridge + menu + Flush skip + URP submit

- [x] Tests first (`ClusterMeshUrpTests`)
- [x] Bridge / Feature / menu / DrawContext / Batcher
- [x] Run `ClusterMesh.Editor.Tests` if editor unlocked
