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
| 0 | 体积归因基线 | 已完成首轮 | 否 | `BuildSize` 报告工具、MainScene 依赖表、APK entry 表 |
| 1 | 无损源文件隔离 | 已完成 | 仅移动已证明非运行时依赖的源文件 | 归档 manifest、场景功能不变 |
| 2 | 导入设置无损优化 | 已完成 | 是，但仅无损项 | Read/Write、无关导入项关闭且截图不变 |
| 3 | 高质量贴图平台规则 | 已完成 | 是，逐类 A/B | Android 压缩规则和截图对比 |
| 4 | 重复材质与贴图合并 | 已完成 | 是，小批量 | 材质引用正确、draw call/包体下降 |
| 5 | 模型与动画轻量化 | 已完成 | 是，小批量 | 猫咪不离地、不闪烁，11 动画可用 |
| 6 | 构建/Shader/Strip 收尾 | 已完成 | 否，主要改构建设置 | Release 构建成功、无新增运行错误 |
| 7 | 回归报告与保留决策 | 已完成 | 否 | 前后体积、性能、截图、功能证据 |

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

### 2026-07-05 第一批隔离

本批只处理已被 Unity `AssetDatabase.GetDependencies` 证明没有运行时用户的重复源文件，不处理任何 MainScene 仍依赖的资源。

归档目录：

```text
work/CatLife_Unity_Main/ArchivedSourceAssets/stage1-source-isolation-20260705/
```

归档 manifest：

```text
work/CatLife_Unity_Main/ArchivedSourceAssets/stage1-source-isolation-20260705/MANIFEST.md
```

已移出 `Assets/`：

| 原 Assets 路径 | 归档路径 | 源文件体积 | 处理理由 |
|---|---|---:|---|
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb` | `ArchivedSourceAssets/stage1-source-isolation-20260705/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb` | 64.20 MiB | 重复 GLB，`MainScene.unity` 使用的是 `20260702_1.glb`；场景/Prefab/材质/脚本配置/控制器依赖扫描用户数为 0。 |
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb.meta` | `ArchivedSourceAssets/stage1-source-isolation-20260705/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb.meta` | 155 bytes | 保留原 Unity GUID 元数据用于回滚。 |

明确不移动：

| 文件 | 原因 |
|---|---|
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702_1.glb` | `MainScene.unity` 当前依赖。 |
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.blend` | `MainScene.unity` 当前依赖。 |
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.fbx` | `MainScene.unity` 当前依赖。 |

复验 inventory：

```text
work/CatLife_Unity_Main/Reports/BuildSize/20260705-171554-inventory/
```

复验结果：

| 指标 | 阶段 0 首轮 | 阶段 1 第一批后 | 变化 |
|---|---:|---:|---:|
| `Assets/` 文件数 | 1336 | 1335 | -1 |
| `Assets/` 源文件总量 | 8911.63 MiB | 8847.52 MiB | -64.11 MiB |

最新 `project_assets_top.csv` 已不再包含 `catlife_v2_island_grass_style_no_skybox_20260702.glb`，仍保留当前运行时依赖 `catlife_v2_island_grass_style_no_skybox_20260702_1.glb`。

### 2026-07-05 第二批隔离与阶段 1 收口

第二批继续只处理 Unity 依赖扫描中用户数为 0 的源文件。全量候选扫描范围为 `Assets/` 下的 `.blend`、`.fbx`、`.glb`、`.gltf`、`.zip`、`.mp4`、`.mov`、`.psd`、`.kra`、`.rar`、`.7z`。

已移出 `Assets/`：

| 原 Assets 路径 | 归档路径 | 源文件体积 | 处理理由 |
|---|---|---:|---|
| `Assets/Art/Cat/Animations/CL_CAT_SRC_Walk_60fps.fbx` | `ArchivedSourceAssets/stage1-source-isolation-20260705/Art/Cat/Animations/CL_CAT_SRC_Walk_60fps.fbx` | 35.48 MiB | 源 FBX 用户数为 0；运行时依赖的是已重定向并跟踪的 `Assets/Art/Cat/Animations/Clips/CL_CAT_SRC_Walk_60fps.anim`。 |
| `Assets/Art/Cat/Animations/CL_CAT_SRC_Walk_60fps.fbx.meta` | `ArchivedSourceAssets/stage1-source-isolation-20260705/Art/Cat/Animations/CL_CAT_SRC_Walk_60fps.fbx.meta` | 2861 bytes | 保留原 Unity GUID 元数据用于回滚。 |

配套代码调整：

- `CatLifeCatTownWalkerSetup` 在源 Walk FBX 已归档时复用 `Assets/Art/Cat/Animations/Clips/CL_CAT_SRC_Walk_60fps.anim`，不会因为源 FBX 离开 `Assets/` 而失败。

阶段 1 收口扫描结果：

| 剩余 DCC/模型候选 | 用户数 | 处理结论 |
|---|---:|---|
| `Assets/Art/Cat/Animations/CatLife_cat_10_actions_final_state.fbx` | 1 | `MainScene.unity` 依赖，不能移出。 |
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.blend` | 1 | `MainScene.unity` 依赖，不能移出。 |
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.fbx` | 1 | `MainScene.unity` 依赖，不能移出。 |
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702_1.glb` | 1 | `MainScene.unity` 依赖，不能移出。 |

阶段 1 完成复验 inventory：

```text
work/CatLife_Unity_Main/Reports/BuildSize/20260705-175025-inventory/
```

| 指标 | 阶段 0 首轮 | 阶段 1 第一批后 | 阶段 1 完成后 | 阶段 1 总变化 |
|---|---:|---:|---:|---:|
| `Assets/` 文件数 | 1336 | 1335 | 1334 | -2 |
| `Assets/` 源文件总量 | 8911.63 MiB | 8847.52 MiB | 8812.04 MiB | -99.59 MiB |
| `MainScene.unity` 依赖源文件总量 | 7619.33 MiB | 7620.28 MiB | 7620.28 MiB | 约持平 |

阶段 1 结论：

- 所有已证明用户数为 0 的 DCC/模型源文件已移出 `Assets/` 并保留归档 manifest。
- `Assets/` 中剩余的大型 DCC/模型源文件都仍被 `MainScene.unity` 依赖，不能在阶段 1 直接移动。
- 下一阶段应进入“阶段 2：导入设置无损优化”，优先处理 MainScene 仍依赖资源的导入选项，而不是继续做源文件隔离。

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

### 2026-07-05 导入设置无损优化

本阶段只处理 Importer 开关，不改模型、材质、贴图压缩、场景引用和 Prefab 引用。

导入设置审计结果：

| 项目 | 审计结果 | 处理 |
|---|---|---|
| `Assets/` 内 Texture2D `Read/Write` | 0 个开启 | 无需修改。 |
| `Assets/UI/` 贴图 mipmap | 0 个开启 | 无需修改。 |
| `Assets/Resources/` 贴图 mipmap | 0 个开启 | 无需修改。 |
| 猫咪动作 FBX `CatLife_cat_10_actions_final_state.fbx` | `Read/Write` 关闭，动画开启，相机/灯光关闭 | 保持不变，避免破坏 11 个动作。 |
| 静态小镇模型 `.blend/.fbx` | 动画、相机、灯光导入仍开启 | 关闭无关导入项。 |

已修改导入设置：

| 资源 | `Read/Write` | `Import Animation` | `Import Cameras` | `Import Lights` | 说明 |
|---|---:|---:|---:|---:|---|
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.blend` | 关 | 关 | 关 | 关 | 当前 Git 可追踪变更在 `.blend.meta`。 |
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.fbx` | 关 | 关 | 关 | 关 | FBX 源文件和 `.meta` 按项目大文件规则被忽略，但当前本地 Unity 工程已写入同一设置。 |

阶段 2 复验 inventory：

```text
work/CatLife_Unity_Main/Reports/BuildSize/20260705-183117-inventory/
```

| 指标 | 阶段 1 完成后 | 阶段 2 后 | 说明 |
|---|---:|---:|---|
| `Assets/` 文件数 | 1334 | 1334 | 未新增或删除运行时资源。 |
| `Assets/` 源文件总量 | 8812.04 MiB | 8812.06 MiB | Importer meta 变化带来的微小统计差异；未新增大资源。 |
| `MainScene.unity` 依赖源文件总量 | 7620.28 MiB | 7620.28 MiB | 场景依赖保持不变。 |

运行验证：

| 验证项 | 结果 |
|---|---|
| Runtime assembly validator | 通过，场景接线、NavMesh、猫行为驱动、识别/LLM 系统、UI 绑定和 11 个 Animator state 均存在。 |
| Play Mode 冒烟 | 通过，主相机、`CatBehaviorDriver`、`CatNavigationAgent`、`CatAnimationController`、`CatDestinationPlanner`、`CatLife_TownWalker` Animator 均存在。 |
| Game View 视觉检查 | 通过，小镇正面、猫咪、专注 UI 和解锁滑槽可见，无缺材质、丢模型或明显画面退步。 |
| Console | 清理临时检查脚本记录后 0 error。 |

视觉证据：

```text
work/CatLife_Unity_Main/Reports/VisualChecks/stage2-import-settings-playmode.png
```

阶段 2 结论：

- 静态小镇模型不再导入无用动画、相机和灯光，降低导入负担和潜在构建冗余。
- 未改变材质、贴图质量、模型网格和猫咪动画链路。
- 本阶段未重新打 Android 包；真实 APK 体积收益在下一次 Android 构建或阶段 6 构建收尾时统一量化。
- 下一阶段进入“阶段 3：高质量贴图平台规则”，只能按视觉风险分组逐类 A/B，不能全局降分辨率。

## 阶段 3：高质量贴图平台规则

先按屏幕占比分组，不做一刀切压缩；在用户确认允许“合理降级贴图分辨率”后，本阶段采用可回滚的 Android 平台导入规则降低小镇贴图最大尺寸。

- 猫咪主贴图、UI、开屏资源：质量优先，不降猫咪分辨率，不动 UI/开屏默认设置。
- 小镇颜色贴图：从 2048 上限保守降到 1024，使用 Android `ASTC_4x4` 高质量压缩。
- 小镇 Metallic/Roughness 生成 mask：从 2048 上限降到 1024，使用 Android `ASTC_8x8` 更高压缩。
- 非小镇/非猫/非 UI 贴图：默认保持不变，避免误伤系统或工具贴图。

退出条件：

- 低机位大厅和猫咪近景不糊。
- 石板路和 UI 字体保持清晰。
- 每类贴图规则都有前后截图或人工确认。

### 2026-07-05 合理降级贴图分辨率与 Android 平台规则

已新增 Editor 工具：

| 菜单 | 用途 |
|---|---|
| `CatLife/Optimization/Stage 3/Audit Android Texture Policy` | 仅审计当前贴图分组、MainScene 依赖和 Android override 状态。 |
| `CatLife/Optimization/Stage 3/Apply Android Texture Policy` | 应用阶段 3 Android 贴图规则并输出报告。 |

工具文件：

```text
work/CatLife_Unity_Main/Assets/Editor/CatLifeTextureImportPolicy.cs
```

阶段 3 规则：

| 分组 | 数量 | MainScene 依赖 | Android 设置 | 处理理由 |
|---|---:|---:|---|---|
| `CatHighQuality` | 5 | 3 | `ASTC_4x4` / max 2048 / quality 100 | 猫咪是主角，保持全分辨率，只做高质量平台压缩。 |
| `TownColorBalanced` | 462 | 308 | `ASTC_4x4` / max 1024 / quality 90 | 小镇颜色贴图占体积大头，1024 上限是当前画面可接受的保守降级。 |
| `TownMaskCompact` | 154 | 154 | `ASTC_8x8` / max 1024 / quality 80 | Metallic/Roughness mask 不直接被用户检查，允许更高压缩。 |
| `KeepDefault` | 19 | 14 | 不改 | UI、开屏资源和其他低风险外资源保持默认。 |

阶段 3 报告：

```text
work/CatLife_Unity_Main/Reports/BuildSize/20260705-185549-stage3-texture-policy-audit/
work/CatLife_Unity_Main/Reports/BuildSize/20260705-191222-stage3-texture-policy-apply-resolution/
```

最终 apply 报告显示：

```text
Changed importers: 616
```

Android ASTC 估算结果：

| 分组 | 全量估算 | MainScene 依赖估算 | 说明 |
|---|---:|---:|---|
| Cat | 26.67 MiB | 16.00 MiB | 猫咪保持 2048 + ASTC 4x4。 |
| TownColor | 616.01 MiB | 410.67 MiB | 小镇颜色贴图 1024 + ASTC 4x4。 |
| TownMask | 51.34 MiB | 51.34 MiB | 小镇 mask 1024 + ASTC 8x8。 |

对比阶段 0/1 的源 PNG 体积：`Assets/Art/Town/Textures` 源 PNG 约 5003.36 MiB，其中 `MainScene.unity` 依赖贴图源约 3870.23 MiB。阶段 3 没有改 PNG 本体，所以 source inventory 不下降；收益会体现在 Android 平台导入结果和后续 APK build 中。

重要版本化说明：

- 猫咪贴图 `.png.meta` 是 Git tracked，本阶段 Android override 会随提交保存。
- 小镇贴图目录 `Assets/Art/Town/Textures/Extracted/` 和 `Assets/Art/Town/Textures/GeneratedMasks/` 按项目大文件规则被 `.gitignore` 整目录忽略，当前本机 Unity 已实际写入 616 个 importer 设置，但这些忽略目录下的 `.png.meta` 不进入 Git。
- 为保证可复现，阶段 3 规则已固化在 `CatLifeTextureImportPolicy` Editor 工具中；重建或换机后运行 `CatLife/Optimization/Stage 3/Apply Android Texture Policy` 可恢复同一规则。

复验 inventory：

```text
work/CatLife_Unity_Main/Reports/BuildSize/20260705-192124-inventory/
```

| 指标 | 阶段 2 后 | 阶段 3 后 | 说明 |
|---|---:|---:|---|
| `Assets/` 文件数 | 1334 | 1335 | 新增阶段 3 Editor 工具。 |
| `Assets/` 源文件总量 | 8812.06 MiB | 8812.11 MiB | 只新增脚本/元数据，PNG 本体未改。 |
| `MainScene.unity` 依赖源文件总量 | 7620.28 MiB | 7620.28 MiB | 场景依赖不变。 |

运行验证：

| 验证项 | 结果 |
|---|---|
| Play Mode 冒烟 | 通过，主相机、`CatLifeHomeUiController`、猫行为驱动、导航、动画控制和目的地规划均存在。 |
| Game View 视觉检查 | 通过，低机位大厅、猫咪、前景石板、专注 UI 和解锁滑槽可见；未发现贴图破损、丢材质或明显不可接受的糊化。 |
| Console | 0 error。 |

视觉证据：

```text
work/CatLife_Unity_Main/Reports/VisualChecks/stage3-texture-policy-playmode.png
```

阶段 3 结论：

- 已完成“合理降级贴图分辨率以缩小体积”的第一批高收益处理。
- 当前策略避免动 UI、开屏和猫咪分辨率，把主要降级集中在小镇贴图和非直观 mask。
- 下一阶段进入“阶段 4：重复材质与贴图合并”，重点处理 462 张小镇颜色贴图和 154 张 mask 中的重复引用，而不是继续盲目降低分辨率。

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

### 2026-07-05 完全等价材质合并

本阶段只合并可证明完全等价的材质引用，不做肉眼相似材质合并，不删除材质资产，不改贴图本体。

已新增 Editor 工具：

| 菜单 | 用途 |
|---|---|
| `CatLife/Optimization/Stage 4/Audit Material Deduplication` | 审计 `MainScene.unity` 当前依赖材质，查找序列化内容完全等价的 `.mat` 组。 |
| `CatLife/Optimization/Stage 4/Apply Material Deduplication` | 将场景 Renderer 上的重复材质引用替换为同一 keeper，并保存场景。 |

工具文件：

```text
work/CatLife_Unity_Main/Assets/Editor/CatLifeMaterialDeduplicationPolicy.cs
```

阶段 4 审计报告：

```text
work/CatLife_Unity_Main/Reports/BuildSize/20260705-194554-stage4-material-dedup-audit/
```

审计结果：

| 指标 | 合并前 |
|---|---:|
| MainScene material dependencies | 349 |
| Renderer material slots | 529 |
| Renderer unique material paths | 350 |
| Exact duplicate groups | 1 |
| Duplicate materials | 153 |

唯一可安全合并组：

| Keeper | 组内材质数 | 涉及 Renderer slots |
|---|---:|---:|
| `Assets/Materials/TownGLB/GLB_Material.001.mat` | 154 | 163 |

阶段 4 apply 报告：

```text
work/CatLife_Unity_Main/Reports/BuildSize/20260705-194637-stage4-material-dedup-apply/
```

应用结果：

| 指标 | 数值 |
|---|---:|
| Renderers changed | 162 |
| Material slots changed | 162 |
| Duplicate groups before apply | 1 |
| Duplicate materials before apply | 153 |

合并后状态：

| 指标 | 合并后 |
|---|---:|
| MainScene material dependencies | 196 |
| Renderer material slots | 529 |
| Renderer unique material paths | 197 |
| Exact duplicate groups | 0 |
| Duplicate materials | 0 |

复验 inventory：

```text
work/CatLife_Unity_Main/Reports/BuildSize/20260705-194745-inventory/
```

| 指标 | 阶段 3 后 | 阶段 4 后 | 变化 |
|---|---:|---:|---:|
| `Assets/` 文件数 | 1335 | 1336 | +1，新增阶段 4 Editor 工具。 |
| `Assets/` 源文件总量 | 8812.11 MiB | 8812.17 MiB | +0.06 MiB，新增脚本/元数据。 |
| `MainScene.unity` 依赖数 | 923 | 770 | -153 |
| `MainScene.unity` 依赖源文件总量 | 7620.28 MiB | 7619.72 MiB | -0.56 MiB |

运行验证：

| 验证项 | 结果 |
|---|---|
| Runtime assembly validator | 通过，场景接线、NavMesh、猫行为驱动、识别/LLM 系统、UI 绑定和 11 个 Animator state 均存在。 |
| Play Mode 冒烟 | 通过，主相机、`CatLifeHomeUiController`、猫行为驱动、导航、动画控制和目的地规划均存在；运行时唯一材质路径数为 197。 |
| Game View 视觉检查 | 通过，小镇、猫咪、石板、专注 UI 和解锁滑槽可见；未发现丢色、错贴或缺材质。 |
| Console | 0 error。 |

视觉证据：

```text
work/CatLife_Unity_Main/Reports/VisualChecks/stage4-material-dedup-playmode.png
```

阶段 4 结论：

- 已把 154 个完全等价的 `TownGLB` 材质引用合并到一个 keeper，消除了 153 个 MainScene 重复材质依赖。
- 本阶段没有删除材质资产，回滚边界清晰；如后续确认长期稳定，可在单独阶段考虑归档未使用重复 `.mat` 资产。
- 下一阶段进入“阶段 5：模型与动画轻量化”，重点应先审计 mesh compression、动画曲线和模型导入设置，不能影响猫咪贴地、Walk 连续播放和 11 个动作。

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

### 2026-07-05 静态模型导入轻量化

本阶段只处理低风险模型导入设置，不改猫咪 rig/root motion/动画曲线，不改猫咪模型压缩，避免引入离地、闪烁、Walk 中断或动作丢失问题。

已新增 Editor 工具：

| 菜单 | 用途 |
|---|---|
| `CatLife/Optimization/Stage 5/Audit Model Import Policy` | 审计阶段 5 关注模型的 mesh、顶点、三角面、BlendShape、动画 clip 和 importer 设置。 |
| `CatLife/Optimization/Stage 5/Apply Model Import Policy` | 对静态小镇模型应用低风险导入设置；猫咪 FBX 仅保护不修改。 |

工具文件：

```text
work/CatLife_Unity_Main/Assets/Editor/CatLifeModelImportPolicy.cs
```

阶段 5 审计报告：

```text
work/CatLife_Unity_Main/Reports/BuildSize/20260705-201058-stage5-model-import-audit/
```

审计结果：

| 模型 | Meshes | Vertices | Triangles | BlendShape meshes | Anim clips | 初始 Mesh Compression | 初始 Import BlendShapes | 决策 |
|---|---:|---:|---:|---:|---:|---|---:|---|
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.blend` | 167 | 1752016 | 850272 | 0 | 0 | Off | on | 静态小镇，可低风险压缩。 |
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.fbx` | 167 | 1752018 | 850272 | 0 | 0 | Off | on | 静态小镇，可低风险压缩；本地应用，`.fbx.meta` 按项目规则忽略。 |
| `Assets/Art/Cat/Animations/CatLife_cat_10_actions_final_state.fbx` | 1 | 138630 | 239991 | 0 | 68 | Off | on | 保护猫咪动画/模型，不修改。 |

阶段 5 apply 报告：

```text
work/CatLife_Unity_Main/Reports/BuildSize/20260705-201149-stage5-model-import-apply/
```

应用结果：

```text
Changed importers: 2
```

已应用设置：

| 模型 | Mesh Compression | Import BlendShapes | 说明 |
|---|---|---:|---|
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.blend` | Low | off | Git 可追踪变更写入 `.blend.meta`。 |
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.fbx` | Low | off | 本地 Unity 工程已写入；`.fbx.meta` 被 `.gitignore` 忽略，可通过阶段 5 工具重跑恢复。 |
| `Assets/Art/Cat/Animations/CatLife_cat_10_actions_final_state.fbx` | Off | on | 未修改，保留猫咪动画和模型安全边界。 |

复验 inventory：

```text
work/CatLife_Unity_Main/Reports/BuildSize/20260705-201343-inventory/
```

| 指标 | 阶段 4 后 | 阶段 5 后 | 说明 |
|---|---:|---:|---|
| `Assets/` 文件数 | 1336 | 1337 | 新增阶段 5 Editor 工具。 |
| `Assets/` 源文件总量 | 8812.17 MiB | 8812.20 MiB | 只新增脚本/元数据，模型源文件本体不改。 |
| `MainScene.unity` 依赖数 | 770 | 770 | 场景依赖不变。 |
| `MainScene.unity` 依赖源文件总量 | 7619.72 MiB | 7619.72 MiB | 源文件体积不变；收益体现在 Unity 导入产物和后续构建。 |

运行验证：

| 验证项 | 结果 |
|---|---|
| Runtime assembly validator | 通过，场景接线、NavMesh、猫行为驱动、识别/LLM 系统、UI 绑定和 11 个 Animator state 均存在。 |
| Play Mode 冒烟 | 通过，主相机、`CatLifeHomeUiController`、猫行为驱动、导航、动画控制和目的地规划均存在。 |
| 动画验证 | `CatLife_TownWalker` Animator 含 11 个 clips；`CL_CAT_SRC_Walk_60fps` 长度 1.00s、`loopTime=true`、112 条曲线。 |
| 猫咪高度 | Play Mode 中 `CatCompanionModel` transform Y 为 `-0.020`，保持当前项目基线；视觉截图中猫咪未离地。 |
| Game View 视觉检查 | 通过，小镇低机位大厅未出现明显破面/变形；猫咪、石板、专注 UI 和解锁滑槽正常。 |
| Console | 0 error。 |

视觉证据：

```text
work/CatLife_Unity_Main/Reports/VisualChecks/stage5-model-import-playmode.png
```

阶段 5 结论：

- 静态小镇模型启用低风险 mesh compression，并关闭已确认无用的 BlendShape 导入。
- 猫咪模型和 11 个动作链路未改，避免破坏贴地、连续行走和动作状态机。
- 下一阶段进入“阶段 6：构建与 Shader 收尾”，重点是 Release 构建、Minify/Stripping 起步设置和 Shader 变体收尾。

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

### 2026-07-05 Android Release 构建策略与首次量化

本阶段不再继续改贴图源文件，不删除资产，不降低猫咪、UI、开屏页和主画面质量。阶段 6 只把前几阶段已经建立的 Android 导入策略真正落到 Release 构建链路，并用详细 Build Report 量化收益。

已新增 Editor 工具：

| 菜单 | 用途 |
|---|---|
| `CatLife/Optimization/Stage 6/Audit Android Release Settings` | 只导出当前 Android Release 构建设置。 |
| `CatLife/Optimization/Stage 6/Apply Android Release Settings` | 应用 Android Release 构建设置并导出审计报告。 |

工具文件：

```text
work/CatLife_Unity_Main/Assets/Editor/CatLifeBuildOptimizationPolicy.cs
```

已接入构建脚本：

| 文件 | 变更 |
|---|---|
| `Assets/Editor/CatLifeAndroidBuild.cs` | Release APK 构建入口统一调用阶段 6 策略，输出构建设置证据。 |
| `Assets/Editor/CatLifeBuildSizeReporter.cs` | Detailed Build Report 构建前统一调用阶段 6 策略。 |

阶段 6 Release 设置：

| 设置 | 当前值 |
|---|---|
| Active build target | Android |
| Development build | false |
| Build app bundle | false |
| Android build system | Gradle |
| Android texture subtarget | ASTC |
| Application identifier | `com.catlife.mvp` |
| Scripting backend | IL2CPP |
| Target architectures | ARM64 |
| Minify release | true |
| Minify debug | false |
| Strip engine code | true |
| Managed stripping Android | Medium |

设置审计报告：

```text
work/CatLife_Unity_Main/Reports/BuildSize/20260705-210110-stage6-android-release-apply/android_release_settings.md
```

Release Detailed Build Report：

```text
work/CatLife_Unity_Main/Reports/BuildSize/20260705-211125-stage6-release-detailed-build/
```

构建结果：

| 项 | 阶段前基线 | 阶段 6 Release 构建 | 变化 |
|---|---:|---:|---:|
| APK 路径 | `06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk` | `Reports/BuildSize/20260705-211125-stage6-release-detailed-build/CatLife_Stage6_Release.apk` | 本地报告产物，不纳入 Git |
| APK bytes | 2803906139 | 424876644 | -2379029495 bytes |
| APK MiB | 2674.97 MiB | 405.19 MiB | -2269.78 MiB |
| Build result | 旧包 | Succeeded | 通过 |
| Build errors | 未统计 | 0 | 无构建错误 |
| Build warnings | 未统计 | 435 | 需要阶段 7 复盘，但不阻塞本阶段 |

APK 内部聚合前项：

| APK group | 压缩体积 | 未压缩体积 | 说明 |
|---|---:|---:|---|
| `assets/bin/Data/sharedassets0.assets` | 377.76 MiB | 627.74 MiB | 仍是最大剩余项，下一阶段只做归因和保留决策。 |
| `lib/arm64-v8a` | 21.09 MiB | 68.61 MiB | IL2CPP/Unity native runtime。 |
| `assets/bin/Data/Managed` | 1.76 MiB | 5.71 MiB | 托管代码资源。 |
| `assets/bin/Data/Resources` | 1.10 MiB | 4.27 MiB | Resources 数据。 |
| `classes.dex` | 0.73 MiB | 0.73 MiB | Android Java/Kotlin 字节码。 |

验证结果：

| 验证项 | 结果 |
|---|---|
| Release APK 构建 | 成功，Gradle `assembleRelease` 完成。 |
| 构建设置复读 | Unity MCP 读取结果与审计报告一致。 |
| Runtime assembly validator | 通过，场景接线、NavMesh、猫行为驱动、识别/LLM 系统、UI 绑定和 11 个 Animator state 均存在。 |
| Console | 0 error。 |
| 场景保护 | 构建过程产生的 `MainScene.unity` 脏改已还原，本阶段不改场景布局、猫咪位置或摄像机位置。 |

阶段 6 结论：

- 体积下降的主要来源是 Android ASTC 平台贴图策略、Release 构建、IL2CPP ARM64、Release minify、Engine stripping 和 Managed stripping Medium 共同生效。
- 当前已从约 2.8GB 降到约 405MB，达成“保证质量不强行压缩前提下显著降低体积”的阶段目标。
- 最大剩余项仍是 `sharedassets0.assets`，下一阶段应做回归报告、截图/功能确认、warning 归类和进一步保留决策，不能直接继续降贴图或删资源。

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

### 2026-07-05 阶段 1-6 回归与保留决策

本阶段不再修改资源、不重新压缩贴图、不删除资产，只审查阶段 1-6 的当前结果是否满足“质量不下降且体积/性能有收益”的保留条件。

#### 体积对比

| 项 | 阶段前基线 | 阶段 6 Release 构建 | 结论 |
|---|---:|---:|---|
| APK bytes | 2803906139 | 424876644 | 下降 2379029495 bytes。 |
| APK MiB | 2674.97 MiB | 405.19 MiB | 下降 2269.78 MiB，保留。 |
| Build result | 旧包 | Succeeded | Release 构建成功。 |
| Build errors | 未统计 | 0 | 无构建错误。 |
| Build warnings | 未统计 | 435 | 阶段 7 归类为非阻塞观察项，后续单独复盘。 |

阶段 6 构建证据：

```text
work/CatLife_Unity_Main/Reports/BuildSize/20260705-211125-stage6-release-detailed-build/
work/CatLife_Unity_Main/Reports/BuildSize/20260705-211125-stage6-release-detailed-build/CatLife_Stage6_Release.apk
```

#### APK 内部剩余体积

| APK group | 压缩体积 | 未压缩体积 | 决策 |
|---|---:|---:|---|
| `assets/bin/Data/sharedassets0.assets` | 377.76 MiB | 627.74 MiB | 保留当前质量；后续只做归因，不直接压缩。 |
| `lib/arm64-v8a` | 21.09 MiB | 68.61 MiB | IL2CPP/Unity native runtime，保留。 |
| `assets/bin/Data/Managed` | 1.76 MiB | 5.71 MiB | 保留。 |
| `assets/bin/Data/Resources` | 1.10 MiB | 4.27 MiB | 保留。 |
| `classes.dex` | 0.73 MiB | 0.73 MiB | 保留。 |

#### Packed asset 前项

| 资源 | Packed size | 决策 |
|---|---:|---|
| `Assets/Art/Cat/Animations/CatLife_cat_10_actions_final_state.fbx` | 12.80 MiB | 猫咪 10 动作/状态机核心资产，保留。 |
| `Assets/Art/Cat/Textures/Meshy_AI_Low_Poly_Orange_Cat_quadruped_texture_0_normal.png` | 5.33 MiB | 猫咪主角近景质量资产，保留。 |
| `Assets/Art/Cat/Textures/Meshy_AI_Low_Poly_Orange_Cat_quadruped_texture_0.png` | 5.33 MiB | 猫咪主角近景质量资产，保留。 |
| `Assets/Art/Cat/Textures/CatLife_OrangeCat_MetallicSmoothness.png` | 5.33 MiB | 猫咪材质资产，保留。 |
| `Assets/Resources/CatLifeSplash/CatLifeSplashLogo.png` | 4.50 MiB | 用户指定真实开屏页资产，保留。 |
| `Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702_1.glb` | 多项约 2.42-5.15 MiB | 小镇主场景资产，保留；后续只做重复/引用归因。 |

#### 运行与功能回归

| 验证项 | 证据 | 结果 |
|---|---|---|
| Editor 状态 | `mcpforunity://editor/state` | Unity 6.4，`MainScene`，无编译/刷新阻塞。 |
| Runtime assembly validator | Unity MCP 执行 `CatLifeRuntimeAssemblyValidator.ValidateCurrentSceneReport()` | 通过；场景接线、NavMesh、猫行为驱动、识别/LLM 系统、BlueLM fallback、开屏页、UI 绑定和 11 个 Animator state 均存在。 |
| Play Mode 视觉 | `Reports/VisualChecks/stage7-regression-playmode.png` | 通过；低机位大厅、猫咪、顶部状态栏、专注计时和解锁滑槽正常。 |
| 摄像头 | Unity MCP camera resource | 仅 `Main Camera`，FOV 80。 |
| 猫咪运行态 | Unity MCP Play Mode 采样 | `CatCompanionModel` active，Y=`-0.02`，Animator=`CatLife_TownWalker`，speed=`1`。 |
| Console | Unity MCP `read_console(types=error)` | 0 error。 |

视觉证据：

```text
work/CatLife_Unity_Main/Reports/VisualChecks/stage7-regression-playmode.png
```

#### 性能观察

Unity Editor 运行态采样：

| 指标 | 当前值 | 说明 |
|---|---:|---|
| Game View resolution | 1080x2400 | 20:9 手机画面。 |
| Draw calls | 159 | 可接受，后续真机用 profiler/logcat 复验。 |
| SetPass calls | 29 | 当前 Editor 采样无阻塞。 |
| Triangles | 1198596 | 小镇完整视觉质量下的当前成本。 |
| Vertices | 1796575 | 小镇完整视觉质量下的当前成本。 |
| Render textures | 33 | 与当前 URP/后处理配置相关。 |
| Render texture memory | 110633528 bytes | 后续真机内存阶段可继续优化。 |

说明：本阶段性能数据来自 Editor Play Mode，不等同于云真机最终性能。阶段 7 只作为“是否保留阶段 1-6 改动”的门禁；真机 FPS、内存和温度应在下一轮 Android 真机 QA 中单独采集。

#### Warning 归类

Release 构建报告记录 435 个 warnings。当前阶段未发现构建错误或运行时阻塞错误；Editor log 中主要可见 Unity/URP shader warning、URP 构建预处理提示和 Android 构建流水日志。决策：

- 不因 warning 回退阶段 1-6，因为 Release APK 成功产出且 Play Mode/Runtime validator/Console 均通过。
- 不在本阶段继续处理 warning，避免把“体积优化收口”扩散为渲染管线专项。
- 下一轮如做真机 QA，应把 warning 分类为：URP shader warning、Android Gradle warning、资源导入 warning、业务脚本 warning，并只优先处理业务脚本和真机阻塞项。

#### 保留/观察/回退决策

| 项 | 决策 | 原因 |
|---|---|---|
| 阶段 1 源文件隔离 | 保留 | 只移出无运行时用户的源文件，保留 manifest，可回滚。 |
| 阶段 2 导入设置无损优化 | 保留 | 不改视觉资产本体，运行回归通过。 |
| 阶段 3 Android 贴图平台规则 | 保留 | 是本轮体积收益主因；猫咪/UI/开屏保质量，视觉回归通过。 |
| 阶段 4 重复材质引用整理 | 保留 | 未发现材质丢失或低机位画面异常。 |
| 阶段 5 静态模型导入轻量化 | 保留 | 猫咪 rig/动画链路未改，运行回归通过。 |
| 阶段 6 Release 构建与 stripping | 保留 | Release APK 成功，0 build error，0 Console error。 |
| 435 warnings | 观察 | 不阻塞当前基线；后续按真机 QA 结果决定处理优先级。 |
| `sharedassets0.assets` 377.76 MiB | 观察 | 当前视觉质量下的最大剩余项；后续先归因，不直接压缩。 |

阶段 7 结论：

- 当前优化链路可以进入项目基线：包体从约 2.8GB 降到约 405MB，核心画面和猫咪行为链路未出现阻塞回归。
- 继续压缩的下一步不是继续降贴图分辨率，而是对 `sharedassets0.assets` 做更细粒度归因：重复小镇网格、重复材质/贴图、开屏图占用、猫咪动画 FBX 打包结构、Resources 引用边界。
- 下一轮建议进入 Android 真机 QA：安装阶段 6 APK，采集启动、首页、专注、设置、猫咪移动、离屏引导、解锁流程的截图/录屏、logcat、内存、帧率和崩溃日志。
