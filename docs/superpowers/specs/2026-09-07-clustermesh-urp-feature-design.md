# ClusterMesh 保留老管线 + URP Feature（R1 / R2）

Date: 2026-09-07  
Status: R1 implemented（R2 停放）  
Module: `Assets/ClusterMesh`  
Parent: `docs/superpowers/specs/2026-09-04-clustermesh-design.md`  
Related: `docs/superpowers/specs/2026-09-06-clustermesh-cpu-object-cull-design.md`  
Backlog: `docs/superpowers/todos/2026-09-06-clustermesh-followups.md`

保留现有 `LateUpdate` → `SceneBatcher.Flush` → `Graphics.DrawMeshInstancedIndirect`。另做 URP Renderer Feature：该相机的 Renderer **挂了且激活** Feature 则走 URP 提交；否则老路径。默认不改工程 URP 资产。菜单只动当前 URP Asset 的 Default Renderer。

Baker、DAG、QEM、合批键、cull kernel、64/124、header 96、组 48 **不动**。探针 / 雾 / DepthNormals 是 P3，不在本期。

## 1. Purpose

老路径能画、能出影，但几何不在 URP Pass 时间线上：深度图、不透明拷贝、TAA、叠相机、和 MeshRenderer 并排时容易对不齐。接 URP 是换 **谁在哪一帧喊出画**，不是重写简化或选层。

## 2. Goals and non-goals

### Goals

- 老路径保留。没跑过菜单、Renderer 上没有 Feature：行为与现在相同，现有 EditMode 必须仍绿。
- U1：该相机 `scriptableRenderer` 上有激活的 Feature → 本帧 LateUpdate **跳过该相机**；否则 Flush 照旧。禁止同一相机双画。
- H1：代码进仓库即可用。默认不改玩法 / Demo 的 URP Renderer。
- T1：`Tools/ClusterMesh/Enable URP Feature` / `Disable` 只写 **当前 `UniversalRenderPipeline.asset` 的 Default Renderer**。已有则打开；Disable 卸下或关掉。
- P2 + **R1**（第一期实现）：一次 cull；Depth / Forward 用 `CommandBuffer.DrawMeshInstancedIndirect`；阴影仍 `Graphics.DrawMeshInstancedIndirect(ShadowsOnly)`，在 `AddRenderPasses` 里登记，交给 URP `MainLightShadowCasterPass`。
- Viewer、Scene、Preview、反射探针不走 Feature。
- 单测：无 Feature = 老路径；有 Feature = Flush 跳过该相机；菜单能对 Default Renderer 挂上/卸下。不测 Game 像素。

### Non-goals

| 不做 | 原因 |
| --- | --- |
| **R2**（自己往级联 atlas `cmd` 画阴影） | 见第 5 节。观感/性能相对 R1 几乎无收益；漏级联风险高 |
| P3 探针 / SH / 雾 / lightmap / DepthNormals | shader 对齐，不是提交时机 |
| 改现有游戏 Universal Renderer（无菜单） | 全工程默默切路径，FogOfWar / 玩法场景会中招 |
| 扫工程里所有 Renderer | T1 只动 Default，避免误改 2D / 叠相机 Renderer |
| 按 cascade 精剔、附加灯体积 | 已在 CPU 剔规格 Non-goals |
| 时域 LOD 滞回、Hi-Z | 另条 TODO |
| 蒙皮接同一 Feature | 2026-09-11 已做：`2026-09-11-clustermesh-skinned-urp-feature-design.md` |
| 改 64/124、header 96、合批键、FogOfWar、Packages | 全期锁定 |

## 3. 不接 URP 的缺陷（为何要做 R1）

`LateUpdate` 提交 **不是画不出来**，是和旁边 MeshRenderer 不在同一趟 Pass：

- `_CameraDepthTexture` 常赶不上 Prepass → SSAO / 接触阴影 / 软粒子缺块
- `_CameraOpaqueTexture` 可能没有 ClusterMesh
- 无 MotionVectors，TAA / TSR 鬼影；换 LOD 更闪
- 叠相机 / Overlay / 探针要自己对 `targetCamera`，对错就漏或双画
- Frame Debugger 里 Indirect 单列，和 Decal / SSAO 顺序不稳定
- EditMode Game 靠 `QueuePlayerLoopUpdate`；Feature 随 URP 渲染走

主光级联影、视锥、Cone、CPU 物体剔、LOD 老路径已经有。这些缺陷是 **时间线**，不是缺 QEM。

## 4. R1 设计（第一期，锁定）

### 4.1 为何选 R1 而不是 R2

URP 14 的级联影图由 `MainLightShadowCasterPass` 按级联切视口、算 VP、写 bias / soft shadow。`Graphics.DrawMeshInstancedIndirect(..., ShadowsOnly)` 仍会被这条 Pass 收进去。

R1 只把 **深度和主画** 推进 Prepass / Opaque 的 `CommandBuffer`，阴影继续交给 URP。这样：

- 对上「不接 URP」里最痛的深度 / 不透明时间点
- 不复刻级联矩阵（与「不按 cascade 精剔」一致）
- 身后方向光影子、分 list、fail-safe 沿用第 3 项
- 产品观感（级联、软阴影）与现在相同，不多一倍阴影 Draw

R1 **不解决** 探针和雾。那是 P3。

### 4.2 路径切换

`LateUpdate` 早于 `AddRenderPasses`，不能靠「本帧 Feature 跑过了」，也不能在 `Create` 里登记 live `ScriptableRenderer`（那时 Flush 已经决定过了）。

- `ShouldSubmitUrp`：`CameraType.Game` 且该相机对应的 `ScriptableRendererData.rendererFeatures` 里有激活的 `ClusterMeshUrpFeature`。编辑器 Play / 非 Play Game View 与 Player 相同
- 相机 → Renderer Data：URP Asset `m_RendererDataList` + 相机 `m_RendererIndex`（`-1` 用 Default）；测试用 `RendererDataOverrideForTests`
- `ShouldSkipLegacyFlush` ≡ `ShouldSubmitUrp`，避免 Scene / Preview / 反射探针被跳过 Flush 又不出 Feature
- Scene View 仍是 `ClusterMeshLifetime` Update + `SceneViewRenderer.Draw`，不进 Feature
- 没挂、没激活、不是 Game、不是 URP → 老路径

同一帧只许一条路径出画。

### 4.3 一帧（URP 相机）

1. `AddRenderPasses`：cull 一次（与现在同一 kernel、两份 append）；`Graphics.DrawMeshInstancedIndirect(ShadowsOnly)` 登记阴影 list（无可用 Sun 则 fail-safe，不拆 list，与第 3 项相同）。
2. Pass `BeforeRenderingPrePasses`：`cmd` 画 DepthOnly。
3. Pass `BeforeRenderingOpaques`：`cmd` 画 Forward，`ShadowCastingMode.Off`，接收阴影沿用 renderer 开关。

`DrawContext` 拆出可测的「cull + 登记阴影 / cmd 深度 / cmd 主画」，老 `Draw(...)` 仍给 Viewer 和无 Feature 相机用。

### 4.4 工具

| 菜单 | 行为 |
| --- | --- |
| `Tools/ClusterMesh/Enable URP Feature` | 当前 `GraphicsSettings` / Quality 正在用的 URP Asset → Default Renderer。没有 Feature 就加并激活；有则 `SetActive(true)` |
| `Tools/ClusterMesh/Disable URP Feature` | 同一份 Default Renderer：卸下或 `SetActive(false)`（实现选一种，卸载更干净） |

不扫 `FindAssets` 全工程。对比 FogOfWar 安装器：FoW 会改所有 Renderer 和深度拷贝；ClusterMesh **禁止** 那样。

### 4.5 怎样算 R1 做对了

1. 未 Enable：现有 `ClusterMesh.Editor.Tests` 全绿；Demo 不挂 Feature 则画面与现在相同。
2. Enable 后：该 Default Renderer 上的相机 LateUpdate 不再 Flush；Frame Debugger 深度在 Prepass 附近、主画在 Opaque 附近。
3. 有 Sun 且 `castShadows`：身后能投进画面的影子仍在（阴影 list + ShadowsOnly）。
4. Disable 后回到 1。
5. Scene / Viewer 不出现 Feature 提交的 Indirect。

做错：同一相机双画；没菜单却改了玩法 Renderer；R2 式自绑 atlas；把 QEM cost 或滞回塞进本期。

## 5. R2 设计（停放，说明原因）

### 5.1 R2 是什么

Shadow / Depth / Forward **全部** `CommandBuffer`。阴影 Pass 自己绑 `_MainLightShadowmapTexture`，按级联视口、light VP、bias 各画一次阴影 list。不再依赖 `Graphics.*ShadowsOnly` 被 URP 收集。

否决的邻案：`AfterRenderingShadows` 再画阴影几何——atlas 已打完，进不去图。

### 5.2 为何现在不做（产品级收益也不大）

玩家看见的影子质量来自 **URP 级联 + 软阴影 + 分 list**，R1 已经走这条。R2 不改变 atlas、不增加点光、不提高分辨率，只换提交 API。帧时间通常更差（每个级联自己 Draw）。

R2 **补不上**：探针、雾、TAA 鬼影、SSAO（R1 的 Depth cmd 已对准 Prepass）、蒙皮。

R2 **真正值钱** 的触发（到了再开，先补本节为实现规格）：

- 升 **URP Render Graph** 后，`Graphics.DrawMeshInstancedIndirect(ShadowsOnly)` 不再进影图（用 Frame Debugger 证伪，不要猜）
- 要做 **按 cascade 精剔** 或自建影图（VSM）——与 followups C 同一前提
- 认证/抓帧要求几何必须全部在 SRP CommandBuffer 里

未触发时开 R2：漏级联、和棱柱剔打架、Tuanjie URP 一升 internal API 就坏，EditMode 也验不了 atlas 像素。

### 5.3 若开 R2（预锁，防以后重讨论）

- 仍只认 `RenderSettings.sun`；无 Sun fail-safe 与第 3 项相同
- 级联数 / 分辨率 / Stable Fit **读 URP 当时值**，禁止写死 4 级
- 主画/深度仍 R1 的两个 Pass；只换阴影提交
- 不和「按 cascade 精剔」绑死同一 PR，但 R2 是它的前置
- 验收：Frame Debugger 打开主光 atlas，身后 caster 在对应 cascade 里有深度；升一档 Shadow Resolution 不花影

## 6. 文件（R1 开工时）

| 文件 | 变化 |
| --- | --- |
| `Runtime/ClusterMeshUrpFeature.cs`（新） | Feature + 两 Pass；登记 Renderer |
| `Runtime/ClusterMeshDrawContext.cs` | 拆 cull / cmd 深度 / cmd 主画 / Graphics 阴影；老 `Draw` 保留 |
| `Runtime/ClusterMeshSceneBatcher.cs` | `Flush` 跳过已登记 Renderer 的相机 |
| `Editor/ClusterMeshUrpFeatureMenu.cs`（新） | Enable / Disable Default Renderer |
| Tests | 检测、跳过 Flush、菜单挂卸；老用例默认无 Feature |
| DrawContext compute / Lit / Baker | **不改语义** |
| `Assets/Settings` URP、FogOfWar | **不改**（除非用户跑菜单） |

## 7. 相关

- 第 1 期 Non-goals「不接 URP Renderer Feature」：以本文件为准，改为可选第二条路径。
- 不改 2026-09-06 LOD / DAG / QEM / CPU 剔正文。
- 时域抗跳仍停在 followups D；TAA 鬼影 R1 只改善「没进深度」，不代替滞回。
