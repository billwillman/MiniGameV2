# ClusterMesh 解决方案完成度评价

- 评价日期：2026-09-14
- 当前结论：核心功能已经完成，进入生产环境压力验证与调优阶段。

## 完成度

| 维度 | 完成度 | 说明 |
| --- | ---: | --- |
| 功能完成度 | 95% | Static/Skinned、编辑器预览、渲染管线、动画、LOD/Culling 和页级 Streaming 主流程已经具备 |
| 代码正确性与回归 | 95% | 已完成编译、EditMode 回归和关键 Streaming 兼容开关测试 |
| 大规模生产环境验证 | 80%～85% | 仍需在目标 PC、手机和真实大场景上完成性能采样与压力测试 |

## 已完成能力

### 渲染与编辑器

- Static Mesh 与 Skinned Mesh ClusterMesh。
- Built-in Render Pipeline、URP Forward、URP Deferred、GBuffer、阴影和 Motion Vectors。
- GameView 与 SceneView，支持 Play 和非 Play 编辑状态。
- Camera Cull、Cone Cull、LOD 和 Cluster 调试显示。
- Baker 提供默认关闭的 `Asset 外边界` 保护；开启后 Static/Skinned QEM 保持源 Mesh 开放轮廓，关闭时仍自动保护当前 LOD 分组共享边及材质/属性接缝。Page Streaming 直接复用同一套 Bake 层次。
- SceneView 的裁剪使用业务目标相机，不使用 SceneView 内置相机替代。

### Skinned Mesh 与动画

- Vertex Texture Fetch GPU 动画路径。
- CPU 动画路径及 Burst/Job System 优化。
- CPU 模式可选并行骨骼前缀计算。
- Human、非 Human 和多个 AnimationClip。
- 按骨骼影响范围改善 Cluster 划分。
- 动画后包围体与 Skinned Cluster 裁剪数据。
- CPU/GPU/CPU+GPU 模式分别保存需要的数据。
- `AnimationCurve` 保留开关。

### Page Streaming

- Static/Skinned Baker 均可选择 Page Streaming，默认关闭。
- 使用版本化 `.cmstream` 外部文件格式。
- 所有平台通过异步 `FileStream` 读取，不强制依赖 `StreamingAssets`。
- 支持 `RootPath` 与 `FilePathResolver` 映射到可写目录、热更新目录或其他路径。
- 根 LOD 常驻，细节页缺失时回退到最近可用父 LOD。
- 支持多页节点，全部页面到齐后才标记 Resident。
- GPU 请求双缓冲位集回读。
- 请求距离排序、等待老化、下一层预取、驻留宽限和 LRU 淘汰。
- 全局限制读取并发、读取带宽、上传页数、上传带宽和解压等待内存。
- Deflate 解压使用 `ArrayPool`，上传复用 staging 数组。
- Static/Skinned Streaming 资产支持跨资产全局共享 GPU Page Pool。
- 仅顶点格式、权重格式和 Page 规格兼容的资产共享池。
- 常驻根页之外始终预留一个完整细节节点空间；不足时自动建立分片。
- 共享池 LRU 使用全局更新时间。
- `Global Shared GPU Pool` 默认开启；关闭后 Streaming 资产回退为每资产独立池，资产格式不变。
- 共享 Buffer 创建失败时释放已有资源并回退到独立池。

## 非 Streaming 隔离保证

- 没有有效 `streamDescriptor` 的资产不会创建 `ClusterMeshPageRuntime`。
- 不创建 Streaming 请求 Buffer、Page Table、Node Resident Buffer 或共享 GPU Pool。
- 不执行文件读取、解压、页面上传、预取或 LRU。
- 原有内嵌数据、GPU Buffer 上传和渲染流程保持不变。
- Streaming 设置不会改变非 Streaming 资产格式或渲染结果。

## 编辑器入口

- `Tools/ClusterMesh/通用设置`：Streaming 设计说明、共享 GPU Pool、I/O/上传预算、预取和驻留宽限。
- Baker 的 `Page Streaming`：决定是否生成 `.cmstream`；关闭时继续使用原有内嵌格式。
- Baker 的 `GPU Page Pool`：共享模式下是最低工作集和容量校验依据；独立模式下是资产独立页池容量。

## 验证结果

- `ClusterMesh.Runtime.csproj`：0 警告，0 错误。
- `ClusterMesh.Editor.csproj`：0 警告，0 错误。
- `ClusterMesh.Editor.Tests.csproj`：0 警告，0 错误。
- 团结引擎 EditMode：224/224 通过，0 失败。
- 已验证兼容 Streaming 资产共享同一个 GPU Pool。
- 已验证关闭共享池后回退到独立池且资产格式不变。
- ClusterMesh 修改文件已统一为 UTF-8 无 BOM、CRLF。

## 剩余生产化工作

1. 在目标 PC、Android、iOS 真机和大场景中采集 CPU、GPU、I/O、GC 与显存峰值。
2. 增加运行时统计面板：池占用率、Resident Page、请求队列、命中率、逐出次数、实际带宽和父 LOD 回退次数。
3. 统计项目 Page 规格；规格过多会产生多个共享池分片，应统一常用 Bake 规格。
4. 快速切场景时取消在途异步读取，减少已经不需要的 I/O。
5. 根据实测决定是否增加基于相机速度和方向的预测预取。
6. 由项目资源系统接入 `.cmstream` 下载、版本管理、热更新安装和完整性清单。

## 发布判断

当前版本适合进入项目集成和真实场景性能验证。核心功能和非 Streaming 兼容边界已经具备；正式大规模发布前，应以目标设备压力测试和运行时统计数据作为最终验收依据。

## 2026-10-11：读页复用与压缩选项

- `ClusterMeshStreamReader` 为每个活动流式资产懒复用一个句柄及 64 KiB FileStream 缓冲。元数据、根页和细页在 Worker 读取；同文件 Seek+Read 原子化，解压/CRC 不持有读锁。卸载拒绝后续读请求，由在途 Worker 安全清理，不阻塞主线程等待 IO。
- `ClusterMeshBakeSettings.streamingCompression` 当前默认 `ClusterMeshPageCompression.FastDeflate`（2026-10-12 按用户要求改回压缩），仍可显式选 `None`。在 `Tools → ClusterMesh → Baker` 的“几何页压缩”设置；静态和蒙皮烘焙入口均使用该项。仅在压缩后更小时保存压缩页。它只控制 `.cmstream` 几何页，不控制纹理/Unity 包的压缩，也不覆盖已有配置的显式选择。
- 格式及读取兼容性不变；旧文件保持原压缩状态，改设置后需重新烘焙并发布，不能直接替换已发布文件而不更新描述。主工程 BigWorld 的 Profile、派生设置与缓存配方同步支持该选项，本轮未重烘焙已有正式地图。
- 两库独立引擎回归：主库 167 通过、MiniGame 154 通过，各 0 失败、2 可选 Radiant 缺失跳过；压缩/不压缩静态网格实际烘焙并读取通过，8 Worker 并发读及卸载测试通过。暖态 128 次底层读复用测试 0 GC.Alloc 事件，不等于整个加载流程零 GC；蒙皮双模式专项、Player/IL2CPP/手机与全场景 GC 未验证。
- 主库记录：`D:/MrGame/Docs/Navigation/地图制作与寻路验收.md` 第 72 节；原始报告 `D:/MrGame/MyMobileGame/Logs/ClusterMeshReadReuse-20261011/{ModuleFinal2,MiniGameFinal2}`。该同步保留两库已有差异，没有整体覆盖 BigWorld 或 MiniGame 配置。
- 后续 Reader 接入恢复及大载荷回归：`RepairMain` 168 通过、`RepairMiniGame` 155 通过，各 0 失败、2 跳过。主工程正式地中海隔离快照静止/向东 240 单位/回程/卸载测试通过：同时打开 Reader 采样峰值 13，文件缓冲配置合计 0.8125 MiB，Clear 后句柄为 0。页解压数组/元数据/数组池保留另算，不把 64 KiB 每 Reader 或 32 MiB 逻辑解码预算当作总内存硬上限。详细口径见维护文档 72.1；全场景 GC 仍未验证。

## 2026-10-12：默认压缩与全区域 Reader 复核

- Baker 默认改回 FastDeflate，保留显式 None，不重烘已有资源、不强改调用方显式配置。仅同步通用框架改动，保留两库既有 LOD、调度/预算差异；BigWorld 不同步到 MiniGame。
- 本轮主库 169 通过、MiniGame 156 通过，各 0 失败、2 Radiant 缺失跳过；含默认窗口、默认烘焙与显式压缩一致性、双存储模式运行加载及 Reader 并发/释放。证据：`D:/MrGame/MyMobileGame/Logs/ClusterMeshCompressionDefault-20261012/{MainRestored,MiniGame}`。
- 主工程原发布隔离快照 15 区 Reader 专项通过：加勒比峰值 14、南非 9、最高北欧 19，全部 Clear 后归零。是默认起点附近 240 单位往返实测，不是全局上限；Resources 新加载器专项、Player/手机及全场景 GC 未验证。完整表、方法和源码恢复记录见维护文档 72.2–72.3。


## 2026-10-12：共享 GPU 缓冲按需增长和预算重建

共享池现将常驻根页与细节页分开定尺寸；细节页无需占满烘焙根页的大槽。保留原最大缓存页数和完整节点接纳，细节 GPU 空间从小容量开始增长，无须改资产格式或重烘焙。

在 `Tools/ClusterMesh/通用设置` 或运行时持有的 `ClusterMeshSettings` 上设置：

```csharp
settings.MaxSharedGpuMegabytes = 384; // 总共享几何预算，含迁移时的新旧缓冲
settings.InitialDetailGpuKiB = 1024; // 每个新池的初始细节空间，至少一个完整节点
settings.DetailGpuGrowthKiB = 1024;  // 增长步长，仍受原缓存页数上限约束
```

通用类默认总额为 256 MiB；MrGame 的配置资产显式设为 384 MiB，MiniGame 可自行配置。总额的 1/8 只作为重建余量，不提前分配：384 MiB 时普通空间为 336 MiB，迁移预留 48 MiB。统计 `SharedGpuBufferBytes` 包括未完成 GPU 复制的旧缓冲；`GpuMigrationBytes` 是其中的迁移部分，不能重复相加。

预算不足会触发自动收缩细节缓存并重建，保留根页，细节回退到父 LOD，并记录包含容量及设置位置的 Error；不回退独立大缓冲或无限等容量。若连根页或最小重建峰值也放不下，将记录无法恢复的原因并拒绝新请求，保留已有几何。调整配置后可通过既有 `ClusterMeshStreaming.Retry(asset)` 重试。缩减预算至已使用量以下无法保证立即无损达标。

迁移用 GPU 按位复制，没有同步等待或 CPU 几何回读；每次中央更新最多启动一池重建。发生迁移时会有短时双份缓冲，且收缩会损失部分细节缓存。总额不包含纹理、渲染目标、页表/簇/绘制缓冲或 CPU 内存，不是整个游戏显存限制。没有 Compute/异步回读的平台使用完整的紧凑槽容量，仍受预算约束。

主工程模块 197 通过、MiniGame 实际模块 171 通过，各 0 失败、2 可选 Radiant 跳过；包含自动收缩恢复、数据位保真和迁移峰值测试。原稳定更新 GC 回归保留。完整实测与未验证项见 MrGame `Docs/Navigation/地图制作与寻路验收.md` 第 85 节；Player/IL2CPP/手机尚未验证。

## GPU 选择标记清零与解压器 A/B（2026-10-13）

- 已同步 GPU 清零：构造和 stamp 回绕在既有选择标记缓冲上派发计算着色器，移除临时 CPU 全零数组。没有新增 GPU 缓冲或大块 CPU 常驻缓存；运行路径不读回、不主动等待 GPU。原有矩阵/SH/请求数组仍有必要分配，不能称整个 DrawContext 零 GC。
- 正式页读取继续使用原生 DeflateStream，保留 worker IO、Reader/FileStream 及外层 packed/raw 数组池。它的逐页内部缓冲分配仍存在，不宣称页解压已零 GC。本轮新托管解压器已退出 Assets，MiniGame 不保留新的解压工作区常驻内存。
- 决策依据是 MrGame 原船地中海→开普敦 A→B→B→A：复用候选的完整采集 GC 从 13 次降到 9 次，移动区间从 2–3 次降到 1 次；但回到原生的 A2 移动最大帧约 125 ms，优于候选两轮 263/509 ms，没有稳定证明航行更顺。团结随附 Mono 独立语料测试候选约 1704 ms、原生约 752 ms，CPU 有回退，因此不只按 GC 指标正式启用。候选及独立语料工具保留在 MrGame Tools/BigWorld/Experimental，历史候选同步提交为 c0c25869。
- 收口后的实际 MiniGame 模块在 D3D11 隔离工程通过 68 项回归，主工程通过 71 项，均无失败或跳过。覆盖读取准入、压缩、根页初始化，以及清零边界、真实缓冲初值和 uint 回绕；16 次热态清零 0 个 GC 分配事件，4096 B 正对照可见。GPU 读回只用于测试。
- 两库单/双 Pass 等差异及预算设置保持原样。完整数据、常驻口径、工具和限制见 MrGame `Docs/Navigation/地图制作与寻路验收.md` 第 93 节。共享主机负载、OS/驱动缓存没有完全控制；Player、IL2CPP、手机及旧驱动等待的改善未验证。

## 可选复用解压开关（2026-10-13）

- 通用设置新增「复用解压器（实验）」及 `ReuseDecompressionContexts`，默认关闭。开关旁常显 Warning 和 Tooltip 说明降低 GC 与 CPU 回退的取舍，引用同批 Mono 语料 752/1704 ms，说明不是单页时间/固定平台比例，航行长帧收益未证实。
- 开启后在 Worker 按读取并发数租用工作区；关闭丢弃空闲工作区，在途任务完成后归还，已初始化的约 2.8 KiB 静态表保留到域重载。上下文数组每份 3174 B，默认 8 份约 24.8 KiB，不含对象头和页面数组。默认不预建工作区，Reader 与外层数组池继续保留。
- 本轮实际引擎测试 79 项全通过、0 失败/跳过；主库 82 项通过。覆盖开关生命周期、并发上限、错误归还及真实读页。实际运行时源码通过 25617 例语料。完整航行、手机/IL2CPP 与界面目视未验证，详见主库维护记录第 93.1 节及 Logs/ClusterReuseSwitch-20261013。
