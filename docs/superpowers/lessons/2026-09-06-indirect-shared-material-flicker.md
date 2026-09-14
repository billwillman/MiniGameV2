# 经验：Indirect 共用 Material 会闪

Date: 2026-09-06  
Module: `Assets/ClusterMesh`  
Symptom: 拆主画 / ShadowsOnly 两份 list 后，画面闪  
Related spec: `docs/superpowers/specs/2026-09-06-clustermesh-cpu-object-cull-design.md`

## 现象

有合格 `RenderSettings.sun`、走「一次 compute、两份 append」时，网格每帧闪、破面或乱跳。关 Sun（走回单 list）则不闪。EditMode 单测仍绿。

## 根因

`Graphics.DrawMeshInstancedIndirect` **只登记、不立刻画**。URP 稍后按相机 / 阴影 pass 才读 Material。

错误写法（同一份 `Material`）：

1. `mat.SetBuffer(_VisibleClusterIds, 主画list)` → 提交主画（args 的 `instanceCount` = 主画个数）
2. `mat.SetBuffer(_VisibleClusterIds, 阴影list)` → 提交 ShadowsOnly
3. 真正光栅时，两份 draw 都读到 **最后绑的阴影 list**
4. 主画的 `instanceCount` 仍是主画个数，和阴影 buffer 长度不一致 → 越界或错 cluster → **闪**

不是 LOD 跳变，也不是 CPU 视锥在边界抖。是 **延迟绘制 + 可变 Material 状态**。

## 处理

主画和阴影各一份 Material 实例。提交主画后不要再改那份的 `_VisibleClusterIds`。

- 对：`_materials[i]` 绑主画 buffer；`_shadowMaterials[i]` 绑阴影 buffer。
- 错：一份 mat 画完主画再 `SetBuffer` 阴影。
- `MaterialPropertyBlock.SetBuffer` 在部分版本上对 procedural instancing 不可靠，本项目用双 Material。

回归：`Constructor_ValidBake_ShadowMaterialIsDistinctInstance`（两份 Material 不是同一引用）。

## 以后

只要同一帧对 **同一 Material** 提两次以上 `DrawMeshInstancedIndirect`，且两次要读不同 StructuredBuffer / 不同 args，就必须：

- 两份 Material，或
- 确认 MPB 对该 shader 的 buffer 绑是按 draw 生效的

**禁止**「先 Draw 再改同一份 Material 再 Draw」当成分开两次绘制。
