# 05｜《星渊誓约》测试与验收规范

## 1. 质量目标

MVP 必须证明：

- Core 与 Unity 解耦；
- Command 是唯一状态入口；
- 规则确定；
- Preview 与 Resolve 一致；
- Replay 与当前状态一致；
- Undo 可重建状态；
- Unity 表现跟随 Core；
- 配置错误可定位；
- 玩家能完整通关。

## 2. 测试分层

### 纯 C# / Core

优先使用 EditMode 或不依赖场景的测试。

覆盖：

- 值对象；
- 状态；
- Hash；
- Command；
- Resolver；
- Replay；
- 数据校验。

### Unity EditMode

覆盖：

- asmdef；
- Core 依赖守卫；
- JSON 文件读取；
- DefinitionDatabase；
- Editor 下配置校验。

### PlayMode

覆盖：

- 场景；
- Bootstrap；
- Presenter；
- 输入；
- HUD；
- Core 与表现同步。

### 手工验收

覆盖完整玩家流程和视觉反馈。

## 3. 测试命名

格式：

```text
MethodOrFeature_Condition_ExpectedResult
```

例如：

```text
PhaseFlip_Fall_DamagesAndMovesToNearestLegalTile
```

每个测试应清楚区分：

- Arrange；
- Act；
- Assert。

## 4. 必须存在的 EditMode 测试

```text
GridPos_EqualValues_AreEqual
GridPos_Sort_OrdersByYThenX
Board_IsInside_RejectsOutOfBounds
Board_IsWalkable_UsesUnitLayerAndActiveLayer
UnitQuery_DifferentInsertionOrder_ReturnsStableOrder

BattleState_Clone_ModifyingCloneDoesNotChangeSource
BattleState_Hash_DifferentInsertionOrderProducesSameHash
BattleState_Hash_ChangedFieldChangesHash
BattleState_Hash_RepeatedHashIsStable

CommandExecutor_InvalidCommand_DoesNotMutateState
CommandExecutor_InvalidCommand_DoesNotWriteHistory
CommandExecutor_Success_WritesPostStateHash

Move_LegalPath_ExecutesAllSteps
Move_PathLongerThanMov_Fails
Move_PathThroughBlockedTile_Fails
Move_TargetOccupied_Fails
Move_NonCurrentTeamUnit_Fails
Move_EventsMatchStepCount
Move_SameInput_ProducesSameEventOrder

PhaseFlip_NoPv_Fails
PhaseFlip_InvalidPosition_Fails
PhaseFlip_Valid_ConsumesOnePv
PhaseFlip_Invalid_DoesNotWriteHistory
PhaseFlip_Fall_DamagesAndMovesToNearestLegalTile
PhaseFlip_Crush_ReturnsToLastLegalPositionAndStuns
Fall_EqualDistanceCandidate_UsesYThenX
Fall_NoLegalTile_DefeatsUnit
Fall_MultipleUnits_ProcessesByUnitId

Damage_Physical_UsesPowAndArm
Damage_Magical_UsesArcAndRes
Damage_Minimum_IsOne
Damage_ZeroDefense_UsesExpectedFormula
Damage_CollapseRegion_IgnoresPhysicalDefense
DamagePreview_EqualsResolvedDamage
Attack_DefeatedUnit_CannotAct
Status_Stunned_SetsApToZero
Status_Paralyzed_BlocksMoveOnly
Status_Guarding_ReducesDamage
Status_Marked_AppliesOnce

CommandReplay_SameCommands_ProducesSameStateHash
Undo_RemovesLastPlayerCommandAndReplays
Undo_Success_AddsTenCv
Undo_DoesNotEnterCommandHistory
Undo_Failure_DoesNotAddCv
Undo_EndTurn_IsNotUndoable
Undo_EnemyCommand_IsNotUndoable

AnchorPolygon_ThreeAnchors_ContainsBoundaryUnits
AnchorPolygon_ThreeCollinear_IsRejected
AnchorPolygon_SelfIntersectQuad_IsRejected
AnchorPolygon_MultipleRegions_ChoosesLargest
AnchorPolygon_EqualArea_UsesCanonicalTieBreak
AnchorPolygon_DifferentInputOrder_ProducesSameResult

GravityDecree_EnemyMoveStep_TriggersBeforeMoveContinues
GravityDecree_ChoosesDirectionWithMoreLegalDistance
GravityDecree_Tie_UsesGridOrder
GravityDecree_NoKnockback_AppliesParalyzed
GravityDecree_Trigger_ConsumesDecree
GravityDecree_SameEvent_TriggersAtMostOne
GravityDecree_Replay_ProducesSameResult

DefinitionValidator_MissingId_ReturnsPath
DefinitionValidator_DuplicateId_Fails
DefinitionValidator_MissingReference_Fails
DefinitionValidator_TileCountMustEqualBoardSize
DefinitionValidator_DuplicateCoordinates_Fails
DefinitionValidator_OverlappingSpawns_Fails
DefinitionValidator_InvalidSpawnTile_Fails
DefinitionValidator_MvpData_IsValid
```

## 5. 必须存在的 PlayMode 测试

```text
MvpBattle_LoadsWithoutUnhandledException
MvpBattle_CreatesEightyTiles
MvpBattle_CreatesAtLeastFourPlayerUnits
BattleHud_ShowsPvAndCv
BattleHud_ShowsCurrentTeamRoundAndObjective
Input_Move_SubmitsMoveCommand
Input_PhaseFlip_SubmitsPhaseFlipCommand
Input_Attack_SubmitsAttackCommand
Input_Decree_SubmitsDeployDecreeCommand
Input_Undo_SubmitsUndo
Input_RightClick_CancelsMode

Presenter_Move_MatchesCorePosition
Presenter_PhaseFlip_UpdatesTileVisual
Presenter_Damage_UpdatesHpDisplay
Presenter_UnitDefeated_RemovesOrHidesObject
Presenter_Undo_FullyResyncsFromCore
Presenter_DoesNotChangeCoreStateDirectly

Bootstrap_MissingConfig_ShowsExplicitError
Bootstrap_InvalidConfig_ShowsFileAndField
```

## 6. Core 依赖守卫

测试读取 `Starfall.Core` 程序集引用，必须确认不存在：

```text
UnityEngine
UnityEditor
```

并可扫描源码禁止：

```text
MonoBehaviour
ScriptableObject
GameObject
Transform
UnityEngine.Random
```

注意字符串或文档中的词不应误报，优先检查语义依赖和程序集引用。

## 7. 确定性测试

### 同进程重复

同一状态和 Command 重复 100 次：

- Final Hash 相同；
- BattleEvent 数量相同；
- Event 顺序相同。

### 不同插入顺序

对 Unit、Status、Decree、Tile 列表随机重排后：

- Canonical Hash 相同；
- 规则结果相同。

测试数据的随机重排可使用固定 seed，仅用于测试输入生成，不得进入正式战斗逻辑。

### Replay

记录：

```text
InitialStateHash
Command list
FinalStateHash
```

Replay 后：

```text
ActualFinalHash == ExpectedFinalHash
BattleStateComparer == Equal
```

失败时输出第一个差异路径。

## 8. 伤害验收

每个伤害测试必须同时检查：

- baseDamage；
- defenseValue；
- defenseFactor；
- 状态修正；
- finalDamage；
- HP；
- Event；
- Preview 值。

使用整数期望值，不使用浮点近似。

## 9. 相位验收

测试场景应分别覆盖：

- 当前格翻转后仍合法；
- Crush；
- Fall；
- 无合法格；
- Fall 伤害致死；
- LastLegalPosition 被占用；
- 多单位；
- PV 不足；
- 非 PhaseMutable；
- 超出距离 3。

## 10. Undo 验收

成功 Undo 前后记录：

```text
History count
State hash
PV
CV
Unit positions
HP
Statuses
Decrees
Objective
```

Undo 后必须等于“从历史中删除该 Command 再 Replay”的状态，之后仅 CV 多 10。

## 11. 手工通关验收

### 启动

- 打开 `MvpBattle`；
- 无红色 Console Error；
- 生成 80 格；
- 生成 4 名玩家；
- HUD 显示 Round、Team、PV=3、CV=0、目标。

### 基础操作

- 选择玩家；
- M 移动；
- A 攻击；
- F 翻转；
- D 部署律令；
- 右键取消；
- Space 结束回合；
- Z Undo。

### 特色机制

必须实际观察：

- 相位格视觉变化；
- 挤压回退和 Stunned；
- 坠落移动和 20% MaxHP 伤害；
- 三个锚点形成围区；
- 围区物理防御归零；
- 敌人 MoveStep 触发引力律令；
- 击退方向符合规则；
- 无法击退时 Paralyzed；
- Undo 后画面和 Core 一致；
- CV 增加 10。

### 目标闭环

- 防守区连续获得 3 次进度；
- 目标切换为撤离；
- 所有存活玩家进入撤离区；
- 触发胜利。

### 失败

至少人工验证：

- 所有玩家死亡；
- CV 达到 100；
- 超过 12 Round。

## 12. 验收证据

自动测试报告必须包含：

```text
Unity version
Git commit
Branch
Test suite
Total
Passed
Failed
Skipped
Duration
Result file
```

手工验收必须记录：

```text
日期
执行人/Agent
构建或 Commit
步骤
实际结果
截图或日志
问题编号
```

没有证据不得标记通过。

## 13. 缺陷等级

### Blocker

- 项目无法打开；
- 无法编译；
- 数据损坏；
- Replay 不确定；
- 核心场景无法启动；
- 无法完成关卡。

### Critical

- 状态与表现严重不同步；
- Undo 产生错误状态；
- Command 绕开历史；
- 伤害预览错误；
- 相位导致不可恢复异常。

### Major

- 功能可绕过或部分失败；
- HUD 关键数据错误；
- 敌方 AI 卡死；
- 锚点或律令边界错误。

### Minor

- 非阻断视觉或文案问题。

MVP 发布前：

```text
Blocker = 0
Critical = 0
Major 必须有用户批准的限制记录
```

## 14. CI 建议

仓库稳定后增加 GitHub Actions：

- 检查 JSON 格式；
- 检查敏感文件；
- 检查 Core 禁止引用；
- Unity EditMode；
- Unity PlayMode；
- 上传测试结果。

在 CI 可用前，本机测试结果同样必须保留。

## 15. 最终 Done 定义

只有全部满足才能标记 MVP 完成：

- Unity 工程正常打开；
- 无编译错误；
- 主场景运行；
- 玩家可通关；
- Core 测试通过；
- PlayMode 关键测试通过；
- Replay Hash 一致；
- Core 不引用 Unity；
- Preview 与 Resolve 一致；
- Undo 与 Replay 一致；
- 两阶段目标正常；
- 文档与代码一致；
- 未实现内容进入限制文档；
- 用户完成最终验收。
