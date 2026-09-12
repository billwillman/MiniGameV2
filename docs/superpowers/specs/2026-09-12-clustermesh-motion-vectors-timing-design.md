# ClusterMesh Motion Vector 时机（TAA / Radiant 之前）

Date: 2026-09-12  
Status: Implemented（EditMode 被编辑器占用，batchmode 未跑）  
Module: `Assets/ClusterMesh`  
Parent: `docs/superpowers/specs/2026-09-07-clustermesh-urp-feature-design.md`  
Related: `docs/superpowers/todos/2026-09-06-clustermesh-followups.md` D. MotionVectors

R1 之后已经有物体 Motion Vector Pass（shader pass 4、默认关）。挂点错了：写在 `BeforeRenderingPostProcessing - 1`，Radiant 在 `AfterRenderingSkybox + 2` 已经采过 `_MotionVectorTexture`。TAA / Radiant Temporal 把 ClusterMesh 当静态世界。本期只改 **谁先写进 MV 图**，不重写历史矩阵、不改默认开关、不做 LOD 滞回。

## 1. Purpose

Game 相机、Feature 激活、且该相机合批里有 `enableMotionVectors` 时，ClusterMesh 的物体速度必须写在 Radiant / URP TAA 读 MV 之前。官方 URP 14 物体 MV 在天空盒之后；Radiant 注释写明 2022.3 跟深度拷贝，自己挂 `AfterRenderingSkybox + 2`。我们对齐官方槽位。

## 2. Locked

| 项 | 决定 |
| --- | --- |
| 注入点 | 项目设置 `ClusterMeshSettings.motionVectorSlot`，窗口 `Tools/ClusterMesh/通用设置`。默认 `AfterSkyboxPlus1` = `AfterRenderingSkybox + 1` |
| 其它槽 | `AfterOpaques`、`AfterSkybox`、`BeforePostProcessingMinus1`（旧挂点，2022.3 上晚于 Radiant） |
| 解析 | `ClusterMeshUrpBridge.ResolveMotionVectorPassEvent`；`MotionVectorPassEvent` 仍表示默认槽 |
| 默认 | `enableMotionVectors` 仍默认 **关**。不改现有场景资产勾选 |
| 入队 | 仍仅当 `HasMotionVectors(camera)`（静态或蒙皮任一为真） |
| 目标 | 仍绑 URP `m_MotionVectorColor` / `m_MotionVectorDepth`；对不上则本帧不画 MV |
| 历史 | `CapturePreviousMotionMatrix` / 蒙皮 pose 时间 不改 |
| Shader | pass 4、`ClusterMeshMotionVectors.hlsl`、NDC 速度 不改 |
| 路径 | 只动 Feature 的 Game 相机 Pass。Scene / Viewer / Preview / 反射仍不走 Feature |
| 引擎 | 锁定 Tuanjie 2022.3 / URP 14。不预适配 Unity 6 Render Graph（那时官方 MV 可能挪到 Post 前，Radiant 会自己后移） |

## 3. Non-goals

| 不做 | 原因 |
| --- | --- |
| 默认打开所有 Renderer 的 MV | 历史和 Pass 有成本；角色验收时手工勾 |
| LOD 选层滞回 | followups D 另一条；硬切闪不是时机问题 |
| 改 Radiant Feature 事件 | 邻居资产；我们靠到它前面 |
| Depth Prepass 加回来 | z-fight 已撤；MV 不依赖它 |
| Hi-Z / 探针 / 雾 | P3 和下一项，不混 |

## 4. 一帧（URP Game + 有物体 MV）

1. GBuffer 或 Forward 照旧（`BeforeRenderingGbuffer + 1` / `BeforeRenderingOpaques`）
2. URP 写相机深度拷贝、官方物体 MV（若有 MeshRenderer）
3. **本 Pass** `AfterRenderingSkybox + 1`：`unity_MotionVectorsParams = (0,1,0,0)`，静态 + 蒙皮 `SubmitUrpMotionVectors`
4. Radiant `AfterRenderingSkybox + 2` 读到已含 ClusterMesh 的 MV 图
5. TAA / 后处理同图

没有物体勾 MV：不入队，行为与现在关开关相同。

## 5. 怎样算做对了

1. `ClusterMeshUrpBridge.MotionVectorPassEvent` 等于 `(RenderPassEvent)((int)AfterRenderingSkybox + 1)`
2. 新 Feature / 未写过该字段的 Renderer：`MotionVectorSlot == AfterSkyboxPlus1`，解析结果等于 `MotionVectorPassEvent`
3. 仍只在 `HasMotionVectors` 时 `EnqueuePass(_motionPass)`
4. 默认 `enableMotionVectors == false` 的现有单测仍绿
5. 现有 pass 索引（Forward 0 … MotionVectors 4）不变
6. **手工（Game View）**：Demo 角色勾上 MV，转相机 / 走角色，Radiant Temporal 与 TAA 不再把轮廓当静态世界拖边。Scene View 不验收

做错：把 Radiant 往后挪；默认全开；改历史公式；把滞回塞进来。

## 6. 文件

| 文件 | 变化 |
| --- | --- |
| `Runtime/ClusterMeshUrpBridge.cs` | 增加 `MotionVectorPassEvent` |
| `Runtime/ClusterMeshUrpFeature.cs` | `motionVectorSlot` + 入队前 `Resolve` |
| `Editor/ClusterMeshUrpFeatureMenu.cs` | Feature Inspector 下拉 |
| `Tests/Editor/ClusterMeshUrpTests.cs` | 断言常量 + Feature 源文件挂点 |
| Baker / LOD / Lit / 合批键 / 64/124 | **不改** |
| Radiant、URP Renderer 资产 | **不改** |
