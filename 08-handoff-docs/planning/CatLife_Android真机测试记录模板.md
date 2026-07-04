# CatLife Android 真机测试记录模板

日期：2026-06-29
用途：APK 构建完成后，按同一格式记录安装、启动、主流程、性能和证据文件。没有这张表，不应宣称 APK 已通过。

## 1. 测试环境

| 项 | 值 |
|---|---|
| 测试日期 | 待填 |
| 测试人 | 待填 |
| Unity 版本 | 待填 |
| APK 文件 | 待填 |
| APK SHA256 | 待填 |
| 设备型号 | 待填 |
| Android 版本 | 待填 |
| 测试方式 | 本地真机 / vivo 云真机 |
| 是否录屏 | 待填 |
| logcat 文件 | 待填 |
| vivo 云端 API AppID | `2026414599` |
| APK 是否包含本机私密 AppKEY | 是 / 否 / 不适用 |
| 代码包是否排除明文 AppKEY | 待填 |

## 2. ADB 基础命令

```powershell
adb devices
adb install -r "06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk"
adb logcat -c
adb shell monkey -p com.catlife.mvp 1
adb logcat -d > "06-deliverables/final-submission/android-runtime-logcat.txt"
adb logcat -d | Select-String "CatLife|vivo|LLM|fallback" > "06-deliverables/final-submission/vivo-cloud-llm-logcat.txt"
```

推荐使用 Stage9 采证脚本统一保存证据：

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/collect-stage9-android-evidence.ps1 `
  -ApkPath "06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk" `
  -CloudAdbEndpoint "<vivo cloud adb ip:port>"
```

脚本输出目录：

```text
06-deliverables/final-submission/evidence/android/
  00-build/private_config_presence_redacted.txt
  00-build/apk-sha256.txt
  01-install/adb_devices.txt
  01-install/install.log
  02-startup/logcat_startup.txt
  03-llm/logcat_vivo_cloud_llm.txt
  03-llm/logcat_bluelm_init.txt
  03-llm/logcat_bluelm_generate.txt
  04-focus/logcat_5min_focus.txt
  05-summary/stage9_cloud_phone_result.md
```

注意：`private_config_presence_redacted.txt` 只能记录私有配置存在、是否被 `.gitignore` 忽略、AppID 和 AppKEY 是否存在，不得记录完整 AppKEY。

设备信息：

```powershell
adb shell getprop ro.product.model
adb shell getprop ro.build.version.release
adb shell dumpsys package com.catlife.mvp | Select-String version
```

## 3. 测试用例

| 编号 | 用例 | 步骤 | 通过标准 | 结果 | 证据 |
|---|---|---|---|---|---|
| T01 | 安装 | `adb install -r` | 返回 Success | 待填 | install log |
| T02 | 冷启动 | 点击图标或 monkey | 5 秒内进入首屏/主场景 | 待填 | 录屏 |
| T03 | 主场景 | 进入 mainscene | 猫和小镇可见 | 待填 | 截图 |
| T04 | 普通状态 | 等待默认状态 | 猫 idle/轻动作正常 | 待填 | 录屏 |
| T05 | 过渡状态 | 触发专注入口 | 猫切换到过渡动作 | 待填 | 录屏 |
| T06 | 专注状态 | 等待状态推进 | UI 降干扰，猫安静陪伴 | 待填 | 录屏 |
| T07 | 奖励状态 | 完成会话 | 出现奖励反馈 | 待填 | 录屏 |
| T08 | 退出流程 | 上滑/返回 | 可退出，无误触退出 | 待填 | 录屏 |
| T09 | 横竖屏 | 保持目标方向 | 构图不破坏 | 待填 | 录屏 |
| T10 | 稳定性 | 连续运行 3 分钟 | 无 crash/ANR | 待填 | logcat |
| T11 | vivo 云端大模型调用 | 进入主场景并等待 LLM 刷新周期，或触发猫咪/专注状态变化 | logcat 能显示真实调用成功，或显示失败原因并 fallback | 待填 | vivo-cloud-llm-logcat |
| T12 | 密钥安全 | 检查 GitHub/代码包，不检查 APK 内部私密配置 | 代码包无明文 AppKEY；真实版/最终提交 APK 可包含云真机可用 key | 待填 | git grep / 手动记录 |

## 4. 性能记录

| 指标 | 值 | 证据 |
|---|---:|---|
| 冷启动耗时 | 待填 | 录屏计时 |
| 平均 FPS | 待填 | Profiler/Stats/云真机性能监控 |
| 峰值内存 | 待填 | Profiler/Android Studio/云真机性能监控 |
| Batches | 待填 | Unity Stats |
| Triangles | 待填 | Unity Stats |
| APK 大小 | 待填 | 文件属性 |
| 3 分钟运行结果 | 待填 | logcat |
| 大模型调用结果 | 成功 / fallback / 未触发 | vivo-cloud-llm-logcat |

## 5. 结论

| 项 | 结论 |
|---|---|
| 是否可用于录制演示视频 | 待填 |
| 是否可作为最终 APK 候选 | 待填 |
| 必须修复的问题 | 待填 |
| 可接受的已知限制 | 待填 |
