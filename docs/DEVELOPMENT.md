# 开发与资产准备

[产品主页](../README.md) · [当前进展](STATUS.md)

## 环境

- Unity **6000.4.9f1**，以[ProjectVersion.txt](../work/CatLife_Unity_Main/ProjectSettings/ProjectVersion.txt)为准。
- Android Build Support及对应SDK、NDK、OpenJDK；目标为Android ARM64。
- Blender用于模型与动作制作，运行时无需安装。
- Unity依赖由[manifest.json](../work/CatLife_Unity_Main/Packages/manifest.json)与[packages-lock.json](../work/CatLife_Unity_Main/Packages/packages-lock.json)管理。

## 当前源码交付状态

仓库可用于阅读工程与实现记录，**当前尚不支持克隆后直接完整构建**。本地存在未整合的运行改动，且原始美术、派生FBX和部分运行资产独立保留；本次主页整理没有把这些改动自动认定为发行源码。

具备项目所需资产时，在Unity Hub中打开`work/CatLife_Unity_Main`，检查资源引用及编译状态，再打开`Assets/Scenes/CatLifeMobile.unity`。资产未齐全时先阅读代码和技术规格，不把占位模型导入当成正确运行结果。

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

## 资源与权利

原始资产、运行导出与宣传素材分别维护，保持稳定资产ID。当前未提供统一开源许可证；代码、模型、字体与历史材料的使用许可需要分别确认，不能把仓库可见性视为统一商业授权。
