# Starfall.Tests.PlayMode

PlayMode 测试集（M3+ 落地）。

## 当前内容

- 空（架构预留）。
- MVP 阶段的 PlayMode 测试由 `xingyuan-ui-tools` 在其工作区落地。

## asmdef 关键字段

- `excludePlatforms: ["Editor"]` — 仅 Player 编译。
- `defineConstraints: ["UNITY_INCLUDE_TESTS"]` — 仅在 Test Runner 启用时编译。
- `precompiledReferences: ["nunit.framework.dll"]` — 引用 NUnit。