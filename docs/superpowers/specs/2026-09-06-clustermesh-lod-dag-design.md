# ClusterMesh 锁边 + 多层 DAG 设计

Date: 2026-09-06  
Status: Implemented（含 Baker 窗口 `buildLodHierarchy` 开关）  
Module: `Assets/ClusterMesh`  
Parent LOD: `docs/superpowers/specs/2026-09-06-clustermesh-lod-design.md`  
Parent: `docs/superpowers/specs/2026-09-04-clustermesh-design.md`

本文件覆盖 **Nanite 式离线层次**：分组、锁外圈、三角减半、再切开、收到根。不改 2026-09-04 规格正文。合批键、Indirect、template 372、4 个 vertex StructuredBuffer、Cone、分色、阴影开关一律沿用。不重写 2026-09-06 两层 LOD 规格正文；新 Bake 以本文件为准。

几何瘦身与存盘压缩见 `docs/superpowers/specs/2026-09-06-clustermesh-geometry-pack-design.md`。

允许 **重新 Bake**。`hierarchyVersion`：`0` 预 LOD；`1` 两层树（`parentIndex` 仍是 cluster）；`2` 本组 DAG（`parentIndex` 是 **父组** 下标）。

## 1. Purpose

两层父块不锁边，叶挨父会裂；且远景仍是若干父块，收不到根。这一期离线建成 DAG，运行时只按屏幕误差切一刀。

## 2. Goals and non-goals

### Goals

- Baker 在叶子之后做 Nanite 循环：优先 4 块一组；剩 2～3 整组；剩 1 即该分支的根。
- 组外圈锁死（按位置量化对齐）。组内最短边折叠，目标三角大约一半，再切回 ≤64 / ≤124。
- 循环直到池子 `< 2`、整轮建不出父、或 16 层封顶。目标收到尽量少的根，不保证大网一定 1 块。
- 资产增加 `ClusterGroup[]`。Header 仍 96 字节。`parentIndex` 在 version 2 表示父组。
- Cull：version 2 用组误差选层。`T <= 0` 只出叶子。
- 单测：锁边位置、4→2 同组、2 合 1、父子不同时可见、v0/v1 行为、叶子三角和等于源。
- 不改坏合批、Cone、分色、阴影、放置菜单。默认阈值 0 与现在同画面。

### Non-goals

- Hi-Z、软件光栅、流式、METIS、meshoptimizer。
- 时域抗跳变。
- 改 64/124、header 96、四条 vertex stream。
- 改 2026-09-04 规格正文；不改 FogOfWar / Packages / 玩法脚本。

## 3. Locked decisions

| Topic | Decision |
| --- | --- |
| 离线 / 运行时 | DAG 只在 Bake 建。运行时不算简化 |
| 分组 | 能凑 4 就 4；剩 2 或 3 整组；剩 1 停。同一 `materialIndex` / submesh。只把 **共享量化边界边** 的 cluster 放一组。先打下标最小，再在邻接里按 AABB 近。不相邻的不硬凑。层 1 以上 **禁止拆开同一 owning group**：种子若属于某组，必须先带上该组仍在池里的全部成员；再按邻接整组往上加。一组的输出 cluster 同生同死，避免只吞 P0、P1 被整组藏掉却无人替换 |
| 简化 | 组汤先按位置焊成一份顶点，再锁外圈、最短边折到约 50% 三角，再贪心切块。折叠若导致三角法线对折（新旧法线点积 < 0）则跳过该边。切块只按共点/共边生长，**禁止 `FindAnyFit` 硬塞不相邻三角** |
| 再切 | 新 cluster 仍 ≤64 顶点 / ≤124 三角。典型 4→2、2→1 |
| 收根 | `while pending >= 2` 且未达 16 层。失败保底见第 5 节 |
| 锁边 | 组并集外圈（焊后只出现一次的边）顶点不删、不移。可把未锁点焊到锁点上 |
| 超预算 | 某组锁点无法切进 64、简化零进展、出洞或细长刺：该组不建父。失败成员进 **下一池**（与无邻居相同），不是踢出 DAG。本轮 `remaining` 不再拿它们 |
| `parentIndex` | v2：父 **组** 下标；`-1` 无父。v1：仍是父 cluster |
| `lodError` | 叶子 0。组 = `max(双向简化偏差, 孩子 lodError)`。双向 = 父顶点到源汤、**源顶点到父表面**（折叠几乎不挪点，只量前者会恒≈0，任何 T>0 都会直接切到被折烂的根） |
| `flags` | bit0 = 非叶（`FlagParent`）。bits 8–15 = lod 层号。`PackFlags(level)` |
| version | 新 Bake 且 `buildLodHierarchy`：`2`。关层次：`0`。已盘 v1 走老 TestLod |
| 阈值 | `T <= 0`：只出层 0。`T > 0`：v2 **整组进、整组出**（见下）。v1 仍按单 cluster `Project(self)` |
| 投影 / 距离 | 与两层期相同，但 `lodError`（物体空间）须乘 `MaxAxisScale(localToWorld)` 再除世界距离。v2 父块的 self 投影用 **所在组** AABB 中心 + 组 `lodError` |
| 选层空洞 | 同组 2 个父块距离不同时，禁止一个过 `Project(self)`、一个不过。`T>0` 且 v2 时，**有 owning group 的父块不再做 per-cluster Cone**（视锥仍按块，屏外不画不是洞）。叶子 Cone 照旧 |
| 合批 | 批内阈值 **max**。默认 0 |
| Inspector 阈值档 | `ClusterMeshRenderer` **保留** `lodErrorThreshold` 数字框。右侧下拉只改这个字段，不另存档位。`ClusterMeshLodQuality`：极度精细 `0.5`、精细 `1`、一般 `2`、粗糙 `4`。对不上（含默认 `0`）显示「自定义」。精细对标 Nanite `r.Nanite.MaxPixelsPerEdge = 1`。Viewer 仍用滑条，不改 |
| 多材质 | 每个 submesh 一棵 DAG，各自收根 |
| Draw | 不变。父块仍是普通 cluster |
| Baker 窗口 | `Tools/ClusterMesh/Baker` 有 **Build LOD Hierarchy**，默认勾选（`true`）。关掉则只切叶子，`hierarchyVersion = 0`，资产更小。成功提示写出 cluster 数、组数、version |

## 4. 数据

`ClusterHeader` 字段顺序不变（96 字节）。v2 的 `parentIndex` 语义改为组下标。

`ClusterGroup`（**48 字节**，C# / HLSL 一致）：

```
int  clusterStart
int  clusterCount      // 本组切出的父 cluster，连续
int  parentGroupIndex  // 再上一层组，根为 -1
float lodError
float4 aabbCenter
float4 aabbExtents
```

`ClusterMeshLimits.ClusterGroupStride = 48`。

`ClusterMeshAsset` / `ClusterMeshBakeResult` 增加 `ClusterGroup[] groups`。`CopyFrom` 一并写。

`ClusterMeshLod`：

- `HierarchyVersionTwoLevel = 1`，`HierarchyVersionDag = 2`
- `MaxLodLevels = 16`
- `NoParent = -1`，`FlagParent = 1`，`LodLevelShift = 8`，`LodLevelMask = 0xFFu`
- `PackFlags(level)`，`Level(flags)`（无层号时 bit0 仍当 1，兼容 v1）
- `IsVisible`：v0 全可见；`T<=0` 只层 0；否则 self / 父误差。v1 父误差来自父 cluster；v2 来自父组
- `TryGetOwningGroup(clusterIndex)`：`clusterStart <= i < start+count` 的组。叶子不在任何组的 cluster 区间里
- `IsClusterVisible` v2：若能取到所在组，self 用该组误差和组中心；父用 `parentGroupIndex`。否则按叶子（`parentIndex` 组）
- `TryGetParent(...)`：按 version 从 clusters 或 groups 取父误差与局部中心

DrawContext 增加 groups 的 GraphicsBuffer（空则绑 1 条占位）。这不是第 5 条 vertex stream。

## 5. Baker

现有叶子 `BakeSubmesh` / `EmitCluster` 不变含义。叶子：`parentIndex = -1`，`lodError = 0`，`flags = PackFlags(0)`。

`buildLodHierarchy` 时每个 submesh 调 `ClusterMeshLodBaker.BuildHierarchy`（替换 `BuildParents`）：

1. `pending` = 本 submesh 新叶子下标。`level = 0`。
2. 当 `pending.Count >= 2` 且 `level < 16`：`level++`。从 pending 反复取组：`size = pending剩余 >= 4 ? 4 : 剩余`。选未用里下标最小的，先并上其 owning group 仍在池里的全部成员（可超过 4，宁整组不拆），再在 **共享边界边** 的邻接里按 AABB 近、同样整组往上加。种子没有任何剩余邻居则本轮不建组，原块进入下一池。
3. 建组：展开孩子三角 → **按位置量化焊并重映射三角**（组内接缝合成一条边）→ 出现一次的边为外圈锁点 → 折叠到 `max(1, srcTris/2)`（只折未锁–未锁或未锁→锁；两锁点之间的边不折；对折边跳过）→ 若三角数未减少则失败 → 简化汤再贪心切块（只共点生长）→ 新 cluster 的 `flags = PackFlags(level)`，`lodError = 组误差`，`parentIndex = -1`。
4. 成功：append `ClusterGroup`；孩子 `parentIndex = 该组`；仅当该 owning group 的成员 **全部** 被同一新组吞掉时，才写 `parentGroupIndex`；新 cluster 进 `nextPending`。失败：孩子保持无父，**进入下一池**，本轮 `remaining` 不再拿。
5. 剩 1 个进 `nextPending`（与新父同池，便于下一轮 2/3 合）。
6. 本轮零成功则停。`pending = nextPending`。

组 AABB = 源孩子 AABB 并。组误差 = `max(父顶点到源汤, 源顶点到父表面, 孩子 lodError)`。折叠后开边数变多（出洞）或最长边超过源汤 8 倍（细长刺）则该组不建父。

切块复用 Baker 贪心（抽成可调用的 soup 入口），以便再切与叶子同一套 64/124。

**收不到单根（接受）：** 锁点切不进 64；简化零进展；某 submesh 只有 1 个叶子。大网格外轮廓锁点可能 `> 64`，最高层会停在若干粗块，而不是 1 块。不解锁凑根。

叶子三角对源仍覆盖一次。断言「三角数等于源」只数层 0。

`hierarchyVersion = settings.buildLodHierarchy ? 2 : 0`。

`ClusterMeshBakerWindow` 把该开关画在 Cluster 预算下面。默认与 `ClusterMeshBakeSettings` 一致为 `true`。Demo Scene 菜单仍用默认设置（建层次），不另开开关。

## 6. Cull / 场景

`ClusterMeshCull.compute`：`StructuredBuffer<ClusterGroup> _Groups`，`uint _GroupCount`。所在组由加载时推导的 `_OwningGroups[clusterIndex]` O(1) 读取（见 `2026-09-06-clustermesh-cull-lookup-design.md`），不每 thread 扫组表。

`TestLod`：

- `version < 1`：通过
- `T <= 0`：层 0 才通过（`Level(flags)==0`）
- `version == 1`：现有「父是 cluster」
- `version >= 2`：所在组下标来自 `_OwningGroups`（叶子 `-1`）。找到则 `LodProjected(组)` 做 self，`parentGroupIndex` 做父。找不到（叶子）则 self 仍用 cluster，父是 `_Groups[parentIndex]`。越界当无父

Viewer / Renderer 阈值默认 0。`ClusterMeshRenderer` Inspector 由 `ClusterMeshRendererEditor` 画：同一行左侧 `PropertyField`（可手改），右侧 Popup（极度精细 / 精细 / 一般 / 粗糙）。Show Lod Levels 按层号着色（不只有 L0/L1）。Gizmo 可见性走 `TryGetParent` + `IsVisible`。

`ClusterMeshLod.MaxAxisScale` / `UsePerClusterCone`：C# 与 compute 同文。`T>0` + v2 + 已有 owning group → 跳过该 cluster 的 Cone。

## 7. 测试

- `ClusterGroup` stride 48；header 仍 96。
- v2：`T<=0` 只叶子；大 T 出父组块、藏孩子；v0 不被 LOD 否决；v1 旧公式仍过。
- v2：同组两个父块，相机靠近其中一块时，两块可见性相同（整组进/出，不因各自 AABB 距离一出一藏）。
- Bake：`buildLodHierarchy=false` → version 0。`true` → version 2，Grid 有组；4 个孩子同一 `parentIndex`；组 `clusterCount` ≥ 1。
- `WriteAsset` 关层次 → `hierarchyVersion == 0`，无组。
- 锁边：每个成功组，外圈锁点位置出现在该组父 cluster 顶点里（epsilon）。
- 4 个邻接 quad 焊后 9 顶点 / 外圈 8 条开边；该组父块并起来开边数仍是 8（没有多出来的内洞）。
- 同一 submesh 两块不相邻的三角：不建父组。
- 中间凸起的 4 quad：父块 `lodError` 必须跟上凸起高度（不能因折叠不挪点而≈0）。
- `ClusterMeshLodQuality`：`0.5/1/2/4` 分别对应极度精细/精细/一般/粗糙；`0` 与 `10` 为自定义。
- 同一 submesh 两块不相邻三角、默认 64/124：2 个 cluster（不 `FindAnyFit` 塞成 1 块）。
- 任一 `ClusterGroup.parentGroupIndex >= 0` 时，该组 `clusterStart..count` 里每个 cluster 的 `parentIndex` 都等于该父组（禁止半组被吃）。
- `UsePerClusterCone`：v2 且 T>0 且有 owning group 为 false；叶子或 T<=0 为 true。
- 均匀缩放 2：同一 `lodError` 的投影约为未缩放的 2 倍。
- 2 块可合成时能到 1 个无父的粗块（小网格）。
- 合批 10 物体仍 1 Indirect（阈值 0）。
- 叶子三角和等于源。

## 8. 验证

Tuanjie `2022.3.48t2`，`-batchmode -nographics`，`ClusterMesh.Editor.Tests`。编辑器打开时不跑。要看多层须重 Bake 后拉阈值。
