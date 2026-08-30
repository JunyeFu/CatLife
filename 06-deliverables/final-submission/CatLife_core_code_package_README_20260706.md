# CatLife 核心代码包说明

生成日期：2026-07-06

本代码包用于提交“核心功能实现”材料，不包含小镇场景、模型、贴图、动画、材质、Unity Library、APK 构建缓存和私有凭据。

## 包含内容

- `Assets/Scripts/LLM/`：大模型接入、提示词、隐私网关、云端 Demo 回退、端侧 BlueLM 适配。
- `Assets/Scripts/Recognition/`：用户行为事件、实时特征提取、识别快照和 Android 行为桥。
- `Assets/Scripts/Cat/`：猫咪行为驱动、导航、动画偏好、行为记忆、兴趣点和 LLM 行为解释。
- `Assets/Scripts/UI/`：主界面、专注状态、解锁滑槽、猫咪离屏提示和反馈气泡。
- `Assets/Scripts/SceneInteraction/`：可交互场景点、气泡联动和交互记忆。
- `Assets/Scripts/Camera/`：广场相机旋转控制。
- `Assets/Configs/`：行为事件、LLM 输出、BlueLM 请求/响应 schema，以及云端凭据示例文件。
- `Assets/Plugins/Android/`：Android Manifest、BlueLM Java 桥、SDK 反射适配层、权限辅助和 Unity 回调。
- `Assets/Editor/`：Release 构建、体积优化策略、运行时装配验证脚本。

## 明确排除内容

- `Assets/Scenes/`
- `Assets/Art/`
- `Assets/Materials/`
- `Assets/UI/`
- `Assets/Resources/CatLifePrivate/`
- `.fbx`、`.glb`、`.blend`、`.png` 大图资源、视频、Unity `Library/`

真实 APK 可以包含云真机演示所需的私有配置；本代码包不包含真实 AppKEY，只保留 `Assets/Configs/vivo_cloud_credentials.example.json` 作为格式说明。

## 大模型调用链重点

### 1. Unity 侧统一接口

入口接口：`Assets/Scripts/LLM/ICatLLMClient.cs`

猫咪行为系统通过接口请求大模型建议，不直接依赖某一种模型实现。核心调用方是：

- `Assets/Scripts/Cat/CatBehaviorDriver.cs`
- `Assets/Scripts/Cat/CatBehaviorTelemetry.cs`
- `Assets/Scripts/UI/CatLifeHomeUiController.cs`

这些模块把识别结果、专注状态、猫咪状态、场景上下文整理成 `CatPromptContext`，再交给 LLM 客户端。

### 2. 隐私网关

关键文件：`Assets/Scripts/LLM/PrivacyGateway.cs`

所有发往大模型的上下文先经过隐私处理，只保留聚合后的专注时长、打断次数、状态标签、动作偏好等低敏特征，不上传输入内容、跨 App 明文内容、联系人、账号、定位或支付信息。

### 3. 提示词与结构化请求

关键文件：

- `Assets/Scripts/LLM/CatPromptBuilder.cs`
- `Assets/Scripts/LLM/CatPromptContext.cs`
- `Assets/Scripts/LLM/BlueLmUnityRequest.cs`
- `Assets/Configs/bluelm_unity_request_schema.json`
- `Assets/Configs/bluelm_catlife_feedback_schema.json`

提示词要求大模型只输出结构化建议，包括：

- `message`：低压力提醒语。
- `cat_state`：推荐猫咪动作状态。
- `movement_bias`：靠近用户、回到镜头范围、远离镜头、原地等待等行为倾向。
- `task_return_hint`：任务回归建议。
- `safety_flags`：安全与隐私状态。

### 4. 端侧 BlueLM 路径

当前代码包交付的是 Unity/Java 桥接源码；未包含官方 `llm-sdk-release.aar`。截至 2026-08-30，本机也没有可用于验收的 vivo 真机，因此本节描述实现接口，不代表端侧模型已完成真机闭环。

Unity 侧：

- `Assets/Scripts/LLM/BlueLmOnDeviceClient.cs`
- `Assets/Scripts/LLM/BlueLmCallbackReceiver.cs`
- `Assets/Scripts/LLM/BlueLmAndroidEvent.cs`

Android 侧：

- `Assets/Plugins/Android/src/main/java/com/catlife/bluelm/BlueLmUnityBridge.java`
- `Assets/Plugins/Android/src/main/java/com/catlife/bluelm/BlueLmEngine.java`
- `Assets/Plugins/Android/src/main/java/com/catlife/bluelm/BlueLmSdkAdapter.java`
- `Assets/Plugins/Android/src/main/java/com/catlife/bluelm/BlueLmJsonGuard.java`
- `Assets/Plugins/Android/src/main/java/com/catlife/bluelm/BlueLmPromptBuilder.java`

调用流程：

1. `BlueLmOnDeviceClient` 生成 `BlueLmUnityRequest`。
2. Unity 通过 `AndroidJavaClass` 调用 `BlueLmUnityBridge.generate(...)`。
3. Java 层 `BlueLmEngine.generateJsonAsync(...)` 校验请求 JSON。
4. `BlueLmSdkAdapter` 通过反射适配 vivo/BlueLM SDK 的初始化与生成方法。
5. 输出经过 `BlueLmJsonGuard.validateOutput(...)` 校验。
6. `BlueLmUnityCallback` 回调 Unity 的 `BlueLmCallbackReceiver.OnBlueLmEvent(...)`。
7. Unity 解析为 `LLMBehaviorSuggestion`，交给猫咪行为驱动模块。

### 5. 云端 Demo 路径

关键文件：

- `Assets/Scripts/LLM/VivoCloudDemoConfig.cs`
- `Assets/Scripts/LLM/MockCatLLMClient.cs`
- `Assets/Configs/vivo_cloud_credentials.example.json`

云端 Demo 使用 `UnityWebRequest` POST 到 `https://api-ai.vivo.com.cn/v1/chat/completions`。真实凭据在 APK 的私有资源目录中读取；代码包只保留 example 文件。请求失败、凭据缺失或输出不安全时，会回退到 `LocalTemplateFallback`，保证演示流程不中断。

### 6. 猫咪行为联动

关键文件：

- `Assets/Scripts/Cat/CatBehaviorDriver.cs`
- `Assets/Scripts/Cat/CatBehaviorBrainScorer.cs`
- `Assets/Scripts/Cat/CatLlmBehaviorInterpreter.cs`
- `Assets/Scripts/Cat/CatNavigationAgent.cs`
- `Assets/Scripts/Cat/CatAnimationController.cs`

LLM 只提供行为建议，不直接移动猫咪。最终执行由本地状态机、NavMesh、禁走区、安全守卫和动画控制器决定，避免穿模、瞬移和隐私越界。
