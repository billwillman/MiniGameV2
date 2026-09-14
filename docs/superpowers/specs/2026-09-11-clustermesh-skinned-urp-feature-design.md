# ClusterSkinnedMesh 接 URP Feature（与静态同一开关）

Date: 2026-09-11  
Status: Implemented  
Module: `Assets/ClusterMesh`  
Parent: `docs/superpowers/specs/2026-09-07-clustermesh-urp-feature-design.md`

静态 R1 已有：`Tools/ClusterMesh/Enable URP Feature` 后，Game 相机若挂了激活的 `ClusterMeshUrpFeature`，`ShouldSubmitUrp` 为真 → LateUpdate **跳过该相机**，Depth/Color 由 Feature 用 CommandBuffer 交；否则 `Flush` → `Graphics.DrawMeshInstancedIndirect`。编辑器 Play / 非 Play 的 Game View 与 Player 相同；Scene View 仍走 Lifetime Update + `SceneViewRenderer.Draw`。

本期只把 **蒙皮** 接到同一套开关。不新开 Feature，不改菜单，不改 Baker / 64/124 / P3。

## 1. Purpose

Enable Feature 之后，静态和蒙皮必须走同一条提交时间线。现在蒙皮 `ClusterSkinnedMeshSceneBatcher.Flush()` 不看 `ShouldSkipLegacyFlush`，Feature 也不调蒙皮，所以开关对角色无效。

## 2. Locked

| 项 | 决定 |
| --- | --- |
| 开关 | 复用 `ClusterMeshUrpBridge.ShouldSubmitUrp` / `ShouldSkipLegacyFlush`。不另做 SetupURP 字段 |
| Feature | 仍是 `ClusterMeshUrpFeature`。`AddRenderPasses` 先静态后蒙皮 |
| 编辑器 Game View | Play 与非 Play 都走 Feature（Setup URP 且 Feature 激活）。非 Play 仍靠 `OnEditModeUpdate` Flush（跳过 Draw）+ `QueuePlayerLoopUpdate` 把 URP 帧拉起来 |
| Scene / Viewer / Preview / 反射 | 仍直接 `Draw(...)`，不进 Feature |
| R1 阴影 | 仍 `Graphics.ShadowsOnly`。蒙皮保持现有二次 Dispatch（关 cone、关相机 cull），**不**搬静态的 Sun 棱柱拆 list |
| 动画 | 求值、上传 palette / VTF、cull 都在 `PrepareUrp`。Depth/Color 只绑已有缓冲 |
| 合批键 | 不变。context 仍按 `(asset, camera, clip, eval, …)` |
| 双画 | 同一 Game 相机只许 Feature 或 Flush 一条路径出主画 |
| 跳过 Flush 时的 cache | 仍 `GetOrCreate` 并记 `UsedContexts`，禁止因没 Draw 把 context Dispose 掉再被 Feature 重建 |

## 3. 一帧（URP Game 相机）

1. LateUpdate `Flush`：该相机 `ShouldSkipLegacyFlush` → 收集 batch、保住 context、**不** `Draw`
2. `AddRenderPasses`：静态 + 蒙皮各 `PrepareAndSubmitUrpShadows(camera)`（cull 一次 + ShadowsOnly）
3. `BeforeRenderingPrePasses`：两边 `SubmitUrpDepth`（shader pass 2）
4. `BeforeRenderingOpaques`：两边 `SubmitUrpColor`（shader pass 0）

没 Feature / 非 Game：Flush 仍 `Draw`，Feature `AddRenderPasses` 直接 return。

## 4. API

`ClusterSkinnedMeshDrawContext`：

- `Draw(...)` = `TryPrepare` + 现有 Graphics 主画/阴影（Viewer、Scene、老路径）
- `PrepareUrp(...)` = `TryPrepare` + `Graphics.ShadowsOnly`
- `SubmitUrpDepth(cmd)` / `SubmitUrpColor(cmd)` = `cmd.DrawMeshInstancedIndirect`

`ClusterSkinnedMeshSceneBatcher`：

- `Flush` 对 `ShouldSkipLegacyFlush(camera)` 的 batch 跳过 `Draw`
- `PrepareAndSubmitUrpShadows` / `SubmitUrpDepth` / `SubmitUrpColor` 与静态同名、只处理蒙皮注册表

## 5. Non-goals

- R2 自绑阴影 atlas、P3 探针/雾/lightmap、MotionVectors
- 蒙皮改用静态 Sun 棱柱 / 分 list
- 改 Default Renderer（除非用户自己跑菜单）
- 改 64/124、header 96、合批键、GpuOnly 默认、压缩开关

## 6. 怎样算做对

1. 未 Enable：现有蒙皮 Flush、SceneView `Draw` 行为不变；全装配体绿
2. Enable + Game 相机（含编辑器 Play / 非 Play Game View）：`ShouldSubmitUrp` 真，蒙皮 Flush **不计** legacy submit
3. SceneView / Preview / 反射仍 `ShouldSubmitUrp` 假
4. Feature 对同一相机既准备静态也准备蒙皮；`cmd == null` 不抛
5. Disable / 无 Feature：回到 1
