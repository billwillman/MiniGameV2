# Skinned Baker Clip Folder Scan Implementation Plan

> **For agentic workers:** Implement inline after this plan. Do not commit unless the user asks.

**Goal:** 蒙皮 Baker 可从 Project 目录递归追加 AnimationClip（散落资产 + Prefab 内部子对象），并保留逐条添加。

**Architecture:** 静态 `ClusterSkinnedAnimationClipFolderScan.Collect` / `AppendUnique`；窗口只做目录校验、中文提示和列表追加。

**Tech Stack:** Tuanjie 2022.3 Editor, `AssetDatabase.FindAssets` / `LoadAllAssetsAtPath`.

## Global Constraints

- 只改 `Assets/ClusterMesh/**` 与本 docs。不改 FogOfWar、Packages、ProjectSettings。
- 两种扫描都跑，无开关。追加去重，不重排已有项。
- 不跟 Animator Controller 到目录外。
- 不提交除非用户要求。
- EditMode 测试：`ClusterMesh.Editor.Tests`。编辑器占用时不编造成功。

## File map

- Create: `Assets/ClusterMesh/Editor/ClusterSkinnedAnimationClipFolderScan.cs`
- Modify: `Assets/ClusterMesh/Editor/ClusterMeshBakerWindow.cs`
- Create: `Assets/ClusterMesh/Tests/Editor/ClusterSkinnedAnimationClipFolderScanTests.cs`
- Spec 已有：`docs/superpowers/specs/2026-09-11-clustermesh-skinned-clip-folder-scan-design.md`

---
