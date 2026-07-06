# CatLife Release APK 运行教程

生成日期：2026-07-06

## 1. 交付文件

- 优化 Release APK：`CatLife_MVP_Android_v0.1.0_release_optimized.apk`
- 核心代码包：`CatLife_core_code_package_20260706.zip`
- 本教程：`CatLife_release_run_tutorial_20260706.md`

桌面会保留同名拷贝，便于直接上传或安装测试。

## 2. Android 安装环境

推荐环境：

- Android 9.0 及以上设备，或官方 Android Emulator。
- 竖屏设备比例接近 20:9。
- 已安装 Android Platform Tools。

本机 adb 路径：

```powershell
C:\Users\fujunye\AppData\Local\Android\Sdk\platform-tools\adb.exe
```

## 3. 安装 APK

连接设备后执行：

```powershell
$adb = "C:\Users\fujunye\AppData\Local\Android\Sdk\platform-tools\adb.exe"
& $adb devices -l
& $adb install -r "C:\Users\fujunye\Desktop\CatLife_MVP_Android_v0.1.0_release_optimized.apk"
```

如果已有旧版本且安装失败，可先卸载：

```powershell
& $adb uninstall com.catlife.mvp
& $adb install "C:\Users\fujunye\Desktop\CatLife_MVP_Android_v0.1.0_release_optimized.apk"
```

## 4. 启动应用

```powershell
& $adb shell monkey -p com.catlife.mvp -c android.intent.category.LAUNCHER 1
```

预期启动表现：

1. 冷启动先显示白底 CatLife 猫咪开屏图。
2. 进入主界面后显示 20:9 竖屏小镇画面。
3. 顶部显示 CatLife 和今日专注真实统计。
4. 右侧显示猫咪、记录、旋转、设置按钮。
5. 点击“开始专注”后进入专注状态，普通按钮隐藏，底部显示向上滑动解锁控件。

## 5. 基础真机测试命令

清理并采集日志：

```powershell
& $adb logcat -c
& $adb shell monkey -p com.catlife.mvp -c android.intent.category.LAUNCHER 1
Start-Sleep -Seconds 8
& $adb logcat -d > "C:\Users\fujunye\Desktop\CatLife_release_logcat.txt"
```

截图：

```powershell
& $adb exec-out screencap -p > "C:\Users\fujunye\Desktop\CatLife_release_screenshot.png"
```

查看关键日志：

```powershell
Select-String -Path "C:\Users\fujunye\Desktop\CatLife_release_logcat.txt" -Pattern "CatLife|startup|focus_start|llm_request|llm_result|BlueLm|vivo_cloud"
```

## 6. 需要确认的功能点

- APK 能安装并启动。
- 冷启动显示 CatLife 白底猫咪开屏图。
- 主界面不是设置页，而是小镇主界面。
- 摄像头为低机位广场视角。
- 猫咪在主场景内可见并持续运行行为模块。
- 开始专注按钮可进入专注状态。
- 专注状态隐藏猫咪、记录、旋转、设置按钮。
- 解锁滑槽可退出专注状态。
- 设置页可显示并修改每轮专注分钟数、自动进入专注秒数。
- 日志中可看到本地识别、LLM 请求或本地回退状态。

## 7. 大模型与隐私说明

真实 APK 可携带云真机演示所需私有配置；核心代码包不包含真实 AppKEY。运行时会优先使用可用的大模型路径：

1. 端侧 BlueLM：通过 Unity `BlueLmOnDeviceClient` 调用 Android `BlueLmUnityBridge`。
2. vivo 云端 Demo：通过 `UnityWebRequest` 调用 `https://api-ai.vivo.com.cn/v1/chat/completions`。
3. 本地模板回退：当 SDK、网络、凭据或输出安全校验不满足时启用。

隐私边界：

- 不采集屏幕内容。
- 不读取输入框明文。
- 不上传跨 App 明文内容。
- 不收集联系人、账号、定位、支付信息。
- 大模型只接收聚合后的行为特征、专注状态和猫咪状态。

## 8. 从 Unity 重新构建 Release APK

在 Unity 中打开：

```text
C:\Users\fujunye\Desktop\Agent\05-AIGC\work\CatLife_Unity_Main
```

执行菜单：

```text
CatLife > Build > Build Android Release APK
```

构建策略会自动应用：

- Android Release
- IL2CPP
- ARM64
- ASTC
- Minify Release
- Managed Stripping Medium
- CatLife 白底猫咪原生开屏页

也可以用 Unity MCP 或批处理调用 `CatLife.Editor.CatLifeAndroidBuild.BuildApk`，输出路径建议使用：

```text
C:\Users\fujunye\Desktop\Agent\05-AIGC\06-deliverables\final-submission\CatLife_MVP_Android_v0.1.0_release_optimized.apk
```

