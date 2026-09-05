# ClusterMesh 几何打包 + 存盘压缩

Date: 2026-09-06  
Status: Implemented  
Module: `Assets/ClusterMesh`  
Parent: `docs/superpowers/specs/2026-09-06-clustermesh-lod-dag-design.md`

缩小 `.asset` 与运行时顶点/索引缓冲。不改 2026-09-04 规格正文。合批、Indirect、template 372、Cone、分色、阴影、LOD DAG 选层一律沿用。

**不兼容旧资产。** 无双路径。现有 float32 + `uint` 的 `Mia.asset` 等必须重 Bake。

## 1. Purpose

`Mia.asset` 约 3.4 MB，因为每个顶点 64 字节、索引 32 位、整棵 LOD 树都按解压数组落盘。这一期：运行时用瘦顶点 / 16 位索引，落盘再 Deflate。加载解压后按瘦格式上传，shader 还原成 float。

这一期是**编码瘦身**，不是拓扑瘦身。cluster / 父子层仍不共享顶点。体积会明显下降，但不保证小于源 FBX，也不写死「一半」。

## 2. Goals and non-goals

### Goals

- GPU 顶点 stride **32**。位置保持 float。法线 / 切线 / UV 用 IEEE half。
- 切线 z：假定 T ⊥ N 重建；`n.z ≈ 0` 时用 `position.w` 里的符号。
- 索引按 16 位存储；GPU 用 `StructuredBuffer<uint>`，每 uint 打包 2 个 `ushort`。
- 资产里顶点、索引以 Deflate 字节存放。Header / Group 不压。
- **`ClusterMeshBakeResult` 仍是解压工作集**（`ClusterVertex[]` + `uint[]`）。打包 + Deflate 只发生在 `CopyFrom` / 加载。
- 加载一次解压，Draw 路径仍是 4 条 buffer（Clusters / Vertices / Indices / Visible）。
- 单测：打包往返、stride 32、`geometryVersion`、关层次 Bake 仍 hierarchy 0、开层次叶子三角和等于源。
- 默认阈值 0 画面应与重 Bake 后的新格式一致（half 误差范围内）。

### Non-goals

- 兼容旧 float32 资产。
- 跨 cluster 共用一份顶点。
- 量化位置、meshoptimizer、LZ4 新依赖、流式分页。
- 改 64/124、header 96、Hi-Z、URP Feature。
- octa / 10_10_10 法线、`ByteAddressBuffer` 索引。
- 关层次（已有 Baker 开关）。本文件不管层数，只管几何编码。

## 3. Locked decisions

| Topic | Decision |
| --- | --- |
| 旧资产 | 不读。`geometryVersion != 1` 或解压失败 → DrawContext 报错，提示重 Bake |
| 位置 | `float`（`float4.xyz`）。`w` = `sign(tangent.z)`，给 `n.z ≈ 0` 用 |
| 法线 / 切线 xy / 切线 w / UV | IEEE half。切线 w 仍是 ±1 |
| 切线 z | T ⊥ N 重建。`|n.z| >= 1e-4`：`t.z = -(n.x*t.x + n.y*t.y)/n.z`。否则：`t.z = position.w * sqrt(max(0, 1-t.x²-t.y²))`。再 Gram-Schmidt 正交化 |
| 顶点 stride | GPU **32**。C# `ClusterPackedVertex` 与 HLSL `ClusterVertex` 字段一致 |
| Baker 工作顶点 | 仍是 `ClusterVertex`（4×float4）。`BakeResult` 不改成 `byte[]` |
| 索引 | 逻辑 `ushort`（cluster 内 < 64）。GPU：两个 ushort 打进一个 `uint`，奇数补 0。`indexOffset` 仍是逻辑下标 |
| 压缩 | `System.IO.Compression.DeflateStream`。只压顶点块和索引块。**只减磁盘，不减显存** |
| 压缩时机 | `CopyFrom` 时压。DrawContext 构造时解压到托管数组再 `SetData` |
| Header / Group | 仍解压数组，96 / 48 字节 |
| LOD | 不改。`hierarchyVersion` 0/1/2 含义不变 |
| Baker 开关 | `buildLodHierarchy` 照旧。打包与是否建层次正交 |
| 格式号 | `geometryVersion = 1`。下一轮改编码再加 2，不靠「有没有 byte[]」猜 |
| UV | half。0–1 够用；高 tiling（约 >8）可能微动或接缝，本期接受 |
| 写出前 | 法线 normalize；切线对法线 Gram-Schmidt 后再打包 |

## 4. GPU 顶点布局（32 字节）

HLSL `float3` 后面会按 16 字节对齐，所以位置用 `float4`。

```
float4 position     // xyz = 物体空间位置，w = sign(tangent.z)
uint   nrmXY        // half2(normal.xy)
uint   nrmZ_tanW    // half2(normal.z, tangent.w)
uint   tanXY        // half2(tangent.xy)
uint   uv           // half2(uv)
```

C# `ClusterPackedVertex` 同样 5 个字段（`Vector4` + 4×`uint`）。`ClusterMeshLimits.ClusterVertexStride = 32`。`Marshal.SizeOf<ClusterPackedVertex>() == 32`。

打包：`Mathf.FloatToHalf` / `Mathf.HalfToFloat`。两个 half 打成 `uint`：低 16 位 x，高 16 位 y。

HLSL 拆 half 用 `f16tof32`。重建切线 z 的公式与 C# `ClusterMeshGeometry` 相同。

## 5. 索引

逻辑索引仍是每个三角 3 个局部下标。`BakeResult.indices` 仍是 `uint[]`（值 < 64）。

GPU：`count = (indexCount + 1) / 2` 个 `uint`。

```
uint packed = (uint)i0 | ((uint)i1 << 16);
```

Shader：

```
uint raw = _Indices[(h.indexOffset + vertexID) >> 1];
uint localIndex = ((h.indexOffset + vertexID) & 1u) == 0u
    ? (raw & 0xFFFFu) : (raw >> 16);
```

`indexOffset` 是**逻辑**下标（第几个 16 位），不是 uint 下标。相邻 cluster 可以挤在同一个 `uint` 里。

## 6. 资产

`ClusterMeshAsset`：

- 删除 `ClusterVertex[] vertices`、`uint[] indices`。
- 增加：`int geometryVersion`、`int vertexCount`、`int indexCount`、`byte[] packedVertices`、`byte[] packedIndices`。
- `CopyFrom` 从 `BakeResult` 的工作集打包 → Deflate → 写入两个 `byte[]`。`geometryVersion = 1`。
- `vertexCount` / `indexCount` 是逻辑个数。

`ClusterMeshBakeResult` **保持** `vertices` / `indices` 解压数组，供 Baker 和单测使用。

解压失败、`geometryVersion != 1`、或长度与 count 不符 → 资产损坏或未重 Bake，`IsReady = false`。

Inspector / Viewer 显示 `vertexCount` / `indexCount`。

Demo 持久化若需要再 `CopyFrom`，先把资产解压回工作集，或直接拷贝 packed 字段。

## 7. Draw / Shader

`ClusterMeshDrawContext`：校验 version → 解压 → `GraphicsBuffer` 顶点 stride 32；索引 stride 4、元素数为 `(indexCount+1)/2`。

`ClusterMeshBuffers.hlsl` 的 `ClusterVertex` 改成第 4 节。`ClusterMeshLit.hlsl` 的 `FetchClusterVertex`：拆 half，重建 `tan.z`，位置用 `.xyz`。

仍是一条顶点 StructuredBuffer。Visible / Clusters / Groups 不变。

## 8. 测试

- `Marshal.SizeOf<ClusterPackedVertex>() == 32`，`ClusterVertexStride == 32`。
- 打包往返：单位立方内位置误差 < 1e-4；法线点积 > 0.999；含 `n.z ≈ 0`（法线沿 X、切线沿 Z）。
- 索引打包：奇数个下标补 0，解出与源一致。
- Deflate 往返：随机字节块一致。
- `CopyFrom` 后 `geometryVersion == 1`，`packedVertices` 非空。
- Bake 开/关层次：`hierarchyVersion` 与叶子三角和断言与现在相同（读 `BakeResult`）。
- `WriteAsset` 关层次仍 `hierarchyVersion == 0`，且 `geometryVersion == 1`。
- 旧版资产（version 0 / 无 packed）DrawContext 不 Ready。
- 合批 10 物体阈值 0 仍 1 Indirect。

## 9. 验证

Tuanjie `2022.3.48t2`，`ClusterMesh.Editor.Tests`。编辑器打开时不跑。Mia 必须重 Bake 后才能加载。体积以实 Bake 为准，不写死 KB。

## 10. 明确不改

- octa / 10_10_10（收益小、测试重）。
- `ByteAddressBuffer`（少一次打包，但绑定都要改）。
- 共享顶点 / 分页。
