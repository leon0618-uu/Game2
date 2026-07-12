# Starfall.Data

数据契约层（Definition / JSON 加载 / 校验 / BattleState 构建）。

## 职责

- 加载关卡 / 单位 / 技能 / 状态 Definition（JSON 源）。
- 校验 Definition 完整性（字段类型、引用完整性、范围合法）。
- 由 Definition 构建 `BattleState`（详见 ADR-0001）。
- 不依赖 Unity（`noEngineReferences: false`，但本目录不引入 `using UnityEngine;`）。

## 依赖

- 仅依赖 `Starfall.Core`（`references: ["Starfall.Core"]`）。
- 不引用 `Starfall.Unity`，不读取场景、Prefab、Presenter。

## 错误处理

- 配置错误不得静默跳过。
- 错误必须包含文件路径、字段路径、错误值、原因。
- 失败配置不得生成「部分可运行」状态。