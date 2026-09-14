# ClusterMesh Edit Mode Game View Implementation Plan

> **For agentic workers:** Implement inline in this session (user: OK after spec). Use TDD. Do not commit unless the user asks. Do not change `LateUpdate` / `Draw` / `Flush` semantics.

**Goal:** Non-Play Game view stably shows ClusterMesh via `EditorApplication.update`, with no Play-path or leak changes.

**Architecture:** Existing `ClusterMeshLifetime` owns a named `OnEditModeUpdate`. Subscribe only in Edit; unsubscribe before Play / reload / quit, then dispose. `Flush` stays as-is. `QueuePlayerLoopUpdate` is only called from the edit callback when `RegisteredCount > 0`.

**Tech Stack:** Tuanjie 2022.3.48t2, `ClusterMesh.Editor` + `ClusterMesh.Editor.Tests`.

## Global Constraints

- Namespace `ClusterMesh`. Tests in `ClusterMesh.Tests`.
- Write only `Assets/ClusterMesh/**` plus this plan/spec. No FogOfWar / Packages / ProjectSettings / UserSettings.
- Do not edit `ClusterMeshRenderer.LateUpdate`, `ClusterMeshDrawContext`, shaders.
- `QueuePlayerLoopUpdate` must not live in `Flush()`.
- Do not invent passing test output. Verify with Tuanjie `-runTests` assembly `ClusterMesh.Editor.Tests` if the editor is closed.
- Do not commit unless asked.

## File map

- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshSceneBatcher.cs` — `RegisteredCount`
- Modify: `Assets/ClusterMesh/Editor/ClusterMeshLifetime.cs` — tick subscribe / unsubscribe
- Create: `Assets/ClusterMesh/Tests/Editor/ClusterMeshEditModeTickTests.cs`

`ClusterMeshLifetime` is currently internal. Tests are another assembly → make the class and new members **public**.

---

### Task 1: RegisteredCount + tick API tests then implementation

**Files:**
- Create: `Assets/ClusterMesh/Tests/Editor/ClusterMeshEditModeTickTests.cs`
- Modify: `Assets/ClusterMesh/Runtime/ClusterMeshSceneBatcher.cs`
- Modify: `Assets/ClusterMesh/Editor/ClusterMeshLifetime.cs`

**Interfaces:**
- `ClusterMeshSceneBatcher.RegisteredCount` → `int` (non-null renderers, no list mutation)
- `ClusterMeshLifetime.EditModeTickActive` → `bool`
- `ClusterMeshLifetime.SyncEditModeTick()`
- `ClusterMeshLifetime.SyncEditModeTick(bool playingOverride)` — `true` = treat as Play (unhook)
- `ClusterMeshLifetime.ShouldQueuePlayerLoop(int registeredCount)` → `registeredCount > 0`

- [ ] **Step 1: Write failing tests** (`ClusterMeshEditModeTickTests.cs`)

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshEditModeTickTests
    {
        readonly List<Object> _trash = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            ClusterMeshSceneBatcher.ResetForTests();
            ClusterMeshLifetime.SyncEditModeTick();
            for (int i = 0; i < _trash.Count; i++)
            {
                if (_trash[i] != null)
                    Object.DestroyImmediate(_trash[i]);
            }

            _trash.Clear();
        }

        [Test]
        public void ShouldQueuePlayerLoop_Zero_IsFalse()
        {
            Assert.That(ClusterMeshLifetime.ShouldQueuePlayerLoop(0), Is.False);
        }

        [Test]
        public void ShouldQueuePlayerLoop_One_IsTrue()
        {
            Assert.That(ClusterMeshLifetime.ShouldQueuePlayerLoop(1), Is.True);
        }

        [Test]
        public void RegisteredCount_Empty_IsZero()
        {
            ClusterMeshSceneBatcher.ResetForTests();
            Assert.That(ClusterMeshSceneBatcher.RegisteredCount, Is.EqualTo(0));
        }

        [Test]
        public void RegisteredCount_OneRegistered_IsOne()
        {
            var renderer = CreateRenderer();
            ClusterMeshSceneBatcher.Register(renderer);
            Assert.That(ClusterMeshSceneBatcher.RegisteredCount, Is.EqualTo(1));
        }

        [Test]
        public void RegisteredCount_DestroyedWithoutUnregister_IsZero()
        {
            var renderer = CreateRenderer();
            ClusterMeshSceneBatcher.Register(renderer);
            Object.DestroyImmediate(renderer.gameObject);
            Assert.That(ClusterMeshSceneBatcher.RegisteredCount, Is.EqualTo(0));
        }

        [Test]
        public void SyncEditModeTick_False_SetsActive()
        {
            ClusterMeshLifetime.SyncEditModeTick(true);
            ClusterMeshLifetime.SyncEditModeTick(false);
            Assert.That(ClusterMeshLifetime.EditModeTickActive, Is.True);
        }

        [Test]
        public void SyncEditModeTick_True_ClearsActive()
        {
            ClusterMeshLifetime.SyncEditModeTick(false);
            ClusterMeshLifetime.SyncEditModeTick(true);
            Assert.That(ClusterMeshLifetime.EditModeTickActive, Is.False);
        }

        [Test]
        public void SyncEditModeTick_FalseTwice_StaysActive()
        {
            ClusterMeshLifetime.SyncEditModeTick(false);
            ClusterMeshLifetime.SyncEditModeTick(false);
            Assert.That(ClusterMeshLifetime.EditModeTickActive, Is.True);
        }

        ClusterMeshRenderer CreateRenderer()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            _trash.Add(mesh);
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings());
            _trash.Add(asset);
            var go = new GameObject("CMTickRenderer");
            go.SetActive(false);
            _trash.Add(go);
            var renderer = go.AddComponent<ClusterMeshRenderer>();
            renderer.asset = asset;
            renderer.cullShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            renderer.litShader = Shader.Find("ClusterMesh/Lit");
            return renderer;
        }
    }
}
```

- [ ] **Step 2: Confirm RED** — compile fails on missing `RegisteredCount` / `SyncEditModeTick` / `ShouldQueuePlayerLoop` / `EditModeTickActive`.

- [ ] **Step 3: Minimal implementation**

`RegisteredCount` counts non-null entries; does not `RemoveAt`.

`ClusterMeshLifetime` becomes `public static`. Named `OnEditModeUpdate` only. `+=` after `-=`. Static ctor / playMode / reload / quit: unhook then dispose. `OnEditModeUpdate`: if `isPlayingOrWillChangePlaymode` → `SyncEditModeTick(true)` and return; else `Flush()`; if `ShouldQueuePlayerLoop(RegisteredCount)` → `QueuePlayerLoopUpdate()`.

Play state machine:

- `ExitingEditMode` → `SyncEditModeTick(true)` then Dispose
- `EnteredPlayMode` → `SyncEditModeTick(true)`
- `ExitingPlayMode` → Dispose (same as today)
- `EnteredEditMode` → `SyncEditModeTick(false)`
- `beforeAssemblyReload` / `quitting` → `SyncEditModeTick(true)` then Dispose
- static ctor ends with `SyncEditModeTick()`

- [ ] **Step 4: Run `ClusterMesh.Editor.Tests`** if Tuanjie is not locking the project.

```text
"C:\Program Files\Tuanjie\Hub\Editor\2022.3.48t2\Editor\Tuanjie.exe" -batchmode -nographics -projectPath D:/MiniGameV2 -runTests -testPlatform EditMode -assemblyNames ClusterMesh.Editor.Tests -testResults D:/MiniGameV2/.superpowers/sdd/editmode-tick-results.xml -logFile D:/MiniGameV2/.superpowers/sdd/editmode-tick.log
```

- [ ] **Step 5: Do not commit** unless asked.

## Spec coverage

| Spec | Task |
| --- | --- |
| Edit-only `EditorApplication.update` | 1 Lifetime |
| Unhook on Play / reload / quit | 1 state machine |
| No leak (named + `-=` first) | 1 |
| `QueuePlayerLoopUpdate` only in callback, count > 0 | 1 |
| Do not change LateUpdate / Flush / Draw | 1 (no edits) |
| Tests for queue predicate + subscribe override | 1 |
