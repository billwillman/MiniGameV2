# ClusterMesh 非 Play 时 Game 窗口稳定显示

Date: 2026-09-06  
Status: Implemented  
Module: `Assets/ClusterMesh`  
Parent: `docs/superpowers/specs/2026-09-05-clustermesh-object-batching-design.md`

非 Play 时 Game 窗口应稳定看见场景里的 ClusterMesh。根因是 Indirect 只活一帧，而 `ExecuteAlways` 的 `LateUpdate` 在编辑器里不连续跑。这一期只补 **Edit Mode 的 editor tick**。Play、合批、cull、Draw 语义一律不动。

## 1. Purpose

`DrawMeshInstancedIndirect` 不是 `MeshRenderer`：必须每帧重新 `Flush`。编辑器非 Play 时 `LateUpdate` 和 Game 重画经常错拍，所以有时有模型、有时没有。Play 里 `LateUpdate` 每帧跑，已经稳定，不要改那条路。

## 2. Goals and non-goals

### Goals

- 非 Play、场景里有已注册 `ClusterMeshRenderer`、物体在 `Camera.main`（或 `targetCamera`）视锥内：Game 窗口稳定显示，不必开 Always Refresh，也不必先点 Scene。
- Tick 用 `EditorApplication.update`，**仅非 Play**。
- 进 Play / 将要进 Play：必须摘掉该回调。Play 只走现有 `LateUpdate → Flush`。
- 不泄漏：具名方法、`+=` 前先 `-=`、reload / 进出 Play / 退出编辑器都摘掉。
- 无注册物体时不 `QueuePlayerLoopUpdate`，空场景不拖着编辑器转。

### Non-goals

| 不做 | 原因 |
| --- | --- |
| 改 `ClusterMeshRenderer.LateUpdate` / Play 绘制 | 运行时已对；动它就是影响老功能 |
| 改 `Flush` / `Draw` / CPU 剔 / 双 Material / 合批键 | 与「Game 何时 flush」正交 |
| `Camera.onPreCull` 当主人 | 已定 `EditorApplication.update` |
| 改 Viewer / Preview / Gizmo | 各用自己的相机，不是 Game 窗口 |
| Hi-Z、第 4 项、矩阵 GraphicsBuffer | 另一期 |
| 重 Bake | 只加 editor 订阅 |

## 3. 怎样算做对了

必须同时满足：

1. **非 Play：** Demo 在主相机镜头里，只开 Game、不点 Scene，模型稳定在，不会过一会儿空白。
2. **Play：** 画面、合批、阴影分 list、CPU 剔与现在相同。`EditorApplication.update` 上没有 ClusterMesh 的回调。
3. **停 Play / 脚本 reload：** 能再订上；Game 仍稳定。Dispose 之后不会再 Flush。
4. **订两次仍只有一次回调**（先 `-=` 再 `+=`）。
5. **无注册 renderer：** 不调用 `QueuePlayerLoopUpdate`。
6. 不改 FogOfWar / Packages / 玩法场景。

算做错（即使单测绿）：

- Play 里仍挂着 `EditorApplication.update`，或 Play 的 `LateUpdate` 被改掉 / 被跳过。
- 把 `QueuePlayerLoopUpdate` 写进 `Flush()`（测试和 Play 都会走到 `Flush`）。
- lambda 订 update，reload 后摘不掉。
- 先 Dispose 再被 update flush。

## 4. Locked decisions

| Topic | Decision |
| --- | --- |
| 运行时 | **零行为变化。** 不改 `LateUpdate` 正文。Play 仍每帧 `EnsureInitialized` + `Flush`。不改 `Draw`、合批、CPU 剔、shader |
| Edit tick | 只加在现有 `[InitializeOnLoad] ClusterMeshLifetime`。不新建生命周期类 |
| 回调 | 具名 `OnEditModeUpdate`。禁止 lambda / 匿名 |
| 订 | `+=` 前必 `-=`。静态构造时若已在 Edit；`EnteredEditMode` |
| 摘 | `ExitingEditMode`、`EnteredPlayMode`、`beforeAssemblyReload`、`quitting`。`OnEditModeUpdate` 里若 `isPlayingOrWillChangePlaymode`：立刻 `-=` 再 return |
| 顺序 | reload / 进出 Play / 退出：**先摘 update，再** 现有的 `DisposeCachedContexts` |
| Edit 里 `LateUpdate` | **保持现在也 Flush**。与 editor update 可能同帧两次进 `Flush`，`_flushedFrame` 去重。不删这条，避免改老路径 |
| `OnEditModeUpdate` 做什么 | 若将要/已经 Play → 摘掉并 return。否则 `Flush()`。若有注册 renderer → `EditorApplication.QueuePlayerLoopUpdate()` |
| `QueuePlayerLoopUpdate` | **只**从 `OnEditModeUpdate` 调。不进 `Flush`、不进 Play |
| 有无物体 | `ClusterMeshSceneBatcher.RegisteredCount`（只读，扫掉 null 后的个数）。`> 0` 才排队 player loop |
| Camera | 仍 `targetCamera ?? Camera.main`。解析不到相机则本批不画（与现在相同） |
| 空列表 Flush | 与现在相同：不 Draw。Lifetime 不因此排队 |

## 5. API

`ClusterMeshSceneBatcher` 增加只读，不改 `Flush` 语义：

```text
int RegisteredCount   // 去掉 null 项后的 renderer 数
```

`ClusterMeshLifetime`（Editor，可给同程序集测试）：

```text
bool EditModeTickActive
void SyncEditModeTick()                          // 生产：读真实 isPlaying / isPlayingOrWillChangePlaymode
void SyncEditModeTick(bool playingOverride)      // 测试：true 当 Play（摘），false 当 Edit（订）
bool ShouldQueuePlayerLoop(int registeredCount)  // registeredCount > 0
```

无参只转调 `SyncEditModeTick(EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)`。`ShouldQueuePlayerLoop` 是纯函数，单测不碰 `EditorApplication.update`。不在测试里改 `EditorApplication.isPlaying`。

## 6. 订阅状态机

```text
域加载 / 静态构造
  先挂 beforeReload / playMode / quitting（与现在相同，reload/quit 改为先摘再 Dispose）
  SyncEditModeTick()

EnteredEditMode          → SyncEditModeTick()          // 订
ExitingEditMode          → 摘 + Dispose
EnteredPlayMode          → 摘（防漏）
ExitingPlayMode          → Dispose（与现在相同）
EnteredEditMode          → Sync
beforeAssemblyReload     → 摘 + Dispose
quitting                 → 摘 + Dispose
OnEditModeUpdate 见 Play → 摘
```

`SyncEditModeTick`：

```text
EditorApplication.update -= OnEditModeUpdate
EditModeTickActive = false
if !isPlaying && !isPlayingOrWillChangePlaymode:
    EditorApplication.update += OnEditModeUpdate
    EditModeTickActive = true
```

## 7. 测试

程序集仍是 `ClusterMesh.Editor.Tests`。

- `ShouldQueuePlayerLoop(0) == false`，`(1) == true`。
- 当前是 EditMode：`SyncEditModeTick()` 后 `EditModeTickActive == true`。
- 再 `Sync` 一次：仍 `true`（不重复订；可用二次 `Sync` 不抛、Active 仍 true 断言）。
- 无法在单测里真进 Play。调用 `SyncEditModeTick(true)` 应摘掉且 `EditModeTickActive == false`；再 `SyncEditModeTick(false)` 应订上。不改 `EditorApplication.isPlaying`。
- 生产只调无参 `SyncEditModeTick()`。
- 不测 Game 像素。不改 CPU 剔 / 合批 / DrawContext 用例。

## 8. 文件

| 文件 | 变化 |
| --- | --- |
| `Editor/ClusterMeshLifetime.cs` | 订/摘 update、`OnEditModeUpdate`、先摘再 Dispose |
| `Runtime/ClusterMeshSceneBatcher.cs` | 只加 `RegisteredCount` |
| `Tests/Editor/ClusterMeshEditModeTickTests.cs` | 新 |
| `ClusterMeshRenderer` / `DrawContext` / compute / Lit | **不改** |

## 9. 相关

- 合批 Edit Mode 一句（`ExecuteAlways` + `frameCount`）被本文件补全：非 Play 另加 `EditorApplication.update`。不改 2026-09-05 正文。
- 根因讨论见本会话；不另写 lesson。
