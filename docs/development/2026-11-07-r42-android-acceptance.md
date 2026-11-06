# r42 Android 实测与修复候选

2026-11-07：**G3尚未通过。** 首个r42已构建并安装；实际检查发现键盘遮挡与结果切换绘制停帧，保留失败包/原图/原片，修复候选为r42b。不能把Unity通过或旧r41证据当成新Android通过。

## 首包事实

- `com.catlife.review.uistyle`，0.3.0/code3；实际87,589,906字节、83.53MiB，Release构建0错误；Manifest六项检查通过。
- 构建依赖中私有配置不存在/Resources不可读，构建后源配置及meta恢复；没有实际模型请求。完整空岛229,286三角面、16材质、167 ID验证通过。
- 专用CatLifeInterview20261008，1080×2424/420；飞行模式开启、Wi-Fi关闭。MVP与r41包/数据未覆盖。首帧白屏后加载了真实小镇/领取页，原白屏与后续图均保留，不将Activity返回时间冒称完整UI可用时间。

## 实际失败与修改

| 问题 | 证据及处理 |
|---|---|
| 命名框被软键盘遮住 | `r42/device/1080/name-keyboard-visible.png`。双环可见，但键盘覆盖输入位置。新增被动键盘避让：仅编辑时抬升输入父卡片，保证整个输入框高于IME，收起/离页/后台恢复。普通按钮坐标和输入所有权不变。 |
| 结果页绘制停帧 | 隐藏评审分钟自然到时附近，EGL连续帧记录停止于16:58:26；结果卡未完整出现。请求100秒的原片实际78.29秒/1017帧，已解码并读片尾，不能判完成。证据把卡点缩到会后字显首次绘制附近，尚未证明驱动内部根因。候选保留64点质量，将采样坐标移至CPU预计算，GPU使用有统一边界的短循环，去掉大段展开和GPU log/sin/cos；把所需Canvas顶点通道提前序列化，避免首次字显重建整个Canvas；增加无正文/凭据的字显起止事件，供这次复测定位。16点试验出现格纹，未采用。 |
| 隐藏评审层对比不足 | 实际评审图标题灰暗且面板沿用旧描边。接入中性面板，并补DebugTitle为第五个暗底460角色及亮字；普通布局不移动。 |

[Unity说明](https://docs.unity3d.com/ja/6000.0/ScriptReference/TouchScreenKeyboard-inputFieldAppearance.html)确认GameActivity始终隐藏原生键盘输入栏；[键盘区域接口](https://docs.unity3d.com/jp/current/ScriptReference/TouchScreenKeyboard-area.html)在Android返回空矩形。本轮使用[Android WindowInsets IME](https://developer.android.com/reference/android/view/WindowInsets.Type#ime())读取停靠键盘；API29使用可视窗口框，保留Android10范围，没有迁移Activity或引入兼容框架。

## 当前验证与下一项

键盘避让首轮13/13通过（含两画幅×两个输入、变化高度、不累积位移、失焦恢复）。五暗底角色与字显相关18/18通过；最终64点短循环及预先序列化顶点通道的字显补测5/5通过，0/30/90/270ms及裁切原图重新读取。以上都是Unity，不是Android修复通过。

r42b独立目录保存，沿用同一个隔离包名，旧r42原包保留。下一项在r42b先验真实键盘和会后绘制，再完成同包七页、普通准备→到位→趴卧→专注、离线奖励/中断/恢复、至少10分钟及用户视觉确认。r42本轮由Agent生成的测试存档可重置以验证新候选首次领取；不清MVP/r41。G4再换真实候选图，保留r41真实MiMo版本及来源。

原始证据位于本机`F:/AgentTools/CatLifeBuilds/ui-style-20261106/r42/`，修复过程与Unity结果在`Reports/ui-style-20261106/WP07.4/`。此文随实际结果更新；没有全功能或真机完成声明。
