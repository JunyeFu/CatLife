# CatLife Android 打包与真机 QA 方案

日期：2026-06-29
范围：Unity 已准备好后，如何构建 APK、安装到 Android 真机、采集证据、判定是否可提交。

## 1. 当前前提

已存在的准备项：

- Unity 交接包：`06-deliverables/unity-handoff-20260629/`
- MVP 增量资产：`06-deliverables/unity-handoff-20260629/mvp-unity-assets/`
- Android 构建入口：`06-deliverables/unity-handoff-20260629/mvp-unity-assets/Assets/Editor/CatLifeAndroidBuild.cs`
- 动画猫 FBX：`06-deliverables/cat-animation-final-package-20260629/CatLife_cat_10_actions_final_state.fbx`

尚未证明的事项：

- 正式 Unity 工程是否已经合入小镇场景。
- APK 是否已成功生成。
- APK 是否可在真机安装、启动、完成专注流程。
- 真机 FPS、内存、发热和崩溃情况。

复赛官方约束：作品文件必须可运行，可以是 APK、快应用、智能体、小程序或 SDK 等形态。CatLife 当前选择 APK 路线，因此 APK 可安装启动是 P0 红线，不可用视频或 PPT 替代。

## 2. 官方依据

- Unity Android 构建流程：Unity 支持直接构建 APK，也可导出 Gradle 工程；测试设备可使用默认 debug signing，发布时才需要自定义签名。https://docs.unity3d.com/Manual/android-BuildProcess.html
- Unity Profiler：性能数据应通过连接到目标平台上的应用来采集，而不是只看编辑器。https://docs.unity3d.com/Manual/profiler-profiling-applications.html
- Android `adb screenrecord`：可录制设备屏幕为 MP4，默认最长 3 分钟，且不录制音频。https://developer.android.com/tools/adb#screenrecord
- Android vitals：核心质量关注 crash、ANR、过量唤醒/耗电等指标。https://developer.android.com/topic/performance/vitals

## 3. 构建配置

| 项 | MVP 建议 |
|---|---|
| Unity 版本 | 优先 `6000.4.9f1`，记录任何工程升级提示 |
| Scripting Backend | IL2CPP |
| Target Architecture | ARM64 |
| Build Type | Development Build 首轮；提交前 Release Build |
| Signing | 首轮 debug signing；最终提交如只交 APK 可继续 debug signing，但需备注不可上架 |
| Texture Compression | ASTC 优先；兼容低端机再评估 ETC2 |
| Scenes In Build | `startscene`, `mainscene`, `FocusScene` |
| Package Name | `com.catlife.mvp` |
| Version | `0.1.0` |

## 4. 推荐构建命令

在正式 Unity 工程根目录执行，输出到最终提交目录：

```powershell
D:\UnityEngine\6000.4.9f1\Editor\Unity.exe `
  -batchmode `
  -projectPath "C:\path\to\CatLifeUnityProject" `
  -executeMethod CatLife.Editor.CatLifeAndroidBuild.BuildApk `
  -outputPath "D:\Agent\AIGC innovation\06-deliverables\final-submission\CatLife-MVP-android-dev.apk" `
  -quit `
  -logFile "D:\Agent\AIGC innovation\06-deliverables\final-submission\android-build.log"
```

注意：上面路径中的 `C:\path\to\CatLifeUnityProject` 必须替换成真实 Unity 工程根目录。

## 5. 真机安装与日志命令

```powershell
adb devices
adb install -r "D:\Agent\AIGC innovation\06-deliverables\final-submission\CatLife-MVP-android-dev.apk"
adb logcat -c
adb shell monkey -p com.catlife.mvp 1
adb logcat -d > "D:\Agent\AIGC innovation\06-deliverables\final-submission\android-runtime-logcat.txt"
```

vivo 云真机路径：

1. 在网页端上传 APK 并安装。
2. 如需 ADB，使用云真机页面提供的 `adb connect <ip:port>`。
3. 用云真机自带性能监控、截图、录屏保存证据。
4. 记录云真机型号、系统版本、测试时间和是否有排队/释放情况。

录屏：

```powershell
adb shell screenrecord --bit-rate 8000000 --time-limit 180 /sdcard/catlife-demo.mp4
adb pull /sdcard/catlife-demo.mp4 "D:\Agent\AIGC innovation\06-deliverables\final-submission\catlife-demo-device.mp4"
```

截图：

```powershell
adb exec-out screencap -p > "D:\Agent\AIGC innovation\06-deliverables\final-submission\catlife-device-screenshot.png"
```

## 6. 真机 QA 表

| 编号 | 用例 | 操作 | 通过标准 | 证据文件 |
|---|---|---|---|---|
| A01 | 安装 | `adb install -r` | Success，无签名/ABI 错误 | `android-install.txt` |
| A02 | 冷启动 | 点击图标或 `monkey` | 5 秒内进入首屏或主界面 | 录屏/日志 |
| A03 | 主场景 | 进入 `mainscene` | 猫和小镇可见，无遮挡 | 截图 |
| A04 | 普通状态 | 默认等待 5 秒 | 猫 idle 呼吸或轻动作 | 录屏 |
| A05 | 过渡状态 | 触发专注开始 | 猫切到 sniff/listen 等动作 | 录屏 |
| A06 | 专注状态 | 等待状态机推进 | UI 降干扰，猫安静陪伴 | 录屏 |
| A07 | 奖励状态 | 完成会话 | 猫开心/尾巴动作，出现奖励反馈 | 录屏 |
| A08 | 退出 | 上滑或返回 | 可退出但不是误触退出 | 录屏 |
| A09 | 横竖屏 | 保持目标方向 | 不旋转破坏构图 | 录屏 |
| A10 | 稳定性 | 连续运行 3 分钟 | 无 crash、ANR、明显掉帧 | logcat |

## 7. 性能记录模板

| 项 | 数值 | 证据 |
|---|---:|---|
| 设备型号 | 待填 | `adb shell getprop ro.product.model` |
| Android 版本 | 待填 | `adb shell getprop ro.build.version.release` |
| APK 大小 | 待填 | 文件属性 |
| 冷启动耗时 | 待填 | 录屏计时 |
| 平均 FPS | 待填 | Unity Profiler / Game View Stats / 真机观察 |
| 峰值内存 | 待填 | Unity Profiler / Android Studio Profiler |
| Batches | 待填 | Unity Stats |
| Triangles | 待填 | Unity Stats |
| 3 分钟运行结果 | 待填 | logcat + 录屏 |

## 8. 失败定位

| 失败 | 优先检查 |
|---|---|
| `Target architecture not specified` | Android targetArchitectures 是否为 ARM64，构建脚本是否合入 |
| `No such method CatLife.Editor...` | `Assets/Editor/CatLifeAndroidBuild.cs` 是否在 Unity 工程中 |
| APK 安装失败 | ABI、minSdk、签名、设备存储、旧包冲突 |
| 启动黑屏 | 首场景、相机、脚本异常、缺少资源 |
| 猫不显示 | FBX 是否复制、`.meta` GUID 是否保留、Prefab 引用是否断开 |
| 小镇不显示 | 场景是否导入、Root 是否启用、摄像机 clipping/位置 |
| 卡顿 | 先禁用小镇装饰物，再查材质、贴图、阴影、粒子 |

## 9. APK 可提交定义

只有同时满足以下条件，才能标记 APK 通过：

- APK 文件存在于 `06-deliverables/final-submission/`。
- 至少一台 Android 真机可安装启动。
- 主场景可见小镇和动画猫。
- 至少一次完成普通、过渡、专注、奖励流程。
- 有录屏、截图、logcat 和构建日志。
- 文档明确写出 Unity 版本、设备型号、APK 文件名和已知限制。
