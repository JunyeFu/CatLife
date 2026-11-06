# WP06.5 基线与样式基础交付

日期：2026-11-06。**WP06.5.1–.3 完成，G0 通过；G1–G4 未执行。** 下一项为 WP06.6 静态 UI。执行依据：[增量开发控制](2026-11-06-ui-style-repair-plan.md)。

## 已实际完成

| 小任务 | 结果与证据 |
|---|---|
| WP06.5.1 通道与数据 | 确认专用 AVD `CatLifeInterview20261008`、ADB 54036、控制 5556/5557。保留 data 重启后，boot_completed=1、package/activity 可用、user0、1080×2424/420dpi；原 `com.catlife.review.ui` 0.3.0(3) 冷启动成功。读取实际主页、设置及成长页，记录14、成长10、爪印10；AI关、自动专注关、本地统计开。没有开始会话或清除数据。 |
| WP06.5.1 开发隔离 | 从 main `781e94dc` 建立 `feat/ui-style-20261106`，工作区 `F:/AgentTools/CatLifeBuilds/ui-style-20261106/source`。后续验证复用 `repair-20261105-handoff/restored-project`；App、ExitGesture 和实际 Figma Prefab 三个关键文件与当前源一致，没有复制 Library。 |
| WP06.5.2 样式与字体 | 建立唯一 `CL_UI_StyleTokens.json`：中性色、品牌强调、双环参数、90/30ms/6px字显、1200/80及15/35/65%手势参数。导出静态460字体136,480字节，固定“专注中0123456789:”无缺字，数字 advance 全为564/1000em；保留OFL与派生元数据。 |
| WP06.5.3 风格参考 | 现有Chrome无窗口渲染并实际打开参考图。两张r41原截图保持不变，旁列拟用表面、输入和500→460实际字形。浏览器确认两个字体加载、460实际字重、原图1080×2424、无横向溢出。参考不是r42运行截图或视觉签收。 |

本轮只准备了参数、字体和参考，**没有修改运行脚本、Prefab、场景或构建APK**。资源与生产脚本在[独立开发分支](https://github.com/JunyeFu/CatLife/tree/feat/ui-style-20261106)，未提前合入main；main仅同步本轮状态文档。真实Unity导入和暗底角色绑定仍在G1。

## 故障与边界

- 起初shell可用而package/activity缺失，崩溃缓冲区含audioserver超时。一次保留数据的`adb reboot`客户端12秒超时，但随后设备实际重启、服务恢复。启动期出现System UI无响应对话框，通过当前UI树中的Wait恢复，之后主页与页面路由可用。不能据此宣称模拟器无ANR；G3的同包10分钟观察仍未执行。
- 安装包不是debuggable；`run-as`被系统拒绝后没有绕过私有数据访问。这里证明的是页面显示的记录/成长/设置与既有基线一致，未读取完整存档JSON。
- 第一张标为settings的截图仍是主页；后续读取实际页面并保留了这份失败证据。后续输入须先确认目标页面已渲染，不能把截图文件名当成状态断言。
- 成长页的`<b>O</b>`是既有昵称测试数据，按纯文本显示符合IA-07契约，**不是新缺陷**。见[此前R3修复证据](../audits/2026-11-05-repair-results.md)。不解析用户昵称为富文本，不修改该存档。
- 本次进程日志`[CatLife] llm_request`为0条，AI开关关闭；没有构建、安装或发送新模型请求。读取完成后已force-stop原review包，专用AVD与54036保留，其他ADB/设备未操作。

## 产物与复用

本地证据根为`D:/Agent/catlife/Reports/ui-style-20261106/`：

- `WP06.5.1/`：environment-before/recovered、recovery-action、reboot-probe、data-baseline，以及实际UI树、输入点与主页/设置/成长截图。
- `WP06.5.2/`：font-preparation.json、foundation-reference.html。
- `WP06.5.3/`：foundation-reference.png、reference-render.json；实际字体加载和参考画面已读取。

`tools/ui/prepare_style_foundation.py`消费同一token，原变量字体、Medium字体及截图均为只读输入；使用已有fontTools环境，未安装依赖或下载字体。完整字库与预览不进入运行依赖；460派生字库目前也未被任何Prefab引用。

当前仅给FocusTitle、TimerText定义460角色；其他暗底文本需在WP06.6.3按实际底色/字形识别，不批量替换所有白字。App统一字体覆盖与准备页运行时纯黑回写仍待修；新字体导入、两输入聚焦、七页及短屏实装结果均不得预记通过。

下一项 **WP06.6.1**：让皮肤生成/安装入口消费token，替换中性表面、硬描边和纯黑文本；保持按钮几何、层级、GUID与业务引用。随后依次完成双环、字体绑定和排版，取得G1实际画面。
