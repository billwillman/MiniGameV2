# ClusterMesh 锁边 + 多层 DAG 设计

Date: 2026-09-06  
Status: Locked for implementation  
Module: `Assets/ClusterMesh`  
Parent LOD: `docs/superpowers/specs/2026-09-06-clustermesh-lod-design.md`  
Parent: `docs/superpowers/specs/2026-09-04-clustermesh-design.md`

本文件覆盖 **Nanite 式离线层次**：分组、锁外圈、三角减半、再切开、收到根。不改 2026-09-04 规格正文。合批键、Indirect、template 372、4 个 vertex StructuredBuffer、Cone、分色、阴影开关一律沿用。不重写 2026-09-06 两层 LOD 规格正文；新 Bake 以本文件为准。

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
| 分组 | 能凑 4 就 4；剩 2 或 3 整组；剩 1 停。同一 `materialIndex` / submesh。先打下标最小，再按 AABB 中心近 |
| 简化 | 锁外圈后最短边折叠到约 50% 三角，再走现有贪心切块 |
| 再切 | 新 cluster 仍 ≤64 顶点 / ≤124 三角。典型 4→2、2→1 |
| 收根 | `while pending >= 2` 且未达 16 层。失败保底见第 5 节 |
| 锁边 | 组并集外圈（焊后只出现一次的边）顶点不删、不移。可把未锁点焊到锁点上 |
| 超预算 | 某组锁点无法切进 64，或简化零进展：该组不建父 |
| `parentIndex` | v2：父 **组** 下标；`-1` 无父。v1：仍是父 cluster |
| `lodError` | 叶子 0。组 = `max(本组简化偏差, 孩子 lodError)`。该组切出的父块写入同一误差 |
| `flags` | bit0 = 非叶（`FlagParent`）。bits 8–15 = lod 层号。`PackFlags(level)` |
| version | 新 Bake 且 `buildLodHierarchy`：`2`。关层次：`0`。已盘 v1 走老 TestLod |
| 阈值 | `T <= 0`：只出层 0。`T > 0`：`Project(self) < T` 且（无父组或 `Project(组) >= T`） |
| 投影 / 距离 | 与两层期相同。组距离：相机到组世界 AABB 中心 |
| 合批 | 批内阈值 **max**。默认 0 |
| 多材质 | 每个 submesh 一棵 DAG，各自收根 |
| Draw | 不变。父块仍是普通 cluster |

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
- `TryGetParent(...)`：按 version 从 clusters 或 groups 取父误差与局部中心

DrawContext 增加 groups 的 GraphicsBuffer（空则绑 1 条占位）。这不是第 5 条 vertex stream。

## 5. Baker

现有叶子 `BakeSubmesh` / `EmitCluster` 不变含义。叶子：`parentIndex = -1`，`lodError = 0`，`flags = PackFlags(0)`。

`buildLodHierarchy` 时每个 submesh 调 `ClusterMeshLodBaker.BuildHierarchy`（替换 `BuildParents`）：

1. `pending` = 本 submesh 新叶子下标。`level = 0`。
2. 当 `pending.Count >= 2` 且 `level < 16`：`level++`。从 pending 反复取组：`size = pending剩余 >= 4 ? 4 : 剩余`。选未用里下标最小的，再取 AABB 中心最近的 `size-1` 个。
3. 建组：展开孩子三角 → 按位置量化焊 → 出现一次的边为外圈锁点 → 折叠到 `max(1, srcTris/2)`（只折未锁–未锁或未锁→锁；两锁点之间的边不折）→ 若三角数未减少则失败 → 简化汤再贪心切块 → 新 cluster 的 `flags = PackFlags(level)`，`lodError = 组误差`，`parentIndex = -1`。
4. 成功：append `ClusterGroup`；孩子 `parentIndex = 该组`；先前产出这些孩子的组写入 `parentGroupIndex`；新 cluster 进 `nextPending`。失败：孩子保持无父，本轮不再试。
5. 剩 1 个进 `nextPending`（与新父同池，便于下一轮 2/3 合）。
6. 本轮零成功则停。`pending = nextPending`。

组 AABB = 源孩子 AABB 并。组误差 = `max(新顶点到源汤最大偏差, 孩子 lodError 的 max)`。

切块复用 Baker 贪心（抽成可调用的 soup 入口），以便再切与叶子同一套 64/124。

**收不到单根（接受）：** 锁点切不进 64；简化零进展；某 submesh 只有 1 个叶子。大网格外轮廓锁点可能 `> 64`，最高层会停在若干粗块，而不是 1 块。不解锁凑根。

叶子三角对源仍覆盖一次。断言「三角数等于源」只数层 0。

`hierarchyVersion = settings.buildLodHierarchy ? 2 : 0`。

## 6. Cull / 场景

`ClusterMeshCull.compute`：`StructuredBuffer<ClusterGroup> _Groups`，`uint _GroupCount`。

`TestLod`：

- `version < 1`：通过
- `T <= 0`：层 0 才通过（`Level(flags)==0`）
- `version == 1`：现有「父是 cluster」
- `version >= 2`：父是 `_Groups[parentIndex]`（越界当无父）。组世界中心做 `LodProjected(g.lodError, ...)`

Viewer / Renderer 阈值默认 0。Show Lod Levels 按层号着色（不只有 L0/L1）。Gizmo 可见性走 `TryGetParent` + `IsVisible`。

## 7. 测试

- `ClusterGroup` stride 48；header 仍 96。
- v2：`T<=0` 只叶子；大 T 出父组块、藏孩子；v0 不被 LOD 否决；v1 旧公式仍过。
- Bake：`buildLodHierarchy=false` → version 0。`true` → version 2，Grid 有组；4 个孩子同一 `parentIndex`；组 `clusterCount` ≥ 1。
- 锁边：每个成功组，外圈锁点位置出现在该组父 cluster 顶点里（epsilon）。
- 2 块可合成时能到 1 个无父的粗块（小网格）。
- 合批 10 物体仍 1 Indirect（阈值 0）。
- 叶子三角和等于源。

## 8. 验证

Tuanjie `2022.3.48t2`，`-batchmode -nographics`，`ClusterMesh.Editor.Tests`。编辑器打开时不跑。要看多层须重 Bake 后拉阈值。
