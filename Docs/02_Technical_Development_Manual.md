# 02｜《星渊誓约》技术开发手册

## 1. 技术基线

```text
Engine: Unity 6.3 LTS
Render Pipeline: URP
Language: C#
Primary Target: Windows PC
Repository: leon0618-uu/Game2
```

核心目标：

- 战斗 Core 不依赖 Unity；
- Command 驱动全部状态变化；
- 可 Replay、Undo、Hash；
- 表现层可随时从 Core 状态重建；
- 配置错误可定位；
- 测试覆盖关键规则。

## 2. 推荐目录

```text
Assets/
└─ Starfall/
   ├─ Core/
   │  ├─ Battle/
   │  ├─ Board/
   │  ├─ Commands/
   │  ├─ Events/
   │  ├─ Movement/
   │  ├─ Combat/
   │  ├─ Phase/
   │  ├─ Anchors/
   │  ├─ Decrees/
   │  ├─ Objectives/
   │  └─ Determinism/
   ├─ Data/
   │  ├─ Definitions/
   │  ├─ Loading/
   │  ├─ Validation/
   │  └─ Builders/
   ├─ Unity/
   │  ├─ Bootstrap/
   │  ├─ Board/
   │  ├─ Units/
   │  ├─ Input/
   │  ├─ UI/
   │  ├─ Camera/
   │  └─ Presentation/
   ├─ Tests/
   │  ├─ EditMode/
   │  └─ PlayMode/
   ├─ Art/
   ├─ Scenes/
   └─ Settings/
docs/
```

## 3. 程序集

创建：

```text
Starfall.Core
Starfall.Data
Starfall.Unity
Starfall.Tests.EditMode
Starfall.Tests.PlayMode
```

依赖：

```text
Starfall.Core
  -> 无 Unity 引用

Starfall.Data
  -> Starfall.Core
  -> 可使用普通 .NET JSON 能力
  -> 不依赖场景和 Presenter

Starfall.Unity
  -> Starfall.Core
  -> Starfall.Data
  -> UnityEngine

Starfall.Tests.EditMode
  -> Core
  -> Data
  -> 必要时 Unity Test Framework

Starfall.Tests.PlayMode
  -> Unity
  -> Core
  -> Data
```

必须增加 Core 依赖守卫测试，检查禁止命名空间和类型。

## 4. Core 数据模型

### GridPos

建议使用不可变 `readonly struct`，实现：

```text
IEquatable<GridPos>
IComparable<GridPos>
```

比较：

```text
y
→ x
```

提供：

```text
Add
Subtract
ManhattanDistance
FourNeighbors
```

不要把运行时 `GetHashCode()` 当作跨进程稳定哈希。

### BattleState

至少包含：

```text
BattleId
Round
CurrentTeam
Board
Units
Resources
Decrees
Objective
Outcome
CommandHistory
NextCommandSequence
NextEventSequence
```

### BoardState

至少包含：

```text
Width
Height
Tiles
IsInside
GetTile
IsWalkable
IsOccupied
GetUnitAt
```

影响结果的查询必须稳定排序。

### UnitState

至少包含 GDD 中的属性，并提供显式深拷贝。

### TileState

建议包含：

```text
Position
ActiveLayer
RealityWalkable
AstralWalkable
PhaseMutable
Tags
AnchorState
```

## 5. Command 架构

接口：

```csharp
public interface ICommand
{
    string CommandId { get; }

    bool CanExecute(
        BattleState state,
        out CommandError error);

    CommandResult Execute(
        BattleState state,
        CommandExecutionContext context);
}
```

`CommandError` 使用稳定错误码，不只返回自由文本。

`CommandResult`：

```text
Success
ErrorCode
ErrorReason
BattleEvents
PresentationEvents
PostStateHash
ConsumedAP
ConsumedPV
HistoryEntry
```

### CommandExecutor

统一流程：

1. 验证 Command 非空和 ID；
2. 创建执行前快照或哈希；
3. 调用 `CanExecute`；
4. 非法时确认状态未变化；
5. 执行；
6. 处理 Reaction；
7. 写入历史；
8. 生成执行后哈希；
9. 返回事件。

Command 实现不得绕开 Executor 直接写历史。

## 6. Event 架构

### BattleEvent

参与规则链或记录：

```text
MoveStarted
MoveStep
MoveStopped
TilePhaseFlipped
UnitCrushed
UnitFell
DamageApplied
StatusApplied
StatusRemoved
UnitDefeated
AnchorActivated
AnchorRegionChanged
DecreeDeployed
DecreeTriggered
ObjectiveChanged
TurnStarted
TurnEnded
BattleWon
BattleLost
```

字段应使用 ID 和值对象，不包含 GameObject。

### PresentationEvent

仅表现：

```text
PlayMove
PlayAttack
PlayDamage
PlayFall
PlayCrush
PlayPhaseFlip
ShowError
RefreshHud
ShowUndo
```

表现事件失败不得改变 Core 结果。

## 7. 确定性

### 稳定排序

- Tile：`GridPos`；
- Unit：`UnitId`；
- Status：`StatusId`、RemainingTurns、InstanceId；
- Decree：`InstanceId`；
- Command：Sequence；
- Event：Sequence；
- Polygon：规范化顶点序列。

### 哈希

建议 MVP 使用显式 FNV-1a 64 位或同等稳定算法。

要求：

- 固定字节序；
- 字符串以 UTF-8；
- 每个字段写入类型标记和长度；
- 枚举写入显式整数值；
- 列表先排序，再写数量和元素；
- 不包含 Unity InstanceID、内存地址或本地时间。

哈希字段包括：

```text
Round
CurrentTeam
PV
CV
Tiles
Units
Statuses
Decrees
Objective
Outcome
Command history identifiers
```

### 深拷贝

`BattleStateCloner` 必须复制所有可变容器，不能共享 List、Dictionary 或 Status 实例。

### 比较

`BattleStateComparer` 应能返回第一个差异路径，便于 Replay 诊断。

## 8. Replay 与 Undo

战斗初始化时保存：

```text
InitialBattleState
```

Replay：

```text
Clone(InitialBattleState)
→ 依序执行历史 Command
→ 比较 CurrentBattleState
```

历史必须保存可重新构造 Command 的全部参数，不保存闭包或场景引用。

Undo：

1. 验证当前为玩家回合且战斗未结束；
2. 查找本玩家回合最后一个可撤销 Command；
3. 删除；
4. Replay；
5. CV +10；
6. 重新 Hash；
7. Presenter 全量同步。

禁止逆向“减回去”式 Undo。

## 9. 移动与寻路

### Pathfinder

MVP 使用 BFS，单位移动代价均为 1。

固定邻居：

```text
Down
Left
Right
Up
```

路径相同长度时，首次发现即为选定路径。

### MovementResolver

逐步执行：

```text
validate step
→ update position
→ publish MoveStep
→ resolve reactions
→ decide continuation
```

若单位在反应中：

- 被击退；
- 被击败；
- 获得禁止移动状态；

则终止原路径。

## 10. 相位与异常占用

`PhaseFlipResolver` 只负责翻转和调用：

```text
InvalidTileOccupancyResolver
```

后者按顺序：

1. 尝试 Crush；
2. 尝试 Fall；
3. 无合法格时 UnitLostToVoid。

BFS 搜索候选时需排除：

- 不合法层；
- 不可通行；
- 已占用；
- 越界；
- 禁止进入标签。

多单位按 `UnitId` 处理，前一单位移动结果会影响后一单位占用判断。

## 11. 伤害

`DamageResolver` 同时服务：

```text
Preview
Resolve
```

推荐输入：

```text
AttackerSnapshot
TargetSnapshot
SkillDefinition
BattleContext
```

输出：

```text
BaseDamage
DefenseBeforeRules
DefenseAfterRules
Modifiers
FinalDamage
StatusesToApply
```

Preview 不修改状态。

整数运算使用 `long` 中间值，最后安全转换为 `int`，避免乘法溢出。

## 12. 状态系统

状态定义数据化，规则实现采用显式 Handler：

```text
IStatusRule
OnTurnStart
OnTurnEnd
BeforeCommand
BeforeDamage
AfterDamage
```

MVP 只注册五种状态。

状态合并：

- 同 ID 默认刷新为较大剩余回合；
- 唯一实例状态不堆叠；
- Marked 消耗时发布 StatusRemoved；
- 状态遍历稳定。

## 13. 锚点围区

### PolygonUtility

三点：

- 叉积面积非 0。

四点：

- 必须先求非自交环顺序；
- 排除自交；
- 面积使用整数二倍面积，避免浮点误差。

Point-in-polygon：

- 边界单独检测；
- 边界返回 inside；
- 可使用整数叉积和射线法；
- 不用浮点近似作为唯一判断。

最大围区：

1. 面积更大；
2. 面积相同，规范化顶点序列字典序更小。

## 14. ReactionResolver

输入：

```text
BattleEvent
BattleState
ReactionContext
```

MVP 仅处理 `MoveStep` → Decree。

反应安全限制：

- 同一 Event 最多触发一个律令；
- 使用 ReactionDepth 防止循环；
- 默认最大深度 8；
- 每个律令单次触发后立即标记失效；
- 反应产生的事件追加稳定 Sequence；
- Replay 使用相同 Resolver。

## 15. JSON 数据层

### 加载流程

```text
发现文件
→ 读取 UTF-8
→ 反序列化 DTO
→ 基础字段校验
→ 跨引用校验
→ 构建 DefinitionDatabase
→ 构建 BattleState
```

不得由 Core 直接读取 JSON。

### 错误模型

```text
DefinitionError
- FilePath
- JsonPath
- Code
- InvalidValue
- Message
```

一次加载尽可能返回全部可定位错误，而不是只报第一个。

## 16. Unity 启动

`BattleBootstrapper`：

1. 读取配置；
2. 校验；
3. 构建初始状态；
4. 创建 Executor；
5. 初始化 Presenter；
6. 全量刷新；
7. 错误时显示明确错误面板。

### Presenter

`BoardPresenter`：

- 根据 TileState 生成/更新格子；
- 显示 ActiveLayer、不可通行、锚点、围区。

`UnitPresenterRegistry`：

- `UnitId -> UnitPresenter`；
- 从 UnitState 同步；
- 不保存 HP、位置等第二份真值。

`BattleHud`：

- 只读取状态或 ViewModel；
- 所有操作提交 Command。

## 17. 输入

输入模式：

```text
None
Move
PhaseFlip
Attack
DeployDecree
```

快捷键：

```text
M F A D Z Space
```

输入层流程：

```text
selection
→ build command
→ CanExecute / preview
→ Executor.Execute
→ play PresentationEvents
→ resync presenters
```

右键取消当前模式。

## 18. 测试架构

EditMode：

- Core 值对象；
- 状态复制和哈希；
- Command；
- 移动；
- 相位；
- 伤害；
- Replay/Undo；
- 锚点；
- 律令；
- 数据校验。

PlayMode：

- 场景启动；
- 80 格；
- Unit Presenter；
- HUD；
- 输入到 Command；
- Presenter 与 Core 同步；
- Undo 后全量重建。

## 19. 日志

日志分类：

```text
BOOT
CONFIG
COMMAND
EVENT
REACTION
REPLAY
UNDO
PRESENTATION
TEST
```

开发构建可输出详细日志，正式 MVP 默认仅显示警告和错误。

任何“测试通过”报告必须包含：

- 命令；
- Unity 版本；
- 测试数量；
- 通过/失败/跳过；
- 结果文件路径；
- 失败摘要。

## 20. 代码规范

- 类型和公共成员英文；
- 文档与报告中文；
- 公共 API 使用 XML Documentation；
- 避免静态可变全局状态；
- 优先组合；
- 一个类型一个主要职责；
- 不捕获后静默忽略异常；
- 不通过反射绕开架构约束；
- 所有 TODO 带 Issue 或原因。
