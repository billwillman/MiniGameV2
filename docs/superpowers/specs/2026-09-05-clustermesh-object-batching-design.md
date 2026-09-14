# ClusterMesh 跨物体合批设计

Date: 2026-09-05  
Status: Locked for implementation  
Module: `Assets/ClusterMesh`  
Parent spec: `docs/superpowers/specs/2026-09-04-clustermesh-design.md`

本文件只覆盖「多个相同 ClusterMesh 物体合成每材质 1 次 DrawCall」。Baker、Viewer、cluster 容量、capability 门、不接 URP Renderer Feature，一律沿用父规格。

## 1. Purpose

第一期每个 `ClusterMeshRenderer` 自建 `ClusterMeshDrawContext` 并在 `LateUpdate` 各提一次 `DrawMeshInstancedIndirect`。10 个同一份资产、单材质的物体 = **10 DrawCall**。

这一期把场景里**同一份 `ClusterMeshAsset` + 同一台 Camera** 的 renderer 收成一条 Indirect：`instanceCount` = 各物体可见 cluster 之和。10 个同样的单材质物体 = **1 次主画**。2 材质仍是 **2 次主画**。方向光拆阴影 list 时另加 ShadowsOnly，见 `2026-09-06-clustermesh-cpu-object-cull-design.md`。

## 2. Goals and non-goals

### Goals

- 场景 `ClusterMeshRenderer` 不再自己 `Draw`；由场景 Batcher 每帧 flush 一次。
- GPU 几何（cluster / vertex / index）按资产共享，不按物体复制。
- 运行时材质按资产共享一份（不再每物体 `new Material`）。
- 可见项编码为 `(objectIndex, clusterId)`，一次 Indirect 画完该组。
- Demo 菜单生成 **10** 个共用同一资产的物体，便于 Frame Debugger 核对。
- Viewer / Inspector 仍走私有 `ClusterMeshDrawContext`（单物体、可 isolate），不合进场景批。

### Non-goals

- 每物体覆盖材质属性（颜色、贴图）。合批键不看 renderer 上的材质覆写；没有这个字段。
- 不同 `ClusterMeshAsset` 实例之间合批（引用相等，两份拷贝的 ScriptableObject 是两批）。
- URP Renderer Feature、Hi-Z、LOD、蒙皮、GLES fallback。
- 用第 5 个 vertex-stage `StructuredBuffer` 传物体矩阵（会把 `maxComputeBufferInputsVertex < 4` 的门抬到 5，D3D11 常见上限是 4）。
- 跨 Camera 合批。`targetCamera` 不同（含 `null`→`Camera.main`）就是不同批。
- 自动统计 Frame Debugger；EditMode 用可预测的 draw 次数公式断言。

## 3. Locked decisions

| Topic | Decision |
| --- | --- |
| 合批键 | `(ClusterMeshAsset 引用, Camera)` |
| 所有权 | 静态 `ClusterMeshSceneBatcher`，不要求场景里挂额外组件 |
| 几何 | 每资产一个 `ClusterMeshDrawContext`，Batcher 引用计数持有 |
| 矩阵 | 每帧上传 `localToWorld` / `worldToLocal`。Cull 与 VS 都走 **CBUFFER 数组**，不增加 vertex StructuredBuffer |
| 容量 | `ClusterMeshLimits.MaxBatchedObjects = 256`。超出按 256 切块，每块每材质再 1 次 Draw |
| 可见 ID | `uint packed = (objectIndex << 16) \| clusterIndex`。物体与 cluster 各 16 bit |
| Cull | 世界空间 AABB + 世界空间 cone；一次 Dispatch，线程数 = `objectCount * clusterCount` |
| 视锥平面 | `GeometryUtility.CalculateFrustumPlanes` 的世界平面，不再先变到物体局部 |
| Draw API | 仍是 `Graphics.DrawMeshInstancedIndirect`，每批每材质 1 次 |
| Isolate | 仅 Viewer 私有 context；场景批 `IsolateIndex = -1` |
| Edit Mode | `ClusterMeshRenderer` 仍 `[ExecuteAlways]`；Batcher 用 `Time.frameCount` 每帧 flush 一次 |
| Demo | `CreateDemoObjects` 生成 10 个子物体，同一 `ClusterMeshAsset`，沿 X 错开 |

成功标准（单材质、同一资产、同一 Camera、物体数 ≤ 256）：

```text
DrawCall = 1
```

多材质：`DrawCall = materialCount`。物体数 `N > 256`：`DrawCall = materialCount * ceil(N / 256)`。

## 4. Architecture

```text
ClusterMeshRenderer (N)
        | Register / Unregister / LateUpdate -> Flush
        v
ClusterMeshSceneBatcher
        | group by (asset, camera)
        | cache DrawContext per asset
        v
ClusterMeshDrawContext.Draw(matrices[], camera)
        | cull: world planes + object CBUFFER
        | append packed (object, cluster)
        v
DrawMeshInstancedIndirect  × materialCount × chunks
```

`ClusterMeshRenderer` 只负责：解析 shader、在启用且资产非空时注册、每帧请求 flush。不持有 context，不调用 `Draw`。

Viewer 继续 `new ClusterMeshDrawContext` + `Draw(singleMatrix, previewCamera)`。内部走同一条 `Draw(IList<Matrix4x4>, Camera)`，`objectCount = 1`。

## 5. Data and packing

`ClusterMeshLimits` 增加：

- `MaxBatchedObjects = 256`
- `PackVisibleId(int objectIndex, int clusterIndex) -> uint`
- `UnpackVisibleId(uint packed, out int objectIndex, out int clusterIndex)`

范围：`0 <= objectIndex < 256`（切块后的块内下标），`0 <= clusterIndex <= 65535`。

Append buffer 容量：`clusters.Length * MaxBatchedObjects`（每材质一条，与第一期「每材质独立 Append」相同，避免多材质互相覆盖）。

HLSL / C# 的 `ClusterHeader`、`ClusterVertex` 布局不变。

VS / `ClusterMeshSetup`：

```text
packed      = _VisibleClusterIds[unity_InstanceID]
objectIndex = packed >> 16
clusterId   = packed & 0xFFFF
unity_ObjectToWorld = _ObjectLocalToWorld[objectIndex]
unity_WorldToObject = _ObjectWorldToLocal[objectIndex]
header      = _Clusters[clusterId]
```

顶点 StructuredBuffer 仍是 4 条：`_Clusters` `_Vertices` `_Indices` `_VisibleClusterIds`。capability 门不改。

## 6. Cull (world space)

Kernel `CullClusters` 改为：

```text
i = SV_DispatchThreadID.x
if i >= _ObjectCount * _ClusterCount: return
objectIndex  = i / _ClusterCount
clusterIndex = i % _ClusterCount
header       = _Clusters[clusterIndex]
if header.materialIndex != _MaterialIndex: return
if isolate >= 0 and clusterIndex != isolate: return
world AABB   = transform(header.aabb, _ObjectLocalToWorld[objectIndex])
world cone   = transform(header.cone, _ObjectLocalToWorld[objectIndex])
if !TestAabb(world AABB, _Planes[6 world]) or !TestCone(world, _WorldCameraPos): return
Append(Pack(objectIndex, clusterIndex))
```

世界 AABB（保守，局部 AABB 经线性变换）：

```text
worldCenter  = mul(M, float4(localCenter, 1)).xyz
worldExtents = abs(M.c0.xyz)*e.x + abs(M.c1.xyz)*e.y + abs(M.c2.xyz)*e.z
```

世界 cone：apex 用点变换，axis 用 3×3 后 normalize。`cutoff < 0` 仍禁用 cone。非均匀缩放下 cone 略有误差，本 prototye 接受。

`ClusterMeshFrustum` 增加与 HLSL 同公式的 C#：`TransformAabb`、`WorldPlanes`。原有局部 `CameraToLocalPlanes` / `TestAabb` / `TestCone` 保留给已有单测；场景路径不再调用局部平面。

## 7. Batcher

`ClusterMeshSceneBatcher`（静态）：

- `Register(ClusterMeshRenderer)` / `Unregister(ClusterMeshRenderer)`：幂等。资产为空或未启用则当作未注册。
- `Flush()`：若 `Time.frameCount` 已 flush 过则返回。按 `(asset, camera)` 分组，收集 `localToWorld`，对每组 `context.Draw(matrices, camera)`。
- 每资产 context：引用计数。最后一个 renderer 注销时 `Dispose`。
- Capability 失败：整帧不 Draw，错误字符串只 log 一次（与第一期 renderer 行为一致）。
- `ResetForTests()`：清注册表、释放 cache、重置 frame 标记。测试 TearDown 必调。
- `CountDrawCalls(int objectCount, int materialCount)`：`objectCount <= 0` → 0；否则 `materialCount * ceil(objectCount / 256)`。
- `CollectBatches(...)`：纯 CPU 分组，供测试断言批次数、每批物体数、draw 次数，不碰 GPU。

Camera 解析与现在相同：`renderer.targetCamera != null ? targetCamera : Camera.main`。Camera 为 null 的 renderer 本帧不进入任何批。

## 8. Renderer

`[ExecuteAlways]` 保留。

- `OnEnable` / 资产变为有效：`Register`
- `OnDisable` / 资产被清空：`Unregister` 并丢掉本地错误标记
- `LateUpdate`：确保已注册，然后 `Flush`
- `OnValidate`：先 Unregister，再按当前资产 Register（编辑器改引用立即换批）
- 不再 `new ClusterMeshDrawContext`，不再 `Draw`

空资产仍然合法：不注册、不 Error。

## 9. Demo

`ClusterMeshDemoSceneMenu.InstanceCount = 10`。

`CreateDemoObjects`：Bake 一次 cube，生成父物体，10 个子物体各挂 `ClusterMeshRenderer`，共用同一 runtime `ClusterMeshAsset`，位置 `x = (i - 4.5) * 1.5`。`CreateDemoScene` 把该资产 Persist 成 `DemoCube.asset` 后赋给全部 10 个 renderer。

## 10. Testing

EditMode，装配体仍是 `ClusterMesh.Editor.Tests`。新增：

- Pack / Unpack 往返；`objectIndex=10, clusterIndex=3` 的位模式。
- `CountDrawCalls(10, 1) == 1`，`(10, 2) == 2`，`(256, 1) == 1`，`(257, 1) == 2`，`(0, 1) == 0`。
- 10 个 renderer、同一资产、同一 Camera → `CollectBatches` 一批，`objectCount=10`，`drawCallCount=1`。
- 两个不同资产 → 两批。
- 同一资产、两台 Camera → 两批。
- Unregister 后该物体不再出现在批里。
- `TransformAabb`：单位矩阵下 world == local；纯平移下 center 平移、extents 不变。
- Demo：`CreateDemoObjects` 得到 10 个 renderer，且 `asset` 引用相同。
- 既有 24 个用例继续绿。空资产 OnEnable 仍不 throw、不 Error。Viewer 单测仍用私有 context。

`-nographics` 下 capability 仍可能失败：场景 Flush 若 log Error，Demo 测试继续 `LogAssert.Expect`。

## 11. Error handling

- 空资产 / 未注册：跳过，不每帧打日志。
- Capability / 缺 shader：Batcher 或 context 创建失败时 log 一次 `ClusterMesh: <reason>`，不 Draw。
- `Dispose` 仍可调两次。
- 切块时块内 `objectIndex` 从 0 重计，与 CBUFFER 下标一致。
- 静态 Batcher 在域重载后由 Unity 清静态字段；`ResetForTests` 覆盖 EditMode 连续用例。

## 12. Closed alternatives

- **每物体仍 Draw，靠 SRP Batcher / GPU Instancing**：运行时材质是拷贝，且 InstancedIndirect 不走那条合批。否决。
- **物体矩阵 StructuredBuffer 进 VS**：第 5 个 vertex compute buffer，抬高 capability 门。否决。
- **URP Renderer Feature 里合批**：父规格禁止改 URP renderer 资产。否决。
- **按源 Mesh 合批、忽略资产实例**：两份独立 bake 的 buffer 不能共用。否决。
