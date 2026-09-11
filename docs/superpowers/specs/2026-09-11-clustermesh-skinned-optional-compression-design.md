# ClusterSkinnedMesh 可选压缩（默认关闭）

Date: 2026-09-11  
Status: Implemented  
Module: `Assets/ClusterMesh`

在已有 GpuOnly / VTF FPS / CPU 拟合之上，增加四项**可选**压缩。全部默认关闭。关闭时磁盘与 GPU 布局与现在完全一致，旧资产不用重烤。

## 1. Purpose

Baker「蒙皮动画优化」里按模式显示未做的压缩项，勾上才改对应数据。默认关，不能改坏现有 GpuOnly / 16 字节权重 / 32 字节顶点 / 64 字节 cull。

## 2. Options

| 开关 | 显示 | 默认 | 开了之后 |
|---|---|---|---|
| 权重 8 字节 | 所有蒙皮模式 | 关 | 骨索引/权重 `uint8`。任一带权索引 >255 则**回退 16 字节**，不失败 |
| 紧凑 GPU Palette | GpuOnly / GpuAndCpu | 关 | 每骨 2 像素：quat + 位移/均匀缩放。CPU palette 仍 3×4 |
| 压缩 Cull 表 | 所有蒙皮模式 | 关 | 每秒 2 段（现 4）+ 磁盘 Deflate；GPU 仍 64 字节结构 |
| 紧凑 Rest 顶点 | 所有蒙皮模式；静态 Baker 有独立开关 | 关 | geometry 24 字节 oct 法线/切线。`CopyFrom` 三参数读 `ClusterMeshBakeSettings.packTightRestVertices`（默认 false） |

## 3. Compatibility

- 新字段缺省 = 关。`skinWeightStride` 0 当 16；`vertexStride` 0 当 32；`packedCullFrames` 空则用 `cullFrames`。
- 默认 shader 路径不改：16 字节 `StructuredBuffer<ClusterPackedSkinWeight>`、32 字节 `ClusterVertex`、每骨 3 像素。开启时另绑缓冲 / 另分支。
- 不升 `geometryVersion` / `skinningVersion`。`TryReadGpuGeometry` 仍拒绝 24 字节包；静态 `ClusterMeshDrawContext` / Viewer / Renderer 走 `TryReadTightVertices`。
- 不改 64/124、合批、Indirect、CSM。
- 静态没有逐 clip `cullFrames`，**不**做 Cull 表压缩。静态 cull 仍是 `ClusterHeader` 里一份 AABB+cone。

## 4. Non-goals

- BC/ASTC 图集、双四元数混合（紧凑 Palette 用 TRS）、meshopt、VAT。
- 改静态 ClusterMesh 默认 32 字节格式。
- 给静态加逐帧 Cull 表或压缩 ClusterHeader。

## 5. 静态紧凑 Rest

- `ClusterMeshBakeSettings.packTightRestVertices` 默认 false。Baker 静态页「高级压缩」开关 + 按顶点数量建议（≥1024 推荐），不自动勾选。
- `WriteAsset` / `CopyFrom(result, mesh, settings)` 把该开关传给 `WritePacked`。显式四参数仍可覆盖。
- 旧资产 `vertexStride` 0 或 32 仍走 32 字节 GPU 缓冲。勾选后 `vertexStride=24`，Lit 用 `_RestVertexTight` + `_VerticesTight`。
