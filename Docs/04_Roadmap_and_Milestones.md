# 04｜《星渊誓约》OpenClaw 开发路线与里程碑

## 1. 执行原则

- 由 `xingyuan-lead` 接收用户任务；
- 每轮只执行一个任务包；
- Lead 指派专业 Agent；
- 任务包必须有验收门槛；
- 未通过当前 Gate 不进入下一阶段；
- 所有写入在独立 Worktree 和分支进行；
- Push、PR、合并必须用户批准。

## 2. 当前团队

| Agent | 主要工作区 | 主要职责 |
|---|---|---|
| xingyuan-lead | 主项目 | 调度、汇总、验收 |
| xingyuan-architect | architect worktree | 架构、ADR、审查 |
| xingyuan-gameplay | gameplay worktree | Core 与玩法 |
| xingyuan-ui-tools | ui-tools worktree | Unity、UI、数据工具 |
| xingyuan-qa | qa worktree | 测试、验收、质量 |

## 3. 里程碑

### M0：仓库可开发

目标：

- 工程和环境审计完成；
- Unity 版本明确；
- 文档已提交；
- Worktree 与 Agent 正常；
- 不修改玩法。

### M1：确定性 Core 骨架

范围：

- 程序集；
- GridPos、状态模型；
- Clone、Compare、Hash；
- Command 和 Event 框架。

### M2：战斗核心规则

范围：

- 移动；
- 相位；
- 坠落和挤压；
- 攻击和状态；
- 回合、Replay、Undo。

### M3：特色系统与数据

范围：

- 锚点围区；
- 律令 Reaction；
- JSON 定义与校验；
- MVP 数据。

### M4：Unity 可玩纵切

范围：

- Bootstrap；
- 棋盘和单位表现；
- 输入；
- HUD 和预览；
- 目标与敌方行动。

### M5：验收交付

范围：

- 自动测试；
- 手工通关；
- 文档同步；
- 已知限制；
- 最终构建。

## 4. 任务包

### Task 01：仓库与环境审计

主责：

```text
xingyuan-lead
xingyuan-architect
xingyuan-qa
```

交付：

```text
docs/OPENCLAW_REPOSITORY_AUDIT.md
```

只读检查：

- Assets、Packages、ProjectSettings；
- Unity 版本；
- asmdef；
- Core/Data/Unity/Tests；
- 编译错误；
- 文档；
- Unity Test Framework；
- Git 状态和 Worktree。

Gate：

- 不修改玩法；
- 明确下一步是否可执行。

### Task 02：工程骨架

主责：

```text
xingyuan-architect
xingyuan-ui-tools
xingyuan-qa
```

交付：

- 目录；
- 5 个程序集；
- Core 依赖守卫测试。

Gate：

- 编译通过；
- Core 无 Unity 引用；
- 不实现玩法。

### Task 03：Core 基础状态

主责：

```text
xingyuan-gameplay
xingyuan-architect
xingyuan-qa
```

范围：

- Enum；
- GridPos；
- BattleState、BoardState、TileState、UnitState 等；
- 基础测试。

### Task 04：Clone、Compare、Hash

主责：

```text
xingyuan-gameplay
xingyuan-architect
xingyuan-qa
```

Gate：

- 列表顺序不同仍同 Hash；
- 深拷贝独立；
- 差异字段能被发现。

### Task 05：Command 与 Event 框架

主责：

```text
xingyuan-architect
xingyuan-gameplay
xingyuan-qa
```

只创建基础框架和最小命令占位。

### Task 06：移动

主责：

```text
xingyuan-gameplay
xingyuan-qa
```

Gate：

- 确定性路径；
- MoveStep 顺序；
- 非法路径不改状态。

### Task 07：相位翻转

主责：

```text
xingyuan-gameplay
xingyuan-qa
```

Gate：

- PV；
- PhaseMutable；
- 距离；
- 历史与事件。

### Task 08：坠落与挤压

主责：

```text
xingyuan-gameplay
xingyuan-qa
```

Gate：

- Crush；
- Fall BFS；
- 无合法格；
- 多单位顺序。

### Task 09：攻击与状态

主责：

```text
xingyuan-gameplay
xingyuan-architect
xingyuan-qa
```

Gate：

- DamagePreview 与 Resolve 一致；
- 五个状态规则；
- 整数伤害。

### Task 10：回合、Replay、Undo

主责：

```text
xingyuan-architect
xingyuan-gameplay
xingyuan-qa
```

Gate：

- Replay 哈希一致；
- Undo 删除历史并重放；
- CV 正确；
- EndTurn 不可撤销。

### Task 11：锚点围区

主责：

```text
xingyuan-gameplay
xingyuan-architect
xingyuan-qa
```

Gate：

- 三点；
- 四点；
- 自交；
- 边界；
- 最大面积和稳定 Tie-break。

### Task 12：律令 Reaction

主责：

```text
xingyuan-gameplay
xingyuan-architect
xingyuan-qa
```

Gate：

- MoveStep 中优先触发；
- 确定性垂直方向；
- 单事件单律令；
- Replay 一致。

### Task 13：JSON 数据层

主责：

```text
xingyuan-ui-tools
xingyuan-architect
xingyuan-qa
```

Gate：

- 完整错误路径；
- 跨引用；
- 配置可构建状态。

### Task 14：MVP 数据

主责：

```text
xingyuan-ui-tools
xingyuan-gameplay
xingyuan-qa
```

Gate：

- 80 格；
- 4 玩家；
- 敌方；
- 坠落、挤压、锚点、律令、防守和撤离布局；
- 全部校验通过。

### Task 15：Unity 启动

主责：

```text
xingyuan-ui-tools
xingyuan-qa
```

Gate：

- 配置错误可见；
- 正确配置建立 BattleState；
- 场景无异常。

### Task 16：棋盘与单位表现

主责：

```text
xingyuan-ui-tools
xingyuan-qa
```

Gate：

- 80 格；
- 单位显示；
- Presenter 无第二份真值。

### Task 17：输入与 Command

主责：

```text
xingyuan-ui-tools
xingyuan-gameplay
xingyuan-qa
```

Gate：

- M/F/A/D/Z/Space；
- 左右键；
- 输入不直改状态。

### Task 18：HUD 与预览

主责：

```text
xingyuan-ui-tools
xingyuan-gameplay
xingyuan-qa
```

Gate：

- PV/CV/AP/目标；
- 合法格；
- 坠落；
- 伤害；
- 律令；
- 围区。

### Task 19：关卡闭环

主责：

```text
xingyuan-gameplay
xingyuan-ui-tools
xingyuan-qa
```

Gate：

- 防守三次；
- 撤离；
- 敌方确定性行动；
- 胜负。

### Task 20：测试与交付

主责：

```text
xingyuan-qa
xingyuan-lead
全员
```

交付：

```text
README.md
docs/IMPLEMENTATION_STATUS.md
docs/KNOWN_LIMITATIONS.md
docs/MANUAL_ACCEPTANCE_CHECKLIST.md
```

## 5. 推荐执行轮次

| 轮次 | Task |
|---:|---|
| 1 | 01 |
| 2 | 02 |
| 3 | 03 + 04 |
| 4 | 05 |
| 5 | 06 |
| 6 | 07 + 08 |
| 7 | 09 |
| 8 | 10 |
| 9 | 11 |
| 10 | 12 |
| 11 | 13 + 14 |
| 12 | 15 + 16 |
| 13 | 17 + 18 |
| 14 | 19 |
| 15 | 20 |

只有用户明确批准时才可组合非相邻任务。

## 6. 分支建议

```text
agent/01-repository-audit
agent/02-project-skeleton
agent/03-core-state
agent/04-deterministic-hash
agent/05-command-framework
agent/06-movement
agent/07-phase-flip
agent/08-fall-crush
agent/09-combat-status
agent/10-replay-undo
agent/11-anchor-region
agent/12-decree-reaction
agent/13-data-layer
agent/14-mvp-content
agent/15-unity-bootstrap
agent/16-presenters
agent/17-input
agent/18-hud-preview
agent/19-objective-loop
agent/20-acceptance
```

## 7. Issue 模板

```markdown
## 目标

## 范围

## 不在范围

## 依赖文档

## 实现要求

## 测试要求

## 验收标准

## 风险

## 负责 Agent
```

## 8. Lead 的调度模板

```text
你是 xingyuan-lead。

本轮只处理 Task XX。
先读取 AGENTS.md 和 01～05 文档。

请委派：
- architect：……
- gameplay：……
- ui-tools：……
- qa：……

要求：
- 各 Agent 在自己的 Worktree 工作；
- 不修改未授权模块；
- 不 Push、不建 PR、不合并；
- 必须提供测试证据。

所有成员完成后，统一输出标准开发报告。
```

## 9. 合并 Gate

Lead 建议合并前必须确认：

- 分支基于最新 main；
- 无未解决冲突；
- 架构审查通过；
- 相关测试通过；
- QA 提供证据；
- 文档更新；
- `git diff` 无无关改动；
- 用户批准 Push/PR/合并。

## 10. 第一轮任务

当前文档建立后，第一轮应执行：

```text
Task 01：仓库与开发环境审计
```

在 Task 01 结束前不得开始创建玩法代码。
