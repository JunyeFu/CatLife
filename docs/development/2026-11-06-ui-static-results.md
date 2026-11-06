# WP06.6 静态 UI 修复与 G1 验收

2026-11-06：**WP06.6.1–.4完成，G1通过；G2–G4待执行。** 实现留在`feat/ui-style-20261106`，main只同步文档。下一项WP06.7.1：逐字真实模糊与字形映射验证。

| 小任务 | 实现与实际检查 |
|---|---|
| .1 中性色与去描边 | 新增17张可切片皮肤，最大边256；保留原Figma素材。两份UI Prefab各49按钮，原锚点/位置/尺寸未改；原75/76个纯黑文本（含隐藏层）均清零，旧描边皮肤引用为0。按钮按下/禁用SpriteState也已接入新资源，避免按住时闪回旧皮肤；次级按钮压暗为中性色。 |
| .2 输入聚焦 | 命名、分钟输入使用内部双环/软内阴影，失焦关闭；叠层不拦截输入。实际Game view读过选中/失焦图，交互用例通过。Android软键盘随G3同包检查，未以Editor代签。 |
| .3 暗底字重 | 根据实际祖先底色识别FocusTitle、TimerText、TransitionText、DebugText四角色；500字库换成460实例，保留既有FontStyle。App统一字体绑定后重新应用角色，测试确认不会被启动覆盖。官方Noto源裁切后152144字节/390码点，原17MB字库在Assets外；数字advance均564/1000em，Unity中数字等宽且固定/调试文案有字形。 |
| .4 排版与状态 | 保持字体大小、按钮居中、安全区及既有滚动。会后正文仅在自动末行孤字时调整显示断行，原串和显式换行保留；运行时选项/成长赋色使用序列化中性色，不回写纯黑。短屏开关图片保持比例，点击热区不缩小。 |

## 验证证据

- `Reports/agent-runs/ui-style-static-g1-20261106/`：最终13/13，其中**12项功能/资源回归+1项按钮状态图采集**；不把截图采集当成业务用例。
- `ui-style-static-final-20261106`：幂等回归+七页/输入双画幅采集2/2；`ui-style-settings-bottom-20261106`补设置底部采集1/1。不累加重复用例宣称额外覆盖。
- `Reports/ui-style-20261106/WP06.6/editor-final/`：1080×2424、720×1280七页、两个输入状态、设置底部及按下/禁用，共24张原PNG，均已解码；七页联系表与相关原图已实际读取。数据为明确合成的本地会话，场景为实际UiStyleCandidate，不是新Android包或云端成功证据。
- `prefab-geometry-and-color.json`记录两份Prefab的49→49、按钮几何零变化和纯黑清零；`font/font-preparation.json`保存字形/实例参数。原r41 Focus截图与新Game view已对照，小镇/猫/摄像机未替换。
- 本批日志`[CatLife] llm_request`为0；私有配置及meta已恢复原位置，值未读取或输出。没有构建/安装新APK，没有改Android r41/MVP存档、Session键或记录格式。

## 修复过程中保留的失败

首轮相关用例11/11。随后四暗底角色扩展后一次12项中10过、2失败：重复安装把深灰误归为次级灰；旧截图工具给Standalone组设值，实际得1080×2160。已修颜色幂等并补原按钮几何回归，截图按当前Game view组取值；最终正确尺寸图已读取。短屏开关拉伸与按钮SpriteState旧引用均在提交前修复，并补实际图片。

字库许可证使用派生专名`OFL-CatLifeUI-Dark460.txt`，避免将来合并覆盖主工作树已有的原OFL及meta。原始许可证与全部美术文件保持。

## 下一门

WP06.7先查真实uGUI网格与Unicode元素映射，再实现90ms/30ms、6设计px的真实模糊。继续保持会后单次完整请求；同时完成唯一可见Progress的1200/80弹簧、松手65%及猫爪接触。G2通过后才构建一个独立r42，再验Android键盘、功能、录像及10分钟运行。

技术参考：[Google Fonts Noto Sans SC](https://github.com/google/fonts/tree/main/ofl/notosanssc)、[fontTools裁切文档](https://fonttools.readthedocs.io/en/latest/subset/index.html)。此次字体实例与显示结果的结论来自本地TTF和Unity验证，不以文档存在替代运行。
