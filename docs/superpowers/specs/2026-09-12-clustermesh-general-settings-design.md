# ClusterMesh 通用设置窗口

Date: 2026-09-12  
Status: Approved for implementation  
Module: `Assets/ClusterMesh`  
Parent: `docs/superpowers/specs/2026-09-12-clustermesh-motion-vectors-timing-design.md`

管线级选项（Motion Vector Pass 挂点等）集中到一份项目资产，菜单 `Tools/ClusterMesh/通用设置` 编辑。不写在 `ClusterMeshRenderer` / `ClusterSkinnedMeshRenderer` 上。Feature 只读这份设置。

## 1. Purpose

避免每个物体脚本各写一套管线时机。Game 相机 Feature 入队 MV 时读项目设置。缺资产时回退 `AfterSkyboxPlus1`。

## 2. Locked

| 项 | 决定 |
| --- | --- |
| 资产 | `Assets/ClusterMesh/Resources/ClusterMeshSettings.asset`，运行时 `Resources.Load` |
| 窗口 | `Tools/ClusterMesh/通用设置`，中文。没有资产则创建 |
| 本期字段 | `motionVectorSlot`，默认 `AfterSkyboxPlus1` |
| Feature | 删除自身 `motionVectorSlot`。入队用 `ClusterMeshSettings.CurrentMotionVectorSlot` |
| 单物体 | `enableMotionVectors` 仍在各 Renderer：只表示「这个物体写不写 MV」，不表示 Pass 挂点 |
| 测试 | `OverrideForTests`；TearDown 清掉。缺资产时 Current 仍是默认槽 |

## 3. Non-goals

- 不把 `enableMotionVectors` / LOD / Cone 收到窗口当全局覆盖
- 不改 64/124、合批键、Radiant、玩法场景
- 不加 CPU 剔总闸（仍是 followups）

## 4. 怎样算做对了

1. 新 Feature 实例没有 `MotionVectorSlot` 字段；源文件读 `CurrentMotionVectorSlot` / `CurrentMotionVectorPassEvent`
2. `CurrentMotionVectorSlot` 默认 `AfterSkyboxPlus1`；Override 能改解析结果
3. 菜单字符串存在 `Tools/ClusterMesh/通用设置`
4. 现有 MV 解析四槽、pass 索引、默认关物体 MV 仍绿
