# MAP-12 Gate Report (qa independent verification)

## 验证环境

- **Unity**: 6000.5.3f1 (Editor: `C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe`)
- **验证分支**: `agent/qa-map-12-gate`
- **实现分支（merged in）**: `agent/map-12-anchor-link` (HEAD: `8048e16`)
- **基于 main HEAD**: `5832e8c` (merge base 验证一致)
- **验证时间**: 2026-07-16 11:08 GMT+8
- **验证人**: xingyuan-qa (subagent, 独立验证)
- **工作区**: `D:\AI-Worktrees\Xingyuan\qa`

## 测试结果

- **EditMode 总数**: **1326 / 1326 PASS** ✓
- **Failed**: 0
- **Skipped**: 0
- **Duration**: 1.1077322 s
- **Result**: `Passed`
- **测试结果文件**: `D:\AI-Worktrees\Xingyuan\qa\Logs\qa-map-12-editmode-results.xml`
- **Unity 日志**: `D:\AI-Worktrees\Xingyuan\qa\Logs\qa-map-12-editmode.log`

### Test Suite 分布

| Suite | Cases | Passed | Failed |
|-------|-------|--------|--------|
| XingyuanCovenant (总) | 1326 | 1326 | 0 |
| Starfall | 1326 | 1326 | 0 |
| EditMode | 1326 | 1326 | 0 |
| Map | 1138 | 1138 | 0 |
| **Anchor** (新) | **97** | **97** | **0** |
| Collapse | 186 | 186 | 0 |
| Commands | 212 | 212 | 0 |
| Coordinates | 61 | 61 | 0 |
| Cover | 23 | 23 | 0 |
| Height | 37 | 37 | 0 |
| LineOfSight | 35 | 35 | 0 |
| Pathfinding | 93 | 93 | 0 |
| Regions | 141 | 141 | 0 |
| State | 45 | 45 | 0 |
| Tile | 208 | 208 | 0 |

### MAP-12 专项 Test Fixtures (140 cases)

| Test Fixture | Passed | Failed |
|--------------|--------|--------|
| AnchorLinkClonerTests | 7 / 7 | 0 |
| AnchorLinkHasherTests | 12 / 12 | 0 |
| AnchorLinkTests | 14 / 14 | 0 |
| BatchTransitionAnchorLinksCommandTests | 9 / 9 | 0 |
| ConstellationPolygonTests | 17 / 17 | 0 |
| ConstellationValidatorTests | 17 / 17 | 0 |
| ConstellationVertexTests | 8 / 8 | 0 |
| Map12_HashStability_Tests | 7 / 7 | 0 |
| Map12_Regression_Tests | 7 / 7 | 0 |
| Map12_TaskId_AssertedString_Tests | 8 / 8 | 0 |
| RegisterAnchorLinkCommandTests | 9 / 9 | 0 |
| TransitionAnchorLinkStateCommandTests | 12 / 12 | 0 |
| UnregisterAnchorLinkCommandTests | 6 / 6 | 0 |
| UpdateConstellationPolygonCommandTests | 7 / 7 | 0 |
| **Total MAP-12** | **140** | **0** |

## 编译验证

- **命令**: `Unity.exe -batchmode -nographics -projectPath . -quit -logFile Logs/qa-map-12-compile.log`
- **退出码**: 0 ✓
- **`error CS` 计数**: 0 ✓
- **`warning CS` 计数**: 0 ✓ (无新增；亦无 pre-existing 残留)
- **编译日志**: `D:\AI-Worktrees\Xingyuan\qa\Logs\qa-map-12-compile.log`

## Core 依赖守卫

`Assets/Starfall/Core/Map/Anchor/*.cs`（8 个新文件）：

| File | using UnityEngine | using UnityEditor |
|------|-------------------|-------------------|
| AnchorLink.cs | 0 | 0 |
| AnchorLinkCloner.cs | 0 | 0 |
| AnchorLinkHasher.cs | 0 | 0 |
| AnchorLinkId.cs | 0 | 0 |
| ConstellationPolygon.cs | 0 | 0 |
| ConstellationPolygonId.cs | 0 | 0 |
| ConstellationValidator.cs | 0 | 0 |
| ConstellationVertex.cs | 0 | 0 |
| **Total** | **0** | **0** |

**Core 依赖守卫: 8 / 8 PASS** ✓（任务原 spec 要求 4/4，实际 8 个 Anchor 源文件全部 0 引用）

> 备注：守卫扫描范围已扩展至全部 `Assets/Starfall/Core/Map/**/*.cs` (含 State / Commands)，共 0 违规。

## ADR-0009 tag 修正确认

文件：`Assets/Starfall/Core/Map/Anchor/AnchorLinkHasher.cs`

| Tag | 期望值 | 实际值 | 状态 |
|-----|--------|--------|------|
| `TagAnchorLinkId` | 0x43 | 0x43 | ✓ |
| `TagVertexEntry` | 0x44 | 0x44 | ✓ |
| `TagAnchorLinkCurrentState` | 0x45 | 0x45 | ✓ |
| `TagAnchorLinkStateTick` | 0x46 | 0x46 | ✓ |
| `TagAnchorLinkPostStateHash` | 0x47 | 0x47 | ✓ |
| `0x40 / 0x41` 冲突残留 | 必须无 | 仅在文档注释中引用 legacy TagAnchorZoneId / TagAnchorOwner | ✓ |

AnchorLinkHasher.cs 注释（line 18-25）明确说明：
> 任务原 spec 建议 `0x40/0x41`，但 [ADR-0003 §4] 既有字段已占用 `0x40 = TagAnchorZoneId`、`0x41 = TagAnchorOwner`、`0x42 = TagAnchorVertex`。本实现改用 `0x43-0x47`（与 legacy anchor sub-tags `0x40-0x42` 邻接但**无碰撞**）。

**ADR-0009 tag 冲突: 已解决** ✓

## 既有 MAP 回归

### TaskId_AssertedString 套件（per spec）

| Fixture | Passed | Failed |
|---------|--------|--------|
| Map03_TaskId_AssertedString_Tests | 17 / 17 | 0 ✓ |
| Map05_TaskId_AssertedString_Tests | 14 / 14 | 0 ✓ |
| Map09_TaskId_AssertedString_Tests | 8 / 8 | 0 ✓ |
| Map11_TaskId_AssertedString_Tests | 9 / 9 | 0 ✓ |
| Map12_TaskId_AssertedString_Tests | 8 / 8 | 0 ✓ |

### Map12_Regression_Tests（最小回归断言，按 doc2 编号）

| Test | Result |
|------|--------|
| Regression_MapState_PublicSignatures_Preserved | ✓ PASS |
| Regression_BattleState_PublicSignatures_Preserved | ✓ PASS |
| Regression_Tile_AddRemove_StillWorks (MAP-04) | ✓ PASS |
| Regression_IMapCommand_RunAndUndo_StillWorks (MAP-03) | ✓ PASS |
| Regression_ModifyAnchorState_StillWorks (MAP-07/08) | ✓ PASS |
| Regression_GlobalCV_StillUsable (MAP-11a) | ✓ PASS |
| Regression_LocalCV_AddGetRemove (MAP-11a) | ✓ PASS |

> 注：MAP-04/06/07/08 在 EditMode 套件中**没有独立 TaskId 文件**（因原 spec 早期未硬性要求 TaskId 串行化断言），但已通过 Map12_Regression_Tests 的 7 条最小回归断言覆盖其核心 API 路径。**未发现断裂。**

### 既有 18 字段 tag 字节流保持

`MapStateHasher.cs` 中：

- 0x10-0x16 字段（MapId / Width / Height / InitialActiveLayer / InitialGlobalCollapseValue / TilesetId / EnvironmentScheduleId）：**未改**
- 0x20-0x22 字段（Version / ActiveLayer / GlobalCollapseValue）：**未改**
- 0x30-0x37 字段（Tiles / Anchors / Regions / MapObjects / RegionStates / SpawnPoints / GlobalCV / LocalCVs）：**未改**
- 0x38（AnchorLinks，MAP-12 增量）：**新增，邻接且无碰撞** ✓

**`MapState.Anchors` legacy 字段保留** ✓（line 107, 139, 206-215）— 与新 `AnchorLinks` 共存，未删除。

## 范围验证（git diff origin/main..HEAD）

### 统计

| 类别 | 数量 | 备注 |
|------|------|------|
| 新增 .cs | 27 | Anchor (8) + Commands (5) + Tests Anchor (9) + Tests Commands (5) |
| 修改 .cs | 3 | MapState.cs / MapStateCloner.cs / MapStateHasher.cs |
| **.cs 合计** | **30** | （gameplay 报告 "26" 口径未含 3 个 modified existing files）|
| 新增 .cs.meta | 27 | 全部为新增 .cs 配套（modified .cs 的 .meta 引用 GUID 不变） |
| 新增目录 .meta | 2 | `Assets/Starfall/Core/Map/Anchor.meta` + `Assets/Starfall/Tests/EditMode/Map/Anchor.meta` |
| **.meta 合计** | **29** | |
| **总变更文件** | **59** | 4152 insertions, 1 deletion |

### 范围守卫

| 路径 | 修改数 | 期望 | 状态 |
|------|--------|------|------|
| `Assets/Starfall/Unity/*` | 0 | 0 | ✓ |
| `Packages/manifest.json` | 0 | 0 | ✓ |
| `ProjectSettings/*` | 0 | 0 | ✓ |
| `Assets/Starfall/Core/Anchor/*` | 0 (未改) | 不应改 | ✓ |
| `Assets/Starfall/Core/Map/Anchor/*` | 8 (新增) | 新增 | ✓ |
| `Assets/Starfall/Core/Map/Commands/*` | 5 (新增) | 新增 | ✓ |
| `Assets/Starfall/Core/Map/State/*` | 2 modified + 0 new | 增量 | ✓ |
| `Docs/ADR/ADR-0009-anchor-link-polygon.md` | **不在此分支** | 见"已知问题" §1 | ⚠️ |

## 偏差说明

1. **AnchorLinkId.Value / ConstellationPolygonId.Value 仍为 string**（`readonly struct` 包 `string Value`）— 与 §7.1 一致 ✓
2. **AnchorZoneState 复用 8 值**（Inactive=0, PlayerControlled=1, EnemyControlled=2, Neutral=3, Overloaded=4, Damaged=5, Destroyed=6, Locked=7）— `AnchorLinkState` 通过 `(AnchorZoneState)int` cast 复用，与 §7.2 一致 ✓
3. **CreateAnchorLinkCommand 不双写** — 旧命令（MAP-03）继续写 `MapState.Anchors`（legacy AnchorZone），新 `RegisterAnchorLinkCommand` 写 `MapState.AnchorLinks`（新集合）。两条路径操作不同字段，未做双写以避免数据冲突。接受（与 §7.3 一致） ✓

## 已知问题

### 1. ADR-0009 文档本身**不在本分支**

- **事实**：`Docs/ADR/ADR-0009-anchor-link-polygon.md` 位于 `agent/adr-0009-anchor-link-polygon` 分支（commit `8f3d7f7`），**未合并**到 `agent/map-12-anchor-link`。
- **影响**：
  - **编译/测试影响**：无。代码注释中 `// ADR-0009 §9` 仅是注释引用，git 编译器和 NUnit 不读。
  - **文档一致性影响**：合并到 main 后，若未同时合并 `agent/adr-0009-anchor-link-polygon` 分支，则 main 上 ADR-0009 文档缺失。代码中的 `// ADR-0009 §9 ComputeStateHash` / `// ADR-0009 §9 — AnchorLink sub-tags` 等注释将指向**不存在**的文档。
- **建议**（不阻塞 Gate，但 Lead 决策前需注意）：
  - 选项 A：先合 `agent/adr-0009-anchor-link-polygon`（仅含 ADR 文档，~611 行）→ 再合 `agent/map-12-anchor-link`。
  - 选项 B：把 ADR-0009 文件 rebase 进 `agent/map-12-anchor-link` 末尾，重新跑 Gate。
  - 选项 C：合并后 Lead 提示用户，main 上的 ADR 编号存在 1-7 + 8 缺口，ADR-0009 文档独立 merge。

### 2. ADR-0009 文件编码为 UTF-16 LE (BOM: FF FE)

- **事实**：`8f3d7f7` 中提交的文件以 `FF FE` 开头，每个 ASCII 字符后跟 `\x00`，中文为 UTF-16 LE 双字节。
- **影响**：与同目录 `ADR-0001` ~ `ADR-0007`（纯 UTF-8 / ASCII）编码风格不一致。PowerShell `cat` 在默认 GBK/UTF-8 codepage 下显示为 `??`（这与"实际乱码"是两类问题 — 文件本身是合规的 UTF-16，只是与其他 ADR 文件编码不一致）。
- **建议**：architect 后续在 `agent/adr-0009-anchor-link-polygon` 上将 ADR 转码为 UTF-8（无 BOM），与其他 ADR 文件保持一致。**不阻塞本 Gate**。

## 结论

**PASS** — MAP-12 (AnchorLink + ConstellationPolygon) 实现分支可合 main，前提是 Lead 决定 ADR-0009 文档的合并策略（见"已知问题 §1"）。

实现侧全部通过：
- 1326/1326 EditMode 测试 ✓
- 编译退出码 0，0 errors / 0 warnings ✓
- Core 依赖守卫 8/8 ✓
- ADR-0009 tag 0x43-0x47 修正到位，无 0x40/0x41 冲突 ✓
- 既有 18 字段 tag 字节流未改 ✓
- `MapState.Anchors` legacy 字段保留 ✓
- MAP-02/03/04/07/08/09/11a 回归断言 7/7 PASS ✓
- 范围守卫：Unity / manifest / ProjectSettings 0 修改 ✓

## QA 提交

- 分支: `agent/qa-map-12-gate`
- 待提交: `Docs/qa-reports/map-12-gate.md`（本报告）
- 提交类型: `docs(qa): MAP-12 Gate verification (qa independent)`
- **不 push**（user 未批准；按 AGENTS.md §9 "必须先问" 规则）
