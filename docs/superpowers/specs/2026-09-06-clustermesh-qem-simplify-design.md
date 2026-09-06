# ClusterMesh Baker 离线 QEM 简化

Date: 2026-09-06  
Status: Locked for implementation  
Module: `Assets/ClusterMesh`  
Parent: `docs/superpowers/specs/2026-09-06-clustermesh-lod-dag-design.md`

建 LOD DAG 时，组内简化可选 **QEM 边折叠**（Nanite 同构：位置 + 法线/UV）。默认开。关掉则必须与现在的最短边 **逐条相同**。不引进 meshoptimizer 包。运行时选层、合批、cull 不动。

## 1. Purpose

最短边专折短边，父块容易刺、塌。Nanite 离线用 QEM 选边并优化落点。这一期只换「折哪条、折到哪」；锁边、分组、再切、`lodError` 量法沿用 DAG 规格。

## 2. Goals and non-goals

### Goals

- `ClusterMeshBakeSettings.useQemSimplify`，默认 **`true`**。
- Baker 窗口：仅当 **Build LOD Hierarchy** 勾选时显示「QEM Simplify」。关层次忽略该字段，只切叶子。
- `false`：现有 `TryCollapseShortest`，不改选边/折点/属性平均。
- `true`：自写 QEM。语义对照 meshoptimizer 的 vertex lock，不整文件摘抄、不引新包。
- 锁边与现在相同。属性进误差（位置 + 法线 + UV），权重写死，无滑条。
- 存盘 `lodError` 仍是双向表面偏差。Renderer / Viewer 的 **Lod Error Threshold 不藏**，仍是运行时 T。
- 不勾 QEM 的重 Bake、以及显式 `useQemSimplify = false` 的单测，与改前一致。
- 已烤资产不重 Bake 则父块仍是最短边，运行时不变。

### Non-goals

| 不做 | 原因 |
| --- | --- |
| 引进 meshoptimizer | 只要 lock/QEM 语义 |
| `lodError` 改存裸 QEM cost | 量纲不是米，现有 T 档失效 |
| 藏 Lod Error 阈值 | T 仍是选层门槛；T=0 只出叶子 |
| 属性权重 UI | 第一期写死 |
| METIS、时域抗跳、改 64/124 / header 96 | DAG 非目标 |
| 改 `LateUpdate` / Draw / CPU 剔 | 正交 |

## 3. 怎样算做对了

必须同时满足：

1. **`useQemSimplify = false`：** 同一网格、同一 `buildLodHierarchy`，Bake 与现在一致（叶子三角和、锁边点不移、现有锁边/4→2/翻面用例绿）。
2. **默认 / `true`：** 锁–锁不折；未锁–锁只折到锁点，锁点坐标与属性不变；外圈与源组边界对齐（量化焊后）。
3. **`lodError`：** 仍 `max(1e-6, 孩子, 父→源, 源→父)`。QEM cost 不写入 header / group。
4. **运行时：** `lodErrorThreshold` 公式与现在相同。不勾 QEM 的旧资产不重 Bake，画面与现在相同。
5. 关 **Build LOD Hierarchy**：与现在一样只出叶子，`hierarchyVersion = 0`，不跑任何折叠。
6. 不改 FogOfWar / Packages。

算做错：

- `false` 时改了 `Consider` / 最短边折点 / 未锁属性平均。
- 开 QEM 却移动锁点，或锁–锁被折。
- 把 QEM cost 当 `lodError` 或藏掉阈值控件。
- 现有「锁边 / 最短边拓扑」单测因默认改成 true 而红，却没把它们钉成 `false`。

## 4. Locked decisions

| Topic | Decision |
| --- | --- |
| 默认 | `useQemSimplify = true`。窗口与 `new ClusterMeshBakeSettings()` 皆然 |
| 关层次 | 不折叠。字段可留在 settings，Bake 不读 |
| 入口 | `CollapseHalf` 按开关选 `TryCollapseShortest` 或 `TryCollapseQem`。`MarkLocked`、翻面、边界变多失败、细长刺失败、再切，共用 |
| 锁边 | 锁–锁：不折。未锁–锁：只折到锁点。未锁–未锁：QEM 最优位置（可限制在边上）。锁点不删、不移，法线/UV 不动 |
| 最短边路径 | **禁止改语义**。QEM 新文件/新函数 |
| QEM | Garland–Heckbert 面积加权平面 quadric + 法线 xyz、UV xy。`NormalWeight = 1`，`UvWeight = 1`。无 UI |
| 参考 | 可对照 meshoptimizer simplify + vertex_lock **语义**。不粘贴其源码进仓库 |
| 目标 | 仍约 50% 三角。折不动则停，失败保底仍进下一池 |
| 翻面 | 新旧三角法线点积 `< 0` 跳过该边（与现在相同） |
| `lodError` | 双向表面偏差，不用 QEM cost |
| 阈值 UI | Renderer 数字 + 档位、Viewer 滑条 **不藏、不改含义** |
| version | 仍 0 / 2。不新增 hierarchyVersion |
| 资产 | 不强制存「用了哪种简化器」。要看 QEM 父块必须重 Bake |
| 依赖 | 不改 Packages |

## 5. API

`ClusterMeshBakeSettings` 增加：

```text
bool useQemSimplify = true
```

`ClusterMeshLodBaker`（或旁路静态类，Editor）：

```text
TryCollapseQem(pos, nrm, tan, uv, tris, locked) -> bool
```

生产路径：`CollapseHalf` 读 `settings.useQemSimplify`。纯函数单测可直接打 `TryCollapseQem`。

权重常量（与实现同一处，测试可引用）：

```text
QemNormalWeight = 1f
QemUvWeight = 1f
```

## 6. 测试

程序集仍是 `ClusterMesh.Editor.Tests`。

**钉死老路径（必须 `useQemSimplify = false`）：**

- 所有现有断言锁边坐标、4→2 拓扑、折叠选最短边、边界边数不增的 Bake 用例。
- 不要依赖「默认 false」。默认已是 true。

**可不钉（叶子 / 能力 / 合批）：**

- 只断言叶子三角和等于源、`hierarchyVersion`、context 能建的，允许默认 QEM。若偶发失败再钉 false，不要改叶子切块去迁就 QEM。

**新用例：**

- `TryCollapseQem`：锁–锁不选；未锁–锁后锁点坐标不变。
- 一块有凹凸、关锁边足够折的汤：QEM 与最短边都能减到 ≤50% 三角；QEM 后锁点仍在。
- `useQemSimplify = false` 与改前最短边金样一致（可用现有 FourQuads 锁边用例）。
- 窗口：层次开时画出 QEM 勾；层次关时不要求能改到折叠（隐藏或 disable）。

不测 Game 像素。不改 CPU 剔 / 合批用例逻辑。

## 7. 文件

| 文件 | 变化 |
| --- | --- |
| `Runtime/ClusterMeshTypes.cs` | `useQemSimplify` |
| `Editor/ClusterMeshBakerWindow.cs` | 层次下的 QEM 勾，默认开 |
| `Editor/ClusterMeshLodBaker.cs` | `CollapseHalf` 分支；最短边函数不改语义 |
| `Editor/ClusterMeshQem.cs`（新，或 LodBaker 内新区） | QEM + 属性 |
| `Tests/Editor/ClusterMeshQemTests.cs` | 新 |
| 现有 Baker / LOD Bake 测试 | 锁边类显式 `useQemSimplify = false` |
| DrawContext / compute / Lit | **不改** |

## 8. 相关

- 补全 DAG 规格「组内最短边折叠」：现为可选 QEM。不改 2026-09-06 DAG 正文段落，以本文件为准。
- 不改 2026-09-06 LOD 选层 / 阈值档含义。
