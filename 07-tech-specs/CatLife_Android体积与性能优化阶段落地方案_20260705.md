# CatLife Android 体积与性能优化阶段落地方案

日期：2026-07-05

目标：以当前约 2.8GB APK 为基线，在不降低游戏质量、猫咪表现、低机位小镇画面和 UI 清晰度的前提下，逐阶段降低安装包体积并优化运行性能。100MB 以内不作为当前硬性目标，只作为远期探索参考；任何压缩动作必须先证明画质和功能不退步。

## 总原则

- 不强行压缩，不做一刀切降分辨率。
- 不直接删除源资产；需要移出 Unity `Assets/` 时先归档并保留可追溯 manifest。
- 每一阶段都有独立提交、可回滚边界和验证记录。
- 先归因，再处理；没有报告证据前不改导入设置、不移动资产、不重烘焙 atlas。
- 关键截图门禁固定为：开屏页、低机位大厅、猫咪近景、地面石板、设置页、专注页。
- 功能门禁固定为：Play Mode Console 无新增错误，猫咪导航/动画/专注状态/UI 页面可用。

## 阶段状态表

| 阶段 | 名称 | 当前状态 | 是否允许改资产 | 退出证据 |
|---|---|---|---:|---|
| 0 | 体积归因基线 | 正在落地 | 否 | `BuildSize` 报告工具、MainScene 依赖表、APK entry 表 |
| 1 | 无损源文件隔离 | 待开始 | 仅移动已证明非运行时依赖的源文件 | 归档 manifest、场景功能不变 |
| 2 | 导入设置无损优化 | 待开始 | 是，但仅无损项 | Read/Write、无关导入项关闭且截图不变 |
| 3 | 高质量贴图平台规则 | 待开始 | 是，逐类 A/B | Android 压缩规则和截图对比 |
| 4 | 重复材质与贴图合并 | 待开始 | 是，小批量 | 材质引用正确、draw call/包体下降 |
| 5 | 模型与动画轻量化 | 待开始 | 是，小批量 | 猫咪不离地、不闪烁，11 动画可用 |
| 6 | 构建/Shader/Strip 收尾 | 待开始 | 否，主要改构建设置 | Release 构建、无新增运行错误 |
| 7 | 回归报告与保留决策 | 待开始 | 否 | 前后体积、性能、截图、功能证据 |

## 阶段 0：体积归因基线

本阶段只回答“2.8GB 由哪些文件和依赖贡献”，不改变项目资源。

### 已落地工具

Unity 菜单：

- `CatLife/Optimization/Stage 0/Export Project Size Inventory`
  - 不打包。
  - 输出当前 `Assets/` 最大文件、MainScene 依赖、资源类型汇总和已有 APK zip entry 表。
- `CatLife/Optimization/Stage 0/Build Android Detailed Size Report`
  - 可选耗时构建。
  - 使用 `BuildOptions.DetailedBuildReport` 输出 `build_files.csv`、`packed_assets.csv`、`scenes_using_assets.csv`。

Batchmode 入口：

```powershell
Unity.exe -batchmode -quit -projectPath "C:\Users\fujunye\Desktop\Agent\05-AIGC\work\CatLife_Unity_Main" -executeMethod CatLife.Editor.CatLifeBuildSizeReporter.ExportProjectSizeInventoryBatch
```

完整 Android DetailedBuildReport：

```powershell
Unity.exe -batchmode -quit -projectPath "C:\Users\fujunye\Desktop\Agent\05-AIGC\work\CatLife_Unity_Main" -executeMethod CatLife.Editor.CatLifeBuildSizeReporter.BuildAndroidDetailedSizeReportBatch
```

默认输出位置：

```text
work/CatLife_Unity_Main/Reports/BuildSize/<timestamp>-inventory/
work/CatLife_Unity_Main/Reports/BuildSize/<timestamp>-android-detailed-build/
```

### 报告文件

| 文件 | 用途 |
|---|---|
| `build_summary.md` | 阶段 0 总览、项目路径、APK 路径、源资产总量 |
| `project_assets_top.csv` | `Assets/` 下最大 500 个源文件 |
| `asset_type_summary.csv` | 按扩展名和 Unity asset type 汇总大小 |
| `main_scene_dependencies.csv` | `MainScene.unity` 当前依赖图 |
| `apk_entries.csv` | APK 内部 zip entry 按压缩体积排序 |
| `apk_entry_groups.csv` | APK 内部 entry 聚合，尤其用于汇总 `sharedassets*.split*` |
| `build_files.csv` | DetailedBuildReport 输出文件表 |
| `packed_assets.csv` | DetailedBuildReport packed assets 反射导出 |
| `scenes_using_assets.csv` | DetailedBuildReport 场景资源关系反射导出 |

### 阶段 0 退出条件

- 能明确列出当前 APK 内最大 entry。
- 能明确列出 `MainScene.unity` 依赖中的最大源文件。
- 能区分“工程里存在的大文件”和“实际被首场景引用的大文件”。
- 未修改任何贴图、模型、Prefab、场景和导入设置。

### 2026-07-05 首次 inventory 验证

执行方式：通过 Unity MCP 调用 `CatLife.Editor.CatLifeBuildSizeReporter.ExportProjectSizeInventoryFromMenu()`，未执行 Android 构建，未修改资产。

报告目录：

```text
work/CatLife_Unity_Main/Reports/BuildSize/20260705-145532-inventory/
```

该目录已被 `.gitignore` 排除，仅作为本地阶段证据。

核心发现：

| 指标 | 当前值 |
|---|---:|
| `Assets/` 文件数 | 1336 |
| `Assets/` 源文件总量 | 8911.63 MiB |
| `MainScene.unity` 依赖数 | 920 |
| `MainScene.unity` 依赖源文件总量 | 7619.33 MiB |
| 当前已有 APK | `06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk` |
| 当前已有 APK 大小 | 2803906139 bytes |

APK 聚合体积前项：

| APK group | 压缩体积 |
|---|---:|
| `assets/bin/Data/sharedassets0.assets` | 2622.32 MiB |
| `lib/arm64-v8a` | 32.44 MiB |
| `classes.dex` | 7.12 MiB |
| `assets/bin/Data/Managed` | 2.77 MiB |

`MainScene.unity` 当前最大依赖：

| 依赖资源 | 源文件体积 |
|---|---:|
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.blend` | 3619.96 MiB |
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702_1.glb` | 64.20 MiB |
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.fbx` | 40.83 MiB |
| `Assets/Art/Cat/Textures/Meshy_AI_Low_Poly_Orange_Cat_quadruped_texture_0_normal.png` | 26.74 MiB |
| `Assets/Art/Cat/Textures/Meshy_AI_Low_Poly_Orange_Cat_quadruped_texture_0.png` | 25.15 MiB |
| `Assets/Art/Cat/Animations/CatLife_cat_10_actions_final_state.fbx` | 19.44 MiB |

资源类型汇总前项：

| 类型 | 数量 | 源文件总量 |
|---|---:|---:|
| `.png` / `Texture2D` | 640 | 5060.89 MiB |
| `.blend` / `GameObject` | 1 | 3619.96 MiB |
| `.glb` / `GameObject` | 2 | 128.40 MiB |
| `.fbx` / `GameObject` | 3 | 95.75 MiB |
| `.anim` / `AnimationClip` | 11 | 2.14 MiB |

阶段 0 结论：当前体积优化的第一优先级不是强行贴图降质，而是确认为什么首场景依赖仍包含大型 DCC 源文件和 640 张 PNG。下一阶段只能先做“无损源文件隔离和引用断开验证”，不能直接批量压缩贴图。

## 阶段 1：无损源文件隔离

进入条件：阶段 0 报告证明某些 `.blend`、`.glb`、`.fbx`、中间 PNG、视频或 zip 不应被运行时引用。

执行方式：

1. 先复制到项目外归档目录。
2. 生成 SHA256 manifest。
3. 用 `main_scene_dependencies.csv` 和 Unity 引用检查确认没有运行时依赖。
4. 从 `Assets/` 移出，不直接删除。
5. 重新跑阶段 0 inventory 和 Play Mode 功能验证。

退出条件：

- MainScene、猫咪 Animator、NavMesh、UI 正常。
- 包体或依赖源文件体积下降。
- 源文件有归档 manifest，可回滚。

## 阶段 2：导入设置无损优化

只做不会直接改变视觉结果的导入项：

- 不需要运行时读取的 Mesh/Texture 关闭 `Read/Write`。
- 静态小镇模型关闭动画、摄像机、灯光导入。
- UI 图片关闭 mipmap。
- 小镇静态模型关闭不需要的 Import Cameras/Lights/Animation。

退出条件：

- 关键截图无可见退步。
- Play Mode Console 无新增错误。
- Android 构建体积和运行内存有可解释下降。

## 阶段 3：高质量贴图平台规则

先按屏幕占比分组，不做全局统一压缩：

- 猫咪主贴图、近景地面、主建筑、UI：高质量优先。
- 远景树木、栅栏、装饰物：中等压缩。
- Mask/AO/Roughness 等非颜色图：关闭 sRGB，允许更高压缩。

退出条件：

- 低机位大厅和猫咪近景不糊。
- 石板路和 UI 字体保持清晰。
- 每类贴图规则都有前后截图或人工确认。

## 阶段 4：重复材质与贴图合并

目标是减少重复引用和 draw call，不追求激进 atlas。

执行方式：

- 先用报告找重复大贴图和重复材质。
- 只合并肉眼等价或远景低风险材质。
- 小批量改，每批都回归低机位画面。

退出条件：

- 材质不错贴、不丢色。
- Draw call 或包体有下降。
- 回滚边界清晰。

## 阶段 5：模型与动画轻量化

目标是降低模型和动画负担，但猫咪优先保质量。

执行方式：

- 小镇静态模型可使用安全 mesh compression。
- 猫咪模型只做低风险压缩。
- 动画只清理常量曲线和无用导入，不破坏 loop/root 设置。

退出条件：

- 猫咪不离地、不闪烁。
- Walk 连续播放正常。
- 10 个动作和专注/非专注动画偏好仍可触发。

## 阶段 6：构建与 Shader 收尾

只在资源阶段稳定后执行：

- Release 构建。
- Minify Release。
- Managed Stripping 从 Medium 起步，不直接上 High。
- Shader variant stripping 只移除确认不用的变体。

退出条件：

- APK 正常启动。
- 首页、设置、专注、解锁、猫咪行为链路正常。
- Console/logcat 无新增阻塞错误。

## 阶段 7：回归报告与保留决策

每阶段最终都要更新一份对比：

| 项 | 内容 |
|---|---|
| 体积 | APK 总大小、`sharedassets`、Top entries |
| 画质 | 关键截图对比 |
| 功能 | UI、猫咪、专注、动画、识别/LLM fallback |
| 性能 | FPS、batches、triangles、内存，如工具可用 |
| 决策 | 保留、回退、继续观察 |

只有同时满足“质量不下降”和“体积/性能有收益”的改动，才进入长期基线。
