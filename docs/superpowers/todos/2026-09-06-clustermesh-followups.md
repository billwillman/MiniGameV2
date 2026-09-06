# ClusterMesh 后续 TODO

Date: 2026-09-06  
Status: Living backlog  
Module: `Assets/ClusterMesh`  
Branch: `feat/clustermesh-lod`

讨论过、还没做或要等条件再做的事项。**不是实现计划。** 开一项仍走：讨论 → spec 落盘 → 实现 → 测试。未写 spec 不动算法。提交只在你点头后做，不推远程。

沿用锁定：不改 64/124、header 96、组 48、四条 vertex stream、合批键 `(asset, Camera)`。不改 FogOfWar / Packages / 玩法场景 / URP Renderer。第 1～3 项运行时不用重 Bake；Baker 变了才重烤 Mia。

## 已落地（对照，不要回头重做）

- [x] 第 1 项：加载推导 `cluster → owningGroup`，GPU O(1)（`6ceeefd`）
- [x] 第 2 项：Indirect 用缓存资产 AABB，不再每帧扫全部 cluster（`6ceeefd`）
- [x] 第 3 项：CPU 物体剔 + 方向光挤出棱柱 + 主画/阴影分 list（`93391f8`）
- [x] 拆 list 后主画 / 阴影各一份 Material，避免 Indirect 延迟绑 buffer 闪屏（lesson：`docs/superpowers/lessons/2026-09-06-indirect-shared-material-flicker.md`）
- [x] 跨物体合批、几何打包、两层 LOD、锁边多层 DAG

规格：`2026-09-06-clustermesh-cull-lookup-design.md`、`2026-09-06-clustermesh-cpu-object-cull-design.md`。

---

## A. 卫生 / 验证

多数不用新 spec。有数再开 B。

- [ ] **出包 Profiler**：第 3 项画像（同资产 ~200、镜内 ~20）Dispatch / 主画实例是否按留下的降，不是按注册总数。Demo 10 个全在画面里应几乎持平。
- [ ] **GC Alloc**：对着 `ClusterMeshDrawContext.Draw` 看。预期扎在 `SetMatrixArray` / `SetVectorArray`，C# 热路径稳态接近 0。
- [ ] **EditMode**：关 Tuanjie 后跑 `ClusterMesh.Editor.Tests`（编辑器占用时 batchmode 会被拒）。含 `Constructor_ValidBake_ShadowMaterialIsDistinctInstance`。
- [ ] **场景 Sun**：拆阴影 list 只认 `RenderSettings.sun`（存在、开、Directional、`shadows != Off`）。没赋 Sun 走 fail-safe，不剔、不拆 list。

---

## B. 下一期（已讨论，先 spec）

按「第 3 项进包并量过」再开。不要和第 3 项阴影搅在同一期。

- [ ] **第 4 项：按层 / 按组减 dispatch**  
  现在 T>0 仍是 `物体 × 全 DAG cluster` 一次 Dispatch，选层在 kernel 里扫。目标：少发对当前 T 不可能可见的层/组。  
  前置：A 的 Profiler 证明 compute 仍贵。  
  正交：不要和 cascade / 附加灯混做。  
  出处：`cull-lookup` / `cpu-object-cull` 的 Non-goals。

- [ ] **矩阵改 GraphicsBuffer，去掉 `SetMatrixArray` GC**  
  常驻 `StructuredBuffer<float4x4>`（256），每帧 `SetData`。compute / Lit 不再用 CBUFFER `float4x4[256]`。  
  上传源：复用的 `Matrix4x4[]`，或这时再上 `NativeArray<Matrix4x4>`。  
  **不要**只把 `_l2w` 改成 Native / NativeList 却继续 `SetMatrixArray`（API 仍吃托管数组，GC 还在）。  
  6 个平面的 `SetVectorArray` 可顺带进同一块或小 buffer。

- [ ] **CPU 剔全局 static 总闸**  
  现在只有每物体 `enableCpuObjectCull`。规格写了「总闸以后再加」。

---

## C. 有条件才做

Profiler 打到对应热点再讨论 + spec。不要为「感觉该上」开工。

- [ ] **Job / Burst 做 `KeepObject`**  
  盒 × 平面在 N≤几百时调度往往比主线程 for 贵。等到几千实例且 Alloc/Time 打在这段。

- [ ] **NativeArray 当 `GraphicsBuffer.SetData` 源**  
  只跟 B 的矩阵 buffer 一起做。单独换 `List` / `_l2w` 不划算。`NativeList` 不适合固定 256 槽。

- [ ] **合批配对从 O(N²) 改按 asset 桶**  
  主线程风险在 `Flush` 配对和求逆，不在盒 × 平面。Profiler 打在 `ClusterMeshSceneBatcher.Flush` 再做。

- [ ] **按 cascade 精剔**  
  要复刻 URP 级联球/矩阵，级联一变就漏远处 caster。棱柱偏宽可接受；证明仍太贵再开。

---

## D. 停放（新讨论 + 新 spec 才能开工）

第 3 项和第 1 期已明确不做。列在这里避免当漏项。

| 项 | 为何停 |
| --- | --- |
| 附加灯 / 点光体积 | 和方向光棱柱不是一套；猜错漏灯边影 |
| 修画面内 Cone 减影 | 原有问题；第 3 项只保证屏外 caster 不吃相机 Cone |
| 两次 Dispatch | 一次 kernel 写两份 buffer 足够 |
| NativeList / NativeArray 换现有复用数组 | 消不掉 `SetMatrixArray` GC |
| Nanite VSM / 多阴影视图 | 另一条管线 |
| Hi-Z / 软件光栅 / 流式 | 第 1 期 + DAG 非目标 |
| 蒙皮 | 只要静态 `MeshFilter` |
| URP Renderer Feature / 接游戏场景 | 研究原型自包含；正式接入另开 |
| GLES 降级绘制 | 无 Compute / Indirect 报错不画 |
| METIS / meshoptimizer / 时域抗跳变 | DAG 非目标 |
| 改 64/124、header 96、组 48 | 全期锁定 |

---

## 相关文档

- `docs/superpowers/specs/2026-09-06-clustermesh-cull-lookup-design.md`
- `docs/superpowers/specs/2026-09-06-clustermesh-cpu-object-cull-design.md`
- `docs/superpowers/lessons/2026-09-06-indirect-shared-material-flicker.md`
- `docs/superpowers/specs/2026-09-04-clustermesh-design.md`（v1 非目标）
