# Tests/EditMode/Map/Commands/ — MAP-08 测试集

doc2 §3.4 / MAP_SYSTEM_AUDIT §4 Row 135 验证矩阵 + Lead self-fix playbook 6 类常见 bug 防御。

## 文件

- `FlipTilePhaseTests.cs` — 单 tile 翻转（≥14 tests）
- `FlipRegionPhaseTests.cs` — 区域翻转（≥8 tests）
- `FallResolutionTests.cs` — Fall 解析（≥16 tests）
- `PhaseCompressionTests.cs` — 挤压解析（≥10 tests）
- `MultiTilePhaseFlipTests.cs` — 多 tile flip + LOS / Cover / Height 集成（≥8 tests）
- `FallingCommandCompatTests.cs` — FallingCommand 重构版 + BattleEvent 集成（≥6 tests）

## 测试目标数

- **目标**：≥65 tests / ALL PASS
- **验收 #12**：每个 fixture 第一项测试断言任务 ID 字符串 `"MAP-08"`（用户 2026-07-14 14:18 规则）

## 严格确定性

- 集合遍历按 GridCoord.CompareTo（Y → X → Layer）升序
- 距离并列时 CompareTo 作为 tie-break
- 不读时间、不读线程、不读 Unity 实例
- 不引入随机源（fallback 优先 Manhattan distance，再 CompareTo）

## 攻防要点（Lead self-fix playbook）

1. `.cs.meta` 必须遵循最小 YAML 格式（`fileFormatVersion: 2 + guid: ...`）
2. 拼写错：TerrainType 枚举固定 Plain/Rough/Ruins/Wall/BrokenBridge/LightBridge/Void/ShallowAstralTide/DeepAstralTide/GateTile/AnchorTile
3. 数组初始化：多层嵌套初始化分行写
4. 扩展方法歧义：避免直接对 IReadOnlyList 调用 .Contains()
5. 校验范围：测试期望 throw 时不要把生产代码 clamp
6. 跨层测试必须注册 Astral 层
