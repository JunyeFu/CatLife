# Android 当前回归证据

日期：2026-08-30

## 环境

- APK：`CatLife_MVP_Android_v0.1.0_release_optimized.apk`
- 设备：`emulator-5554`，Google Android Emulator，Android 15，1080 × 2424
- 包名：`com.catlife.mvp`
- Activity：`com.unity3d.player.UnityPlayerGameActivity`

## 操作与结果

1. 卸载旧包并流式安装当前 APK：成功。
2. 冷启动：Activity 启动成功；Unity 完整首绘约 33 秒后显示主页。
3. 进入专注：成功，显示计时与滑动解锁控件。
4. 向上滑动解锁：成功，日志出现 `focus_unlocked`。
5. 第二次进入专注：成功，日志再次出现 `focus_start`。
6. 返回桌面后重新启动：HOT 恢复，99 ms，仍回到 CatLife Activity 和专注画面。
7. 失败模式检索：SuspendedAppActivity、FATAL EXCEPTION、ANR、Activity pause/destroy timeout 均为 0 条。

## 截图

- `02-home-ready.png`：完整首绘后的专注画面。
- `03-unlocked.png`：滑动解锁后的主页。
- `04-focus-second.png`：第二次进入专注。
- `05-resume-focus.png`：返回桌面后热恢复。

## 大模型观察

- 当前 APK 能发起 vivo 云请求，并在云路径不可用时继续运行本地模板。
- 独立结构探针返回 HTTP 200，业务错误为 `400 no model access permission`。
- 本机没有 BlueLM AAR，ADB 只有 Google 模拟器，因此本轮不声明 vivo 云模型或端侧 BlueLM 已闭环。
