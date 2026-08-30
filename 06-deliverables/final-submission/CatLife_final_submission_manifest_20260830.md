# CatLife 最终提交清单

日期：2026-08-30

本清单以文件可消费性和当前运行证据为准，不使用哈希校验。

## 五件正式交付物

| 类别 | 文件 | 大小 | 基本验收 |
| --- | --- | ---: | --- |
| Android APK | `CatLife_MVP_Android_v0.1.0_release_optimized.apk` | 425,788,640 字节 | 可作为 ZIP 打开；包含 `AndroidManifest.xml`、`classes.dex`、ARM64 `libunity.so` 与 Unity 数据；2026-08-30 在 Android 15 模拟器完成干净安装和运行回归 |
| 演示视频 | `CatLife_作品演示视频_v1.mp4` | 102,821,661 字节 | MP4/isom 容器；时长 2:52.502 |
| 作品介绍 PPT | `CatLife_作品介绍PPT_最终提交版.pptx` | 83,143,199 字节 | PPTX 可打开为标准包；23 页、45 个媒体对象 |
| 作品海报 | `CatLife_作品海报_最终提交版.pdf` | 13,517,929 字节 | PDF 1.6 文件头与 EOF 标记完整 |
| 核心代码包 | `CatLife_core_code_package_20260706.zip` | 204,049 字节 | ZIP 共 193 项；含 72 个 C#、8 个 Java 和 3 个 README 文件 |

以上五件文件统一位于本目录。早期 PPT 与 PNG 海报继续保留，但不作为本清单指定的正式版本。

重复执行无哈希硬门：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/final-submission/check-five-deliverables.ps1
```

## 当前运行门

- Unity 6000.4.9f1 批处理运行时装配验证通过：主场景、NavMesh、猫行为、识别/LLM、Android 桥、开屏、配置 schema、UI 和 11 个 Animator 状态均进入验证。
- Android 15 Google 模拟器完成卸载重装、冷启动、进入专注、滑动解锁、第二次进入专注、返回桌面和热恢复。
- 热恢复仍回到 `com.catlife.mvp/com.unity3d.player.UnityPlayerGameActivity`；本轮检索未出现 SuspendedAppActivity、FATAL EXCEPTION、ANR 或 Activity 超时。
- 证据位于 `evidence/android/09-current-regression-20260830/`。

## 大模型能力边界

- 当前可运行能力：本地模板。
- vivo 云端：2026-08-30 实测 HTTP 200，业务错误 `400 no model access permission`；不声明当前模型授权可用。
- 端侧 BlueLM：代码包含 Unity/Java 桥接骨架，但本机没有官方 AAR，ADB 也没有 vivo 设备；不声明端侧真机闭环。
- 2026-07-05 的 vivo 云端 200 日志属于历史证据，不替代当前授权验证。

## Git 边界

APK、视频、PPTX 和 ZIP 按仓库规则作为本地交付物保存，不进入 Git。文档、代码和本轮 Android 证据可以独立提交；Git 推送成功不等于上述二进制已经发布到远程制品存储。
