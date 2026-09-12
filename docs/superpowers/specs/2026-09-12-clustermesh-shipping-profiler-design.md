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

机器 / 日期：未量。2026-09-12 本机会话：Tuanjie 已打开 `D:/MiniGameV2`，batchmode 被拒；未建 200 实例临时场景（规格禁止改玩法场景）。关编辑器后按第 2 节再填。

| 项 | 画像 A（200 / 剔开） | 画像 A（200 / 剔关） | 画像 B（Demo≈10） |
| --- | --- | --- | --- |
| 进槽物体 | 未量 | 未量 | 未量 |
| Dispatch 次数 | 未量 | 未量 | 未量 |
| 主画实例（估） | 未量 | 未量 | 未量 |
| Cull ms | 未量 | 未量 | 未量 |
| GC Alloc B/帧 | 未量 | 未量 | 未量 |
| 分配栈顶 | 未量 | 未量 | 未量 |

过关签名：进槽与 Dispatch 在「剔开」下列显低于「剔关」；画像 B 不恶化；GC 结论写清是否只剩 Set*Array。

## 6. 文件

只改本文件第 5 节 + followups A 勾选。不改 Runtime / Shader / URP 资产。
