# KNOWN LIMITATIONS · MVP "断裂点三号"

> 最后更新：2026-07-13

本文档记录 MVP 纵切版本中**有意未实现**或**已知存在问题**的功能。这些不是 bug，而是 MVP 范围外的设计选择。

## 1. 范围外功能（AGENTS.md §12 禁止）

未经用户单独批准，**绝不实施**：

- 暴击
- 随机闪避 / 随机掉落
- 装备系统 / 角色养成 / 抽卡 / 商业化
- Addressables
- 自定义关卡编辑器
- 联机 / 多人
- 正式存档（当前 UndoStack 是内存栈）
- 复杂行为树 AI（当前是 ImprovedEnemyAI 单层确定性 AI）
- 付费美术资产
- 未在项目文档定义的新战斗机制

## 2. 已降级 / 简化实现

| 功能 | 当前实现 | 后续计划 |
|---|---|---|
| **锚点围区可视化** | LineRenderer 简单多边形 + BoardPalette 颜色 | 完整多边形位置数据 + 编辑器 |
| **律令** | DecreeRegistry 持有少量内置律令；D 键循环选择 | JSON 加载自定义律令 |
| **撤退 / 击退** | 当前仅在 DamageFormula 内置 phase modifier | 显式 KnockbackCommand |
| **Phase 2 / Phase 3 角色技能** | 未实现 | 后续扩展 |
| **AI 难度** | 仅 ImprovedEnemyAI 一种 | 多档难度 |
| **HUD 视觉** | uGUI Text + 默认字体 | 美术优化 + TMPro 样式 |

## 3. Stub / Fallback 文件

为避免破坏既有测试，**Stub 文件暂时保留**：

- `Assets/Starfall/Unity/StubBoardPresenter.cs`
- `Assets/Starfall/Unity/StubBattleHud.cs`

当前 `BattleBootstrap.cs` 默认挂载 `RealBoardPresenter` / `RealBattleHud`。如需测试 Stub 路径，可手动在 Inspector 切换。

## 4. 视觉验证缺口

按 AGENTS.md 流程，M-35 视觉验证由用户手动执行：

- ✅ Lead 通过 batchmode 验证编译 + EditMode 测试
- ❌ 未在 PlayMode 中由 Lead 跑过视觉验证（无图形环境）
- ✅ 用户已确认 Task 16 视觉（M-35）

## 5. 文档已知缺口

- `docs/ADR/` 仅有 ADR-0001 / ADR-0002。后续 Task（如新 Command、新 Presenter）应追加 ADR
- 中文文档与代码注释风格统一性未做 Lint
- `docs/OPENCLAW_REPOSITORY_AUDIT.md` 累积 ~3700 行，仅作为审计存档，不替代 README / IMPLEMENTATION_STATUS

## 6. 性能 / 规模

- 单场战斗：80 格 + 10 单位，EditMode 跑通
- 性能未做 profiling（不在 MVP 范围）
- 未做 mobile / console 适配

## 7. 持续保留的安全机制

- **Core 无 UnityEngine**：由 `CoreDependencyGuardTests` 自动验证
- **BattleState 唯一真值**：Presenter / InputController / RealBoardPresenter 不持有 BattleState；通过 grep 验证
- **确定性**：相同初始 + 相同 commands → 相同 Outcome + GuardsCompleted（由 LevelLoopTests 验证）
- **Replay 一致**：Round-trip 后 Hash 不变
- **Undo 删除历史**：Undo 不破坏 Replay 序列

## 8. 后续可清理项

如需精简仓库（不在 MVP 范围内，仅作建议）：

- 删除 `Backup/pre-merge-2026-07-13-0915` tag（旧 main snapshot）
- 删除已合并的 17 个 `agent/*` 分支（保留可回溯）
- 删除 `scripts/generate_meta.ps1`（仅用于生成 Unity .meta 文件一次性工具）
- 合并 `docs/OPENCLAW_REPOSITORY_AUDIT.md` 章节至独立 Task 报告文件（当前 §1-§12 单文件 ~3700 行）

## 9. 风险与缓解

| 风险 | 等级 | 缓解 |
|---|---|---|
| 玩家通关时长 | 中 | 当前 MVP 节奏未做玩家测试；建议 60-90 分钟通关 |
| 规则理解门槛 | 中 | GDD §3 / §4 已写明核心术语；HUD 实时提示模式 |
| Replay 文件兼容性 | 低 | ReplayCodec 是版本化 JSON，加 magic + version 字段 |
| 资产 License | 低 | 全 Unity 原生，无外部资产 |
