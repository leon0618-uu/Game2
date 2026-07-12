# 03｜《星渊誓约》数据与内容规范

## 1. 数据目录

```text
Assets/Starfall/Data/Definitions/
├─ levels/
│  └─ mvp_gate_fault_03.json
├─ units/
│  ├─ player_units.json
│  └─ enemy_units.json
├─ skills/
│  └─ mvp_skills.json
├─ decrees/
│  └─ mvp_decrees.json
└─ statuses/
   └─ statuses.json
```

全部使用：

```text
UTF-8
LF
JSON
```

## 2. ID 规范

ID：

- 小写；
- `snake_case`；
- 全局同类型唯一；
- 发布后不因显示名改变而改变。

前缀：

```text
level_
unit_
skill_
decree_
status_
objective_
```

实例 ID 由战斗构建器确定生成，不能依赖 Dictionary 遍历顺序。

## 3. 枚举字符串

```text
LayerId: Reality | Astral
TeamId: Player | Enemy
DamageType: Physical | Magical
TargetType: Self | Unit | Tile
ObjectiveType: Defend | Evacuate
BattleOutcome: None | Won | Lost
```

TileTag：

```text
PhaseMutable
Anchor
DecreeDeployable
DefenseZone
EvacuationZone
Blocked
Hazard
```

未知枚举值必须报错。

## 4. LevelDefinition

建议结构：

```json
{
  "id": "level_gate_fault_03",
  "displayName": "断裂点三号",
  "width": 8,
  "height": 10,
  "initialResources": {
    "pv": 3,
    "cv": 0,
    "collapseThreshold": 100
  },
  "maxRound": 12,
  "tiles": [],
  "playerSpawns": [],
  "enemySpawns": [],
  "objective": {},
  "enemyAi": {}
}
```

### TileDefinition

```json
{
  "x": 0,
  "y": 0,
  "activeLayer": "Reality",
  "realityWalkable": true,
  "astralWalkable": true,
  "tags": []
}
```

`tiles` 必须覆盖全部 80 个坐标，不能重复。

## 5. 断裂点三号坐标基线

### 玩家出生点

```text
player_vanguard  (2,1)
player_arcanist  (3,1)
player_ranger    (4,1)
player_warden    (5,1)
```

### 敌人出生点

```text
enemy_raider_01  (1,8)
enemy_raider_02  (3,8)
enemy_guard_01   (5,8)
enemy_caster_01  (6,7)
```

### 防守区域

```text
(3,4) (4,4)
(3,5) (4,5)
```

### 撤离区域

```text
(0,8) (1,8)
(0,9) (1,9)
```

若撤离格与敌人出生冲突，敌人出生必须调整，最终配置不得重叠。推荐将 `enemy_raider_01` 调整为 `(2,8)`。

### 锚点

```text
(2,4)
(5,4)
(2,6)
(5,6)
```

### 相位测试地块

用于挤压：

```text
(2,5)
Reality walkable
Astral blocked
PhaseMutable
```

用于坠落：

```text
(4,6)
Reality walkable
Astral blocked
PhaseMutable
```

用于反向相位通路：

```text
(3,7)
Reality blocked
Astral walkable
PhaseMutable
```

### 律令部署区

建议：

```text
(3,6)
(4,6)
(3,7)
(4,7)
```

添加 `DecreeDeployable`，但必须按最终通行状态校验。

## 6. UnitDefinition

```json
{
  "id": "unit_player_vanguard",
  "displayName": "誓锋",
  "team": "Player",
  "layer": "Reality",
  "maxHp": 120,
  "pow": 75,
  "arc": 20,
  "arm": 35,
  "res": 20,
  "mov": 4,
  "maxAp": 2,
  "skillIds": ["skill_star_slash"]
}
```

校验：

- MaxHP > 0；
- HP 初始等于 MaxHP，除非关卡显式指定；
- 属性 ≥ 0；
- MOV ≥ 0；
- MaxAP > 0；
- 技能存在；
- 出生坐标合法且不重叠。

## 7. 玩家单位基线

### Vanguard

```json
{
  "id": "unit_player_vanguard",
  "displayName": "誓锋",
  "team": "Player",
  "layer": "Reality",
  "maxHp": 120,
  "pow": 75,
  "arc": 20,
  "arm": 35,
  "res": 20,
  "mov": 4,
  "maxAp": 2,
  "skillIds": ["skill_star_slash"]
}
```

### Arcanist

```json
{
  "id": "unit_player_arcanist",
  "displayName": "星咏",
  "team": "Player",
  "layer": "Reality",
  "maxHp": 85,
  "pow": 25,
  "arc": 90,
  "arm": 15,
  "res": 35,
  "mov": 4,
  "maxAp": 2,
  "skillIds": ["skill_astral_bolt"]
}
```

### Ranger

```json
{
  "id": "unit_player_ranger",
  "displayName": "裂弦",
  "team": "Player",
  "layer": "Reality",
  "maxHp": 90,
  "pow": 65,
  "arc": 30,
  "arm": 20,
  "res": 25,
  "mov": 5,
  "maxAp": 2,
  "skillIds": ["skill_rift_shot"]
}
```

### Warden

```json
{
  "id": "unit_player_warden",
  "displayName": "守誓",
  "team": "Player",
  "layer": "Reality",
  "maxHp": 105,
  "pow": 45,
  "arc": 60,
  "arm": 28,
  "res": 40,
  "mov": 4,
  "maxAp": 2,
  "skillIds": ["skill_oath_pulse"]
}
```

## 8. 敌方单位基线

### Raider

```json
{
  "id": "unit_enemy_raider",
  "displayName": "裂隙袭击者",
  "team": "Enemy",
  "layer": "Reality",
  "maxHp": 80,
  "pow": 60,
  "arc": 10,
  "arm": 18,
  "res": 15,
  "mov": 4,
  "maxAp": 2,
  "skillIds": ["skill_enemy_strike"]
}
```

### Guard

```json
{
  "id": "unit_enemy_guard",
  "displayName": "断层守卫",
  "team": "Enemy",
  "layer": "Reality",
  "maxHp": 130,
  "pow": 55,
  "arc": 15,
  "arm": 40,
  "res": 25,
  "mov": 3,
  "maxAp": 2,
  "skillIds": ["skill_enemy_strike"]
}
```

### Caster

```json
{
  "id": "unit_enemy_caster",
  "displayName": "星渊投影",
  "team": "Enemy",
  "layer": "Reality",
  "maxHp": 70,
  "pow": 15,
  "arc": 70,
  "arm": 12,
  "res": 30,
  "mov": 3,
  "maxAp": 2,
  "skillIds": ["skill_enemy_bolt"]
}
```

关卡可从这些 Definition 创建多个实例，实例 ID 唯一。

## 9. SkillDefinition

```json
{
  "id": "skill_star_slash",
  "displayName": "断星斩",
  "damageType": "Physical",
  "power": 110,
  "minRange": 1,
  "maxRange": 1,
  "requiresLineOfSight": false,
  "targetType": "Unit",
  "applyStatus": null,
  "selfStatus": null
}
```

MVP 射程采用曼哈顿距离。

无障碍远程攻击暂不实现复杂视线；`requiresLineOfSight=false`。

### MVP 技能数据

```json
[
  {
    "id": "skill_star_slash",
    "displayName": "断星斩",
    "damageType": "Physical",
    "power": 110,
    "minRange": 1,
    "maxRange": 1,
    "requiresLineOfSight": false,
    "targetType": "Unit"
  },
  {
    "id": "skill_astral_bolt",
    "displayName": "星辉术",
    "damageType": "Magical",
    "power": 115,
    "minRange": 1,
    "maxRange": 3,
    "requiresLineOfSight": false,
    "targetType": "Unit"
  },
  {
    "id": "skill_rift_shot",
    "displayName": "裂弦射击",
    "damageType": "Physical",
    "power": 95,
    "minRange": 2,
    "maxRange": 4,
    "requiresLineOfSight": false,
    "targetType": "Unit",
    "applyStatus": {
      "statusId": "status_marked",
      "turns": 1
    }
  },
  {
    "id": "skill_oath_pulse",
    "displayName": "守誓冲击",
    "damageType": "Magical",
    "power": 85,
    "minRange": 1,
    "maxRange": 2,
    "requiresLineOfSight": false,
    "targetType": "Unit",
    "selfStatus": {
      "statusId": "status_guarding",
      "turns": 1
    }
  },
  {
    "id": "skill_enemy_strike",
    "displayName": "裂击",
    "damageType": "Physical",
    "power": 90,
    "minRange": 1,
    "maxRange": 1,
    "requiresLineOfSight": false,
    "targetType": "Unit"
  },
  {
    "id": "skill_enemy_bolt",
    "displayName": "渊流",
    "damageType": "Magical",
    "power": 90,
    "minRange": 1,
    "maxRange": 3,
    "requiresLineOfSight": false,
    "targetType": "Unit"
  }
]
```

## 10. StatusDefinition

建议：

```json
{
  "id": "status_stunned",
  "displayName": "眩晕",
  "stackPolicy": "RefreshDuration",
  "maxStacks": 1,
  "defaultTurns": 1,
  "rule": "Stunned"
}
```

MVP：

```text
status_stunned
status_paralyzed
status_move_zero_next_turn
status_guarding
status_marked
```

行为由 Core Handler 实现，JSON 只选择规则和参数。

## 11. DecreeDefinition

```json
{
  "id": "decree_gravity_rift",
  "displayName": "引力律令·崩裂",
  "trigger": "EnemyMoveStepEnterTile",
  "range": 3,
  "charges": 1,
  "singleUse": true,
  "effect": {
    "type": "PerpendicularKnockback",
    "distance": 2,
    "directionPolicy": "MaxLegalDistanceThenGridOrder",
    "fallbackStatusId": "status_paralyzed",
    "fallbackTurns": 1
  }
}
```

## 12. ObjectiveDefinition

```json
{
  "id": "objective_gate_fault_03",
  "phases": [
    {
      "id": "defend",
      "type": "Defend",
      "zoneTag": "DefenseZone",
      "requiredProgress": 3,
      "progressRule": "AtLeastOnePlayerAtPlayerTurnEnd"
    },
    {
      "id": "evacuate",
      "type": "Evacuate",
      "zoneTag": "EvacuationZone",
      "successRule": "AllLivingPlayersInside"
    }
  ],
  "loseConditions": {
    "allPlayersDefeated": true,
    "collapseThreshold": 100,
    "roundLimit": 12
  }
}
```

## 13. Enemy AI 配置

```json
{
  "mode": "DeterministicRuleBased",
  "unitOrder": "UnitId",
  "neighborOrder": ["Down", "Left", "Right", "Up"],
  "targetPriority": [
    "CanDefeatThisTurn",
    "LowestHp",
    "Nearest",
    "UnitId"
  ]
}
```

## 14. 校验规则

必须校验：

- 文件可读取；
- JSON 可解析；
- ID 非空；
- 同类型 ID 唯一；
- 引用存在；
- 枚举可解析；
- Width/Height > 0；
- Tile 数量为 `width × height`；
- 坐标全覆盖且不重复；
- 出生点不重叠；
- 出生格对单位合法；
- PV、CV、Threshold 合法；
- HP、属性、MOV、AP 合法；
- 技能射程合法；
- Decree fallback 状态存在；
- 目标区域非空；
- 至少 3 个 Anchor；
- 至少 1 个 PhaseMutable；
- 至少 1 个 DecreeDeployable；
- 防守区与撤离区存在；
- 敌人和撤离出生不冲突。

## 15. 错误格式

```text
Code: DEF_REFERENCE_NOT_FOUND
File: Assets/.../player_units.json
Path: $[0].skillIds[0]
Value: skill_missing
Message: Referenced skill does not exist.
```

加载失败不得创建部分可运行 BattleState。

## 16. 配置版本

每个顶层文件建议加入：

```json
{
  "schemaVersion": 1
}
```

不支持的版本必须明确拒绝，不自动猜测迁移。
