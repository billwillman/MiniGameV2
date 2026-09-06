# ClusterMesh 两层 Cluster LOD 设计

Date: 2026-09-06  
Status: Locked for implementation planning  
Module: `Assets/ClusterMesh`  
Parent: `docs/superpowers/specs/2026-09-04-clustermesh-design.md`  
Batching: `docs/superpowers/specs/2026-09-05-clustermesh-object-batching-design.md`

本文件只覆盖 **两层 cluster 层次 LOD**（叶子 + 一层父块）。不改 2026-09-04 规格正文。合批键、Indirect、template 372、4 个 vertex StructuredBuffer、Cone、分色、阴影开关一律沿用。

新 Bake 的锁边 + 多层 DAG 以 `docs/superpowers/specs/2026-09-06-clustermesh-lod-dag-design.md` 为准（`hierarchyVersion = 2`）。

允许 **重新 Bake** 资产。旧资产靠 `hierarchyVersion` 跳过 LOD 选择，不把旧 header 的 `pad0==0` 当成「父节点是 cluster 0」。

## 1. Purpose

单层 cluster 只能整网同一细度。远景仍画全部叶子，Cluster 技术的收益只剩视锥和 Cone。

这一期在现有叶子之上合并出父块：近处画叶子，远处画父。可见集仍是 cluster 下标，Draw 路径不换。

## 2. Goals and non-goals

### Goals

- Baker 在现有贪心切块之后，按材质把约 4 个叶子合成 1 个简化父块。
- `ClusterHeader` 带 `parentIndex`、`lodError`、`flags`。结构体仍是 **96 字节**（替换现有 pad）。
- `ClusterMeshAsset.hierarchyVersion`：`0` = 预 LOD Bake（忽略父子，行为与现在相同）；`1` = 含层次。
- Cull 在世界 AABB + Cone 之后做 LOD 选择。阈值 `<= 0` 只出叶子（与现在同画面）。
- Viewer 与 `ClusterMeshRenderer` 可调阈值。合批用该批阈值的 **max**。
- 单测覆盖公式、无父叶子、近细远粗、旧 version 不误读 parent。
- 不改坏合批、Cone、分色、Cast/Receive Shadows、放置菜单。

### Non-goals

- 三层及以上 DAG、Nanite 式 lock border、时域抗跳变。
- Hi-Z、软件光栅、流式。
- 物体级 `LODGroup` 多 Asset。
- meshoptimizer / 二次误差完整实现（v1 用最短边折叠）。
- 混层接缝无裂缝（已知限制，文档写明即可）。
- 改 2026-09-04 规格正文；不改 FogOfWar / Packages / 玩法脚本。

## 3. Locked decisions

| Topic | Decision |
| --- | --- |
| 层数 | 恰好两层：叶子（LOD0）与父（LOD1） |
| 合并 | 同一 `materialIndex`，按 AABB 中心最近，**4 个叶子 → 1 个父**。余下 1～3 个叶子无父 |
| 简化 | 合并三角后最短边折叠，直到 `vertexCount <= 64` 且 `triangleCount <= 124` |
| `lodError` | 父：每个父顶点到「四叶子源三角soup」最近点的距离的最大值，物体空间。叶子：`0` |
| `parentIndex` | 叶子指向父的下标；无父或父块自己为 `-1` |
| `flags` | bit0 = `IsLodParent`（1 = 父块） |
| 旧资产 | `hierarchyVersion < 1` 时 **不做** TestLod，视锥+Cone 通过即 Append。旧盘里的 pad 不当父子 |
| 新 Bake | `hierarchyVersion = 1`，`BakeSettings.buildLodHierarchy` 默认 `true` |
| 阈值 | 屏幕像素。`T <= 0`：只输出 `IsLodParent == 0` 的 cluster |
| 投影 | 透视：`screenError = lodError * (0.5 * pixelHeight) / max(distance, 1e-4) / tan(0.5 * fovY)`。正交：`lodError * pixelHeight / max(2 * orthographicSize, 1e-4)` |
| 选择 | `hierarchyVersion >= 1` 且 `T > 0` 时：`selfProjected < T`，并且（无父 **或** `parentProjected >= T`） |
| 合批阈值 | 同一 `(asset, camera)` 批内取各 renderer `lodErrorThreshold` 的 **max** |
| 默认阈值 | Viewer 与 Renderer 均为 `0`（只出叶子） |
| Draw | 不变。父块也是普通 cluster，走同一 template / 分色 / Isolate |
| Header 步长 | 仍 96。`ClusterMeshDrawContext` 里 `GraphicsBuffer` stride 保持 96 |
| 接缝 | v1 接受叶子与邻父之间可能裂缝 |

## 4. Header 与资产

C# / HLSL 字段顺序必须一致：

```
uint vertexOffset
uint vertexCount
uint indexOffset
uint triangleCount
uint materialIndex
int  parentIndex      // 原 pad0
float lodError        // 原 pad1
uint flags            // 原 pad2
float4 aabbCenter
float4 aabbExtents
float4 coneAxisCutoff
float4 coneApex
```

`ClusterMeshLimits.ClusterHeaderStride = 96`。

`ClusterMeshAsset` / `ClusterMeshBakeResult` 增加 `int hierarchyVersion`。`CopyFrom` 一并写入。

`ClusterMeshBakeSettings.buildLodHierarchy` 默认 `true`。单测若只需叶子，设 `false`。Baker 窗口开关见 DAG 规格第 3 节；新 Bake 以 DAG 规格为准。

## 5. Baker

现有 `Bake` / `BakeSubmesh` / `EmitCluster` 不变含义。叶子 emit 时：`parentIndex = -1`，`lodError = 0`，`flags = 0`。

若 `buildLodHierarchy`：

1. 记录本 submesh 新叶子的下标区间。
2. 在该区间内，同一材质（本 submesh 已保证）重复：选未用叶子里下标最小的，在剩余叶子里取 AABB 中心最近的 3 个，凑满 4 个则建父；否则停止。
3. 建父：把 4 个叶子的 packed 三角（用已写入的 `vertices`/`indices` 展开到物体空间）合成 soup → 最短边折叠到预算 → 当作新 cluster append（新 vertex/index 跑）。`BuildCone` 用父三角。`flags |= 1`，`parentIndex = -1`，`lodError` 按第 3 节。4 个叶子的 `parentIndex` 写成该父下标。
4. 折叠实现放 `ClusterMeshLodBaker`（Editor 程序集），不进 Runtime。

叶子三角对源网格仍覆盖一次。父是额外几何。断言「三角数等于源」只数 `IsLodParent == 0`。

## 6. 选择公式（C# 与 compute 同文）

`ClusterMeshLod`（Runtime）：

- `NoParent = -1`
- `IsParent(uint flags)` → `(flags & 1u) != 0`
- `ProjectionScale(Camera)` → 第 3 节透视/正交
- `ProjectError(lodError, distance, scale)`
- `Select(self, parentOrNull, selfDist, parentDist, scale, threshold, hierarchyVersion)` → bool 可见

`ClusterMeshLod.IsVisible`：`hierarchyVersion < 1` 恒为 true（LOD 不否决；旧资产没有父块几何）。`threshold <= 0` 时可见 ⟺ `!IsParent(self.flags)`。否则：`Project(self) < T` 且（无父或 `Project(parent) >= T`）。凑不齐 4 个、没有父的叶子在 `T > 0` 时一直画，接受。

距离：相机到 **世界 AABB 中心**（已有 `TransformAabb`）。父与子各用自己的世界中心。

## 7. Cull / DrawContext / 场景

`ClusterMeshCull.compute`：

- `int _HierarchyVersion`
- `float _LodErrorThreshold`
- `float _LodProjectionScale`
- 读 `h.parentIndex` / `h.lodError` / `h.flags`；父 header 用 `_Clusters[parentIndex]`（先做范围检查，非法当无父）

`ClusterMeshDrawContext`：

- `HierarchyVersion`（从 asset 读，只读）
- `LodErrorThreshold` 默认 `0`
- 每帧按 camera 算 scale，set compute

`ClusterMeshRenderer.lodErrorThreshold` 默认 `0`。  
Batcher Flush：批内 max，赋给 context。Cone / 分色 / 阴影逻辑不改。

Viewer：`LOD Error Threshold` 滑条，默认 0。

## 8. 测试

- `ClusterMeshLodTests`：`T<=0` 只叶子；叶子+父在大 T 出父不出该叶；`version==0` 的 Select 不被 cull 误用（Draw 侧跳过）。
- Baker：`buildLodHierarchy=false` 保持现有条数；`true` 时 Grid 有父，叶子三角和仍等于源。
- 合批 10 物体仍 1 Indirect（阈值 0）。
- Header stride 96（测试 `Marshal.SizeOf<ClusterHeader>() == 96`）。

## 9. 验证

Tuanjie `2022.3.48t2`，`-batchmode -nographics`，`ClusterMesh.Editor.Tests`。编辑器打开时不跑。Mia 需重 Bake 后拉阈值才能看到换层。
