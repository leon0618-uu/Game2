# MANUAL ACCEPTANCE CHECKLIST · MVP "断裂点三号"

> 最后更新：2026-07-13  
> 执行人：用户（手动）  
> 环境：Unity 6.5 (6000.5.3f1) · Windows PC · URP

按 AGENTS.md §1 / Docs/05 / M-35，本清单验证 MVP 完整性。所有项目应通过；任何失败项需报告 Lead。

## 0. 环境检查

- [ ] Unity Hub 已安装 6000.5.3f1
- [ ] 仓库根目录可被 Unity Hub 识别
- [ ] 首次 Package Manager 解析完成（5-10 分钟）
- [ ] `Assets/StreamingAssets/data/battle_default.json` 存在
- [ ] 编辑器 Console 无编译错误

## 1. 战斗初始化

- [ ] 创建空场景（File → New Scene → Basic (Built-in)）
- [ ] 创建空 GameObject（GameObject → Create Empty），命名为 `BattleBootstrap`
- [ ] 添加 `Starfall.Unity.BattleBootstrap` 组件
- [ ] Play 模式启动
- [ ] 预期结果：
  - Scene 中自动出现 8×10 棋盘（Quad 阵列）
  - 4 个 Player 单位（蓝色 / 不同位置）
  - 6 个 Enemy 单位（红色 / 不同位置）
  - 顶部 HUD 显示当前 Phase（Guard）+ Round + AP/PV/CV
  - Console 出现 `[BattleBootstrap] Auto-attached RealBoardPresenter / RealBattleHud / InputController` 三条 Debug.Log

## 2. 移动（M 键）

- [ ] 按 `M` → HUD 提示"Move Target"模式
- [ ] 棋盘合法格高亮（绿色光晕）
- [ ] `↑ ↓ ← →` 移动光标
- [ ] 选合法格 → `Enter` 或点击 → 单位移动到该格
- [ ] HUD 更新（PV 减少）
- [ ] 预期：移动后 BattleState 哈希变化（可在 Editor Inspector 看到）
- [ ] 按 `Esc` → 模式复位，光标在原单位上

## 3. 攻击（A 键）

- [ ] 选一个 Player 单位 → 按 `A`
- [ ] HUD 提示"Attack Target"
- [ ] 邻格敌对单位高亮（红色边框）
- [ ] 选一个 → `Enter`
- [ ] 预期：
  - 伤害数字浮现在目标上方
  - 目标 HP 减少
  - 若目标 HP ≤ 0 → 目标消失 / 标记为死亡
  - BattleState 哈希变化

## 4. 相位翻转（F 键）

- [ ] 选一个 Player 单位 → 按 `F`
- [ ] HUD 提示"Phase Flip Target"
- [ ] 选邻格 → `Enter`
- [ ] 预期：
  - 该格 Phase 颜色切换（Light ↔ Dark）
  - 若该格有单位，触发 PhaseFlipValidator 检查（保持原位 / 翻转 / 坠落）
  - BattleState 哈希变化

## 5. 律令（D 键）

- [ ] 按 `D` → HUD 提示"Decree Select"
- [ ] 当前可用律令显示在 HUD
- [ ] `Enter` 或再次按 `D` 循环选择
- [ ] 确认 → 律令被 ApplyDecreeCommand 写入 EventSink
- [ ] 预期：Console 出现 `BattleEvent.DecreeApplied`

## 6. 撤销（Z 键）

- [ ] 执行任何上述操作后，按 `Z`
- [ ] 预期：
  - BattleState 回到上一步
  - EventSink 删除最后一条
  - HUD 显示旧状态
  - **撤销会推高 CV**（AGENTS.md §1 核心术语）

## 7. 结束回合（Space）

- [ ] 按 `Space`
- [ ] 预期：
  - EndTurnCommand 执行（TurnNumber++）
  - TickEndTurnCommand 执行（状态衰减）
  - 切换到 Enemy 回合 → AI 自动行动
  - 切回 Player 回合

## 8. 防守阶段（Guard → Retreat 切换）

- [ ] 持续按 `Space` 完成 3 个 Player 回合（每个回合 Player 至少 1 个单位存活 + 至少 1 个 Enemy 单位存活）
- [ ] 预期：
  - 第 3 次成功 EndTurn 后，HUD 显示 "ObjectivePhase = Retreat"
  - Console 出现 `BattleEvent.ObjectiveAdvanced`
  - 退出目标 ExitTile 在棋盘上有视觉标识

## 9. 撤离阶段（Retreat 胜负）

- [ ] 所有 Player 单位移到 ExitTile（或邻接 4-邻居）
- [ ] 按 `Space`
- [ ] 预期：
  - HUD 显示 "BattleOutcome = PlayerWins"
  - Console 出现 `BattleEvent.RetreatComplete`
  - 输入被拒绝（战斗已结束）

## 10. 战败路径

- [ ] 重新启动战斗，让所有 Player 单位 HP 归零
- [ ] 预期：
  - HUD 显示 "BattleOutcome = EnemyWins"

## 11. Replay

- [ ] 在战斗进行中，通过编辑器断点或程序化调用 `ReplayFile.WriteAsync` 保存当前 Replay
- [ ] 重启 Play 模式，调用 `ReplayFile.Read` + `ReplayPlayer.Play`
- [ ] 预期：
  - 重建的 BattleState 与原 BattleState 哈希一致
  - Replay 序列（Command 序列）一致

## 12. Undo 完整性

- [ ] 执行 M → A → F → Space 各一次
- [ ] 按 `Z` 4 次
- [ ] 预期：
  - BattleState 完全回到操作前
  - CV 累计 +4
  - Replay 历史被删除（无法 Replay 已撤销操作）

## 13. 确定性验证

- [ ] 在相同初始 state 下，记录一个 Command 序列（如 M1 → A1 → F1 → Space）
- [ ] 重复执行两次
- [ ] 比对两次的最终 BattleState Hash
- [ ] 预期：完全相同

## 14. Core 依赖守卫（自动化）

执行 EditMode 测试：
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "<repo root>" `
  -runTests -testPlatform EditMode `
  -testResults "Logs/manual-acceptance-results.xml" `
  -logFile -
```

预期：179 / 179 PASS，0 错误。

## 15. 已知限制确认

参考 [docs/KNOWN_LIMITATIONS.md](KNOWN_LIMITATIONS.md)：
- [ ] 理解"未实施范围"（§1）
- [ ] 理解 Stub 文件保留（§3）
- [ ] 理解视觉验证缺口（§4）

## 通过 / 失败记录

| 项 | 通过 / 失败 | 备注 |
|---|---|---|
| 0. 环境 | | |
| 1. 初始化 | | |
| 2. 移动 | | |
| 3. 攻击 | | |
| 4. 相位翻转 | | |
| 5. 律令 | | |
| 6. 撤销 | | |
| 7. 结束回合 | | |
| 8. 防守→撤离 | | |
| 9. 撤离胜利 | | |
| 10. 战败 | | |
| 11. Replay | | |
| 12. Undo | | |
| 13. 确定性 | | |
| 14. Core 守卫 | | |
| 15. 限制理解 | | |

**签名**：___________  **日期**：___________

---

任何失败项请截图 + 描述步骤报告给 Lead（xingyuan-lead）。
