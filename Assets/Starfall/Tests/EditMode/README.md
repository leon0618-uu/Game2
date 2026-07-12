# Starfall.Tests.EditMode

EditMode 测试集。

## 当前内容

- `CoreDependencyGuardTests`：4 项 Core 依赖守门检查（详见 `Assets/Starfall/Core/README.md`）。

## 运行

```text
Unity.exe -batchmode -runTests -testPlatform editmode
```

（QA 阶段 Task 02 Phase D 才会实际跑；本任务包不运行 Unity BatchMode。）

## asmdef 关键字段

- `includePlatforms: ["Editor"]` — 仅 Editor 编译。
- `defineConstraints: ["UNITY_INCLUDE_TESTS"]` — 仅在 Test Runner 启用时编译。
- `precompiledReferences: ["nunit.framework.dll"]` — 引用 NUnit。