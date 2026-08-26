# Puerts JS Binder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 用官方 JsBehaviour 模式落地独立的 V8 启动器、`ITSBinder`、`TSUIBinder` 和 `rotate.mjs` 示例，且不改现有 Lua `GameStart`。

**Architecture:** `JsGameStart` 常驻创建唯一 V8 `JsEnv` 并只在这里 `Tick`。`ITSBinder` 对 `TsPath` 做 `ExecuteModule`，取导出 `init(bindTo)`，把生命周期写进 C# `Action` 字段后由 Unity 回调 Invoke。`TSUIBinder` 只保存 Inspector 里的 `UIBehaviour[]`，JS 直接读字段。

**Tech Stack:** Unity 2022.3 / Tuanjie 1.9.3、C#、Puerts 3.0.2 V8（`Assets/Purts/`）、ESM `.mjs` + `DefaultLoader`（Resources）。

## Global Constraints

- 不修改 `Assets/Script/GameStart.cs`、`Assets/Script/GamePlay/ILuaBinder.cs`、`Assets/Script/GamePlay/UIBinder.cs`
- 不实现 `RegisterTsEvent`、`New(self, binder)`、`Dictionary` 缓存 JS 函数、`Eval`
- 不做 `InitRegisterControls`、不写 JS `bp` 表
- 不做 AB/热更 loader、不做微信/WebGL、不建 tsc 工程
- Env：`new JsEnv(new DefaultLoader(), -1, BackendType.V8, IntPtr.Zero, IntPtr.Zero)`
- 命名空间：`SOC.GamePlay`；示例 specifier：`@Ts/rotate.mjs`
- 全项目只有 `JsGameStart.Update` 调用 `JsEnv.Tick()`
- 本仓库无强制 PlayMode 自动化测试：每个任务用文件存在性 / `rg` 约束 + Editor 编译/手动 Play 验收
- 提交信息风格：`【增加】…`（本仓库现有习惯）

## File Structure

| Path | Responsibility |
|---|---|
| `Assets/Script/GamePlay/JsGameStart.cs` | 唯一 V8 Env、Tick、Dispose |
| `Assets/Script/GamePlay/ITSBinder.cs` | ExecuteModule + `init` + Action 生命周期 |
| `Assets/Script/GamePlay/TSUIBinder.cs` | Inspector `UIBehaviour[]` 引用容器 |
| `Assets/Script/Editor/PuertsGameBinding.cs` | `[Configure]` + `[Binding]` 类型列表 |
| `Assets/Resources/@Ts/rotate.mjs` | 官方风格示例：旋转 + 可选打印 UI 引用 |

`.meta` 交给 Unity 导入生成，不要手写 guid。`.mjs` 由已有 `Puerts.Editor.MJSImporter` 导入为 `TextAsset`。

---

### Task 1: JsGameStart

**Files:**
- Create: `Assets/Script/GamePlay/JsGameStart.cs`
- Test: 无独立测试项目；用 `rg` + Unity Console

**Interfaces:**
- Consumes: `Puerts.JsEnv`、`Puerts.DefaultLoader`、`Puerts.BackendType`、`UnityEngine.MonoBehaviour`
- Produces: `public static JsGameStart Instance`；`public static JsEnv JsEnv`；`Awake` 创建 Env；`Update` 调用 `JsEnv.Tick()`；`OnDestroy` Dispose

- [ ] **Step 1: 确认目标文件还不存在**

Run (PowerShell，仓库根目录 `D:\MiniGameV2`):

```powershell
Test-Path Assets/Script/GamePlay/JsGameStart.cs
```

Expected: `False`

- [ ] **Step 2: 写入 `JsGameStart.cs`**

完整文件：

```csharp
using System;
using Puerts;
using UnityEngine;

namespace SOC.GamePlay
{
    [DefaultExecutionOrder(-100)]
    public class JsGameStart : MonoBehaviour
    {
        public static JsGameStart Instance = null;
        public static JsEnv JsEnv = null;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[JsGameStart] Duplicate instance. Keeping the first and disabling this one.");
                enabled = false;
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            try
            {
                JsEnv = new JsEnv(new DefaultLoader(), -1, BackendType.V8, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception e)
            {
                JsEnv = null;
                Debug.LogError("[JsGameStart] Failed to create V8 JsEnv: " + e);
            }
        }

        void Update()
        {
            if (JsEnv != null)
                JsEnv.Tick();
        }

        void OnDestroy()
        {
            if (Instance != this)
                return;
            if (JsEnv != null)
            {
                JsEnv.Dispose();
                JsEnv = null;
            }
            Instance = null;
        }
    }
}
```

- [ ] **Step 3: 约束检查**

Run:

```powershell
rg -n "JsEnv\.Tick\(" Assets/Script/GamePlay/JsGameStart.cs
rg -n "using XLua|LuaEnv|EnvLua" Assets/Script/GamePlay/JsGameStart.cs
rg -n "BackendType\.V8" Assets/Script/GamePlay/JsGameStart.cs
rg -n "DefaultExecutionOrder\(-100\)" Assets/Script/GamePlay/JsGameStart.cs
```

Expected: 第 1、3、4 行各命中一处；第 2 行无输出。

- [ ] **Step 4: Unity 编译**

切回 Unity Editor，等脚本编译结束。Console 应无 `JsGameStart` 相关 CS 错误。若 `JsEnv` obsolete 警告，按 spec 保留该类型，不要改成别的公共 API。

- [ ] **Step 5: Commit**

```powershell
git add Assets/Script/GamePlay/JsGameStart.cs
git commit -m "【增加】JsGameStart：独立 V8 Env 与 Tick"
```

Unity 若已生成 `JsGameStart.cs.meta`，一并 add。

---

### Task 2: ITSBinder

**Files:**
- Create: `Assets/Script/GamePlay/ITSBinder.cs`
- Test: `rg` 禁止项 + Unity 编译

**Interfaces:**
- Consumes: `JsGameStart.JsEnv`（`Puerts.JsEnv`，可为 null）；`BaseMonoBehaviour.OnInternalDestroyed`
- Produces: `public string TsPath`；`public Action JsAwake/JsStart/JsUpdate/JsFixedUpdate/JsOnDestroy`；`Awake` 调用模块 `init(ITSBinder)`；生命周期只 `Invoke`，不 `Tick`、不 `Eval`

- [ ] **Step 1: 确认文件不存在**

```powershell
Test-Path Assets/Script/GamePlay/ITSBinder.cs
```

Expected: `False`

- [ ] **Step 2: 写入 `ITSBinder.cs`**

完整文件：

```csharp
using System;
using Puerts;
using UnityEngine;

namespace SOC.GamePlay
{
    public class ITSBinder : BaseMonoBehaviour
    {
        public string TsPath = string.Empty;

        [HideInInspector] public Action JsAwake;
        [HideInInspector] public Action JsStart;
        [HideInInspector] public Action JsUpdate;
        [HideInInspector] public Action JsFixedUpdate;
        [HideInInspector] public Action JsOnDestroy;

        void Awake()
        {
            LoadTs();
            InvokeJs(JsAwake);
        }

        void Start()
        {
            InvokeJs(JsStart);
        }

        void Update()
        {
            InvokeJs(JsUpdate);
        }

        void FixedUpdate()
        {
            InvokeJs(JsFixedUpdate);
        }

        protected override void OnInternalDestroyed()
        {
            InvokeJs(JsOnDestroy);
            ClearActions();
        }

        void LoadTs()
        {
            if (string.IsNullOrEmpty(TsPath))
            {
                Debug.LogError("[ITSBinder] TsPath is empty on " + name);
                return;
            }

            var env = JsGameStart.JsEnv;
            if (env == null)
            {
                Debug.LogError("[ITSBinder] JsGameStart.JsEnv is null on " + name);
                return;
            }

            try
            {
                ScriptObject mod = env.ExecuteModule(TsPath);
                if (mod == null)
                {
                    Debug.LogError("[ITSBinder] ExecuteModule returned null: " + TsPath);
                    return;
                }

                Action<ITSBinder> init = null;
                try
                {
                    init = mod.Get<Action<ITSBinder>>("init");
                }
                catch (Exception e)
                {
                    Debug.LogError("[ITSBinder] Failed to get export 'init' from " + TsPath + ": " + e);
                    return;
                }

                if (init == null)
                {
                    Debug.LogError("[ITSBinder] Module has no export 'init': " + TsPath);
                    return;
                }

                init(this);
            }
            catch (Exception e)
            {
                Debug.LogError("[ITSBinder] ExecuteModule failed: " + TsPath + " : " + e);
            }
        }

        void InvokeJs(Action fn)
        {
            if (JsGameStart.JsEnv == null)
            {
                ClearActions();
                return;
            }
            if (fn == null)
                return;
            try
            {
                fn();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void ClearActions()
        {
            JsAwake = null;
            JsStart = null;
            JsUpdate = null;
            JsFixedUpdate = null;
            JsOnDestroy = null;
        }
    }
}
```

- [ ] **Step 3: 禁止项检查**

```powershell
rg -n "Eval\(|RegisterTsEvent|Dictionary<" Assets/Script/GamePlay/ITSBinder.cs
rg -n "Tick\(" Assets/Script/GamePlay/ITSBinder.cs
rg -n "ExecuteModule" Assets/Script/GamePlay/ITSBinder.cs
```

Expected: 前两行无输出；第三行命中 `env.ExecuteModule(TsPath)`。

- [ ] **Step 4: Unity 编译**

Console 无 `ITSBinder` CS 错误。

- [ ] **Step 5: Commit**

```powershell
git add Assets/Script/GamePlay/ITSBinder.cs
git commit -m "【增加】ITSBinder：ExecuteModule + init Action 回调"
```

若有 `.meta` 一并 add。

---

### Task 3: TSUIBinder

**Files:**
- Create: `Assets/Script/GamePlay/TSUIBinder.cs`
- Test: `rg` 确认没有 `bp` / `InitRegisterControls` / `LuaTable`

**Interfaces:**
- Consumes: `UnityEngine.EventSystems.UIBehaviour`、`BaseMonoBehaviour`
- Produces: `public UIBehaviour[] m_BindControls`；无加载脚本、无把控件写入 JS

- [ ] **Step 1: 确认文件不存在**

```powershell
Test-Path Assets/Script/GamePlay/TSUIBinder.cs
```

Expected: `False`

- [ ] **Step 2: 写入 `TSUIBinder.cs`**

完整文件：

```csharp
using UnityEngine;
using UnityEngine.EventSystems;

namespace SOC.GamePlay
{
    public sealed class TSUIBinder : BaseMonoBehaviour
    {
        public UIBehaviour[] m_BindControls = null;
    }
}
```

- [ ] **Step 3: 约束检查**

```powershell
rg -n "InitRegisterControls|LuaTable|\bbp\b" Assets/Script/GamePlay/TSUIBinder.cs
rg -n "m_BindControls" Assets/Script/GamePlay/TSUIBinder.cs
```

Expected: 第一行无输出；第二行命中字段声明。

- [ ] **Step 4: 确认未改旧 UIBinder**

```powershell
git diff -- Assets/Script/GamePlay/UIBinder.cs
```

Expected: 空（无 diff）。

- [ ] **Step 5: Commit**

```powershell
git add Assets/Script/GamePlay/TSUIBinder.cs
git commit -m "【增加】TSUIBinder：Inspector UI 引用容器"
```

若有 `.meta` 一并 add。

---

### Task 4: rotate.mjs 示例

**Files:**
- Create: `Assets/Resources/@Ts/rotate.mjs`
- Test: Unity Play Mode 手动（spec 测试 1/4/5）

**Interfaces:**
- Consumes: `ITSBinder` 的 `JsUpdate` / `JsOnDestroy`、`transform`；可选 `TSUIBinder.m_BindControls`
- Produces: `export function init(bindTo)`；每帧绕 Y 轴旋转

- [ ] **Step 1: 创建目录并写入模块**

```powershell
New-Item -ItemType Directory -Force -Path Assets/Resources/@Ts | Out-Null
```

写入 `Assets/Resources/@Ts/rotate.mjs`（完整内容）：

```javascript
import { $typeof } from "puerts";

class Rotate {
    constructor(bindTo) {
        this.bindTo = bindTo;
        const uiType = $typeof(CS.SOC.GamePlay.TSUIBinder);
        const ui = bindTo.GetComponent(uiType);
        if (ui != null && ui.m_BindControls != null) {
            console.log("[rotate] TSUIBinder m_BindControls length=" + ui.m_BindControls.Length);
        }
        bindTo.JsUpdate = () => this.onUpdate();
        bindTo.JsOnDestroy = () => this.onDestroy();
    }

    onUpdate() {
        const r = CS.UnityEngine.Vector3.op_Multiply(
            CS.UnityEngine.Vector3.up,
            CS.UnityEngine.Time.deltaTime * 10
        );
        this.bindTo.transform.Rotate(r);
    }

    onDestroy() {
        console.log("[rotate] onDestroy");
        this.bindTo.JsUpdate = undefined;
        this.bindTo.JsOnDestroy = undefined;
    }
}

export function init(bindTo) {
    new Rotate(bindTo);
}
```

- [ ] **Step 2: 确认 Resources 路径与 specifier 规则**

`DefaultLoader` 对 `.mjs` 会去掉后缀再 `Resources.Load`。因此磁盘 `Assets/Resources/@Ts/rotate.mjs` 对应 `TsPath = @Ts/rotate.mjs`，实际 Load `@Ts/rotate`。

```powershell
Test-Path "Assets/Resources/@Ts/rotate.mjs"
```

Expected: `True`

切回 Unity，Project 窗口选中该文件，Inspector 主对象应为 `TextAsset`（`MJSImporter`）。若不是 TextAsset：停下来修 importer，禁止改成 `.js` 或改用 `Eval`。

- [ ] **Step 3: 手动 Play（spec 1、2、3、4）**

1. 临时空场景：空物体挂 `JsGameStart`；Cube 挂 `ITSBinder`，`TsPath` 填 `@Ts/rotate.mjs`。Play：Cube 持续绕 Y 旋转。停 Play，Console 无 Dispose 异常。
2. 把 `TsPath` 改成 `@Ts/not-exist.mjs`。Play：有 `[ITSBinder] ExecuteModule failed`（或同类）错误，编辑器不卡死。
3. 禁用/删掉 `JsGameStart`。Play：`[ITSBinder] JsGameStart.JsEnv is null`，不抛死循环。
4. 两个 Cube 都挂 `ITSBinder` 且同一 `TsPath`：两个都转。

- [ ] **Step 4: 手动 Play（spec 5）**

在其中一个 Binder 物体上再挂 `TSUIBinder`，数组里拖任意一个 `UIBehaviour`（或长度 0）。Play：Console 出现 `[rotate] TSUIBinder m_BindControls length=`。不要求按钮业务。

- [ ] **Step 5: Commit**

```powershell
git add "Assets/Resources/@Ts/rotate.mjs"
git commit -m "【增加】rotate.mjs：ITSBinder 官方 init 示例"
```

若 Unity 生成了 `rotate.mjs.meta` 和 `@Ts.meta`，一并 add。

---

### Task 5: Puerts Binding 配置

**Files:**
- Create: `Assets/Script/Editor/PuertsGameBinding.cs`

**Interfaces:**
- Consumes: `Puerts.ConfigureAttribute`、`Puerts.BindingAttribute`
- Produces: Editor 配置里可扫到 `JsGameStart`、`ITSBinder`、`TSUIBinder`（供后续 d.ts / wrapper；第一期仍允许反射）

- [ ] **Step 1: 写入配置**

目录 `Assets/Script/Editor/` 若不存在则创建。完整文件：

```csharp
using System;
using System.Collections.Generic;
using Puerts;
using SOC.GamePlay;

[Configure]
public class PuertsGameBinding
{
    [Binding]
    static IEnumerable<Type> Bindings
    {
        get
        {
            return new List<Type>()
            {
                typeof(JsGameStart),
                typeof(ITSBinder),
                typeof(TSUIBinder),
            };
        }
    }
}
```

必须放在名为 `Editor` 的文件夹下，否则进 Player 会编不过（`Configure` 在 Editor 程序集）。

- [ ] **Step 2: 确认类型列表**

```powershell
rg -n "typeof\(JsGameStart\)|typeof\(ITSBinder\)|typeof\(TSUIBinder\)" Assets/Script/Editor/PuertsGameBinding.cs
```

Expected: 三处命中。

- [ ] **Step 3: Unity 编译**

Console 无 `PuertsGameBinding` CS 错误。第一期不必跑 Generate Wrapper / d.ts。

- [ ] **Step 4: Commit**

```powershell
git add Assets/Script/Editor/PuertsGameBinding.cs
git commit -m "【增加】Puerts Binding：JsGameStart / ITSBinder / TSUIBinder"
```

若有 `.meta` 一并 add。

---

### Task 6: 验收扫描

**Files:**
- Verify only: `Assets/Script/GameStart.cs`、`Assets/Script/GamePlay/ILuaBinder.cs`、`Assets/Script/GamePlay/UIBinder.cs`、新建的四个 C# 文件

**Interfaces:**
- Consumes: Task 1–5 产物
- Produces: 满足 spec 验收标准的仓库状态

- [ ] **Step 1: GameStart / Lua Binder 未被改动**

```powershell
git diff -- Assets/Script/GameStart.cs Assets/Script/GamePlay/ILuaBinder.cs Assets/Script/GamePlay/UIBinder.cs
rg -n "using Puerts|JsEnv" Assets/Script/GameStart.cs
```

Expected: `git diff` 空；`rg` 无输出。

- [ ] **Step 2: ITSBinder 无 Eval / 字典缓存 / RegisterTsEvent**

```powershell
rg -n "Eval\(|RegisterTsEvent|Dictionary<" Assets/Script/GamePlay/ITSBinder.cs
```

Expected: 无输出。

- [ ] **Step 3: 全 Script 游戏代码里只有 JsGameStart Tick**

```powershell
rg -n "JsEnv\.Tick\(" Assets/Script
```

Expected: 仅 `Assets/Script/GamePlay/JsGameStart.cs` 一处。

- [ ] **Step 4: 示例文件在位**

```powershell
Test-Path "Assets/Resources/@Ts/rotate.mjs"
rg -n "export function init" "Assets/Resources/@Ts/rotate.mjs"
```

Expected: `True`；命中 `export function init`。

本任务无新文件则不提交。若 Step 1–4 有失败，回到对应 Task 修，不要在本 Task 另开新设计。

---

## Self-Review

- Spec 覆盖：独立 `JsGameStart`、V8+DefaultLoader、Tick 唯一、`ITSBinder` init/Action、销毁清 Action、Env 空/路径空/无 init/Execute 失败/回调抛错、重复 Instance、`TSUIBinder` 无 bp、`rotate.mjs`、Binding 列表、不改 GameStart/Lua、手动测试 1–5 → 均有 Task。
- 无 TBD / “类似 Task N” / 空错误处理。
- 名称一致：`JsGameStart.JsEnv`、`TsPath`、`JsAwake` 等与 spec 相同。
- `[DefaultExecutionOrder(-100)]` 落实 spec 的 Script Execution Order，避免改 `ProjectSettings` YAML。
