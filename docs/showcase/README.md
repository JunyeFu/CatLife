# 展示素材

[产品主页](../../README.md)

| 文件 | 来源 | 范围 |
|---|---|---|
| hero-r42b.png | layout.html新样式＋r42b主页/专注实录 | 中性色层、无硬描边容器；应用截图内容不改写 |
| journey-r42b.png | r42b准备、专注、离线完成图 | 同一候选普通1分钟，结果明确为本地洞察 |
| ai-history-r41.png | 新展示样式＋原ai-reward-r41.png | 明确标注r41历史真实MiMo，不冒称新候选云请求 |
| home/setup/focus/reward/records/growth/settings-r42b.png | r42b模拟器1080×2424真实七页 | 等比缩为540×1212，未合成界面或姿态；原尺寸留本地 |
| session-r42b.gif | r42b普通分钟原片第2秒起约22秒，8fps、320px宽 | 真实准备、到位与趴卧；只缩放和抽帧 |
| style-tokens.css、fonts/ | 与Unity同源token；官方Noto Sans SC裁切变量字体及OFL | 展示排版字体wght 100–900，暗底角色减40，数字等宽；截图中的字体不替换 |
| cover.svg | 仓库编写的品牌排版 | 封面，非运行画面 |
| hero-r41.png | layout.html品牌首屏＋r41主页/专注原图 | 商业展示排版，手机边框是装饰；应用截图内容不修改 |
| journey-r41.png | layout.html三步流程＋r41准备/专注/离线结果原图 | 同包真实流程，1分钟是测试时长 |
| ai-r41.png | layout.html可选AI说明＋r41短屏AI结果原图 | 同意、单次请求、聚合值及本地动作映射 |
| home-r41.png | r41模拟器1080×2424原图 | 主页 |
| setup-r41.png | r41同包、统计关闭的手动准备 | 当前1分钟测试时长，预设按钮保留 |
| focus-r41.png | r41同包杀进程恢复后的原图 | 趴卧和原截止倒计时 |
| reward-r41.png | r41同包离线完成原图 | 未采集统计，成长/爪印各+1 |
| ai-reward-r41.png | r41同包720×1280普通分钟原图 | 一次MiMo2.6-flash成功；完整建议通过上下滚动查看 |
| session-r41.gif | r41 `device/720/cloud/cloud.mp4`第2–30秒，8fps、320px宽 | 真实准备到专注片段，不代替完整会话验收 |

以上截图保留原画面内容；GIF只缩放/抽帧，不合成姿态、UI或结果。r41与历史r33/r34素材保留供旧记录引用。原片与完整证据留在本地；此处仅放公开展示需要的轻量文件，不含APK、私有配置或运行模型包。2026-11-07本人已接受r42b本轮UI与字显实际视觉，未扩大为真机或真人收益验收。

## 维护展示排版

`layout.html`是三张展示图的可编辑源文件，可在浏览器中打开，并用`?board=hero`、`?board=journey`、`?board=ai`切换画板。图中应用界面为现有PNG原图，通过缩放、边框和排版展示；不改写按钮、文字、数据或猫咪姿态，不是新的APP版本。

先用`tools/showcase/prepare-style.py --font-source <本地官方变量字体> --report-dir <本地结果目录>`生成小型WOFF与同源色值，再在已有Playwright与Chrome的Node环境中执行`node tools/showcase/render-showcase.cjs`更新三张PNG。工具不安装依赖，也不启动Unity或调用模型。GitHub原生字体、按钮与details交互由平台控制，网页排版CSS只用于生成展示图，不宣称控制整个GitHub页面。
