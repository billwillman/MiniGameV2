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
