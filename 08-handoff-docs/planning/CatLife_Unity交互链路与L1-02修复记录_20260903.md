# CatLife Unity 交互链路与 L1-02 修复记录

日期：2026-09-03
状态：`L1-02_IN_PROGRESS`

## 1. Unity 交互方式调查

当前工程安装 `com.coplaydev.unity-mcp`，包内支持两类交互：

1. 实时 MCP：Unity Editor Bridge 默认从 6400 端口起，连接本地 MCP HTTP Server（默认 6500），再由 Codex MCP 客户端调用场景、对象、脚本、测试、Console 和截图工具。
2. 项目本地批处理：通过 Unity 6 的 `-executeMethod`、`-runTests` 和构建入口直接操作同一工程。

本机具备 `uv`/`uvx`，Unity MCP 包也能在启动日志中发现工具；但当前 Codex 的 `config.toml` 没有 `unityMCP` 服务项，本任务工具注册表中也没有 Unity MCP 工具。该客户端配置不能在当前任务中热加载，且项目规则禁止未经授权修改全局 Codex 配置。因此本轮使用项目本地批处理链路，并保留实时 MCP 的启用步骤：在 Unity 的 `Window > MCP for Unity` 中启动 Bridge、配置 Codex，然后重启 Codex。

## 2. 已修复内容

- 正式移动场景新增 `CatLifeNavigation`，NavMesh 数据独立保存为 `CL_NAV_Mobile.asset`。
- 建立 MainPlaza、左右花园和前路四块受限可走区域。
- 建立 HomeFront、LeftGarden、RightGarden、FrontPath 四个兴趣点。
- 猫接入 `NavMeshAgent`、`CatNavigationAgent`、`CatNavMeshSafetyGuard` 和唯一 `CatBehaviorDriver`。
- Normal 状态由行为驱动器自主漫游；Transition、Focus、Reward 由固定主链动画接管，避免同时写 Animator。
- Animator 映射改为 FBX 中真实存在的 16 个 clips，包含 Walk、9 个旧动作和 6 个主链动作。
- 修复规划器以视觉 Transform 高度计算路径导致起点不在 NavMesh 的问题：路径计算前投影起点。

## 3. 测试证据

- 红灯 1：正式场景缺少 `CatNavigationAgent`。
- 绿灯 1：公开 `TryMoveTo` 接受 LeftGarden，猫产生至少 0.2m 实际位移。
- 红灯 2：正式场景缺少 `CatBehaviorDriver`。
- 诊断：残留 Focus 存档会正确关闭普通态漫游；清理测试数据后 Normal 状态自主移动。
- 绿灯 2：无需用户移动命令，猫在 1 秒内产生至少 0.2m 实际位移，Animator 含真实 Walk 状态。
- 完整 EditMode：10/10 通过。
- 完整 PlayMode：9/9 通过。

## 4. 未完成与下一顺序

G2 尚未通过：

1. `CL_CAT_Runtime.fbx` 缺少 `CL_CAT_IdleBreath_v06_headsync_loop_108f`；当前普通态停驻明确使用真实 `SitIdle`，没有伪造动作名。
2. 建筑禁走区和岛边安全覆盖尚未完成。
3. 尚未验证连续三个不同兴趣点和“移动—到点动作—继续移动”。
4. Transition、Focus、Reward 的动作切换仍需真实运行质量检查。
5. 模拟器 5 分钟录屏尚未执行。

下一步先从旧完整动作 FBX向派生运行 FBX补入 IdleBreath，再建立建筑禁走区和三点巡游测试；上述通过后才进入模拟器观察。
