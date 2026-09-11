# ClusterMesh 蒙皮 Baker 动画目录扫描

Date: 2026-09-11  
Status: Locked for implementation  
Module: `Assets/ClusterMesh`  
Parent: 蒙皮 Baker 窗口（`ClusterMeshBakerWindow` 蒙皮模式）

本文件只覆盖「从 Project 目录把 AnimationClip 追加进 Baker 列表」。Bake、曲线拟合、VTF atlas、逐条加 Clip，一律沿用现窗口。

## 1. Purpose

蒙皮 Baker 现在只能一条条拖 `AnimationClip`。角色动画常按文件夹放：散落的 `.anim`，以及 Prefab 资产里嵌着的 Clip。这一期加「选目录 → 两种来源都扫 → 去重追加」，不用用户选扫描方式。

## 2. Goals and non-goals

### Goals

- 蒙皮模式保留现有 Clip 列表：逐条加、删、上下移、Bake 仍读这份列表。
- 增加「动画目录」+「从目录追加」。
- 一次追加同时做两种扫描，没有开关：
  1. 目录（含子目录）里的 `AnimationClip` 资产。
  2. 目录（含子目录）里每个 Prefab 资产内部的 `AnimationClip` 子对象（只对该路径 `LoadAllAssetsAtPath`，再筛类型）。
- 追加到现有列表后面；已在列表里的跳过（引用相等）。
- 扫描逻辑做成可单测的静态函数，不依赖打开 EditorWindow。

### Non-goals

- 不跟 `Animator.runtimeAnimatorController` / BlendTree 收到目录外的 Clip。
- 不扫旧版 `Animation` 组件上的引用（引用在 Prefab 序列化里、Clip 文件在别处的那种）。
- 不替换、不重排用户已有条目。
- 不改 Bake 输入校验（空项、重复项仍按现在报错）。
- 不自动从 `SkinnedMeshRenderer` 所在 Prefab 推断目录。
- 不扫描系统磁盘路径；只认 Project 内文件夹（`DefaultAsset` + `AssetDatabase.IsValidFolder`）。

## 3. Locked decisions

| Topic | Decision |
| --- | --- |
| 合并 | 追加 + 去重。已有顺序不变，空槽保留。 |
| 递归 | 选中目录及其全部子文件夹。 |
| 来源 1 | `AssetDatabase.FindAssets("t:AnimationClip", new[] { folder })`。含散落 `.anim`，以及该树下其它主资产（例如 FBX）上的 Clip 子资源。 |
| 来源 2 | `FindAssets("t:Prefab", new[] { folder })`，对每个 Prefab 路径 `LoadAllAssetsAtPath`，只收 `AnimationClip`。只看该 Prefab 资产文件内部，不跟随 Prefab Variant 的父资产、不展开 Controller。 |
| 两种来源 | 每次追加都跑，不提供「只扫 Clip / 只扫 Prefab」选项。 |
| 身份 | 同一 `AnimationClip` 引用只出现一次。来源 1 与来源 2 扫到同一份时算重复。 |
| 新项顺序 | 路径稳定排序：先按来源 1 的资源路径，再按来源 2 中尚未出现的项（Prefab 路径 + 子对象名）。 |
| 0 个新 Clip | 列表不改；窗口 Info：说明扫了什么、为何没有新增。 |
| 目录非法 / 未选 | 不改列表；窗口 Error：请指定 Project 内文件夹。 |
| UI 文案 | 中文。说明两种扫描都做、递归、追加去重、仍可逐条加。 |

成功标准：

- 文件夹里有独立 `.anim` 和带内部 Clip 子对象的 Prefab 时，一次「从目录追加」两者都进列表，重复引用只有一条。
- 列表里已有其中一条时，再扫只追加缺少的。
- 手动加、删、改顺序与扫目录可以混用。

## 4. Architecture

```text
ClusterMeshBakerWindow (蒙皮模式)
        | 动画目录 DefaultAsset
        | 从目录追加
        v
ClusterSkinnedAnimationClipFolderScan.Collect(folder)
        | source1: t:AnimationClip 递归
        | source2: t:Prefab → 资产内 AnimationClip
        | 路径排序、引用去重
        v
追加到 _animationClips（跳过已有引用）
        |
        v
现有 ValidatedAnimationClips / BakeSkinned（不改）
```

`Collect` 只返回「该目录两种来源的并集，已去重、已排序」。窗口负责和当前列表做第二次去重再追加。这样测试可以只测 `Collect`，不必驱动 IMGUI。

## 5. UI

蒙皮「输入」里，`DrawAnimationClipList` 上方或列表按钮旁：

- `动画目录`：`DefaultAsset`，必须是文件夹。
- 按钮 `从目录追加`。
- HelpBox：递归扫描该目录及子目录。同时收集其中的 AnimationClip 资产，以及每个 Prefab 资产内部的 AnimationClip 子对象。两种都做，不用选择。结果追加到列表，已有的跳过。仍可一条条添加。

「怎么用」里补一句：蒙皮模式可用动画目录批量追加 Clip。

## 6. Testing

Editor 测试（可建临时资产，测完删除）：

- 空文件夹 / 非法路径：`Collect` 为空或窗口报错，列表不变。
- 仅散落 `.anim`：只出现这些 Clip，顺序按路径。
- Prefab 内多个 Clip 子对象、目录下没有独立 `.anim`：这些内部 Clip 都在结果里。
- 同一 Clip 既是来源 1 又嵌在 Prefab 里：结果只有一条。
- 列表已有 A，目录含 A 和 B：追加后为 `…, B`（A 不重复）。
- 子文件夹里的 Clip / Prefab 会被扫到。

不测：Animator Controller 引用、场景对象上的 Clip、Bake 曲线内容。

## 7. Error handling

| 情况 | 行为 |
| --- | --- |
| 未选目录或不是文件夹 | Error，不改列表 |
| 目录有效但没有新 Clip | Info，不改列表 |
| Prefab 损坏 / Load 失败 | 跳过该 Prefab，其它继续；若因此 0 新增，走 Info |
| 追加后仍有空槽 | 允许；Bake 时仍按现逻辑报「第 i 项为空」 |
