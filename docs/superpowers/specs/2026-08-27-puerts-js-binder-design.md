# Puerts JS Binder（官方 JsBehaviour 模式）

Date: 2026-08-27  
Status: Draft for review

## 背景

项目已有 xLua 的 `ILuaBinder` / `UIBinder`（`New` + `RegisterLuaEvent` + `Dictionary<int, LuaFunction>`）。游戏脚本侧已选定 Puerts **V8**，与 xLua 并行，不替换 Lua。

官方推荐（`chexiongsheng/puerts_unity_demo` 的 `JsBehaviour`）：全进程一个 Env，`ExecuteModule` 加载 ESM，模块导出 `init(bindTo)`，TS/JS 把生命周期写到 C# 的 `Action` 字段。C# 不按名字从 JS 取函数，不用 `Eval`，不维护 `LuaFunction` 字典。

本设计按该官方模式落地，并让 JS 启动与现有 `GameStart`（Lua）完全分离。

## 目标

- 运行时用官方回调字段模式驱动 TS/JS 组件逻辑。
- Lua 启动与 JS 启动互不引用。
- 第一期能在 Editor / Windows Player 上挂组件、加载 `Resources` 里的 `.mjs`、每帧旋转一类示例物体。

## 非目标（第一期不做）

- 不修改 `GameStart`、`ILuaBinder`、`UIBinder`。
- 不实现 `RegisterTsEvent`、`New(self, binder)`、`m_LuaEventMap` 同类字典、`Eval`。
- 不把控件写入 JS `bp` 表，不做 `InitRegisterControls`。
- 不做 AssetBundle / 热更 loader、不做微信小游戏 / WebGL（V8 本来就不能走那条）。
- 不建完整 tsc 工程；第一期直接提交 ESM `.mjs`。
- 不把所有 MonoBehaviour 逻辑都放到 JS（官方也说明这不是性能最佳实践）；只给需要脚本的物体挂 `ITSBinder`。

## 架构

```
JsGameStart (独立常驻)
  └── 创建 V8 JsEnv + DefaultLoader
  └── Update 中 Tick() 一次
  └── OnDestroy Dispose

ITSBinder (挂在具体物体上)
  └── Awake: ExecuteModule(TsPath) → init(this) → JsAwake
  └── Start / Update / FixedUpdate / Destroy: Invoke 对应 Action

TSUIBinder (同物体或子物体，可选)
  └── 只存 Inspector 拖进来的 UIBehaviour[]
  └── TS 在 init 里 GetComponent 后直接读字段
```

`ILuaBinder` 继续只问 `GameStart.EnvLua`。`ITSBinder` 只问 `JsGameStart.JsEnv`。

## 组件

### `SOC.GamePlay.JsGameStart`

独立 `MonoBehaviour`，与 `GameStart` 同级，**不**被 `GameStart` 引用，也**不**引用 Lua。

| 项 | 约定 |
|---|---|
| 单例 | `public static JsGameStart Instance`，`Awake` 赋值 |
| Env | `public static JsEnv JsEnv`；内部 `new JsEnv(new DefaultLoader(), -1, BackendType.V8, IntPtr.Zero, IntPtr.Zero)` |
| 生命周期 | `Awake`：`DontDestroyOnLoad` + 创建 Env。不等 Lua 的 CDN / `Main.lua` |
| Tick | 仅在本组件 `Update` 调用 `JsEnv.Tick()`，每个 Binder **禁止** Tick |
| 销毁 | `OnDestroy`：`JsEnv.Dispose()`，静态引用置空 |
| 场景 | 可与 `GameStart` 同物体，也可单独空物体；场景中只允许一个 |

执行顺序：用 Unity Script Execution Order 让 `JsGameStart.Awake` 早于 `ITSBinder.Awake`（建议 `JsGameStart` = -100）。

`JsEnv` 类型使用 Puerts 官方 demo 的 `Puerts.JsEnv`。若编译出现 obsolete 警告，实现时仍用该类型保持与 demo 一致，不改成另一套公共 API。

### `SOC.GamePlay.ITSBinder`

继承 `BaseMonoBehaviour`（销毁走 `OnInternalDestroyed`，与 `ILuaBinder` 一致）。

Inspector：

- `string TsPath`：ESM specifier，例如 `@Ts/rotate.mjs`。

公开字段（TS 在 `init` 里赋值；未赋值则 C# 不调用）：

- `Action JsAwake`
- `Action JsStart`
- `Action JsUpdate`
- `Action JsFixedUpdate`
- `Action JsOnDestroy`

没有 `SelfTarget`。TS 里的 `bindTo` 就是该 `ITSBinder`。

调用顺序：

1. `Awake`：若 `TsPath` 空或 `JsGameStart.JsEnv` 为空 → `Debug.LogError` 并 return。否则 `ExecuteModule(TsPath)`，取导出 `init`（`Action<ITSBinder>`）。`init` 缺失 → 打错误日志，不抛。调用 `init(this)` 后再 `JsAwake?.Invoke()`。
2. `Start` → `JsStart`
3. `Update` → `JsUpdate`（不 Tick）
4. `FixedUpdate` → `JsFixedUpdate`
5. `OnInternalDestroyed` → `JsOnDestroy`，然后将全部 `Action` 置 `null`

同一 `TsPath` 可挂在多个物体上：Puerts 模块只加载一次，每个实例各自调用 `init`，闭包各自持有自己的 `bindTo`。

禁止：`Eval`、按名字 `Get` 生命周期函数、`Dictionary` 缓存 JS 函数（`Action` 字段本身就是缓存）。

### `SOC.GamePlay.TSUIBinder`

独立组件，**不**加载脚本。

- `public UIBehaviour[] m_BindControls`（与现有 `UIBinder` 相同，Inspector 拖引用）
- 需要 Canvas 时 TS 用 `bindTo.GetComponent(puer.$typeof(CS.UnityEngine.Canvas))` 或 `gameObject`，C# **不**再写 `_Canvas` 进 JS 表
- 无 `InitRegisterControls`，无 `bp`

TS 在 `init` 里 `GetComponent(TSUIBinder)`，读 `m_BindControls`；若要按 `gameObject.name` 查找，在 JS 闭包内建一次局部对象，不要回写 C#。

## JS 模块约定

文件位置：`Assets/Resources/@Ts/<name>.mjs`（Unity `TextAsset`）。

`DefaultLoader` 对 `.mjs` 会去掉后缀再 `Resources.Load`。因此：

- 磁盘：`Assets/Resources/@Ts/rotate.mjs`
- `TsPath`：`@Ts/rotate.mjs`
- 实际 Load：`Resources.Load("@Ts/rotate")`

若 Unity 把 `.mjs` 当成 DefaultAsset 而不是 TextAsset，实现时给该资源指定 TextAsset importer（或官方 demo 同款 `.meta`），**不**改成 `.js`，也**不**改用 `Eval`。

模块必须导出：

```js
export function init(bindTo) {
    bindTo.JsUpdate = () => { /* ... */ };
    bindTo.JsOnDestroy = () => {
        bindTo.JsUpdate = undefined;
        bindTo.JsOnDestroy = undefined;
    };
}
```

实例状态放在 `init` 的闭包或闭包内的 class 实例上。不要求 `export default class`，不要求 `export function New`。

第一期示例：`Assets/Resources/@Ts/rotate.mjs`，`JsUpdate` 里绕 Y 轴旋转 `bindTo.transform`（与官方 `rotate.mjs` 同类）。

## 绑定与类型

第一期允许 Puerts **反射**调用（不强制先生成 Static Wrapper）。

实现时增加 Editor 侧 `[Configure]`，`[Binding]` 列表至少包含：

- `SOC.GamePlay.JsGameStart`
- `SOC.GamePlay.ITSBinder`
- `SOC.GamePlay.TSUIBinder`

以便后续生成 `csharp.d.ts` / wrapper。`Action` 从 JS 赋给 C# 字段由 Puerts 标准委托桥接完成，无需 xLua 式 `CSharpCallLua` 列表。

## 错误处理

| 情况 | 行为 |
|---|---|
| 场景里没有 `JsGameStart` | `ITSBinder` 打错误日志，不加载，不抛 |
| Env 创建失败（缺 V8 原生库等） | `JsGameStart` 打错误日志，`JsEnv` 保持 null |
| `TsPath` 为空 | `ITSBinder` 打错误日志，跳过 |
| 模块不存在 / `ExecuteModule` 抛错 | catch，打错误日志，该 Binder 后续生命周期为空操作 |
| 模块无 `init` 导出 | 打错误日志，跳过 |
| `init` 或某个 `Action` 抛错 | 该次调用 catch + 日志，不让 Unity 生命周期中断；不自动卸载 Binder |
| `JsGameStart` 先于 Binder 销毁 | Binder 在 Invoke 前检查 `JsEnv == null`，是则直接清 Action 并 return |
| 重复 `JsGameStart` | 第二个 `Awake` 打错误日志并 `enabled = false`，不覆盖 `Instance` |

`LuaFunction.Dispose` 的对应物：销毁时把 `Action` 置空，避免 Env `Dispose` 后仍持有 JS 函数。

## 文件布局（第一期）

```
Assets/Script/GamePlay/JsGameStart.cs
Assets/Script/GamePlay/ITSBinder.cs
Assets/Script/GamePlay/TSUIBinder.cs
Assets/Script/Editor/PuertsGameBinding.cs    # [Configure] + [Binding] 类型列表
Assets/Resources/@Ts/rotate.mjs
docs/superpowers/specs/2026-08-27-puerts-js-binder-design.md
```

不新增场景资产也可以验收：在任意测试物体上加 `JsGameStart`（常驻）、`ITSBinder`（`TsPath = @Ts/rotate.mjs`）。

## 测试

手动（Editor Play）：

1. 空场景：物体 A 挂 `JsGameStart`，立方体挂 `ITSBinder` + `rotate.mjs`。Play 后立方体持续旋转；停 Play 无残留异常。
2. `TsPath` 填错：Console 有错误，编辑器不卡住。
3. 无 `JsGameStart`：Binder 报错，不抛到 Unity 死循环。
4. 同场景两个 Binder 指向同一 `TsPath`：两个物体都转。
5. 带 `TSUIBinder` 的 UI 物体：`init` 里能读到 `m_BindControls` 长度与引用（可用 `console.log`）；不要求做完整 UI 业务。

不强制补 PlayMode 自动化测试（仓库现有流程以手动为主）。

## 验收标准

- `GameStart.cs` 无 Puerts using、无 JsEnv。
- `ILuaBinder` / `UIBinder` 行为不变。
- `ITSBinder` 源码中无 `Eval`、无 `Dictionary` 存 JS 函数、无 `RegisterTsEvent`。
- 全项目只有 `JsGameStart.Update` 调用 `Tick`。
- 示例 `rotate.mjs` 在 Editor Play 下可见旋转。
