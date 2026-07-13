# 星渊誓约 · Xingyuan Covenant

> 回合制网格战棋 RPG · MVP 验证版本：断裂点三号  
> Unity 6.5 (6000.5.3f1) · URP · C# · 纯架构分层

《星渊誓约》是一款原创幻想题材、回合制网格战棋 RPG。本仓库包含 MVP 纵切版本"断裂点三号"，覆盖移动 / 攻击 / 相位翻转 / 坠落 / 挤压 / 锚点围区 / 引力律令 / Replay / Undo / 防守→撤离 完整胜负闭环。

## 1. 项目结构

```text
Assets/Starfall/
  Core/        # 纯 C# 战斗逻辑，无 Unity 引用
  Data/        # JSON 定义 / 加载 / 校验
  Unity/       # Bootstrap / Presenter / 输入 / HUD
  Tests/EditMode/  # 179 个 EditMode 测试
Assets/StreamingAssets/data/battle_default.json  # 默认战斗配置
Docs/          # 项目文档（01 GDD、02 技术、03 数据、04 路线、05 测试）
docs/          # 交付文档（IMPLEMENTATION_STATUS / KNOWN_LIMITATIONS / MANUAL_ACCEPTANCE_CHECKLIST）
docs/OPENCLAW_REPOSITORY_AUDIT.md  # 完整审计记录（任务包 / 决策 / 风险）
docs/ADR/      # 架构决策记录
```

## 2. 架构分层硬约束

- `Starfall.Core`：纯 C#，**不引用 UnityEngine / UnityEditor**；所有战斗状态变化必须通过 `ICommand`。
- `Starfall.Data`：负责 Definition / JSON 加载 / 校验；不读取场景对象。
- `Starfall.Unity`：只负责启动 / 输入 / 表现 / 状态同步；Presenter **不持有 battle state**。
- `Starfall.Tests.EditMode`：EditMode 测试，验证 Core 行为 + Presenter 同步。

完整约束见 [Docs/02_Technical_Development_Manual.md](Docs/02_Technical_Development_Manual.md) 与 [AGENTS.md](AGENTS.md)。

## 3. 快速开始

### 环境要求

- Unity Hub + Unity 6.5.3f1（6000.5.3f1）
- Windows PC
- URP（Universal Render Pipeline）

### 在 Unity 中打开

1. 启动 Unity Hub → Add → 选择本仓库根目录
2. 等待 Package Manager 解析（首次 5-10 分钟）
3. 切换到 6000.5.3f1 Editor
4. 打开 `Assets/Starfall/Unity/BattleBootstrap.cs` 作为入口

### 运行战斗

1. 创建空场景 → 挂 `BattleBootstrap` 组件到任意 GameObject
2. 确认 `Assets/StreamingAssets/data/battle_default.json` 存在（默认 8×10 + 4 Player + 6 Enemy）
3. Play 模式启动
4. 控制：
   - `M` 进入移动模式，选合法格 → 单位移动
   - `A` 进入攻击模式，选邻格敌对单位 → 伤害生效
   - `F` 进入相位翻转模式，选邻格 → 翻转相位
   - `D` 进入律令模式，循环选择 → 应用律令
   - `Z` 撤销
   - `Space` 结束当前回合
   - `Esc` 取消当前模式
   - `↑ ↓ ← →` / `W A S D` 移动光标
   - `Enter` / 点击 确认选择

### 编辑模式测试

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "<本仓库根目录>" `
  -runTests -testPlatform EditMode `
  -testResults "Logs/editmode-results.xml" `
  -logFile -
```

预期：179 / 179 PASS（详见 [docs/IMPLEMENTATION_STATUS.md](docs/IMPLEMENTATION_STATUS.md)）。

## 4. 任务路线图完成情况

| 阶段 | 任务 | 状态 | HEAD |
|---|---|---|---|
| M0 | Task 01-02 仓库审计 + 工程骨架 | ✅ | `46e3794` |
| M1 | Task 03-05 Core 状态 + 哈希 + 命令框架 | ✅ | `46e3794` |
| M2 | Task 06-10 移动 / 相位 / 坠落 / 战斗 / Replay-Undo | ✅ | `46e3794` |
| M3 | Task 11-14 锚点 + 律令 + 数据层 + MVP 数据 | ✅ | `46e3794` |
| M4 | Task 15-18 Unity 启动 + 棋盘 + 输入 + HUD | ✅ | `ce2391a9` |
| M4 | Task 19 关卡闭环（防守→撤离） | ✅ | `4b504a7` |
| M5 | **Task 20 测试与交付** | ✅ | `<本 README 时 HEAD>` |

详见 [docs/IMPLEMENTATION_STATUS.md](docs/IMPLEMENTATION_STATUS.md) 与 [Docs/04_Roadmap_and_Milestones.md](Docs/04_Roadmap_and_Milestones.md)。

## 5. 文档导航

| 文档 | 用途 |
|---|---|
| [Docs/01_Project_Overview_and_GDD.md](Docs/01_Project_Overview_and_GDD.md) | GDD 与 MVP 设计 |
| [Docs/02_Technical_Development_Manual.md](Docs/02_Technical_Development_Manual.md) | 技术手册 |
| [Docs/03_Data_and_Content_Spec.md](Docs/03_Data_and_Content_Spec.md) | 数据与内容规范 |
| [Docs/04_Roadmap_and_Milestones.md](Docs/04_Roadmap_and_Milestones.md) | 路线图与里程碑 |
| [Docs/05_Test_and_Acceptance.md](Docs/05_Test_and_Acceptance.md) | 测试与验收规范 |
| [docs/OPENCLAW_REPOSITORY_AUDIT.md](docs/OPENCLAW_REPOSITORY_AUDIT.md) | 完整审计记录 |
| [docs/IMPLEMENTATION_STATUS.md](docs/IMPLEMENTATION_STATUS.md) | 实现状态 |
| [docs/KNOWN_LIMITATIONS.md](docs/KNOWN_LIMITATIONS.md) | 已知限制 |
| [docs/MANUAL_ACCEPTANCE_CHECKLIST.md](docs/MANUAL_ACCEPTANCE_CHECKLIST.md) | 手工验收清单 |
| [AGENTS.md](AGENTS.md) | AI 团队规则 |
| [docs/ADR/](docs/ADR/) | 架构决策记录 |

## 6. 贡献

- AI 团队通过 OpenClaw 调度：`xingyuan-lead`（默认绑定飞书账号 `xingyuan`）接收用户需求
- 详见 [AGENTS.md](AGENTS.md)

## 7. 许可证

未指定。
