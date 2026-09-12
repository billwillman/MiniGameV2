# ClusterMesh 出包 Profiler / GC 验收

Date: 2026-09-12  
Status: Approved for measurement（无功能代码）  
Module: `Assets/ClusterMesh`  
Parent: `docs/superpowers/specs/2026-09-06-clustermesh-cpu-object-cull-design.md`  
Related: `docs/superpowers/todos/2026-09-06-clustermesh-followups.md` A

第 3 项（CPU 物体剔 + 阴影分 list）的**画像**，不是新功能。有数再开 followups B（减 Dispatch、矩阵 GraphicsBuffer）。本期禁止为了「看起来该上」改 runtime。

## 1. Purpose

证明：同资产很多时，Dispatch 和主画实例跟 **CPU 留下的物体** 走，不跟注册总数走。并标出 `Draw` 热路径的托管分配是不是只剩 `SetMatrixArray` / `SetVectorArray`。

## 2. Locked

| 项 | 决定 |
| --- | --- |
| 引擎 | 关着项目占用的 Tuanjie 再跑 EditMode 测；Profiler 用 Play Mode Game 相机 + 已 Enable 的 URP Feature |
| 场景 | 新建或临时复制，**不要**改玩法场景 / FogOfWar。用同一 `ClusterMeshAsset` |
| 画像 A | 同资产 **200** 个 Renderer；镜头里约 **20**；其余在视锥外且方向光棱柱外（放到相机背后、太阳向前） |
| 画像 B | Demo 现有约 **10** 个且全在画面里：Dispatch / 主画与现在几乎持平（允许噪声，禁止数量级变差） |
| 开关 | `enableCpuObjectCull` 开；有 `RenderSettings.sun` 且方向光阴影开。对比组：同一 200 把 `enableCpuObjectCull` 全关 |
| GC | Profiler 勾 **GC Alloc**，对着 `ClusterMeshDrawContext` 的 Prepare/Bind（或 `ClusterMeshSceneBatcher.Flush` / Feature Execute）。稳态转相机 3 秒 |
| 记录 | 写进本文件第 5 节表格，日期 + 机器。不另开功能 PR |

## 3. 看哪些数

| 标记 / 计数 | 过关 |
| --- | --- |
| CPU 进槽物体数 | 画像 A ≪ 200，量级约「20 + 棱柱内」；对比组 ≈ 200 |
| Compute Dispatch（cull kernel） | 跟进槽物体所在 chunk 数走（每 chunk 一次），不是 200 次 |
| 主画 Indirect 实例（CopyCount / Frame Debugger） | 跟视锥内可见 cluster，身后 caster 不进主画 |
| 阴影 ShadowsOnly | 身后能投影的人在阴影 list，不要求进主画 |
| 画像 B | 与当前 Demo 同量级 |
| GC Alloc / 帧（热路径） | 接近 0；若有，只允许 `SetMatrixArray` / `SetVectorArray`。出现 `new` / `GetInterpolatedProbe` 每物体装箱要记下来，**本项不改代码**，交给 B 或 P3 复盘 |

P3 的 `GetInterpolatedProbe` 每槽一次是预期 CPU 成本，不算本项失败；记毫秒即可。

## 4. Non-goals

| 不做 | 原因 |
| --- | --- |
| 改 cull / 合批 / 矩阵 Buffer | 没有画像就改是 followups 明确禁止的 |
| Job/Burst `KeepObject` | 要几千实例且 Alloc 打在这段 |
| 按 cascade 精剔 | 另一条 |
| 把结果当「已经优化」关 followups A | 只有第 5 节填了数才能勾 A |

## 5. 记录（量完再填）

机器 / 日期：BESTZENG-PC1，AMD Ryzen 7 5800X，2026-09-12。

方法：未改玩法场景。`ClusterMeshShippingProfilerTests` 自动建临时 Camera + Sun（光朝 −Z）+ 朝向镜头的同资产三角。batchmode **不要** `-nographics`。进槽读 `KeepObject` / `PrepareUrp` chunk.n；Dispatch 读非空 chunk 数；主画 / 阴影读 Indirect args `CopyCount`（`GetData` args[1]）；GC 用第二次 Prepare 后 `Profiler.GetMonoUsedSizeLong`（`ProfilerRecorder("GC.Alloc")` 在 batchmode EditMode 恒为 0，作废）。不是 Play Mode 转相机 3 秒，followups A **不勾**。

| 项 | 画像 A（200 / 剔开） | 画像 A（200 / 剔关） | 画像 B（合成 10，全在锥内） |
| --- | --- | --- | --- |
| 进槽物体 | **20** | **200** | **10** |
| Dispatch 次数 | **1** | **1**（200&lt;256） | **1** |
| kernel groups（clusters=1） | 1 | 4 | 1 |
| 主画实例（CopyCount） | **20** | **20** | **10** |
| 阴影实例（CopyCount） | **20** | **20** | **10** |
| PrepareUrp ms | 0.578 | 1.255 | 0.267 |
| GC Alloc（Recorder） | 0（采样无效） | 0（采样无效） | 0（采样无效） |
| GC（Mono 增量） | +4096 B | +73728 B | +4096 B |
| 分配栈顶 | `PrepareChunks` 每帧 `new` 矩阵 / SH；`SetMatrixArray`；P3 `GetInterpolatedProbe` | 同左，n=200 | 同左，n=10 |

过关签名：进槽与 Dispatch 在「剔开」下列显低于「剔关」；画像 B 不恶化；GC 结论写清是否只剩 Set*Array。

本次结论：

- **进槽过关**：20 ≪ 200；画像 B 10 未恶化。
- **Dispatch 次数不过关**：都是 1。差在 kernel groups（1 vs 4）和 Prepare 时间（0.58 vs 1.26 ms）。
- **主画 / 阴影次数不降**：GPU 视锥 + 棱柱已经把身后 180 去掉，CopyCount 剔开关都是 20 / 20。CPU 剔省的是进核物体数，不是主画实例。
- **GC 不是「只剩 Set*Array」**：Mono 增量随 n 变（4 KB / 72 KB / 4 KB）。Recorder 在 batchmode 读不到，不能当 Play Mode 每帧 Alloc。

## 6. 文件

只改本文件第 5 节 + followups A 勾选。不改 Runtime / Shader / URP 资产。
