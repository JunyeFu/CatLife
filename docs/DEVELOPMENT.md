# 开发与资产准备

[产品主页](../README.md) · [当前进展](STATUS.md)

本轮UI按[增量开发控制](development/2026-11-06-ui-style-repair-plan.md)执行：[WP06.6静态UI/G1](development/2026-11-06-ui-static-results.md)已通过，包含12项功能/资源回归及24张实际Game view截图。[WP06.7/G2](development/2026-11-07-ui-motion-results.md)也已通过：16核心、33功能及1项画面采集。运行实现留在独立分支，首r42由BuildUiStyleReviewApk构建成功，但[Android检查发现失败](development/2026-11-07-r42-android-acceptance.md)，修复候选r42b继续沿同入口构建；Android和用户视觉仍待G3。

## 环境

- Unity **6000.4.9f1**，以[ProjectVersion.txt](../work/CatLife_Unity_Main/ProjectSettings/ProjectVersion.txt)为准。
- Android Build Support及对应SDK、NDK、OpenJDK；目标为Android ARM64。
- Blender用于模型与动作制作，运行时无需安装。
- Unity依赖由[manifest.json](../work/CatLife_Unity_Main/Packages/manifest.json)与[packages-lock.json](../work/CatLife_Unity_Main/Packages/packages-lock.json)管理。

## 当前源码交付状态

运行源码、生成器、实际场景和轻量引用已收口；**公开克隆仍需额外运行资产包才能完整构建**。用户于11/6确认猫和小镇素材来自Meshy AI，本轮仅作私有实习交付；运行资产包不公开提供。公开资产分发退出本轮范围，这不代表已取得公开再分发授权。

具备项目所需资产时，在Unity Hub中打开`work/CatLife_Unity_Main`，检查资源引用及编译状态，再打开`Assets/Scenes/CatLifeMobile.unity`。资产未齐全时先阅读代码和技术规格，不把占位模型导入当成正确运行结果。

### 11/6本地还原与r41候选

本地交付目录为`F:/AgentTools/CatLifeBuilds/repair-20261105-handoff/`。`CatLife-source-snapshot.zip`与`CatLife-runtime-assets-local.zip`均以Unity项目根为相对路径，依次解压到同一个空目录，保留全部`.meta`；不复制旧Library。`runtime-handoff.json`记录文件路径、大小、Unity版本和本地分发状态。运行资产包不是原始Blender全库，也不含私有配置。

已按此方法完成空目录编译；同一`restored-project/`随后更新源码与两份UI Prefab，完成r41构建及152项去重相关测试。未再次复制Library。场景依赖之外还必须包含ProjectSettings所引用的URP、Renderer和全局配置。详细结果见[执行记录](audits/2026-11-05-repair-results.md)。

```powershell
./tools/agent/Invoke-CatLifeUnityJob.ps1 -Action Start -RunName local-core -UnityProjectPath '<还原目录>' -Platform EditMode -Filter 'CatLifeSessionControllerTests;CatLifeExitPersistenceTests;CatLifeExitGestureTests'
./tools/agent/Invoke-CatLifeUnityJob.ps1 -Action Start -RunName local-review -UnityProjectPath '<还原目录>' -Mode Execute -ExecuteMethod CatLife.Editor.CatLifeAndroidBuild.BuildNavigationTurnReviewApk -ExecuteArguments '-buildTarget','Android','-outputPath','<独立输出>/CatLife-review.apk'
```

默认review构建临时移出本地凭据，构建后恢复。只有显式加`-privateLocalReview`且本地配置存在时，才生成含凭据的**仅本机验收包**；其输出目录有`distribution-policy.txt`，这类APK不得作为GitHub附件或分享资料。无凭据与私有配置的测试结论分别记录。

## 代码导航

| 位置 | 职责 |
|---|---|
| [MobileRuntime/Core](../work/CatLife_Unity_Main/Assets/MobileRuntime/Core) | 会话、记录、统计与本地规则 |
| [Scripts/Mobile](../work/CatLife_Unity_Main/Assets/Scripts/Mobile) | 页面、摄像机、猫咪表现与交互协调 |
| [Scripts/Cat](../work/CatLife_Unity_Main/Assets/Scripts/Cat) | 动画、导航、动作切换 |
| [Scripts/LLM](../work/CatLife_Unity_Main/Assets/Scripts/LLM) | 会话上下文与模型反馈 |
| [Tests](../work/CatLife_Unity_Main/Assets/Tests) | EditMode / PlayMode 用例 |

## 运行原则

计时、奖励及本地存档由客户端决定。AI仅在用户同意后进行会后单次请求，结果映射到本地允许且已解锁的动作；不直接控制骨骼，不修改计时与奖励。凭据使用本地私有配置，不写入源码、日志示例或公开APK。

当前MiMo文本模型为`mimo-v2.6-flash`。旧`mimo-v2.5`文本模型已下线，已有私有配置需要显式更新模型名；默认值不会覆盖用户明确填写的模型。HTTP错误只记录有限的服务端错误码/类型，响应正文不进入日志。r41实际调用及冷启保存结果见执行记录。

## 资源与权利

原始资产、运行导出与宣传素材分别维护，保持稳定资产ID。当前未提供统一开源许可证；代码、模型、字体与历史材料的使用许可需要分别确认，不能把仓库可见性视为统一商业授权。
