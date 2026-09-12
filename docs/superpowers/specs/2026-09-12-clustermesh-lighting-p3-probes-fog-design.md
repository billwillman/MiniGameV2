# ClusterMesh 照明 P3：Light Probe + 雾

Date: 2026-09-12  
Status: Implemented（EditMode 被编辑器占用，batchmode 未跑）  
Module: `Assets/ClusterMesh`  
Parent: `docs/superpowers/specs/2026-09-07-clustermesh-urp-feature-design.md`  
Related: `docs/superpowers/todos/2026-09-06-clustermesh-followups.md` D. P3

R1 只换提交时机。`ClusterMesh/Lit` 与 `SkinnedLit` 仍 `bakedGI = SampleSH(normal)`（全局 `unity_SH*`，合批共享一份环境）且 `fogCoord = 0`。旁边 `MeshRenderer` 吃 Light Probe 四面体插值、吃 URP 雾。室内 ClusterMesh 偏黑、Forward 没有雾。本期只对齐 **探针 SH + Forward 雾**。

## 1. Purpose

每个合批槽按物体原点采当前 Light Probe（没有四面体则退回 `RenderSettings.ambientProbe`），shader 用该物体的 SH 算 `bakedGI`。Forward 把 `fogCoord` 交给 `UniversalFragmentPBR`。Deferred 雾由 URP 延迟光照用深度做，GBuffer 不往 albedo 里混雾。

## 2. Locked

| 项 | 决定 |
| --- | --- |
| 采样点 | 物体 `localToWorld` 平移（原点）。不做 `probeAnchor`、不按顶点 |
| API | `LightProbes.GetInterpolatedProbe(worldPos, null, out SphericalHarmonicsL2)` |
| 无探针 | 与 Unity 行为一致：得到环境 SH（等价 `ambientProbe`）。禁止写 0 把室内打黑 |
| 打包 | 打成 URP `unity_SHAr`…`unity_SHC` 那 7 个 `float4`，函数放 `ClusterMeshLightProbes`，单测对合成 SH |
| 上传 | `StructuredBuffer<ClusterMeshObjectSH>`（stride 112，7×float4）。**禁止**再往 `ClusterMeshBatch` CBUFFER 塞 7 个 `[256]` 数组（和矩阵一起会顶 D3D11 64KB） |
| 谁绑 | 静态 / 蒙皮 `DrawContext` 在 `Prepare` 进槽时写入 CPU 数组，`BindDrawMaterial` 时 `SetBuffer` |
| 采样 | `bakedGI = SampleSH9(objectSH.Ar…C, normalWS)`，不再 `SampleSH(normalWS)` |
| 雾 | 仅 Forward：`#pragma multi_compile_fog`，`fogCoord = ComputeFogFactor(positionCS.z)`。GBuffer / Shadow / Depth / MV **不加雾** |
| TuanjieGI | GBuffer 里 `#if !UNITY_TUANJIEGI` 写 indirect 的闸不变；闸打开时 `bakedGI` 仍来自物体探针 |
| 两边 shader | `ClusterMeshLit.hlsl` 与 `ClusterSkinnedMeshLit.hlsl` 同一套采样 |

`ClusterMeshObjectSH`（C# / HLSL 同序）：

```text
float4 shAr, shAg, shAb, shBr, shBg, shBb, shC;
```

## 3. Non-goals

| 不做 | 原因 |
| --- | --- |
| Lightmap / UV2 | 32-byte pack 没有 UV2；另期 |
| DepthNormals Pass | 和已撤的 Depth Prepass 搅在一起；另期 |
| APV / Probe Volume 3D 纹理 | 2022.3 这台机不是 APV 工作流 |
| 矩阵改 GraphicsBuffer | followups B，等 Profiler |
| 改 Radiant / 加 PRT | 邻居；探针让 SSGI 环境回退不那么假 |
| 点光体积、RSM 看见 ClusterMesh | 不是 shader 对齐 |

## 4. 数据流

```text
Prepare 进槽
    worldPos = l2w.GetColumn(3).xyz
    sh = ClusterMeshLightProbes.Evaluate(worldPos)
    ClusterMeshLightProbes.Pack(sh, out objectSH[n])
Bind
    _ObjectSH.SetData(objectSH, 0, 0, n)
    mat.SetBuffer(_ObjectSH)
Frag
    objectIndex = packed >> 16
    bakedGI = SampleSH9(_ObjectSH[objectIndex], normalWS)
```

蒙皮：同一原点规则（renderer 变换，不是骨骼根的另一套）。

## 5. 怎样算做对了

1. `ClusterMeshLightProbes.Pack`：合成只有 L0 的 SH，打包后 `SampleSH9(+Y)` 与手算 L0 一致（允许 half 误差）
2. 场景无 `LightProbeGroup`：`Evaluate(任意点)` 的系数等于 `RenderSettings.ambientProbe`
3. Forward / GBuffer 源文件不再把 `bakedGI` 赋成 `SampleSH(`；出现 `SampleSH9` 与 `_ObjectSH`
4. Forward shader 有 `#pragma multi_compile_fog`；`ClusterMeshFrag` 用 `ComputeFogFactor`，不再写 `fogCoord = 0`
5. GBuffer / MV / Shadow 源文件没有 `multi_compile_fog` / `MixFog`
6. 现有 URP pass 索引、MV 默认关、64/124 单测仍绿
7. **手工**：室内 Light Probe 旁，ClusterMesh 与旁边 MeshRenderer 亮度同量级；Forward 开雾时角色进雾。Deferred 雾跟 MeshRenderer 走深度，不要求 GBuffer 里看见雾

做错：CBUFFER 再堆 7×256；lightmap 关键字；把雾写进 GBuffer albedo；改合批键。

## 6. 文件

| 文件 | 变化 |
| --- | --- |
| `Runtime/ClusterMeshLightProbes.cs`（新） | Evaluate + Pack |
| `Runtime/ClusterMeshTypes.cs` 或同文件 struct | `ClusterMeshObjectSH` 112 字节 |
| `Runtime/ClusterMeshDrawContext.cs` | 进槽 Evaluate、buffer 上传 |
| `Runtime/ClusterSkinnedMeshDrawContext.cs` | 同上 |
| `Shaders/ClusterMeshLit.hlsl` / `ClusterSkinnedMeshLit.hlsl` | `_ObjectSH` + SampleSH9 + fogCoord |
| `Shaders/ClusterMeshLit.shader` / `ClusterSkinnedMeshLit.shader` | Forward `multi_compile_fog` |
| `Tests/Editor/ClusterMeshLightProbesTests.cs`（新） | Pack / 无探针回退 |
| `Tests/Editor` 源文件断言 | bakedGI / fog 关键字 |
| 64/124、header、Baker、URP 资产 | **不改** |
