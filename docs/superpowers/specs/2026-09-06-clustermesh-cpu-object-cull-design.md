# ClusterMesh 出包 CPU 物体剔 + 方向光阴影 list

Date: 2026-09-06  
Status: Implemented  
Module: `Assets/ClusterMesh`  
Parent: `docs/superpowers/specs/2026-09-06-clustermesh-cull-lookup-design.md`  
Related: `docs/superpowers/specs/2026-09-05-clustermesh-object-batching-design.md`

出包后按物体丢掉「既不进画面、也不能把方向光影子投进画面」的实例，减小 `物体 × cluster` Dispatch。主画不因留下投影体而变慢。选层、合批键、header 96、组 48、64/124、四条 vertex stream、O(1) 所在组、资产 AABB 一律沿用。

这一期就是正常优化的 **复杂度降档 + 主视图/阴影分 list + 主线程先写对**。不 Job/Burst，不复刻 Nanite。怎样算做对了见第 3 节。

## 1. Purpose

同资产很多时，身后和远处的实例仍进矩阵上传和全量 compute。第 3 项在进 chunk 前做 CPU 物体剔。方向光下身后的楼可以投影到身前，只测主相机视锥会错影。一份 `visible` 又给主画又给阴影，屏外 caster 会进主画再 clip，主画变慢。

## 2. Goals and non-goals

### Goals

- CPU：资产世界盒对「主相机视锥 ∪ 方向光挤出棱柱」决定是否进 chunk。
- 脚本 / Inspector：`enableCpuObjectCull` 可关，关则该物体一定留下。
- GPU：一次 Dispatch，两份 append。主画只视锥；阴影走视锥 ∪ 棱柱，不做相机 Cone。
- 主画 `ShadowCastingMode.Off`；阴影 `ShadowsOnly`。`worldBounds` 包全部留下的物体。
- 没有可用主方向光时不猜点光体积：CPU 不剔，不拆阴影 list，行为不低于现在。
- 单测覆盖身后留/丢、开关、fail-safe。不用重 Bake。

### Non-goals（为什么不做）

| 不做 | 原因 |
| --- | --- |
| 按 cascade 精剔 | 要复刻 URP 级联球/矩阵，一变就漏远处级 caster |
| 附加灯 / 点光体积 | 和方向光棱柱不是一套；猜错漏灯边影。无方向光走 fail-safe |
| 修画面内 Cone 减影 | 原有问题。这次只保证屏外 caster 不吃相机 Cone |
| 第 4 项按层减 dispatch | 和阴影体积正交；混一期分不清选层错还是阴影错 |
| Job + Burst | 每物体一次盒+平面，N 在几百时调度往往比 for 贵 |
| Nanite VSM / 多阴影视图 | 另一条管线，不是这一期能捎的 |
| 两次 Dispatch | 一次 kernel 写两份 buffer 足够；再 Dispatch 只加倍 cull |
| 全局 static 总闸 | 脚本关单个 renderer 即可；总闸以后再加 |

Nanite 上面多数都做。我们不做是原型停在「一份 compute + URP 级联」，不是 Nanite 也省了。

## 3. 怎样算做对了

正常优化：先按每帧实际花钱从大到小砍。1、2 已把所在组改 O(1)、AABB 改资产盒。这一期只做下一档：**物体先剔再 Dispatch，阴影不绑在主画 list 上**。主线程先写对、零分配；Job/Burst 要等几千实例且 profiler 打在 `KeepObject` 上。

**做对（必须同时满足）：**

1. 同资产很多、大半既不在视锥也不在方向光棱柱里：进 chunk 的物体数、Dispatch 的 `物体 × cluster`、主画实例，按 **留下的** 算，不按注册总数。验收画像：200 个同一资产，镜头里约 20 个、棱柱里再多一些，Dispatch / 主画按「20 + 棱柱内」降，不是按 200。
2. 身后能把方向光影子投进画面的人：CPU 留下；进 **阴影 list**，不进主画 list。主画顶点不因他们上涨。
3. 身后、光从身前打来、影子进不了画面：CPU 丢掉（开着剔时）。
4. `enableCpuObjectCull == false` 的物体一定留下。
5. 无合格 `RenderSettings.sun`：整批不剔、不拆 list，和改前相同（不猜点光）。
6. `CountDrawCalls(10,1)==1` 仍是主画。拆 list 时另加 ShadowsOnly，不把主画合成 10 次。
7. Demo / 10 个全在画面里：**几乎持平**。那种场景本来就不该当优化成功标准。

**实现上算做错（即使单测绿）：**

- 每帧 `new Plane[]` / 装箱，或 `FindObjects` 扫灯，或改 `camera.farClipPlane`。
- 一份 `visible` 又画颜色又画影（屏外 caster clip 进主画）。
- 只按主相机丢物体（方向光错影）。
- 因「主线程风险」改成 Job/Burst 或放宽棱柱（那不是这一期的对）。

主线程风险在合批 O(N²) 和求逆，不在盒 × 平面。第 3 项对被丢掉的物体不再求逆、不再 Dispatch，主线程是净少。

## 4. Locked decisions

| Topic | Decision |
| --- | --- |
| 开关 | `ClusterMeshRenderer.enableCpuObjectCull`，默认 `true`。脚本可写。Inspector 普通勾选。无全局 static |
| 按物体 | `false`：该物体一定进 chunk。`true`：视锥或棱柱才进。合批逐个看，不整批一个开关 |
| Fail-safe | 批内 `castShadows`（见下）且没有可用主方向光：CPU **整批不剔**（开关无效），GPU **不**开第二份 list，主画 `ShadowCastingMode.On`（与现在相同） |
| 关阴影 | 批内无人投射：CPU 只测视锥（仍尊重每物体开关）；GPU 只有主画，`ShadowCastingMode.Off` |
| 主方向光 | 只认 `RenderSettings.sun`：存在、enabled、Directional、`shadows != Off`。否则 fail-safe。不每帧 `FindObjects` 扫灯（避免 CPU 比剔盒还贵）。场景需把主光赋给 Sun |
| `shadowDistance` | `min(camera.far, URP.asset.shadowDistance)`。URP asset 空则 `camera.far`。接收体远平面用这个，不改 Camera 组件 |
| 接收体 | 与主相机同 FOV/近平面/位姿，远 = `shadowDistance` 的 6 个向内平面 |
| 挤出 | 方向 `E = -light.transform.forward`（朝光源）。保留 `dot(n, E) >= 0` 的平面，其余丢掉。体积只变大。`\|E\|` 过小则视为无挤出、只用接收体 |
| 平面槽 | CPU/GPU 都是 6 槽。没用的槽填「永不剔」平面（法线 0，距离极大） |
| 盒 | `TransformLocalBounds(缓存的 AssetLocalBounds, l2w)`。`TestAabbWorld` 测视锥和棱柱 |
| GPU 主画 | `TestAabb(_Planes)`（现有全相机远平面）+ 现有 Cone 规则 + LOD → `_VisibleClusterIds` |
| GPU 阴影 | `_EnableShadowList != 0` 且 `TestAabb(_ShadowPlanes)` + LOD，**不做 Cone** → `_ShadowClusterIds` |
| 两份 list | 同一 kernel。在两份里都合格的 cluster 两份都 append |
| Draw | 主画绑 color buffer，`ShadowsOff`（拆 list 时）。阴影绑 shadow buffer，`ShadowsOnly`，`receiveShadows=false`。shader 已有 `ShadowCaster` |
| `worldBounds` | 压缩后所有留下物体的世界盒并。必须含只为投影留下的，否则 Unity 可能整 draw 不进 cascade |
| 合批投射 | `batchCastShadows =` 批内任一 `castShadows`。避免 seed 关影、别人开着，整批按无影剔 |
| 合批接收 | 仍用 seed 的 `receiveShadows`（主画） |
| DrawCall | 颜色 Indirect 计数不变：10 单材质 = **1** 次主画。拆 list 时另加 **1** 次 ShadowsOnly / 材质 / chunk |
| CPU | 主线程 for。不 Job/Burst。不因「主线程风险」改算法 |
| 每帧 | 平面/矩阵复用字段，禁止 `new Plane[]`。只认 Sun，禁止 `FindObjects` 扫灯。禁止改 Camera 远平面 |
| 空批 | 压缩后 `n==0`：不 Dispatch、不 Draw |
| Isolate | 两份 list 都仍按 `_IsolateIndex` |

## 5. API

`ClusterMeshRenderer.enableCpuObjectCull`：`bool`，默认 `true`。Tooltip：出包 CPU 物体剔；关掉则该物体一定提交。

`ClusterMeshObjectCull`（纯函数，供 Draw / 单测）：

- `float ShadowDistance(Camera camera)`
- `bool TryGetMainDirectionalShadowLight(out Light light)` — 只检查 `RenderSettings.sun`
- `void CameraPlanes(Camera camera, Plane[] dest6)` — 现有 `WorldPlanes` 即可
- `void BuildReceiverFrustumPlanes(Camera camera, float shadowDistance, Plane[] dest6)`
- `void ExtrudePlanesToward(Plane[] src6, Vector3 direction, Plane[] dest6)` — 丢掉的槽填永不剔
- `bool TestAabbWorld(...)` — 已有
- `bool KeepObject(Bounds world, Plane[] camera6, bool enableCpuCull, bool testShadowVolume, Plane[] shadow6)`  
  `!enableCpuCull` → true；否则视锥或（`testShadowVolume` 且棱柱）

`ClusterMeshDrawContext.Draw` 增加与矩阵等长的 `IList<bool> enableCpuObjectCull`（或并行数组）。Viewer 单物体传 `true` 或 renderer 字段。

`CountDrawCalls(objectCount, materialCount)` **仍是主画次数**。  
`CountIndirectDraws(objectCount, materialCount, bool splitShadows)` = 主画 × (`splitShadows` ? 2 : 1)。

## 6. Batcher / Draw

`Flush` 收集矩阵时同步收集每物体 `enableCpuObjectCull`，`batchCastShadows` 取或。

`Draw`：

1. 算相机 6 平面（现有，给 GPU `_Planes` 和 CPU 视锥）。
2. `splitShadows = batchCastShadows && TryGetMainDirectionalShadowLight`。
3. 若 `splitShadows`：`BuildReceiverFrustumPlanes` + `ExtrudePlanesToward(-light.forward)` → `_ShadowPlanes`。
4. 对每个物体：`KeepObject`；fail-safe（`batchCastShadows && !splitShadows`）则全留。留下的写入 `_l2w/_w2l`，Encapsulate `worldBounds`。
5. `n==0` 返回。
6. Dispatch 一次（两份 Append 都绑；`_EnableShadowList = splitShadows ? 1 : 0`）。`_ShadowClusterIds` 在未拆 list 时仍要绑占位，避免缺 bound。
7. 每材质：CopyCount 主画；`splitShadows` 再 CopyCount 阴影。拆 list：主画 `ShadowsOff` + `receiveShadows`（seed）；阴影 `ShadowsOnly`、不接收。未拆：一次 Draw，`batchCastShadows ? On : Off`（与现在相同）。

## 7. Compute

新增 `AppendStructuredBuffer<uint> _ShadowClusterIds`、`float4 _ShadowPlanes[6]`、`int _EnableShadowList`。

`CullClusters`：材质 / isolate / LOD 与现在相同。Cone **仅**主画且 `inCamera`。  
`inCamera` 否且未拆阴影 → 与现在一样直接 return。

这不是第 5 条 vertex stream。

## 8. 测试

新 `ClusterMeshCpuCullTests`（几何用手工平面/灯，不依赖场景光照）：

- 相机看 +Z。身后物体、光 `forward=+Z`（从后打来）→ `KeepObject` true。
- 同上、光 `forward=-Z`（从前打来）→ false（开着 CPU 剔、测棱柱）。
- 视锥内 → 恒 true。
- `enableCpuCull=false`、身后 → true。
- `testShadowVolume=false`、身后 → false。
- `ExtrudePlanesToward`：`E=-Z` 时近平面被替换成永不剔；身后点对挤出平面为内，对原视锥为外。
- `ShadowDistance`：有 URP asset 时 ≤ `asset.shadowDistance` 且 ≤ `camera.far`。

合批 / DrawCall：

- `CountDrawCalls(10,1)==1` 不变。
- `CountIndirectDraws(10,1,true)==2`，`(10,1,false)==1`。
- `enableCpuObjectCull` 默认 true。
- 压缩计数：同一批 1 个在视锥、3 个在身后且光从身前打来 → `KeepObject` 为 true 的只有 1 个（对应「200 里镜头 20」的缩小版）。

不在 EditMode 里断言 GPU 阴影图像素。阴影正确性由平面用例锁死。不把 Demo 10 物体全在画面当作性能通过标准。

## 9. 验证

Tuanjie `2022.3.48t2`，`-batchmode -nographics`，`ClusterMesh.Editor.Tests`。编辑器占用则不编造成功。不用重 Bake。

出包是否做对：用「同资产约 200、镜头里约 20」看 Dispatch / 主画是否按留下的降。Demo 全在画面里持平才正常。身后投进画面的人只涨阴影 list，不涨主画。
