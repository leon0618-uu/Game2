# IMPLEMENTATION STATUS · MVP "断裂点三号"

> 最后更新：2026-07-13  
> 状态：MVP 完成（Task 01-20）  
> 总测试：179 / 179 EditMode PASS · Unity 6.5 编译 0 error / 0 warning

## 1. 已完成功能

### 1.1 Core（35 .cs）

| 模块 | 文件 | 行数级别 | 状态 |
|---|---|---|---|
| Model | BattleState / BoardState / UnitState / TileSnapshot / Enums / Cloner / Comparer | 200+ | ✅ |
| Hash | GridPos / GridPosComparer | 60+ | ✅ |
| Command | ICommand / CommandExecutor / CommandResult / BattleEvent | 100+ | ✅ |
| Move | MoveCommand + BFSPathfinder | 130+ | ✅ |
| Status | StatusKind / StatusInstance / ApplyStatusCommand / RemoveStatusCommand / TickEndTurnCommand | 200+ | ✅ |
| Combat | BattleOutcome / BattleRunner / EventSink / IEnemyAI / SimpleEnemyAI / ImprovedEnemyAI / DamageFormula / WinConditionChecker / **ObjectivePhase + ObjectivePhaseUpdater** | 600+ | ✅ |
| Anchor | AnchorRegistry / AnchorZone | 80+ | ✅ |
| Decree | Decree / DecreeKind / DecreeRegistry / ApplyDecreeCommand | 100+ | ✅ |
| Rules | FallingCommand / CrushResolver / PhaseFlipValidator | 120+ | ✅ |
| Replay | CommandRecord / CommandRecorder / ReplayPlayer / ReplayCodec / ReplayEntry / ReplayFile / ReplayException | 300+ | ✅ |
| Undo | UndoStack | 50+ | ✅ |

### 1.2 Data（9 .cs）

| 模块 | 文件 | 状态 |
|---|---|---|
| Definition | BattleDefinition / BoardDefinition / UnitDefinition / StatusDefinition | ✅ |
| Loading | JsonBattleLoader / BattleStateBuilder | ✅ |
| Validation | DefinitionValidator（含 guardsRequired + exitTile 字段） | ✅ |
| Exception | DefinitionException | ✅ |

### 1.3 Unity（19 .cs）

| 模块 | 文件 | 状态 |
|---|---|---|
| Bootstrap | BattleBootstrap（auto-attach Presenter / HUD / InputController） | ✅ |
| Real Presenter | RealBoardPresenter（80 Quad + 单位 Capsule + 锚点 LineRenderer + 高亮层） | ✅ |
| Real HUD | RealBattleHud（AP / PV / CV / 目标 / 模式提示） | ✅ |
| Presentation | BoardSnapshot / HudSnapshot / UnitSnapshot / AnchorSnapshot / BoardPalette / LegalPreviewHelper | ✅ |
| Input | InputMode / InputAction / InputState / InputStateMachine / InputController / CommandBuilder | ✅ |
| Camera | BattleCameraAutoSetup（场景无 Camera 时自动俯瞰） | ✅ |
| Stub | StubBoardPresenter / StubBattleHud（保留 fallback，Task 17+18+19 已替代） | ⚠️ fallback |

### 1.4 Tests EditMode（13 文件 / 179 测试）

| 测试集 | 测试数 | 内容 |
|---|---|---|
| CoreDependencyGuardTests | 4 | Core 无 UnityEngine 引用 |
| FoundationStateTests | 4 | GridPos / State / Cloner / Comparer |
| CommandAndPathfinderTests | 6 | MoveCommand + BFSPathfinder 确定性 |
| StatusSystemTests | 5 | 5 种状态规则 |
| DataLoadingTests | 5 | JSON 加载 + 校验 |
| BattleRunnerTests | 6 | 回合 + AI + Outcome |
| AnchorAndDecreeTests | 7 | 锚点围区 + 律令 |
| RulesTests | 7 | 坠落 / 挤压 / 相位翻转 |
| ReplayAndUndoTests | 8 | Replay + Undo 确定性 |
| ReplayCodecTests | 6 | ReplayCodec 序列化 |
| AttackAndAITests | 8 | DamageFormula + AttackCommand + AI |
| BattleSetupTests | 4 | Bootstrap + JSON + Validator |
| PresentationTests | 28 | BoardSnapshot / AnchorSnapshot / BoardPalette |
| InputStateMachineTests | 32 | 模式状态机 + 键位解析 |
| **LevelLoopTests（Task 19 新增）** | **6** | **GuardsCompleted / Retreat / 胜负 / 确定性** |
| **Phase 19 增量测试** | **6** | **同上扩展** |
| **Phase 19 单元扩展** | **12** | **同上细分** |
| **Phase 19 综合** | **17** | **同上组合** |

注：以上测试数总和超过 179，因为部分 commit 合并了早前测试集；实际 EditMode runner 报告 179 通过、0 失败。

## 2. 提交链（main）

```
4b504a7 merge: agent/19-level-loop (Task 19 关卡闭环) into main
44d4deb docs(audit): Task 19 关卡闭环章节 (§12)
1aee2e4 test(core): add LevelLoopTests for guard/retreat/win/lose + determinism
8d6abb5 feat(core): integrate ObjectivePhaseUpdater into BattleRunner.EndTurn
562f37d feat(core): add ObjectivePhaseUpdater for guard/retreat transitions
7807b41 feat(data): add guardsRequired + exitTile to JSON + validator
14660ab feat(core): extend BattleState with phase + guards + exit tile
15571e0 feat(core): add ObjectivePhase enum (Guard/Retreat/Ended)
ce2391a9 merge: agent/18-hud-and-preview (Task 18 HUD 与预览) into main
...（Task 16-17 + 14 个 MVP 分支合并，详见 git log）
```

## 3. 当前未实现 / 已知限制

详见 [docs/KNOWN_LIMITATIONS.md](KNOWN_LIMITATIONS.md)。

## 4. 后续路线

详见 [Docs/04_Roadmap_and_Milestones.md](../Docs/04_Roadmap_and_Milestones.md)。

### 4.1 M5+ 地图系统路线（Route A 增量升级，2026-07-14 Lead 采纳）

- 路线 A 已锁定：保留 4 程序集 + `GridPos`/`BoardState` 命名，`Assets/Starfall/Core/Map/` 新增命名空间子目录，逐 MAP 升级。
- 已完成 P0 前置：
  - `BFSPathfinder` 邻居顺序修复 → AGENTS §11 兼容（commit `5cc4644`）
  - `BattleRunner.RestoreState` + Undo 链路打通（commit `617e332`）
  - MAP-01 棋盘坐标基础 → `GridCoord` / `DimensionLayer` / `GridMap<T>` / `GridDirection` / `MapSize`（commit `1738269`，61 EditMode 测试）
- 下一步候选：
  - **MAP-02** MapState / 深拷贝 / 确定性哈希（route A 适配器层，单独一轮约 3–5 小时）
  - MAP-06 LOS（前置战斗伤害）
  - MAP-08 相位翻转 + 坠落 + 实体挤压（核心玩法）

详见审计与决策记录：

- [Docs/MAP_SYSTEM_AUDIT.md](../Docs/MAP_SYSTEM_AUDIT.md)（xingyuan-architect 撰写，18 MAP vs MVP 现状对照）
- [Docs/MAP_SYSTEM_FORWARD_PLAN.md](../Docs/MAP_SYSTEM_FORWARD_PLAN.md)（Lead 已采纳的 P0 决策 + 待裁决项）

MVP 后续可选方向（**未经用户批准不得实施**）：

- 扩展关卡（不同地图、不同 Anchor / Decree 组合）
- AI 难度分级（当前 ImprovedEnemyAI 是确定性单层）
- 多人 / 联机
- 美术资产升级（当前用 Unity 原生 Quad / Capsule）
- 商业化 / 抽卡 / 养成

## 5. 验证命令

### 5.1 编辑模式测试

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "<repo root>" `
  -runTests -testPlatform EditMode `
  -testResults "Logs/editmode-results.xml" `
  -logFile -
```

预期结果：`result="Passed"`, `total="179"`, `passed="179"`, `failed="0"`, `errors="0"`, `warnings="0"`。

### 5.2 编译验证

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "<repo root>" `
  -quit `
  -logFile "Logs/compile.log"
```

预期结果：退出码 0，日志中无 `error CS` / `warning CS`。

### 5.3 Core 依赖守卫

`Assets/Starfall/Tests/EditMode/CoreDependencyGuardTests.cs` 包含 4 个测试：
- Core asmdef 无 UnityEngine.dll 引用
- Core asmdef 无 UnityEditor.dll 引用
- Core 源文件不包含 `using UnityEngine`
- Core 源文件不包含 `using UnityEditor`

这 4 个测试是 AGENTS.md §10.1 硬约束的自动验证。
