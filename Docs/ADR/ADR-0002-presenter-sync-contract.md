# ADR-0002: Presenter 同步契约

- **状态**：Accepted（Task 02 起草）
- **日期**：2026-07-12
- **作者**：xingyuan-architect
- **关联任务包**：Task 02 Phase A
- **后续落地任务**：Task 16、Task 17、Task 18

---

## Context

Task 16-18 表现层落地时，最大的设计风险是「Presenter 持有第二份战斗真值」与「Transform 被误用为战斗状态」：

- 若 `BattleHud` 缓存了 `BattleState` 引用，Undo / Replay 重置后 HUD 会与 Core 不同步；
- 若 Presenter 直接修改 `Transform.position` 并反向写回 Core，会形成 Core/Unity 双向耦合，破坏 AGENTS.md §10.3「Presenter 不保存独立战斗真值」；
- 若 Command 在执行前就触发表现（动画），Command 失败回滚时表现层无回滚路径；
- 若 Presenter 主动查询 Core，会形成反向依赖，Unity 层侵入 Core 接口。

本 ADR 在 MVP 阶段就锁定「单向同步 + 快照驱动 + 失败吞咽」三原则，避免后续重构。

---

## Decision

### 1. 视图模型结构（命名空间 `Starfall.Unity.Presentation`）

```csharp
namespace Starfall.Unity.Presentation
{
    using Starfall.Core.Model;

    /// <summary>
    /// 单位表现快照。readonly record struct，纯值类型；
    /// Phase / Direction 为 byte，与 ADR-0001 UnitSnapshot 一致。
    /// </summary>
    public readonly record struct UnitSnapshot(
        UnitId UnitId, int X, int Y, int Hp, byte Phase, byte Direction);

    /// <summary>
    /// 棋盘表现快照。TileSnapshots 按 (Y, X) 升序，与 ADR-0001 §3 一致。
    /// </summary>
    public readonly record struct BoardSnapshot(
        int Width, int Height, IReadOnlyList<TileSnapshot> TileSnapshots);

    /// <summary>
    /// HUD 表现快照（回合 / AP / 目标）。
    /// </summary>
    public readonly record struct HudSnapshot(
        int Turn, byte Ap, ObjectiveId Objective);

    /// <summary>
    /// 表现层内部动画/特效触发器。与 BattleEvent 严格区分：
    /// PresentationEvent 由 Presenter 内部消费，不回流 Core。
    /// </summary>
    public readonly record struct PresentationEvent(
        PresentationEventKind Kind, ulong InstanceId, int Payload);
}
```

- 视图模型均为 `readonly record struct`，零分配拷贝，便于 GC 友好。
- `TileSnapshot` 复用 ADR-0001 定义的 `Starfall.Core.Model.TileSnapshot`，不重复声明。

### 2. Presenter 接口

```csharp
namespace Starfall.Unity.Presentation
{
    using Starfall.Core.Model;
    using System.Collections.Generic;

    public interface IBoardPresenter
    {
        /// <summary>
        /// 由 Core 调度器在每帧 / 每回合调用，传入新快照与已成功 Command 产生的事件。
        /// Presenter 不应保留 snapshot 引用（按本 ADR §3）。
        /// </summary>
        void Render(in BoardSnapshot snapshot, in IReadOnlyList<PresentationEvent> events);
    }

    public interface IUnitPresenterRegistry
    {
        void Register(UnitId id, IUnitPresenter presenter);
        IUnitPresenter Resolve(UnitId id);
    }

    public interface IUnitPresenter
    {
        void Render(in UnitSnapshot snapshot, in IReadOnlyList<PresentationEvent> events);
    }

    public interface IBattleHud
    {
        void Render(in HudSnapshot snapshot, in IReadOnlyList<PresentationEvent> events);
    }
}
```

- `Render` 第二个参数是 `PresentationEvent`（不是 `BattleEvent`），由 Bootstrap 层将 `BattleEvent` 转译为 `PresentationEvent` 后再下发；详见 §5。

### 3. BattleHud 不持有第二真值

规则（强制）：

1. **零缓存 BattleState**：`IBattleHud` / `IUnitPresenter` 不得保存 `BattleState` / `UnitState` / `BoardState` 引用。允许缓存快照（`HudSnapshot` / `UnitSnapshot`），但快照每帧被新值替换。
2. **只读派生**：所有 HUD 显示数据必须从 `HudSnapshot` 派生，不得通过 `BattleContext.Current` 或类似全局对象回查。
3. **数据源唯一**：游戏运行时只有一个数据源 = `BattleState`（由 Core 持有），所有其他表现对象都是其「投影」。

### 4. 表现失败不改 Core 结果

规则（强制）：

1. **`Render` 内异常吞咽**：所有 `IBoardPresenter.Render` / `IBattleHud.Render` / `IUnitPresenter.Render` 必须用 `try { ... } catch (Exception ex) { Debug.LogError(...); }` 包裹业务逻辑，不得让异常向上传播到 Core 调度器。
2. **Core 不订阅 Render 回调**：`BattleScheduler` 同步调用 `Render` 后即返回，不接受 Render 的回执事件。
3. **失败信号单向**：若 Render 失败，下一帧 Render 用新快照覆盖即可；不存在「回滚到失败前状态」。

### 5. Command 成功后才播放表现

规则（强制）：

1. **订阅 BattleEvent，不是 ICommand**：`Presenter` 通过 `IBattleEventSubscriber.Subscribe(handler)` 接收 `BattleEvent`，handler 仅在 Command 成功执行后才被调用。
2. **BattleEvent 定义**（由 Core 暴露）：

   ```csharp
   namespace Starfall.Core.Events
   {
       public readonly record struct BattleEvent(
           BattleEventKind Kind, ulong CommandSeq, UnitId? SourceUnitId,
           GridPos? Target, int Payload);
   }
   ```

3. **BattleEvent → PresentationEvent 转译**：由 `Starfall.Unity.Bootstrap.PresentationBridge` 在 Unity Bootstrap 中实现，将 `BattleEvent` 映射为 `PresentationEvent`（动画/音效/VFX 触发器）。`PresentationBridge` 不持有 `BattleState` 引用，每次转译基于传入的 `BattleEvent`。
4. **Bootstrap 调用顺序**（每次 Command 提交）：

   ```text
   Core.Scheduler.Execute(command)
       → 若成功：
           收集本回合所有 BattleEvent
           BattleState.NewSnapshot()
           IBoardPresenter.Render(in snapshot, in events)  // 同步
           IBattleHud.Render(in hudSnapshot, in events)   // 同步
       → 若失败：
           不调用 Render；BattleHud 通过下一回合 Render 自我修正
   ```

---

## Consequences

### 正面

- Task 16-18 实现依据清晰：MVP 只需实现 `IBoardPresenter` / `IUnitPresenter` / `IBattleHud` 三个接口 + `PresentationBridge` 转译层。
- Undo / Replay 自动正确：每次 Render 用最新快照覆盖，失败帧与成功帧行为一致。
- 异常隔离：表现层崩溃不会污染 Core 状态。
- 单向数据流便于后续接入 Animator / Timeline / Addressables（M3+），无需改动 Core。

### 负面 / 约束

- MVP 不支持表现层主动查询 Core（如「悬停高亮显示单位详细属性」），需 M3+ 引入 `IUnitQueryService`（独立 ADR）。
- 不支持表现层异步回调（如动画结束后通知 Core），需 M3+ 引入 `IPresentationCompletionToken`（独立 ADR）。
- `PresentationBridge` 是 Unity 层转译器，必须保证 `BattleEvent → PresentationEvent` 的纯函数性（同 BattleEvent + 同 BattleState 快照 = 同 PresentationEvent 列表）。

### 任务影响

| 任务 | 影响 |
| --- | --- |
| Task 16 | 实现 `IBoardPresenter` / `IUnitPresenter` + `PresentationBridge` |
| Task 17 | 实现 `IBattleHud` + HUD Prefab |
| Task 18 | 实现 Bootstrap 调用顺序（§5 流程图） |
| QA 测试 | Undo / Replay 后视觉一致性（参见 `Docs/05_Test_and_Acceptance.md`） |

---

## Alternatives considered

### MVP 双向订阅（Core ↔ Unity）

- **优点**：可实现「动画结束后通知 Core」「悬停高亮时查询 Core」等交互。
- **否决理由**：增加死锁（同步调用链）与重入（动画回调中触发新 Command）风险；MVP 阶段交互需求未明确，先严格单向。

### Reactive 流（R3 / UniRx）

- **优点**：声明式订阅、组合算子丰富。
- **否决理由**：第三方依赖；MVP 无复杂流组合需求；同步快照驱动已足够。

### 单一 BoardPresenter 单例

- **优点**：实现简单，全局访问。
- **否决理由**：与 §3「BattleHud 不持有第二真值」冲突；扩展性差（无法支持多视图：如缩略图、Replay 浏览器）。

### 选用「单向快照 + 同步 Render」（最终选择）

- **优点**：零额外依赖；同步调用栈便于调试；与 ADR-0001 `PostStateHash` 天然契合（Render 输入 = BattleState 哈希快照）。
- **缺点**：表现层无异步能力（M3+ 再补）。

---

## References

- `AGENTS.md` §10.3（Unity 硬约束：Presenter 不保存独立战斗真值；Transform 不是战斗状态）
- ADR-0001（Core 数据模型与哈希契约）
- `Docs/02_Technical_Development_Manual.md`（待补 Presenter 同步一节，Task 16 前）