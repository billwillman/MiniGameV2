# ClusterMesh 出包 Profiler Implementation Plan

> **For agentic workers:** Measurement only. No runtime code. Do not commit unless asked. Run after P3（探针的 GetInterpolatedProbe 成本一并记下来）。

**Goal:** 填 `docs/superpowers/specs/2026-09-12-clustermesh-shipping-profiler-design.md` 第 5 节，再勾 followups A。

**Architecture:** 临时 200 实例场景 + Demo 对照。Profiler 看进槽 / Dispatch / 主画 / GC。

**Tech Stack:** Tuanjie Profiler，Play Mode，URP Feature 已 Enable。

## Global Constraints

- 不改 Runtime / Shader / 玩法场景 / FogOfWar。
- 不因「感觉慢」开 B 项。
- 不提交 unless asked。

## File map

- Modify: `docs/superpowers/specs/2026-09-12-clustermesh-shipping-profiler-design.md` 第 5 节
- Modify: `docs/superpowers/todos/2026-09-06-clustermesh-followups.md` A 勾选（仅当第 5 节填完且过关）

---

### Task 1: 量并记录

- [x] **Step 1:** 自动测：进槽 20；Dispatch 1；主画/阴影 CopyCount 20；Prepare 0.578 ms；Mono +4 KB。
- [x] **Step 2:** 剔关：进槽 200；Dispatch 1；主画/阴影仍 20；Prepare 1.255 ms；Mono +72 KB。
- [x] **Step 3:** 合成 10：进槽/主画/阴影 10，Dispatch 1。
- [x] **Step 4:** 第 5 节已填。进槽过关；Dispatch/主画次数不降；GC 不是只剩 Set*Array。**不勾** A。
- [x] **Step 5:** 不提交
