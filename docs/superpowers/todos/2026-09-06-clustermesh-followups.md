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
- [x] Baker 离线 QEM 简化（默认开，可关回最短边；`862b496`）

规格：`2026-09-06-clustermesh-cull-lookup-design.md`、`2026-09-06-clustermesh-cpu-object-cull-design.md`。

---

## A. 卫生 / 验证

规格：`2026-09-12-clustermesh-shipping-profiler-design.md`（无功能代码，量完填第 5 节再勾）。有数再开 B。

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

- [x] **URP Feature R1（保留老管线）**  
  规格：`2026-09-07-clustermesh-urp-feature-design.md`。Game 相机且 Renderer 有激活 Feature：Depth/Forward 走 `cmd`，阴影仍 `Graphics.ShadowsOnly`；否则 `LateUpdate` Flush。菜单 `Tools/ClusterMesh/Enable|Disable URP Feature` 只动当前 URP Asset 的 Default Renderer。默认不改工程资产。  
  蒙皮已接同一 Feature / 同一 `ShouldSubmitUrp`：`2026-09-11-clustermesh-skinned-urp-feature-design.md`。

R1 **没做**、且不要混进已勾选项的：P3 照明对齐、R2 自绑阴影 atlas、MotionVectors。见下方 D。

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
| 蒙皮 | 已做独立资产 / Renderer / Baker；URP 与静态同一 Feature 开关 |
| URP Renderer Feature / 接游戏场景 | R1 已做；P3 / R2 / MotionVectors 见下方 |
| GLES 降级绘制 | 无 Compute / Indirect 报错不画 |
| METIS / meshoptimizer | DAG 非目标 |
| 改 64/124、header 96、组 48 | 全期锁定 |

### 时域 LOD 抗跳（2026-09-06 讨论后停）

- [ ] **选层滞回，消阈值附近来回抖**  
  方向已定：**A**（硬切 + 记忆，不 crossfade / 不 geomorph）。Nanite 主路径也是硬切，靠 ~1px 误差 + TAA，不靠淡入。我们 T 常 2～4px 且整组进出，所以会闪。  
  未锁：滞回带宽（倾向 A1：往粗 `T×0.8` / 往细 `T×1.2`）还是延迟 N 帧。系数第一期写死。  
  约束：`T<=0` 仍只出叶子；按 **组** 记历史（与整组进/出对齐）；主画和阴影同一套记忆；不改 64/124 / header 96。  
  开工前：补完 A1/A2 并落盘 spec。不写 spec 不动 `TestLod`。

### URP 照明 / Pass 对齐 P3（R1 明确没做）

- [x] **探针 / SH / 雾**（lightmap / DepthNormals 仍停放）  
  规格：`2026-09-12-clustermesh-lighting-p3-probes-fog-design.md`。每物体 Light Probe SH + Forward 雾。  
  代码已落。`ClusterMesh.Editor.Tests` 因编辑器占用未跑。不要和 R2、时域滞回、Hi-Z、lightmap、DepthNormals 混一期。

- [x] **MotionVectors 时机**  
  规格：`2026-09-12-clustermesh-motion-vectors-timing-design.md`。`MotionVectorPassEvent = AfterRenderingSkybox + 1`。默认开关仍关。测试未跑。

- [ ] **叠相机 / 非 Default Renderer 挂 Feature**  
  T1 锁定：菜单只动当前 URP Asset 的 Default Renderer，禁止扫全工程（避免误改 2D / Overlay / FogOfWar 那套）。  
  Overlay 或相机指定了另一份 Renderer 时，要**手挂** `ClusterMeshUrpFeature`，否则那台相机仍走老 Flush。  
  若以后要工具化：新讨论（仍禁止 FoW 式全工程扫描），不要改 T1 默认。

### URP 阴影 R2（2026-09-07 讨论后停）

- [ ] **自己往级联 atlas `cmd` 画阴影**  
  规格：`2026-09-07-clustermesh-urp-feature-design.md` 第 5 节。  
  **为何现在不做（产品级收益也不大）：** 影子质量来自 URP 级联 + 软阴影，R1 已走 `MainLightShadowCasterPass`。R2 不改变 atlas、不加点数、通常更贵。补不上探针 / 雾 / TAA / 蒙皮。  
  **再开的触发（先证伪再写实现 plan）：** Render Graph 后 `Graphics.ShadowsOnly` 不再进影图；或要按 cascade 精剔 / VSM；或认证要求全在 SRP CommandBuffer。  
  未触发禁止开。不要和 R1 / P3 同一 PR。

---

## 相关文档

- `docs/superpowers/specs/2026-09-06-clustermesh-cull-lookup-design.md`
- `docs/superpowers/specs/2026-09-06-clustermesh-cpu-object-cull-design.md`
- `docs/superpowers/lessons/2026-09-06-indirect-shared-material-flicker.md`
- `docs/superpowers/specs/2026-09-04-clustermesh-design.md`（v1 非目标）
- `docs/superpowers/specs/2026-09-07-clustermesh-urp-feature-design.md`（R1 已做 / P3·R2·MotionVectors 停放）
