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

- [ ] **Step 1:** 临时场景 200 同资产；镜头 20；其余视锥外且棱柱外。`enableCpuObjectCull` 开。记进槽 / Dispatch / 主画 / Cull ms / GC。
- [ ] **Step 2:** 同一场景剔全关，记对比列。
- [ ] **Step 3:** Demo ≈10 全在画面，记画像 B。
- [ ] **Step 4:** 填规格第 5 节。过关则勾 followups A 的 Profiler / GC。测不了（编辑器占用、没有 200 实例资产）则在第 5 节写阻挡原因，不要假勾。
- [ ] **Step 5:** 不提交
