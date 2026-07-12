# Starfall.Unity

Unity 表现层（Bootstrap / Presenter / HUD / Input）。

## 职责

- Unity 启动与场景管理。
- 输入捕获（鼠标 / 键盘 / 手柄）→ Command。
- Presenter 订阅 `BattleEvent` 并播放表现（动画 / 特效 / 音效）。
- HUD 显示（回合 / AP / 目标 / 单位属性）。
- 加载 `Starfall.Data` 输出的 JSON Definition 并喂给 Core。

## 依赖

- `Starfall.Core`（Command / BattleState / BattleEvent 接口）。
- `Starfall.Data`（Definition 加载）。
- UnityEngine / UnityEngine.UI（`noEngineReferences: false`，`autoReferenced: true`）。

## 硬约束（AGENTS.md §10.3）

- 不持有第二份战斗真值（详见 ADR-0002 §3）。
- 不复制 Core 的伤害 / 移动 / 相位 / 锚点 / 律令业务规则。
- `Transform` 不是战斗状态。
- Command 成功后才播放表现（订阅 `BattleEvent`，不是 `ICommand`，详见 ADR-0002 §5）。
- 表现失败不改变 Core 结果（`Render` 异常吞咽 + `Debug.LogError`，详见 ADR-0002 §4）。