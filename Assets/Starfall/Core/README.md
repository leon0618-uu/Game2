# Starfall.Core

纯 C# 业务规则层（BattleState / Command / Pathfinder / Resolver / Status / Replay / Undo）。

## 硬约束（AGENTS.md §10.1）

- 零 Unity 依赖。
- 不允许 `using UnityEngine;` / `using UnityEditor;`。
- 不允许业务型 `MonoBehaviour` / `ScriptableObject` 派生。
- 不允许使用 `UnityEngine.Random`。
- 不允许使用不稳定的 `object.GetHashCode()` / `string.GetHashCode()` 作为跨运行哈希。
- 不依赖当前时间、线程调度、对象地址或 Unity InstanceID。

## 自动守门

由 `Assets/Starfall/Tests/EditMode/CoreDependencyGuardTests.cs` 在 EditMode 测试中强制执行 4 项检查：

1. `Starfall.Core.asmdef` 文本不包含 `UnityEngine` / `UnityEditor`，且声明 `noEngineReferences: true`。
2. 编译产物 `Starfall.Core` 程序集不引用任何 `UnityEngine*` / `UnityEditor*` 程序集。
3. `Starfall.Core` 程序集内不存在 `MonoBehaviour` 派生类型。
4. `Starfall.Core` 程序集内不存在 `ScriptableObject` 派生类型。

任一检查失败 = Core 已被 Unity 污染 = AGENTS.md §10.1 硬约束违反。

## 数据契约

- `GridPos`（`readonly record struct`）：见 ADR-0001 §1。
- `PostStateHash`（FNV-1a 64）：见 ADR-0001 §2 / §3。
- `IBattleStateCloner` / `IBattleStateComparer`：见 ADR-0001 §4。