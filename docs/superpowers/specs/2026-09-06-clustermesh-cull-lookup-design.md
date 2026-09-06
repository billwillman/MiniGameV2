# ClusterMesh 出包 cull：O(1) 所在组 + 资产 AABB

Date: 2026-09-06  
Status: Implemented  
Module: `Assets/ClusterMesh`  
Parent: `docs/superpowers/specs/2026-09-06-clustermesh-lod-dag-design.md`

出包后 T>0 的 GPU cull 不再每 thread 扫组表；Indirect 的 world AABB 不再每帧扫全部 cluster。只改 Play 热路径。选层公式、合批键、header 96、组 48、64/124、四条 vertex stream 一律不动。

## 1. Purpose

商业量级下，`FindOwningGroup` 是 `物体 × cluster × 组`；`TransformBounds` 是 `物体 × cluster × 8` 角。Demo 无感，人群 / 深 DAG 会先顶 compute 和主线程。这两项都可以在加载时算死，运行时 O(1)。

## 2. Goals and non-goals

### Goals

- 加载时从 `groups[].clusterStart/Count` 推导 `int[clusterCount]`：父块 = 组下标，叶子 = `-1`。
- GPU 用 `StructuredBuffer<int>` 按 `clusterIndex` 读所在组。选层结果与扫表相同。
- DrawContext 缓存 `AssetLocalBounds`，每物体只变换这一份局部盒。
- 老资产不用重 Bake。不改 header / 组 stride。
- 单测：推导表与 `TryGetOwningGroup` 一致；变换后的资产盒包住各 cluster 的世界角点。

### Non-goals

- CPU 物体视锥（第 3 项）、按层 / 按组减 dispatch（第 4 项）。
- 把 owning 下标写进 header 或 `.asset`。
- Hi-Z、流式、改 96/48、改选层。
- 改 Baker、Gizmo、Viewer 的每帧逻辑（Gizmo 仍可扫组）。

## 3. Locked decisions

| Topic | Decision |
| --- | --- |
| 存盘 | 不新增字段。表只在 `ClusterMeshDrawContext` 构造时建，随 context 释放 |
| 推导 | `BuildOwningGroupIndices(clusterCount, groups)`。先填 `-1`。组区间落到 `[0, clusterCount)` 的写入组下标。区间非法则跳过。重叠时 **先写生效**（与 `TryGetOwningGroup` 先匹配一致） |
| GPU | `StructuredBuffer<int> _OwningGroups`，stride 4。空资产不建 context。`clusterCount==0` 不出现。占位至少 1 个 int |
| 何时读 | 与现在相同：`hierarchyVersion >= 2` 且 `T > 0` 才用下标。T=0 仍只看层号，不依赖表内容 |
| 越界下标 | `own < 0` 或 `own >= _GroupCount` 当叶子 |
| Header | 仍 96。不占用 `flags` / `aabb*.w` |
| AABB | 构造时 `AssetLocalBounds(asset)` 缓存。`TransformLocalBounds(local, l2w)` 变 8 个角。可比扫各 cluster 更宽，不得更窄 |
| 阴影 | 不改。盒只可能更大，Indirect / 阴影采集不会漏 |
| 合批 | 仍按块 Encapsulate 各物体世界盒 |

## 4. API

`ClusterMeshLod.BuildOwningGroupIndices(int clusterCount, IList<ClusterGroup> groups) → int[]`

- `clusterCount <= 0` → 长度 0。
- `groups == null` 或空 → 全 `-1`。
- `NoParent = -1`。

`ClusterMeshFrustum.TransformLocalBounds(Bounds local, Matrix4x4 localToWorld) → Bounds`

- 局部 `center ± extents` 的 8 角乘 `localToWorld`，再 `SetMinMax`。

`TryGetOwningGroup` 留给 Baker / Gizmo / 单测对照，不删。

## 5. Cull

`ClusterMeshCull.compute` 删掉按 `_GroupCount` 循环的 `FindOwningGroup`。改为 `_OwningGroups[clusterIndex]`。`TestLod` 分支不变。

Draw 每块仍绑 `_Groups` + `_OwningGroups`。这不是第 5 条 vertex stream。

## 6. 测试

- 叶子下标全 `-1`；组区间内等于组下标。
- `groups==null`、`clusterCount==0`、越界 `clusterStart` 不写坏。
- 重叠区间：与 `TryGetOwningGroup` 同一组下标。
- Bake 出的 Grid（开层次）：每个 cluster 的表项与 `TryGetOwningGroup` 相同。
- `TransformLocalBounds` 单位阵等于局部盒；平移只动中心。
- 旋转 + 缩放后，资产盒包含每个 cluster 的 8 个世界角点。
- header 仍 96，组仍 48。

## 7. 验证

Tuanjie `2022.3.48t2`，`-batchmode -nographics`，`ClusterMesh.Editor.Tests`。编辑器占用则不编造成功。不用重 Bake。T>0 画面应与改前一致。
