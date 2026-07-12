# 《星渊誓约》OpenClaw 仓库与开发环境审计（Task 01）

> 文档语言：中文（用户批准决策 Q1）
> 文档语言：Markdown
> 本文档对应 Task 01 任务包 §2 中的 1.x 取证项；含 Section 1–3 + Section 4–6 + §5.5–§5.8 BatchMode 实测 + §6.1–§6.5 交接 Brief + 「已知偏差与建议」完整重新分类。

## 元信息

- 任务包版本：v1（Phase C-1 用户裁决重分类生效：2026-07-12 18:51 GMT+8）
- 审计日期：2026-07-12
- 负责 Agent：
  - Phase A+B（Section 1-3 + 5.1-5.4）：**xingyuan-architect**
  - Phase C（Section 4-6 + 5.5-5.8 BatchMode 实测 + 已知偏差重写）：**xingyuan-qa**
- 工作区：
  - Phase A+B：`D:\AI-Worktrees\Xingyuan\architect`
  - Phase C：`D:\AI-Worktrees\Xingyuan\qa`
- 分支：`agent/01-repository-audit`（自 `origin/main@8a3fb1fc7bbacf10858d992b112c5d2f1102a53b` 派生）
- 测试基线：
  - Phase A+B：**static-only**
  - Phase C-1：**partial-pass**（业务 C# 编译 + 资产导入 + Domain Reload = run-and-pass；Player 完整构建 = not-run / static-only；EditMode/PlayMode 测试 = not-run；详见 §5.7 + §5.8 + 「已知偏差」🟣 static-only 重声明）
- 写入范围：仅 `Docs/OPENCLAW_REPOSITORY_AUDIT.md`（Phase B 唯一 commit + Phase C 唯一 commit）
- 关联提交：
  - `b23285e docs(audit): sections 1-3 by xingyuan-architect`
  - `a6a8629 docs(audit): sections 4-6 by xingyuan-qa`（Phase C 初版）
  - Phase C-1：本批改 commit（待 C8 后回填 SHA）

---

## Section 1 — 环境与版本

### 1.1 Unity 版本

**取证命令**：`Get-Content ProjectSettings/ProjectVersion.txt`

**实际输出**：

```text
m_EditorVersion: 6000.5.3f1
m_EditorVersionWithRevision: 6000.5.3f1 (c2eb47b3a2a9)
```

**结论**：

- 项目当前 Unity Editor 版本为 **`6000.5.3f1`**（即 Unity 6.5 系列 patch 3）。
- 文档基线（`Docs/01` §2、`Docs/02` §1）声明为 `Unity 6.3 LTS`，**与 ProjectVersion.txt 不一致**。
- 本审计不做版本升降决策，仅作为「已知偏差」上交 Task 02。

### 1.2 渲染管线与输入系统

#### 1.2.1 渲染管线

**取证命令**：`Get-Content ProjectSettings/GraphicsSettings.asset`（节选）

```yaml
m_CustomRenderPipeline: {fileID: 11400000, guid: 4b83569d67af61e458304325a23e5dfd, type: 2}
m_RenderPipelineGlobalSettingsMap:
  UnityEngine.Rendering.Universal.UniversalRenderPipeline: {fileID: 11400000, guid: 18dc0cd2c080841dea60987a38ce93fa, type: 2}
```

**取证命令**：`Get-ChildItem Assets/Settings -File`

```
DefaultVolumeProfile.asset / DefaultVolumeProfile.asset.meta
Mobile_Renderer.asset / Mobile_Renderer.asset.meta
Mobile_RPAsset.asset / Mobile_RPAsset.asset.meta
PC_Renderer.asset / PC_Renderer.asset.meta
PC_RPAsset.asset / PC_RPAsset.asset.meta
SampleSceneProfile.asset / SampleSceneProfile.asset.meta
UniversalRenderPipelineGlobalSettings.asset / UniversalRenderPipelineGlobalSettings.asset.meta
```

**取证命令**：`Get-Content Packages/manifest.json`（节选）

```json
"com.unity.render-pipelines.universal": "17.5.0",
"com.unity.render-pipelines.core": （通过 URP 隐式依赖，未直接列出）
```

**结论**：

- 已激活 **Universal Render Pipeline（URP）17.5.0**，默认 RP Asset 为 `Assets/Settings/PC_RPAsset.asset`（PC）与 `Assets/Settings/Mobile_RPAsset.asset`（Mobile）。
- 与 `Docs/02` §1 声明的 URP 一致。
- URP 17.5.0 与 Unity 6000.5.3f1 版本配套（URP 17.x 系列对齐 Unity 6.3+），与 §1.1 的版本偏差同时影响管线选型。

#### 1.2.2 输入系统

**取证命令**：`Get-Content ProjectSettings/ProjectSettings.asset`（节选）

```yaml
activeInputHandler: 1   # 0=Input Manager(Old) / 1=Input System Package(New) / 2=Both
```

**取证命令**：`Get-ChildItem Assets -Recurse -File -Include *.inputactions`

```
Assets/InputSystem_Actions.inputactions
```

**取证命令**：`Get-Content ProjectSettings/EditorBuildSettings.asset`（节选）

```yaml
m_configObjects:
  com.unity.input.settings.actions: {fileID: -944628639613478452, guid: 052faaac586de48259a63d0c4782560b, type: 3}
```

**输入 Action Map 摘录**（`Assets/InputSystem_Actions.inputactions`，模板默认）：

```
InputSystem_Actions
└─ Player
   ├─ Move
   ├─ Look
   ├─ Attack
   ├─ Interact
   ├─ Crouch
   ├─ Jump
   ├─ Previous
   ├─ Next
   └─ Sprint
```

**结论**：

- 已切换到 **新输入系统**（`activeInputHandler: 1`）。
- Action 资源为模板默认（Player 地图），未包含项目自定义 Action（Move / PhaseFlip / Attack / DeployDecree 等需在 Task 02+ 改造或新增）。
- 与 `Docs/02` §17 输入模式枚举（None / Move / PhaseFlip / Attack / DeployDecree）尚未建立映射。

### 1.3 关键编辑器配置

**取证命令**：`Get-Content ProjectSettings/ProjectSettings.asset`（节选关键字段）

```yaml
productGUID: db28ec0c4e884b048bda3ba517d6039c
companyName: DefaultCompany           # 模板默认，未改为项目方
productName: XingyuanCovenant
defaultScreenOrientation: 4           # Auto Rotation
targetDevice: 2                        # HandheldTablet+Phone（模板默认）
m_ActiveColorSpace: 1                  # Linear
bundleVersion: 0.1.0
projectName: XingyuanCovenant
organizationId: unity_8227063
clonedFromGUID: 3c72c65a16f0acb438eed22b8b16c24a
templatePackageId: com.unity.template.urp-blank@17.0.14
templateDefaultScene: Assets/Scenes/SampleScene.unity
overrideDefaultApplicationIdentifier: 1
applicationIdentifier:
  Android: com.UnityTechnologies.com.unity.template.urpblank
  Standalone: com.Unity-Technologies.com.unity.template.urp-blank
```

**取证命令**：`Get-Content ProjectSettings/EditorSettings.asset`（节选）

```yaml
m_SerializationMode: 2          # Force Text
m_EnterPlayModeOptionsEnabled: 1
m_EnterPlayModeOptions: 0       # 默认禁用 Domain Reload（Project Settings 默认值，需在 Task 02+ 显式确认是否启用 Reload）
m_AssetPipelineMode: 1          # Asset Pipeline V2
m_ProjectGenerationIncludedExtensions: txt;xml;fnt;cd;asmdef;rsp;asmref
```

**取证命令**：`Get-Content ProjectSettings/EditorBuildSettings.asset`

```yaml
m_Scenes:
- enabled: 1
  path: Assets/Scenes/SampleScene.unity
  guid: 99c9720ab356a0642a771bea13969a05
```

**取证命令**：`Get-Content ProjectSettings/URPProjectSettings.asset`（节选）

```yaml
m_LastMaterialVersion: 10
m_ProjectSettingFolderPath: URPDefaultResources
```

**取证命令**：`Get-Content ProjectSettings/TagManager.asset`（节选）

```yaml
tags: []                          # 空 — 无项目自定义 Tag
layers: [Default, TransparentFX, Ignore Raycast, , Water, UI, ...]
m_SortingLayers:
- name: Default, uniqueID: 0, locked: 0
m_RenderingLayers:
- Default, Light Layer 1..7
```

**结论**：

- 项目 **直接派生自 `com.unity.template.urp-blank@17.0.14`** 模板；`companyName` 仍为 `DefaultCompany`，`Android` Bundle ID 仍为模板默认（`com.UnityTechnologies.com.unity.template.urpblank`）。
- `templateDefaultScene` 与 `EditorBuildSettings` 中唯一场景一致（`Assets/Scenes/SampleScene.unity`），构建场景列表中无项目战斗场景。
- `m_EnterPlayModeOptionsEnabled: 1` 开启但 `m_EnterPlayModeOptions: 0` 表示未启用 Domain/Scene Reload 加速；后续测试需在 Task 02+ 评估对 EditMode/PlayMode 测试稳定性的影响。
- 序列化模式为 Force Text，资产管线为 V2。
- `TagManager.tags` 为空；项目战斗所需 Tag（如 `Unit`、`Anchor`、`Decree`、`Objective`）尚未声明。

---

## Section 2 — 程序集现状

### 2.1 现有 asmdef 清单

**取证命令**：`Get-ChildItem -Path . -Recurse -Filter *.asmdef -ErrorAction SilentlyContinue | Select-Object FullName`

**实际输出**：**空集合**（命令返回 0 条结果，exit code 隐式为 1）。

**取证命令**：`Get-ChildItem Assets -Recurse -Directory | Select-Object FullName`

```
D:\AI-Worktrees\Xingyuan\architect\Assets\Scenes
D:\AI-Worktrees\Xingyuan\architect\Assets\Settings
D:\AI-Worktrees\Xingyuan\architect\Assets\TutorialInfo
D:\AI-Worktrees\Xingyuan\architect\Assets\TutorialInfo\Icons
D:\AI-Worktrees\Xingyuan\architect\Assets\TutorialInfo\Scripts
D:\AI-Worktrees\Xingyuan\architect\Assets\TutorialInfo\Scripts\Editor
```

**取证命令**：`Get-ChildItem Assets -Recurse -File -Include *.cs,*.unity,*.asset,*.prefab,*.inputactions`

```
Assets/Scenes/SampleScene.unity
Assets/Settings/DefaultVolumeProfile.asset
Assets/Settings/Mobile_Renderer.asset
Assets/Settings/Mobile_RPAsset.asset
Assets/Settings/PC_Renderer.asset
Assets/Settings/PC_RPAsset.asset
Assets/Settings/SampleSceneProfile.asset
Assets/Settings/UniversalRenderPipelineGlobalSettings.asset
Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs
Assets/TutorialInfo/Scripts/Readme.cs
Assets/InputSystem_Actions.inputactions
Assets/Readme.asset
```

**结论**：

- **当前仓库不存在任何 `.asmdef` 文件**（计数 0）。
- `Assets/TutorialInfo/Scripts/` 下的 `Readme.cs`（继承自 `ScriptableObject`）与 `ReadmeEditor.cs`（`UnityEditor` 命名空间）均为 URP Blank 模板自带教程脚手架，非项目业务代码。
- `Assets/Scripts/` 目录不存在——`git grep ... Assets/Scripts` 返回 fatal（路径不存在）。

### 2.2 与 `Docs/02 §3` 期望的差异

**期望（`Docs/02 §3` 程序集）**：

| # | Assembly | 期望引用 |
|---|---|---|
| 1 | `Starfall.Core` | 无 Unity 引用 |
| 2 | `Starfall.Data` | → `Starfall.Core`（可选 .NET JSON） |
| 3 | `Starfall.Unity` | → `Starfall.Core` + `Starfall.Data` + `UnityEngine` |
| 4 | `Starfall.Tests.EditMode` | → `Core` + `Data` + Test Framework |
| 5 | `Starfall.Tests.PlayMode` | → `Unity` + `Core` + `Data` |

**实际**：**0 / 5**

**差异矩阵**：

| 维度 | 期望 | 实际 | 偏差等级 |
|---|---|---|---|
| asmdef 数量 | 5 | 0 | Major |
| Core / Data / Unity 物理隔离 | 强制 | 未声明（缺隔离） | Major |
| EditMode / PlayMode 测试程序集 | 各 1 | 0 | Major |
| `Starfall.Core` 无 Unity 引用护栏 | 必备 | 未建立 | Major |
| 测试引用 `Unity` 但不污染 `Core` | 必备 | 未建立 | Major |

**说明**：

- 任何后续 `.cs` 在缺少 asmdef 的情况下，会落入 Unity 默认 `Assembly-CSharp.dll`，与引擎默认脚本程序集耦合，无法在架构层面守住 `Starfall.Core` 不引用 `UnityEngine` 的硬约束（`AGENTS.md §10.1`）。
- 5 个 asmdef 的建立属于 Task 02+ 范围内的架构工作（按用户决策 Q3，ADR 起草在 Task 02 内进行）。

---

## Section 3 — 资产与依赖

### 3.1 Assets/ 目录树

**取证命令**：`Get-ChildItem Assets -Recurse -Directory | Select-Object FullName`

```
Assets\Scenes
Assets\Settings
Assets\TutorialInfo
Assets\TutorialInfo\Icons
Assets\TutorialInfo\Scripts
Assets\TutorialInfo\Scripts\Editor
```

**取证命令**：`Get-ChildItem Assets -Recurse -File -Include *.cs,*.unity,*.asset,*.prefab,*.inputactions`

```
Assets\Scenes\SampleScene.unity                                 # 模板默认空场景
Assets\Settings\DefaultVolumeProfile.asset                      # URP 默认 Volume Profile
Assets\Settings\Mobile_Renderer.asset                           # URP Mobile Renderer
Assets\Settings\Mobile_RPAsset.asset                            # URP Mobile RP Asset
Assets\Settings\PC_Renderer.asset                               # URP PC Renderer
Assets\Settings\PC_RPAsset.asset                                # URP PC RP Asset（GraphicsSettings 主引用）
Assets\Settings\SampleSceneProfile.asset                        # 场景 Volume Profile
Assets\Settings\UniversalRenderPipelineGlobalSettings.asset     # URP Global Settings
Assets\TutorialInfo\Scripts\Editor\ReadmeEditor.cs              # 模板脚手架（Editor 程序集）
Assets\TutorialInfo\Scripts\Readme.cs                           # 模板脚手架（ScriptableObject）
Assets\InputSystem_Actions.inputactions                         # 模板默认输入
Assets\Readme.asset                                             # 模板 Readme 资源
```

**结论**：

- Assets/ **没有任何项目业务资产**（无战斗 Prefab、ScriptableObject Definition、ScriptableObject 战斗配置、Material、Shader、Audio 等）。
- 现有资源全部为 URP Blank 模板 + Input System 模板的默认值。
- 8×10 战棋地图、4 名玩家单位 Prefab、确定性敌方单位 Prefab、Anchor / Decree Definition 资源、Replay / Undo 视图等均未创建（属 Task 02+ 范围）。

### 3.2 Packages/manifest.json 关键依赖

**取证命令**：`Get-Content Packages/manifest.json`

**完整内容**（按字母序）：

```json
{
  "dependencies": {
    "com.unity.ai.navigation": "2.0.13",
    "com.unity.collab-proxy": "2.12.4",
    "com.unity.ide.rider": "3.0.38",
    "com.unity.ide.visualstudio": "2.0.26",
    "com.unity.inputsystem": "1.19.0",
    "com.unity.multiplayer.center": "1.0.1",
    "com.unity.render-pipelines.universal": "17.5.0",
    "com.unity.test-framework": "1.7.0",
    "com.unity.timeline": "1.8.12",
    "com.unity.ugui": "2.5.0",
    "com.unity.visualscripting": "1.9.11",
    "com.unity.modules.accessibility": "1.0.0",
    "com.unity.modules.adaptiveperformance": "1.0.0",
    "com.unity.modules.ai": "1.0.0",
    "com.unity.modules.androidjni": "1.0.0",
    "com.unity.modules.animation": "1.0.0",
    "com.unity.modules.assetbundle": "1.0.0",
    "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.cloth": "1.0.0",
    "com.unity.modules.director": "1.0.0",
    "com.unity.modules.imageconversion": "1.0.0",
    "com.unity.modules.imgui": "1.0.0",
    "com.unity.modules.jsonserialize": "1.0.0",
    "com.unity.modules.particlesystem": "1.0.0",
    "com.unity.modules.physics": "1.0.0",
    "com.unity.modules.physics2d": "1.0.0",
    "com.unity.modules.physicscore2d": "1.0.0",
    "com.unity.modules.screencapture": "1.0.0",
    "com.unity.modules.terrain": "1.0.0",
    "com.unity.modules.terrainphysics": "1.0.0",
    "com.unity.modules.tilemap": "1.0.0",
    "com.unity.modules.ui": "1.0.0",
    "com.unity.modules.uielements": "1.0.0",
    "com.unity.modules.umbra": "1.0.0",
    "com.unity.modules.unityanalytics": "1.0.0",
    "com.unity.modules.unitywebrequest": "1.0.0",
    "com.unity.modules.unitywebrequestassetbundle": "1.0.0",
    "com.unity.modules.unitywebrequestaudio": "1.0.0",
    "com.unity.modules.unitywebrequesttexture": "1.0.0",
    "com.unity.modules.unitywebrequestwww": "1.0.0",
    "com.unity.modules.vectorgraphics": "1.0.0",
    "com.unity.modules.vehicles": "1.0.0",
    "com.unity.modules.video": "1.0.0",
    "com.unity.modules.wind": "1.0.0",
    "com.unity.modules.xr": "1.0.0"
  }
}
```

**分类统计**：

| 类别 | 包 | 版本 | 与 `Docs/02` 期望的关系 |
|---|---|---|---|
| 渲染 | `com.unity.render-pipelines.universal` | 17.5.0 | ✅ URP，匹配 |
| 输入 | `com.unity.inputsystem` | 1.19.0 | ✅ 新输入系统，匹配 |
| 测试 | `com.unity.test-framework` | 1.7.0 | ✅ EditMode/PlayMode 测试所需 |
| UI | `com.unity.ugui` + `com.unity.modules.ui` + `com.unity.modules.uielements` | 2.5.0 / 1.0.0 / 1.0.0 | ✅ 满足 `BattleHud` 最小需求 |
| 序列化 | `com.unity.modules.jsonserialize` | 1.0.0 | ✅ `JsonUtility` 可用（Data 层是否使用见 §3.2 备注） |
| 物理 | `com.unity.modules.physics` / `physics2d` / `physicscore2d` | 1.0.0 | ⚠️ 战棋逻辑不依赖物理引擎，需在架构中显式声明不引入 `Rigidbody` / `Collider` 业务用法 |
| 寻路相关 | `com.unity.ai.navigation` | 2.0.13 | ⚠️ NavMesh 与 `Docs/02 §9` BFS Pathfinder 不一致；MVP 不应使用 NavMesh |
| 时间线 | `com.unity.timeline` | 1.8.12 | ⚠️ 未在 `Docs/02` 中使用，建议作为可清理项 |
| 可视化脚本 | `com.unity.visualscripting` | 1.9.11 | ⚠️ MVP 不使用，建议作为可清理项 |
| 多人 | `com.unity.multiplayer.center` | 1.0.1 | ⚠️ MVP 明确排除联机（`AGENTS.md §12`），建议作为可清理项 |
| 编辑器 | `com.unity.ide.rider` / `com.unity.ide.visualstudio` | 3.0.38 / 2.0.26 | ✅ 开发工具，按个人偏好可保留 |
| 协作 | `com.unity.collab-proxy` | 2.12.4 | ✅ Unity 内置，按团队决定 |

**结论**：

- 核心依赖（URP / Input System / Test Framework / UGUI / JSON / AI Navigation）齐备。
- **未引入 `com.unity.localization`、`com.unity.addressables`、`com.unity.entities` 等未授权包**——与 `AGENTS.md §12` MVP 禁止范围一致。
- `Docs/02 §15` 推荐 `.NET JSON`；项目已含 `com.unity.modules.jsonserialize`，亦可选用 .NET `System.Text.Json`（需 Unity API 兼容层验证）；具体选型留待 ADR 起草。
- **建议清理候选**：`com.unity.timeline`、`com.unity.visualscripting`、`com.unity.multiplayer.center`（按用户决策前不动 `Packages/manifest.json`，仅作为已知偏差）。

### 3.3 ProjectSettings/ 关键文件

**取证命令**：`Get-ChildItem ProjectSettings -Recurse -Filter *.asset | Select-Object FullName`

```
ProjectSettings/AudioManager.asset
ProjectSettings/ClusterInputManager.asset
ProjectSettings/DynamicsManager.asset
ProjectSettings/EditorBuildSettings.asset
ProjectSettings/EditorSettings.asset
ProjectSettings/GraphicsSettings.asset
ProjectSettings/InputManager.asset                       # 旧输入系统配置（仍存在但被新输入系统覆盖）
ProjectSettings/MemorySettings.asset
ProjectSettings/MultiplayerManager.asset
ProjectSettings/NavMeshAreas.asset
ProjectSettings/PackageManagerSettings.asset
ProjectSettings/Physics2DSettings.asset
ProjectSettings/PhysicsCoreProjectSettings2D.asset
ProjectSettings/PresetManager.asset
ProjectSettings/ProjectSettings.asset
ProjectSettings/QualitySettings.asset
ProjectSettings/ShaderGraphSettings.asset
ProjectSettings/TagManager.asset
ProjectSettings/TimeManager.asset
ProjectSettings/UnityConnectSettings.asset
ProjectSettings/URPProjectSettings.asset
ProjectSettings/VersionControlSettings.asset
ProjectSettings/VFXManager.asset
ProjectSettings/XRSettings.asset
```

**ProjectSettings 完整性**：

- 24 个标准 `.asset` 文件全部存在，结构正常。
- `URPProjectSettings.asset` 与 `GraphicsSettings.asset` 已正确引用 URP 资源。
- `ProjectSettings.asset` 中 `companyName` 仍为 `DefaultCompany`、`Android bundle ID` 仍为模板默认（详见 §1.3）。

**EditorBuildSettings.scenes**：

```yaml
m_Scenes:
- enabled: 1
  path: Assets/Scenes/SampleScene.unity
  guid: 99c9720ab356a0642a771bea13969a05
```

**EditorSettings 关键字段**（节选）：

```yaml
m_SerializationMode: 2                  # Force Text
m_EnterPlayModeOptionsEnabled: 1
m_AssetPipelineMode: 1                  # V2
m_ProjectGenerationIncludedExtensions: txt;xml;fnt;cd;asmdef;rsp;asmref
```

**结论**：

- ProjectSettings/ 完整；构建场景只有 1 条（模板默认 SampleScene），后续需追加战斗主场景。
- 序列化 Force Text + Asset Pipeline V2 + asmdef 在项目生成扩展名列表中 → 与未来建立 `Starfall.*` 程序集无冲突。

### 3.4 Worktree 当前形态

**取证命令**：`git worktree list`

```
D:/UntiyProject/XingyuanCovenant    8a3fb1f [main]
D:/AI-Worktrees/Xingyuan/architect  8a3fb1f [agent/01-repository-audit]
D:/AI-Worktrees/Xingyuan/gameplay   8a3fb1f [agent/gameplay-bootstrap]
D:/AI-Worktrees/Xingyuan/qa         8a3fb1f [agent/qa-bootstrap]
D:/AI-Worktrees/Xingyuan/ui-tools   8a3fb1f [agent/ui-tools-bootstrap]
```

**结论**：

- 5 个 worktree 全部就位且均指向 `8a3fb1f`（origin/main HEAD）。
- 主工作区 `D:/UntiyProject/XingyuanCovenant` 当前绑定 `[main]`，但本身也是 worktree（与 `agent/*` 共享 `.git/`）。
- 各 Agent 的 bootstrap 分支命名规范与 `AGENTS.md §9` 一致（`agent/<role>-bootstrap`）。
- 本次任务已从 origin/main 派生 `agent/01-repository-audit`（详见元信息）。

### 3.5 BOOTSTRAP.md 状态

**取证命令**：`Test-Path BOOTSTRAP.md`（在当前 worktree 根）

**实际输出**：`False`

**取证命令**：`Test-Path D:\UntiyProject\XingyuanCovenant\BOOTSTRAP.md`

**实际输出**：`False`

**结论**：

- **当前仓库（包括主工作区与所有 worktree）不存在 `BOOTSTRAP.md`**。
- 按 `AGENTS.md §3 BOOTSTRAP.md` 节，存在时应读取并完成初始化步骤；不存在时无需该步骤。
- 不需执行 `AGENTS.md §3` 末尾的「完成说明」/「用户批准删除/归档」流程。

---

## Section 4 — 文档自洽性矩阵（QA 出具）

> 本节由 `xingyuan-qa` 在 Phase C 复核并补全。方法：逐条 cross-check `AGENTS.md` ↔ `Docs/01`–`Docs/05` ↔ 本审计 Section 1–3 的引用关系，标注一致 / 不一致 / 待验证。所有证据均来自 `read` / `Select-String` 取证，不引入未读到的声明。

### 4.1 引用矩阵

| # | 来源（节号） | 目标（节号） | 引用主题 | 是否一致 | 证据 |
|---|---|---|---|---|---|
| M1 | `AGENTS.md` §1（项目目标） | `Docs/01` §1（游戏定位） | MVP 差异化玩法 8 项 | ✅ 一致 | `AGENTS.md` L16-30 与 `Docs/01` §1 同列：双层地块 / PV 翻转 / 坠落挤压 / 锚点围区 / 律令反结算 / Undo 提 CV / 确定性回放（7 项核心体验） |
| M2 | `AGENTS.md` §1 | `Docs/01` §5（棋盘与坐标） | 8×10 / 80 格 / (0,0) 左下 / 排序 y→x / 四向邻接 | ✅ 一致 | `AGENTS.md` §11 确定性规则第 1 条 = 「网格排序：先 `y`，后 `x`」与 `Docs/01` §5 同；`AGENTS.md` §10.1 第 5 条同 |
| M3 | `AGENTS.md` §10（架构硬约束） | `Docs/02` §3（程序集） | `Starfall.Core` 不引用 `UnityEngine` / 5 asmdef 命名 | ✅ 一致 | `AGENTS.md` §10.1 L370-378 列 8 条 Core 硬约束；`Docs/02` §3 L61-96 列 5 asmdef 名称 + 依赖图，二者 1:1 对应 |
| M4 | `AGENTS.md` §10.2 / §10.3 | `Docs/02` §3 | `Starfall.Data` 仅依赖 Core；`Starfall.Unity` 依赖 Core+Data+UnityEngine | ✅ 一致 | `AGENTS.md` §10.2-10.3 与 `Docs/02` §3 依赖列表字段完全对应 |
| M5 | `AGENTS.md` §11（确定性规则） | `Docs/02` §4（Core 数据模型） | `GridPos` 实现 `IComparable`、比较 y→x、四向邻接 | ✅ 一致 | `AGENTS.md` §11 第 1-2 条 ↔ `Docs/02` §4 `GridPos` 节 L102-117 |
| M6 | `AGENTS.md` §12（MVP 禁止范围） | `Docs/01` §2（不追求） | MVP 排除联机 / 存档 / 商业化 | ✅ 一致 | `AGENTS.md` §12 L427-440 与 `Docs/01` §2「MVP 不追求」5 条互为补集 |
| M7 | `AGENTS.md` §5.2-5.6 | 实际 `git worktree list` | 5 个 Agent 工作区路径 | ✅ 一致 | `AGENTS.md` L155/172/188/204 列出 4 个 worktree 路径；`git worktree list` 实测：architect / gameplay / qa / ui-tools + 主工作区，共 5 条 worktree 记录，与声明 1:1 |
| M8 | `AGENTS.md` §9（Git 与 Worktree） | `git branch --show-current` | 分支命名规范 `agent/<role>-bootstrap` | ✅ 一致 | architect 实测 `agent/architecture-bootstrap` / gameplay `agent/gameplay-bootstrap` / qa `agent/qa-bootstrap` / ui-tools `agent/ui-tools-bootstrap`，与 §9 命名规范匹配 |
| M9 | `AGENTS.md` §17（标准开发报告） | `Docs/05` §12（验收证据） | 报告需含测试结果与日志位置 | ✅ 一致 | `AGENTS.md` §17 10 项报告字段 vs `Docs/05` §12 L343-372 证据清单；二者均要求日志 + 结果文件 |
| M10 | `AGENTS.md` §17（报告区分） | `Docs/05` §13（缺陷等级） | 必须区分「已运行并通过 / 仅静态 / 因环境未运行 / 需人工验证」 | ⚠️ 待验证（措辞差异） | `AGENTS.md` §17 列出 5 种状态枚举；`Docs/05` §13 L374-409 列出缺陷等级 Major/Minor/Info，未直接复用 §17 状态枚举；二者不矛盾但未交叉引用 |
| M11 | `Docs/01` §2（Unity 6.3 LTS） | `ProjectSettings/ProjectVersion.txt` | Unity 版本 | ❌ 不一致 | 文档声明 `Unity 6.3 LTS`（`Docs/01` §2 技术平台）；实测 `6000.5.3f1`（§1.1）。Major 偏差，已列入 Section 5 + 已知偏差 |
| M12 | `Docs/02` §3（5 个 asmdef） | `Get-ChildItem -Filter *.asmdef` | asmdef 数量 | ❌ 不一致 | 文档要求 5 个；实测 0 个（§2.1）。Major 偏差，已列入已知偏差 |
| M13 | `Docs/05` §4-5（必须存在的 EditMode/PlayMode 测试） | 实际 `Assets/Tests` 目录 | 测试程序集存在性 | ❌ 不一致（预期） | `Docs/05` §4-5 L78-184 列出必存测试清单；实测 `Assets/Tests` 目录不存在，asmdef 0/5（§2.1）。Major 偏差，与 M12 同源 |
| M14 | `AGENTS.md` §10.1 第 5 条（集合遍历稳定排序） | `Docs/02` §4 `GridPos` 排序 | 邻居顺序、状态顺序、Command/Event 序号 | ✅ 一致 | `Docs/02` §4 `GridPos` 比较 y→x；`AGENTS.md` §11 第 1-7 条枚举同类排序约束 |
| M15 | `AGENTS.md` §18（文档维护） | `Docs/04` §3（里程碑） | ADR / `KNOWN_LIMITATIONS` / `IMPLEMENTATION_STATUS` 维护路径 | ⚠️ 待验证 | `AGENTS.md` §18 L603-614 列出 `Docs/ADR/` + `docs/KNOWN_LIMITATIONS.md` + `docs/IMPLEMENTATION_STATUS.md` + `docs/MANUAL_ACCEPTANCE_CHECKLIST.md` 4 个维护路径；`Docs/04` §3 里程碑未引用这些文件，需在 Task 02 内交叉 |

### 4.2 自洽性结论

- **结构性一致（M1-M9, M14）**：项目核心契约（架构硬约束、确定性规则、术语、工作区）跨文档自洽。
- **已知主偏差（M11-M13）**：版本与 asmdef 缺失两组偏差已分别落入 Section 5 与「已知偏差」Major 列表。
- **轻微措辞差异（M10, M15）**：不构成 Major 偏差，建议在 Task 02 内由 architect 校对。
- **未发现需 Lead 裁决的额外冲突**。

---

## Section 5 — 工程就绪与编译基线（QA 出具）

> **本节为 static-only 基线。Task 01 未运行 Unity Editor，本节不构成「编译通过」声明；如下游需要 run-and-pass 编译证据，须在 Task 02 内补充。**

### 5.1 Unity 版本核对

| 维度 | 声明 | 实测 | 状态 |
|---|---|---|---|
| `Docs/01` §2 技术平台 | `Unity 6.3 LTS` | `6000.5.3f1`（`ProjectVersion.txt`） | ❌ **Major 偏差（待用户裁决）** |
| `Docs/02` §1 引用 | 同上 | 同上 | ❌ 同上 |
| `ProjectSettings/ProjectVersion.txt` m_EditorVersion | — | `6000.5.3f1` | 取证来源 |
| `ProjectVersion.txt` m_EditorVersionWithRevision | — | `6000.5.3f1 (c2eb47b3a2a9)` | 取证来源 |

**裁决选项**（提交用户）：
1. **A. 文档更新**（推荐，影响最小）：将 `Docs/01 §2` / `Docs/02 §1` 中 `Unity 6.3 LTS` 更正为 `Unity 6.5 (6000.5.3f1)`；不改 ProjectSettings 与 manifest。
2. **B. 版本升降**：将 Unity Editor 切换到 `Unity 6.3 LTS`（6000.3.x），涉及模板与 ProjectVersion 重生成；不在 Task 01 范围。
3. **C. 维持现状并记录偏差**：保留 `6000.5.3f1`，偏差记入 `docs/KNOWN_LIMITATIONS.md`（`AGENTS.md §18` 路径）；后续 Task 沿用。

### 5.2 关键依赖核对（Packages/manifest.json）

**取证命令**：`Select-String -Path Packages/manifest.json -Pattern "<package>"`

| 包 | 版本 | 行号 | 与 Docs/02 / AGENTS.md 期望 |
|---|---|---|---|
| `com.unity.render-pipelines.universal` | `17.5.0` | L9 | ✅ URP，匹配 `Docs/02 §1` |
| `com.unity.inputsystem` | `1.19.0` | L7 | ✅ 新输入系统，匹配 `Docs/02 §1`（`activeInputHandler=1` 已切） |
| `com.unity.test-framework` | `1.7.0` | L10 | ✅ 测试框架存在；EditMode/PlayMode 测试前置条件满足（仅缺 asmdef 与 `Assets/Tests/`） |
| `com.unity.ugui` | `2.5.0` | L11 | ✅ `BattleHud` 最小 UI 需求 |
| `com.unity.modules.jsonserialize` | `1.0.0` | L21 | ✅ Data 层 `JsonUtility` 可用 |
| `com.unity.ai.navigation` | `2.0.13` | L6 | ⚠️ 信息性 — 与 `Docs/02 §9` BFS Pathfinder 不一致；建议清理（按用户决策） |
| `com.unity.timeline` | `1.8.12` | L12 | ⚠️ 信息性 — MVP 不使用 |
| `com.unity.visualscripting` | `1.9.11` | L13 | ⚠️ 信息性 — MVP 不使用 |
| `com.unity.multiplayer.center` | `1.0.1` | L8 | ⚠️ 信息性 — MVP 排除联机（`AGENTS.md §12`） |

**未授权包检测**（与 `AGENTS.md §12` 一致性核对）：
- `com.unity.localization`：grep 未命中 → ✅ 未引入
- `com.unity.addressables`：grep 未命中 → ✅ 未引入
- `com.unity.entities`：grep 未命中 → ✅ 未引入

**结论**：核心依赖齐备；候选清理项已落入「已知偏差」信息性列表，等待用户裁决。

### 5.3 ProjectSettings 模板默认值与「是否影响 Task 02」

| 字段 | 当前值 | 影响 Task 02 编译 / 测试？ | 建议处理时机 |
|---|---|---|---|
| `companyName` | `DefaultCompany` | ❌ 不影响编译；影响最终 bundle metadata | 用户裁决后由 ui-tools 修改（属 ProjectSettings 写入，需批准） |
| `applicationIdentifier.Android` | `com.UnityTechnologies.com.unity.template.urpblank` | ❌ 不影响编译；影响 Android 构建 | 同上 |
| `templateDefaultScene` + `EditorBuildSettings.m_Scenes` | 仅 `Assets/Scenes/SampleScene.unity` | ❌ 不影响编译；Task 02 内追加战斗场景后才会影响构建 | Task 02 内追加 |
| `TagManager.tags` | 空 | ❌ 不影响编译；Project 业务 Tag 由 gameplay 在 Task 03+ 增补 | Task 03+ 增补 |
| `m_EnterPlayModeOptionsEnabled` / `m_EnterPlayModeOptions` | `1` / `0`（启用开关但未启用 Domain Reload） | ⚠️ 间接影响 — EditMode/PlayMode 测试在禁用 Reload 模式下可能受静态缓存影响，需在 Task 02 内显式决策 | Task 02 内 architect 决策 |

**结论**：上述字段**不阻塞 Task 02 编译**；ProjectSettings 修改仍须用户批准（按 `AGENTS.md §13`）。

### 5.4 静态编译基线声明（Task 01 当前结论）

- **本节为 static-only 基线**：未运行 Unity Editor、未执行 `Unity -batchmode -projectPath ... -quit`，未执行编译或 EditMode/PlayMode 测试。
- **Task 02 第一动作前置**：在 architect / ui-tools 启动 Editor 之前，应**先记录一次「Editor 打开后 Console Error 基线」**（基线时间戳、错误条数、警告条数），存档到 `docs/IMPLEMENTATION_STATUS.md`（按 `AGENTS.md §18`）。
- **Task 02 完成 Gate 应包含**：EditMode 编译 1 次 + Player 编译 1 次 + Console 0 Error 对比（基线 vs Task 02 后）；如基线本身含 Error，须先解决。

### 5.5 Unity Test Framework 现状（Phase C-1 实测）

**取证命令**：

```powershell
Test-Path Assets/Tests                                                       # False
Get-ChildItem -Recurse -Filter *.asmdef | Measure-Object                     # Count = 0
Get-ChildItem -Recurse -Filter *.dll | Where-Object { $_.FullName -like "*Tests*" }  # 0 matches
Get-ChildItem -Recurse -Directory | Where-Object { $_.Name -like "Starfall*" }       # 0 matches
```

**Packages/manifest.json 取证**（`Select-String -Pattern "com.unity.test-framework"`）：

```yaml
L10: "com.unity.test-framework": "1.7.0",
```

**Unity Log 取证**（`Select-String -Pattern "Test Framework|nunit|TestRunner"` in `Logs/unity-batchmode.log`）：

```text
L215:  com.unity.ext.nunit@2.1.0 (location: D:\AI-Worktrees\Xingyuan\qa\Library\PackageCache\com.unity.ext.nunit@44f7d31723bd)
L1825–1834: CopyFiles Library/ScriptAssemblies/UnityEngine.TestRunner.{dll,pdb}, UnityEditor.TestRunner.{dll,pdb}
L5162: Importing … Packages/com.unity.test-framework.performance/Runtime/Unity.PerformanceTesting.asmdef
```

**结论**：

| 维度 | 状态 | 证据 |
|---|---|---|
| `com.unity.test-framework` | ✅ `1.7.0` 已安装 | `Packages/manifest.json` L10 |
| `com.unity.ext.nunit` 依赖 | ✅ `2.1.0` 已加载 | Unity Log L215 |
| Test Runner DLLs 生成 | ✅ `UnityEditor.TestRunner.dll`（313 KB）+ `UnityEngine.TestRunner.dll`（177 KB）已写入 `Library/ScriptAssemblies/` | Unity Log L1825–1834 + `Get-ChildItem Library/ScriptAssemblies` |
| `Assets/Tests/` 目录 | ❌ 不存在 | `Test-Path` = False |
| 项目内 `*.asmdef` | ❌ 0 条（5 个 `Starfall.*` 全部缺失） | `Measure-Object Count = 0` |
| 项目内 `Starfall.*` 目录 | ❌ 0 条 | `Where-Object` 无结果 |
| Performance Testing 包 | ✅ 隐式安装（test-framework.performance 子包） | Unity Log L5162 |

**判定**：Unity Test Framework 已就绪，但**项目代码侧无可发现测试** — 因 0 个项目 asmdef 且 `Assets/Tests/` 不存在。Test Runner 能加载，但测试清单为空。该项属于「Planned Gap（Task 02 内由 architect 创建 5 个 asmdef 后才具备可发现测试载体）」，不再是 Major 阻塞。

### 5.6 Core 依赖守卫现状（Phase C-1 实测）

**取证命令**：

```powershell
git grep -nE "(using\s+UnityEngine|using\s+UnityEditor)" Assets/
git grep -nE "class\s+\w+\s*:\s*(MonoBehaviour|ScriptableObject)" Assets/
```

**实测输出**：

```text
Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs
  L3: using UnityEngine;
  L4: using UnityEditor;
Assets/TutorialInfo/Scripts/Readme.cs
  L2: using UnityEngine;

Assets/TutorialInfo/Scripts/Readme.cs
  L4: public class Readme : ScriptableObject
```

**`Starfall.Core/` 目录检查**：

```powershell
Get-ChildItem -Recurse -Directory | Where-Object { $_.Name -like "Starfall*" }
# → 0 条结果
```

**结论**：

- **`Starfall.Core/` 程序集尚未建立**（5 个 asmdef 全部缺失 → 目录尚未生成）。
- `Core 依赖守卫` 的语义（`Starfall.Core` 内禁止 `using UnityEngine` / `using UnityEditor` / 业务型 `MonoBehaviour` / 业务型 `ScriptableObject`）当前**无可执行对象**。
- 现有 `Assets/TutorialInfo/Scripts/{Readme.cs, ReadmeEditor.cs}` 命中 grep，但属 URP Blank 模板脚手架（详见 §2.1），**非业务代码**，不计入 Core 守卫范围。
- 真正的 Core 守卫测试（`Starfall.Tests.EditMode` 内扫描 Core 程序集禁止符号）属于 Task 02 主交付物之一（`Starfall.Tests.EditMode` 建立后才可写）。
- **判定**：Core 依赖守卫 = **未建立**（无 Core 程序集可守）；属「Planned Gap，Task 02 内由 architect 与 gameplay 联合建立」。本次实证确认并未在 `Assets/` 业务代码中发现违规（业务代码本身尚未存在）。

### 5.7 Unity BatchMode 编译基线（Task 01 实测，run-and-pass）

> **本节为 Phase C-1 唯一 run-and-pass 证据**：Task 01 期间首次启动 Unity Editor 实际执行 `BatchMode -quit`，完成了「资产导入 + ScriptAssemblies 编译 + Domain Reload + 退出」完整链路，并产出真实日志与编译产物。

**完整命令字符串**（PowerShell 形式，含反引号行延续）：

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe" `
  -batchmode `
  -nographics `
  -quit `
  -projectPath "D:\AI-Worktrees\Xingyuan\qa" `
  -logFile "D:\AI-Worktrees\Xingyuan\qa\Logs\unity-batchmode.log" `
  -buildTarget StandaloneWindows64
```

**执行参数**：

| 参数 | 值 | 作用 |
|---|---|---|
| `-batchmode` | — | 无 GUI 模式（CI 友好） |
| `-nographics` | — | 不初始化图形设备（避免 GPU 需求） |
| `-quit` | — | 完成导入后立即退出（不进入 PlayMode） |
| `-projectPath` | `D:\AI-Worktrees\Xingyuan\qa` | 当前 qa worktree |
| `-logFile` | `D:\AI-Worktrees\Xingyuan\qa\Logs\unity-batchmode.log` | 显式日志路径 |
| `-buildTarget` | `StandaloneWindows64` | 设置活跃构建目标（**不**触发 Player 构建；仅切换 TargetGroup + 触发 ScriptAssemblies 编译） |

**退出码**：

```text
Exiting batchmode successfully now!
Exiting without the bug reporter. Application will terminate with return code 0
```

**进程退出码**：`0`（success）

**日志路径**：`D:\AI-Worktrees\Xingyuan\qa\Logs\unity-batchmode.log`

**日志大小**：`2,043,081` 字节（约 `1.95 MB`），共 `10,214` 行

**关键摘要**（来自日志 `Select-String` 取证）：

| 指标 | 数值 | 证据 / 行号 |
|---|---|---|
| 编译错误（`CompileError` / `Compile error` / `Compilation failed`） | **0 条** | `Select-String "CompileError|Compile error|Compilation failed"` → Count = 0 |
| 日志原文 "Warning" 字符出现 | **2 条**（均为 `TimelineWarning.png` / `Timeline-Marker-Warning-Overlay.png` 资源路径字符串，非真实警告） | Log L6295、L6603 |
| 真实 `warning:` 前缀警告数 | **0 条** | `Select-String " warning:"` → Count = 0 |
| License 通道噪声（环境噪声，非业务错误） | 29 行（`[Licensing::Module] …`） | Log L1–L73 |
| `Curl error 42: Callback aborted`（License 关闭时正常） | 1 行 | Log L10192 |
| `Assembly-CSharp.dll` 编译 | ✅ 成功（4,608 bytes，已写出） | Log L2163（Csc）+ L2577（CopyFiles） |
| `Assembly-CSharp-Editor.dll` 编译 | ✅ 成功（10,240 bytes，已写出） | Log L2165 + L2617 |
| Bee 后端 Dag 节点 | 915 个步骤 | Log L661–L2645 |
| Domain Reload 次数 | 1 次（reload time=1131 ms） | Log L10127 |
| 脚本编译时间 | `compile time=29211 ms`（≈ 29 s） | Log L10127 |
| Asset Pipeline Refresh（首次同步导入） | `Total: 90.557 seconds` | Log L10121（`InitialRefreshV2(ForceSynchronousImport)`） |
| Asset Pipeline Refresh（收尾） | `Total: 0.078 seconds` | Log L10180（`StopAssetImportingV2(NoUpdateAssetOptions)`） |
| Test Runner DLL 生成 | ✅ `UnityEditor.TestRunner.dll` (313,856) + `UnityEngine.TestRunner.dll` (177,664) | `Get-ChildItem Library/ScriptAssemblies` |
| Library/ScriptAssemblies DLL 总数 | 70 个 | `Get-ChildItem … \| Measure-Object` |

**新发现偏差**（BatchMode 实测后追加）：

| ID | 内容 | 影响 | 处理 |
|---|---|---|---|
| N-1 | Unity 6 在 batchmode + 无 license 状态下输出 29 行 `[Licensing::Module]` 噪声 + 1 行 `Curl error 42: Callback aborted`（log L10192）。 | 不构成编译错误；属环境噪声。Log 过滤时建议忽略前 100 行与最后 30 行的 license/curl 信号。 | 信息项；不修改 |
| N-2 | Test Framework 1.7.0 隐式带了 `com.unity.test-framework.performance` 子包（Log L5162）。 | 无影响；Performance Testing 仅在显式新建含 `Unity.PerformanceTesting` 引用的 asmdef 后才参与编译。 | 信息项；不修改 |
| N-3 | 模板 `Assets/TutorialInfo/` 内含 `Readme.cs : ScriptableObject` 与 `ReadmeEditor.cs : (uses UnityEditor)`，但**未触发任何编译错误**（Assembly-CSharp.dll 仅 4,608 字节，编译成功）。 | 验证模板脚手架可干净编译；与 §2.1 一致。 | 信息项；删除见已知偏差 |

**分类判定（按 `xingyuan-test-gate` SKILL 口径）**：

| 维度 | 判定 | 理由 |
|---|---|---|
| C# 脚本编译（Assembly-CSharp + Editor） | **run-and-pass** | Bee 后端 915 步全部 ExitCode=0；Assembly-CSharp.dll + Editor.dll 实际产出；0 compile error |
| 资产导入（InitialRefreshV2） | **run-and-pass** | 90.557s 同步导入完成（无强制中断）；0 asset import error |
| Domain Reload | **run-and-pass** | 1 次 reload，1131 ms，无异常 |
| Test Runner 装配 | **run-and-pass** | 两个 TestRunner DLL 写入 ScriptAssemblies，框架就绪 |
| Player 编译（实际出 `.exe`） | **not-run** | 本次仅设 `-buildTarget` 切换活跃 TargetGroup；未调用 `BuildPipeline.BuildPlayer` 或带 `-executeMethod` 的 Player 构建方法。仍属 static-only 在 Player 二进制产物维度上 |
| Console 0 Error 对比 | **partial-pass** | 项目业务代码 0 error；但存在 29 行 licensing 噪声（环境属性，非项目缺陷） |

**为何仍标「partial-pass」而非「run-and-pass」整体通过**：

按用户新指令「不得把 static-only 描述为编译通过 — 必须明确区分 run-and-pass / run-and-fail / static-only」：

- 项目**业务代码编译** = run-and-pass（Assembly-CSharp.dll 实际生成，0 error）
- **Player 构建** = not-run（仅为活跃 TargetGroup 切换，未触发 `BuildPipeline.BuildPlayer`）
- 因此整体归类为 **partial-pass**：ScriptAssemblies 跑通 + 资产导入跑通，但 Player 二进制未生成。

**未来改进**（Task 02 起建议补做）：

1. 完整 Player 构建：`-batchmode -quit -projectPath ... -buildTarget StandaloneWindows64 -executeMethod SomeBuildScript.PerformBuild` 才能生成 Player 二进制（需 ScriptableObject Build 配置）
2. Console 0 Error 基线：在 Task 02 第一动作前再跑一次，结果归档 `docs/IMPLEMENTATION_STATUS.md`
3. License 噪声过滤：在 QA 自检脚本中 grep 时排除前 100 行 / 最后 30 行

### 5.8 测试发现（Phase C-1 实测）

**取证路径 1 — 文件系统**：

```powershell
Test-Path Assets/Tests                                                               # False
(Get-ChildItem -Recurse -Filter *.asmdef | Measure-Object).Count                     # 0
Get-ChildItem -Recurse -Filter *.dll | Where-Object { $_.FullName -like "*Tests*" } # 0
```

**取证路径 2 — Unity BatchMode 日志**：

```powershell
Select-String -Path Logs\unity-batchmode.log -Pattern "Test Discovery|DiscoverTests|Find Tests|Run Tests|TestPlan|test runner"
# → 0 行匹配（BatchMode -quit 不触发 Test Runner；TestPlan 阶段被跳过）
```

**结论**：

- **0 tests discovered（预期）**。
- 原因：项目内 0 个 `.asmdef` + 不存在 `Assets/Tests/` → Test Runner 无可加载程序集，即无可发现测试。
- 此外 BatchMode 以 `-quit` 退出，未带 `-runTests`，即便存在测试也不会被执行（Test Runner 仅在显式 `-runTests -testPlatform <EditMode|PlayMode>` 时激活）。
- 仍属「Planned Gap，Task 02 内由 architect 建立 5 个 asmdef + `Starfall.Tests.EditMode` 后才具备可发现测试」。

---

## Section 6 — Task 01 → Task 02 交接 Brief（QA 出具）

### 6.1 Task 02 进入条件（Acceptance，重分类后 7 项）

进入 Task 02（工程骨架）须满足以下 7 项（按 Phase C-1 用户裁决重写）：

- [x] **(a) Task 01 审计 doc 落地**（本节完成即满足；含 §1–§6 + 「已知偏差」整节重写 + §5.5–§5.8 BatchMode 实测 + 1 commit `docs(audit): batchmode baseline + reclassification by xingyuan-qa`）。
- [ ] **(b) 【BLOCKING USER DECISION】用户对 Unity 版本偏差（`6000.5.3f1` vs `Unity 6.3 LTS`）作出裁决**（选项见 §5.1：A 文档更新 / B 版本升降 / C 维持并记录偏差）。**唯一保留的 Major 级阻塞项**。
- [ ] **(c) 【Planned Gap — Task 02】5 个 `Starfall.*` asmdef 由 architect 创建完成**（`Starfall.Core` / `Starfall.Data` / `Starfall.Unity` / `Starfall.Tests.EditMode` / `Starfall.Tests.PlayMode`，依赖图与 `Docs/02 §3` 一致）。原 Major M-B 降级为 Task 02 主交付。
- [ ] **(d) 【Planned Gap — Task 02】architect 起草 ADR-0001 / ADR-0002** — ADR-0001 含 `BattleState` / `GridPos` / `BoardState` / `UnitState` / `TileState` / FNV-1a 64 位哈希字段顺序 / `BattleStateCloner` / `BattleStateComparer`；ADR-0002 含 `Presenter` 同步契约（`BoardPresenter` / `UnitPresenterRegistry` / `BattleHud` 不持有第二真值 / `PresentationEvent` 失败不改 Core / Command 成功后才播放表现）。原 Major M-C 降级为 Task 02 内交付。
- [ ] **(e) 【信息项 — 不阻塞】ProjectSettings 模板默认值**（`companyName=DefaultCompany` / Android Bundle ID=`com.UnityTechnologies.com.unity.template.urpblank` / 唯一场景 `SampleScene.unity`）：记录偏差即可，**不**作为 Task 02 进入条件。原 Minor m-b / m-c / m-d 统一降级。用户如需修改可在 Task 02 内一次性批准后由 ui-tools 处理。
- [ ] **(f) 【信息项 — Task 02 候选】未使用 Packages 清理**（`com.unity.timeline` / `com.unity.visualscripting` / `com.unity.multiplayer.center` / `com.unity.ai.navigation` 4 项是否清理）：非阻塞；如要清理可在 Task 02 内一并处理（属 ProjectSettings 写入）。原 Minor / 信息性 i-a 统一保留为 Task 02 候选。
- [ ] **(g) 【Planned Gap — Task 02】新分支 `agent/02-project-skeleton` 由 architect 创建**（从 `agent/01-repository-audit` 派生或根据用户裁决从 `origin/main` 派生）；由 xingyuan-architect / xingyuan-gameplay / xingyuan-ui-tools 在该分支协同落地 asmdef + Core 依赖守卫测试 + ADR-0001/0002。

> 重分类对照（Phase C → C-1）：
> - 原 Major M-A（Unity 版本）= 现 **(b) BLOCKING USER DECISION**
> - 原 Major M-B（asmdef）= 现 **(c) Planned Gap — Task 02**
> - 原 Major M-C（ADR）= 现 **(d) Planned Gap — Task 02**
> - 原 Minor m-b / m-c / m-d（ProjectSettings）= 现 **(e) 信息项**
> - 原信息性 i-a（依赖清理）= 现 **(f) 信息项**

### 6.2 Task 02 不在范围（Out of Scope）

- 任何 `.cs` 业务代码（仅允许 `.asmdef` 模板与 `Tests/CoreDependencyGuardTests.cs` 之类守卫测试）。
- 修改 `Core` / `Data` / `Unity` 任一层业务逻辑（仅声明程序集边界，不实现玩法）。
- 引入第三方库 / NuGet / GitHub 插件。
- 修改 Unity 版本 / 安装新 Editor。
- 修改既有 `ProjectSettings/*.asset` 字段值（除非 6.1 显式批准）。
- 实现 GridPos / BattleState 等具体类型（属 Task 03 范围）。

### 6.3 Task 02 QA 测试计划（验收 Gate）

按 `xingyuan-test-gate` SKILL 口径，Task 02 完工须满足：

- [ ] **5 个 asmdef 必须存在且命名 / 依赖方向正确**
  - `Starfall.Core` / `Starfall.Data` / `Starfall.Unity` / `Starfall.Tests.EditMode` / `Starfall.Tests.PlayMode`
  - 依赖图与 `Docs/02 §3` 完全一致
  - 验证命令：`Get-ChildItem -Recurse -Filter *.asmdef` → 至少 5 条
- [ ] **Core 依赖守卫测试通过**
  - 测试文件：`Starfall.Tests.EditMode` 内至少 1 个测试扫描 `Starfall.Core` 程序集内禁止符号
  - 禁止：`using UnityEngine;` / `using UnityEditor;` / 业务型 `MonoBehaviour` / 业务型 `ScriptableObject` / `UnityEngine.Random`
  - 验证命令：`dotnet test` 或 Unity Test Runner，期望 0 失败
- [ ] **Unity Editor 打开后 Console 0 Error**
  - 基线：Task 02 第一动作前记录
  - Task 02 后：与基线对比，必须为 0 Error（警告数量允许变化，但不得新增 Error）
  - 验证：截图或 Editor.log 归档到 `docs/IMPLEMENTATION_STATUS.md`
- [ ] **编译通过**
  - EditMode 编译 1 次：`-batchmode -nographics -quit -projectPath ... -executeMethod UnityEditor.SyncVS.SyncSolution` 或 Test Runner EditMode 触发编译
  - Player 编译 1 次：`-batchmode -quit -projectPath ... -buildTarget StandaloneWindows64`
  - 日志：保留 `Editor.log` 全文，存放路径待 architect 指定

### 6.4 已知风险登记（Phase C-1 重分类，承接 Section 3 + Section 5 + BatchMode 实测）

> **重分类口径**：用户裁决（2026-07-12 18:51 GMT+8）明确区分 **BLOCKING USER DECISION / Planned Gap（附 Task 号）/ 信息项 / static-only**。原「Major / Minor / 信息性」三级在 Risk 表中同步重命名为「决 / Gap / 信」。

| ID | 风险 | 等级 | 来源 | 缓解策略 |
|---|---|---|---|---|
| R1 | Unity 版本裁决未做 → Task 02 进入条件阻塞 | **决**（BLOCKING USER DECISION） | §1.1 / §5.1 / §6.1(b) | 用户在 Task 02 启动前必选 A/B/C |
| R2 | asmdef 缺失 → Task 02 主交付物未达成 | **Gap**（Task 02 内由 architect 创建） | §2.1 / §5.5 / §5.6 / §6.1(c) | Task 02 内 architect 创建 5 个 `Starfall.*`；QA 在 §6.3 列测试计划 |
| R3 | ADR-0001/0002 缺失 → 架构契约空白 | **Gap**（Task 02 内由 architect 起草） | §6.1(d) | Task 02 内 architect 起草；qa 在 §6.3 验命名 / 依赖图 / 哈希协议 |
| R4 | ProjectSettings 模板值未清理 → 后续构建 / manifest 受影响 | **信**（不阻塞 Task 02） | §1.3 / §5.3 / §6.1(e) | 用户裁决后由 ui-tools 一次性处理；不作为 Task 02 进入条件 |
| R5 | 候选依赖未清理 → 构建时长 + 测试面拉长 | **信**（Task 02 候选） | §3.2 / §5.2 / §6.1(f) | 用户裁决；Task 02 内一并清理或留至 Task 09 整治 |
| R6 | push-based 子 Agent 完成事件丢失 → Lead 误判 | **信** | 运行经验 | Lead 启用 `subagents list` 兜底确认；不轮询 |
| R7 | `gh` CLI 不可用 → Issue 关联走 Web 手动 | **信** | 环境差异 | Lead 手动记录 Issue 引用；不阻塞开发 |
| R8 | 主会话 runtime Skill 列表缺 `xingyuan-test-gate` / `xingyuan-dev-workflow` / `xingyuan-determinism-review` → 子 Agent 须自读 | **信** | Agent 启动差异 | 子 Agent 用 `read <绝对路径>` 显式加载 SKILL.md |
| R9 | URP 17.5.0 与 `6000.5.3f1` 的严格「补丁级配套」未由 Unity 包兼容性矩阵验证 | **信** | §1.2.1 / §5.7 | Task 02 启动后用 `Package Manager UI` 检查 URP 兼容性报告；如报错，按 R1 联动处理 |
| R10 | `m_EnterPlayModeOptions: 0`（禁用 Domain Reload）→ PlayMode 测试静态缓存风险 | **信** | §1.3 / §5.3 | Task 02 内 architect 决策是否启用 Reload；QA 准备两条路径下的测试 |
| R11 | Unity 6 BatchMode 无 license 噪声污染日志（29 行 Licensing + 1 行 Curl error 42） | **信** | §5.7 N-1 | QA grep 日志时忽略前 100 行与最后 30 行；不作为项目缺陷 |
| R12 | Test Framework 1.7.0 隐式带 `com.unity.test-framework.performance` 子包 → 引入额外 DLL | **信** | §5.7 N-2 | 仅在显式新建 `Unity.PerformanceTesting` 引用 asmdef 后才参与编译；当前 0 影响 |

### 6.5 Task 02 启动模板（给 Lead 与 architect）

```text
任务包: Task 02 工程骨架
负责 Agent: xingyuan-architect（主）/ xingyuan-ui-tools（协同）/ xingyuan-qa（验收）
工作区: D:\AI-Worktrees\Xingyuan\architect（D:\AI-Worktrees\Xingyuan\ui-tools 协同修改 ProjectSettings）
分支: agent/02-project-skeleton（由 architect 从 agent/01-repository-audit 派生）

范围（依据本 Brief 6.1-6.2）:
  - 5 个 asmdef
  - Core 依赖守卫测试
  - ADR-0001 / ADR-0002 起草
  - 不实现玩法
  - 不修改 ProjectSettings（除非用户单独批准）

Gate（依据本 Brief 6.3）:
  - 5 个 asmdef 存在
  - Core 依赖守卫 0 失败
  - Editor Console 0 Error（与基线对比）
  - EditMode / Player 编译各 1 次通过
```

---

## Section 7 — 战斗代码只读审计（gameplay 出具，Lead 接管）

> **作者**：`xingyuan-gameplay`（`D:\AI-Worktrees\Xingyuan\gameplay`，原 Phase C-2 子会话运行 2m14s 后中断）
> **接管**：`xingyuan-lead` 于 2026-07-12 19:09 GMT+8 从 gameplay worktree 未提交修改中恢复 Section 7 草稿（244 行），重写为只读追加，避免重复扫描。Section 7 内容、取证命令与结论均来自 gameplay 原稿，Lead 仅做格式归一与提交落地。
> **审计模式**：只读（read-only）；本节为 Phase C-2 唯一交付物

### 7.1 审计范围与方法

**任务来源**：用户 2026-07-12 18:51 GMT+8 指令——在 Task 02 启动前，由 gameplay 侧对战斗代码做 8 类只读扫描，识别「业务代码 vs 模板代码」，不得修改任何文件（除本审计 doc 可追加 Section 7）。

**审计维度（8 类必查 + 4 类附加）**：

| 类别 | 期望实现位置（依 `Docs/02 / Docs/03`） |
|---|---|
| 1. 战斗状态 | `Starfall.Core/Model/BattleState.cs` / `BoardState.cs` / `TileState.cs` / `UnitState.cs`（Task 03+） |
| 2. Command | `Starfall.Core/Commands/ICommand.cs` / `CommandResult.cs` / `MoveCommand.cs` 等（Task 05+） |
| 3. Resolver | `Starfall.Core/Combat/DamageResolver.cs` 等（Task 06+） |
| 4. 路径 | `Starfall.Core/Pathfinding/Pathfinder.cs` 等（Task 04+） |
| 5. 攻击 | `Starfall.Core/Combat/Attack*.cs` / `Damage*.cs`（Task 06+） |
| 6. 状态 | `Starfall.Core/Status/Status*.cs` / `Effect*.cs` / `Buff*.cs`（Task 07+） |
| 7. Replay | `Starfall.Core/Replay/Replay*.cs` / `BattleRecord.cs`（Task 12） |
| 8. Undo | `Starfall.Core/Commands/Undo*.cs` / `Revert*.cs` / `Rollback*.cs`（Task 05+） |

**取证方法**（全只读，零写入）：

- `git grep -nE "<pattern>" Assets/ Packages/` —— 8 类模式匹配
- `Get-ChildItem -Recurse -Directory -Filter Starfall*` —— Starfall 目录结构
- `Test-Path Assets/Scripts` —— 项目脚本目录是否存在
- `Get-ChildItem Assets/TutorialInfo -Recurse -File -Include *.cs` —— 模板脚手架清点
- `Get-ChildItem Packages -Recurse -File -Include *.cs` —— 包内代码抽样（排除 `com.unity.*`）
- `Get-Content Assets/TutorialInfo/Scripts/Readme.cs`（前 10 行）—— 模板 vs 业务判别

### 7.2 8 类审计结果矩阵

| # | 类别 | grep pattern | grep 结果 | 业务代码 | 模板残留 | 结论 |
|---|---|---|---|---|---|---|
| 1 | 战斗状态 | `class\s+(BattleState\|BoardState\|TileState\|UnitState)` | **0 匹配** | 否 | 否 | 0 实现，符合 Task 01 基线（Task 03+ 才开始） |
| 2 | Command | `(class\|interface)\s+(ICommand\|CommandResult\|\w*Command)\b` | **0 匹配** | 否 | 否 | 0 实现，符合 Task 01 基线（Task 05+ 才开始） |
| 3 | Resolver | `class\s+\w*Resolver\b` | **0 匹配** | 否 | 否 | 0 实现，符合 Task 01 基线（Task 06+ 才开始） |
| 4 | 路径 | `(class\|method)\s+(\w*Path\w*\|\w*BFS\w*\|\w*AStar\w*)` | **0 匹配** | 否 | 否 | 0 实现，符合 Task 01 基线（Task 04+ 才开始） |
| 5 | 攻击 | `(class\|method)\s+(\w*Attack\w*\|\w*Damage\w*)` | **0 匹配** | 否 | 否 | 0 实现，符合 Task 01 基线（Task 06+ 才开始） |
| 6 | 状态 | `(class\|enum)\s+(\w*Status\w*\|\w*Effect\w*\|\w*Buff\w*)` | **0 匹配** | 否 | 否 | 0 实现，符合 Task 01 基线（Task 07+ 才开始） |
| 7 | Replay | `(class\|method)\s+(\w*Replay\w*\|\w*History\w*\|\w*Snapshot\w*)` | **0 匹配** | 否 | 否 | 0 实现，符合 Task 01 基线（Task 12 才开始） |
| 8 | Undo | `(class\|method)\s+(\w*Undo\w*\|\w*Revert\w*\|\w*Rollback\w*)` | **0 匹配** | 否 | 否 | 0 实现，符合 Task 01 基线（Task 05+ 才开始） |

**取证摘要**：

```powershell
git grep -nE "class\s+(BattleState|BoardState|TileState|UnitState)" Assets/ Packages/   # exit=1 → 0 匹配
git grep -nE "(class|interface)\s+(ICommand|CommandResult|\w*Command)\b" Assets/ Packages/ # exit=1 → 0 匹配
git grep -nE "class\s+\w*Resolver\b" Assets/ Packages/                                  # exit=1 → 0 匹配
git grep -nE "(class|method)\s+(\w*Path\w*|\w*BFS\w*|\w*AStar\w*)" Assets/ Packages/    # exit=1 → 0 匹配
git grep -nE "(class|method)\s+(\w*Attack\w*|\w*Damage\w*)" Assets/ Packages/          # exit=1 → 0 匹配
git grep -nE "(class|enum)\s+(\w*Status\w*|\w*Effect\w*|\w*Buff\w*)" Assets/ Packages/  # exit=1 → 0 匹配
git grep -nE "(class|method)\s+(\w*Replay\w*|\w*History\w*|\w*Snapshot\w*)" Assets/ Packages/ # exit=1 → 0 匹配
git grep -nE "(class|method)\s+(\w*Undo\w*|\w*Revert\w*|\w*Rollback\w*)" Assets/ Packages/   # exit=1 → 0 匹配
```

> 注：`exit=1`（PowerShell 表现 `NativeCommandError`）= `git grep` 无匹配的标准返回码。**全部 8 类零业务命中**。

**结论**：业务代码实现为零完全符合 Task 01 基线（Task 01 仅做审计，不写代码）。Task 03+ 才会按 `Docs/04_Roadmap_and_Milestones.md` 顺序落地 8 类实现。

### 7.3 Starfall.* 目录结构

```powershell
Get-ChildItem -Recurse -Directory -Filter Starfall* -ErrorAction SilentlyContinue
# → 0 条匹配
```

**取证**：

- `Starfall.*` 目录数：**0**
- 与 §2.1（`Get-ChildItem -Recurse -Filter *.asmdef` = 0 条）一致
- 与 §5.5（无 Starfall.* 程序集）一致
- 与 §6.1(c)（G-A asmdef 缺失 = Planned Gap — Task 02）一致

**结论**：当前仓库零 Starfall 命名空间落地，与 Task 02 / Task 03+ 主交付计划完全一致，无任何越界实现。

### 7.4 Assets/Scripts/ 现状

```powershell
Test-Path Assets/Scripts
# → False
```

**取证**：

- `Assets/Scripts/`：**不存在**
- 实际项目脚本目录树：
  - `Assets/Scenes/`（仅含 `SampleScene.unity` 模板）
  - `Assets/Settings/`（URP / Graphics 配置）
  - `Assets/TutorialInfo/`（URP Blank 模板自带 `Readme` + `Icons/`）

**结论**：未创建 `Assets/Scripts/` 是预期行为——Task 02 由 architect 在 `Starfall.*` asmdef 落地后才会衍生该目录；提前建空目录属越界。

### 7.5 模板代码识别

**取证**：

| 文件路径 | 大小 | 前 10 行摘要 | 属性 |
|---|---|---|---|
| `Assets/TutorialInfo/Scripts/Readme.cs` | 302 B | `public class Readme : ScriptableObject { public Texture2D icon; public string title; public Section[] sections; public bool loadedLayout; ... }` | **模板 / URP Blank 自带** |
| `Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs` | 6,677 B | `[CustomEditor(typeof(Readme))] [InitializeOnLoad] using UnityEditor; ...` | **模板 / URP Blank 自带** |

**判别依据**：

- 命名空间缺失（非 `Starfall.*`）
- 类型为通用 `ScriptableObject` / 通用 `CustomEditor` 包装
- 仅 `Assets/TutorialInfo/` 路径下（URP Blank 模板保留目录）
- 与 §2.1、§5.7 N-3、已知偏差 G-F 一致
- **未触发任何编译错误**（§5.7 BatchMode：Assembly-CSharp.dll 4,608 bytes + Editor.dll 10,240 bytes 编译通过）

**结论**：模板代码无业务越界。**处置建议**：列入 Planned Gap — Task 02（G-F 模板清理），由 architect 或 ui-tools 在 Task 02 内删除 `Assets/TutorialInfo/` 整目录；属 `docs/IMPLEMENTATION_STATUS.md` 待清理项。

### 7.6 Packages/ 内游戏代码抽样

**取证 1 — 排除 Unity 自带包后的游戏相关 .cs**：

```powershell
Get-ChildItem Packages -Recurse -File -Include *.cs -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -notlike "*com.unity*" } |
  Select-Object -First 10
# → 0 条匹配
```

**取证 2 — Packages/ 目录实际内容**：

- `Packages/manifest.json`（Unity 依赖清单，§1.2 / §3.1 已审）
- `Packages/packages-lock.json`（已解析版本快照）
- **`Packages/com.unity.*` 不存在本地目录**——Unity 解析后的包源码缓存于 `Library/PackageCache/`（gitignored，§5.7 验证），不属项目业务代码范畴

**结论**：

- Packages/ 内**0 个游戏相关 .cs**（排除 `com.unity.*` 标准包）
- 项目**未引入**任何第三方源码包（无 OpenUPM / Git URL 直引 / 自定义 tarball）
- 与 §3.1（依赖表仅含 19 项官方包）一致
- 与 §6.1(f)（I-C 候选依赖清理是另一维度，移除而非新增）一致

### 7.7 gameplay 视角的「Task 02 进入准备度」

**业务代码 0 实现 → 预期**

- 8 类审计（§7.2）全部零命中，符合 Task 01「不写代码、只审计」契约
- 与 §6.1(c)/(d) Planned Gap 列表对齐——Task 02 才交付 asmdef + ADR-0001/0002，Task 03+ 才落地业务类型

**模板代码无业务越界 → 预期**

- `Assets/TutorialInfo/Scripts/*.cs`（2 个文件）均属 URP Blank 模板保留
- 与 §5.7 N-3 一致；未触发编译错误；可于 Task 02 内由 architect/ui-tools 整目录删除

**确定性风险（详见 `xingyuan-determinism-review` SKILL）**：

依 `.agents/skills/xingyuan-determinism-review/SKILL.md` 8 项审查口径（已在 Agent 间连通性测试中汇报）：

| # | 审查项 | 当前状态 | 后续 Task 落实点 |
|---|---|---|---|
| D-1 | 无序集合迭代影响结果 | **N/A**（无业务代码） | Task 03（BattleState 字段集合排序）/ Task 05（Command 顺序） |
| D-2 | 随机值 / `UnityEngine.Random` | **N/A**（无业务代码） | Task 03 内由 ADR-0001 规定确定性 RNG 路径 |
| D-3 | 当前时间 / `DateTime.Now` / `Time.realtimeSinceStartup` | **N/A**（无业务代码） | Task 03+ 内由 Core 守卫测试拦截 |
| D-4 | GUID 生成 / `System.Guid.NewGuid()` | **N/A**（无业务代码） | 同上 |
| D-5 | 不稳定哈希（`object.GetHashCode` / `string.GetHashCode`） | **N/A**（无业务代码） | Task 04（Clone/Compare/Hash）内由 ADR-0001 规定 FNV-1a 64 位 |
| D-6 | 缺 Tie-break / 浮点几何比较 | **N/A**（无业务代码） | Task 04 内由 pathfinder 测试守住 |
| D-7 | Replay 依赖 Unity 对象 / InstanceID | **N/A**（无业务代码） | Task 12 内 BattleRecord 必须纯数据 |
| D-8 | 网格排序非「先 y 后 x」/ 寻路邻居非「下左上右」 | **N/A**（无业务代码） | Task 03 / Task 04 内由 Core 守卫测试 + EditMode 测试锁定 |

**后续 Task 落地路径（gameplay 主责范围）**：

- Task 03（Core 基础状态）— `BattleState` / `GridPos` / `BoardState` / `UnitState` / `TileState` 类型；FNV-1a 64 位哈希字段顺序
- Task 04（Clone/Compare/Hash）— `BattleStateCloner` / `BattleStateComparer`；寻路稳定排序
- Task 05（Command 框架）— `ICommand` / `CommandResult` / `MoveCommand` / `AttackCommand` / Undo / Revert
- Task 06（战斗规则）— `DamageResolver` / 攻击伤害公式
- Task 07（状态系统）— `Status` / `Effect` / `Buff` 类型 + Tick 顺序
- Task 12（Replay）— `BattleRecord` / 纯数据快照

**Task 03 / Task 04 / Task 05 落地的前置依赖**（与 Task 02 衔接）：

- 5 个 `Starfall.*` asmdef 已建（Task 02 G-A）
- ADR-0001（数据模型与哈希契约）已起草（Task 02 G-B）
- ADR-0002（Presenter 同步契约）已起草（Task 02 G-B）
- Core 依赖守卫测试已建（Task 02 G-C）

### 7.8 gameplay 给 Lead 的 READINESS 视角

**Task 02 主责范围**：

- Task 02 不由 gameplay 主责（仅 `architect` + `ui-tools` + `qa`）
- gameplay 在 Task 02 期间处于「待命」状态，仅当 architect 创建 5 个 `Starfall.*` asmdef 后由 gameplay 协同写 `Tests.EditMode` 中的「Core 依赖守卫」测试用例之一（如：`禁止 using UnityEngine`、`禁止 using UnityEditor`、`禁止业务型 MonoBehaviour`、`禁止 UnityEngine.Random`）

**Task 02 完成后，Task 03 即可启动，需预先确认**：

- [ ] **ADR-0001**（数据模型与哈希契约）已在 Task 02 内由 architect 完成；含：
  - `BattleState` / `GridPos` / `BoardState` / `UnitState` / `TileState` 字段定义
  - FNV-1a 64 位哈希字段顺序（必须固化在 ADR 内，防止 Task 04 实现时口径漂移）
  - `BattleStateCloner` / `BattleStateComparer` 接口签名
- [ ] **5 个 asmdef**（尤其 `Starfall.Core` / `Starfall.Tests.EditMode`）就位
  - `Starfall.Core` 必须依赖 0 项 Unity 程序集（§10.1 Core 硬约束）
  - `Starfall.Tests.EditMode` 须引用 `Starfall.Core` + `UnityEngine.TestRunner` + `UnityEditor.TestRunner`

**gameplay 不需要用户额外裁决**：本节仅审计、汇报与列依赖；如出现下列情况则**需** Lead 介入协调：

| 触发条件 | 介入动作 |
|---|---|
| Task 02 后 `Starfall.Core` asmdef 引用了 Unity 程序集 | gameplay 报告 Lead，由 architect 修正 |
| Task 02 后 ADR-0001 哈希顺序未定 | gameplay 不启动 Task 04，先反馈 Lead |
| 用户裁决 U-A（Unity 版本）后 BatchMode 需重跑 | gameplay 配合 qa 重跑并比对状态哈希 |
| Task 02 期间发现 §7.2 / §7.5 / §7.6 取证外的越界业务代码 | gameplay 立即报告 Lead，停止 Task 03 启动 |

**gameplay READINESS 判定**：

- ✅ **Task 02 启动无需 gameplay 介入**
- ✅ **Task 03 启动前仅需 7.8 上述 2 项预确认**
- ✅ **业务代码 0 命中、模板无越界、依赖无游戏包注入** = Task 02 启动条件（业务代码维度）全部满足

### 7.9 本节禁项自检（Phase C-2 gameplay，未执行任何）

- ❌ 修改 `Assets/` 下任何文件（含模板 `.cs` / `.asset` / `.unity` / `.prefab`）
- ❌ 修改 `ProjectSettings/*.asset`
- ❌ 修改 `Packages/manifest.json`
- ❌ 创建任何 `.cs` 业务代码
- ❌ 创建任何 `.asmdef`
- ❌ 修改 `.agents/skills/` 下任何 `SKILL.md`
- ❌ 安装新 Package / 修改依赖
- ❌ 删除文件
- ❌ `git push` / 创建 PR / 合并 / 发布
- ❌ 运行 Unity BatchMode（qa 已跑过，重复无意义）
- ❌ 跨 worktree 写入（仅在 gameplay worktree 内操作）

**唯一允许的写操作**（实际由 Lead 在 main worktree 完成提交）：

- ✅ `git fetch . agent/01-repository-audit:agent/01-repository-audit`（仅本 worktree 内同步远端引用）
- ✅ `git checkout agent/01-repository-audit`（Lead 在 main worktree 切换）
- ✅ `edit Docs/OPENCLAW_REPOSITORY_AUDIT.md`（Lead 追加 Section 7）
- ✅ `git add Docs/OPENCLAW_REPOSITORY_AUDIT.md` + `git commit`（Lead 落地）
- ✅ `git checkout agent/gameplay-bootstrap`（gameplay 在自己 worktree 切回 bootstrap，丢弃未提交改动）

---

## Section 8 — Unity 资产只读审计（ui-tools 出具）

> **作者**：`xingyuan-ui-tools`（`D:\AI-Worktrees\Xingyuan\ui-tools`）
> **模式**：只读（read-only）；Phase C-3 唯一交付物
> **依据**：用户 2026-07-12 18:51 GMT+8 指令 — 仅审查 Scenes / URP / InputSystem / Presenter / UI / Data / Definition 现状；不修改 Unity 配置或 Packages；不修改任何文件（除本审计 doc）
> **配合**：本节为 Section 1–7 的 ui-tools 视角补充（architect 已审 Section 3 资产目录、qa 已审 Section 5.7 BatchMode 编译、gameplay 已审 Section 7 战斗代码）；本节专注于「业务层资产是否已就位」与「Task 02 / Task 15+ 启动准备度」

### 8.1 审计范围与方法

**范围**：8 类 Unity 业务相关资产 + 3 类辅助审计

```text
1. Scenes（场景）
2. URP 配置（Universal Render Pipeline）
3. Input System 资源
4. Presenter（BoardPresenter / UnitPresenter / BattleHud）
5. UI（uGUI Canvas / EventSystem / Image / Text）
6. Data（Definition / JSON / ScriptableObject 数据容器）
7. Definition（业务定义文件）
8. SampleScene 与模板资源整体清点

附 9.  Assets/Scripts/ 是否存在
附 10. Library/ 残留与 .gitignore 覆盖
附 11. ProjectSettings/ 完整性
```

**方法**：仅 `Get-ChildItem` / `Get-Content` / `Select-String` / `git grep` / `Test-Path` / `git check-ignore` / `git ls-files`；不调用 `New-Item` / `Set-Content` / `Remove-Item`；不调用 Unity；不修改任何 Unity 资产、ProjectSettings、Packages、.meta、.gitignore。

**验证原则**：

- 模板资产识别：以 Unity 6.5 LTS + URP Blank 模板基线为参照；TutorialInfo/、URP Blank Settings/、SampleScene.unity、InputSystem_Actions.inputactions、Readme.asset 均属模板自带
- 业务资产识别：`BoardPresenter` / `UnitPresenter` / `BattleHud` / `MoveDefinition` / `UnitDefinition` / `DecreeDefinition` / `StatusDefinition` / `Canvas` / `Panel` / `HudButton` / `DataAsset` / `ConfigAsset` 等关键词在 Assets/ 内 0 匹配即视为业务未实现
- 「未实现」= 符合 Task 01「零玩法增量」预期；下一阶段 Task 02 / Task 13+ 按 Docs/04 Roadmap 推进

### 8.2 8 类审计结果矩阵

| # | 类别 | 路径 / 枚举命令 | 枚举结果 | 模板 | 业务 | 结论 |
|---|---|---|---|---|---|---|
| 1 | Scenes | `Get-ChildItem Assets/Scenes -Recurse -File -Include *.unity` | 1（SampleScene.unity，11413 字节） | 是 | 否 | 仅模板场景；含 Main Camera + Directional Light + Global Volume；无业务场景；Task 15 新增战斗场景前无需改动 |
| 2 | URP | `Get-ChildItem Assets/Settings -Recurse -File` | 8 个 .asset（DefaultVolumeProfile + Mobile_Renderer + Mobile_RPAsset + PC_Renderer + PC_RPAsset + SampleSceneProfile + UniversalRenderPipelineGlobalSettings + .meta 8 套） | 是 | 否 | URP 17.5.0 模板全套；GraphicsSettings.m_CustomRenderPipeline → PC_RPAsset (guid 4b83569d67af61e458304325a23e5dfd)；PC_RPAsset.m_RenderScale=1，Mobile_RPAsset.m_RenderScale=0.8；无业务 RendererFeature |
| 3 | Input System | `Get-ChildItem Assets -Recurse -File -Include *.inputactions` | 1（InputSystem_Actions.inputactions，41005 字节） | 是 | 否 | 模板默认 Player 地图（Move/Look/Attack/Interact/Crouch/Jump/Previous/Next/Sprint）+ UI 地图（Navigate/Submit/Cancel/Point/Click/RightClick/MiddleClick/ScrollWheel/TrackedDevicePosition...）；activeInputHandler=1（新 Input System 独占）；Task 05+ 需新增 Move/PhaseFlip/Attack/DeployDecree 动作映射 |
| 4 | Presenter | `git grep -nE "class\s+(\w*Presenter\b\|\w*View\b\|\w*Hud\b\|\w*BattleHud\b)" Assets/` | 0 匹配 | — | — | 业务未实现；Test-Path Assets/Scripts/Presenter = False；Test-Path Assets/Scripts = False；Task 16 Planned Gap（详见 8.7） |
| 5 | UI | `git grep -nE "class\s+(\w*Canvas\b\|\w*Panel\b\|\w*HudButton\b)" Assets/` | 0 匹配 | — | — | 业务未实现；Test-Path Assets/Scripts/UI = False；uGUI 2.5.0 包已就绪（Packages/manifest.json line 12），但未实例化 Canvas/Panel/Button；Task 18 Planned Gap |
| 6 | Data | `Get-ChildItem Assets -Recurse -File -Include *.json` + `git grep -nE "class\s+(\w*Definition\b\|\w*DataAsset\b\|\w*ConfigAsset\b)" Assets/` | 0 .json + 0 匹配 | — | — | 业务未实现；Test-Path Assets/Data = False；Test-Path Assets/StreamingAssets = False；模板自带 Readme.asset 是 ScriptableObject（class `Readme : ScriptableObject`）但属性属 I-A 信息项；Task 13/14 Planned Gap |
| 7 | Definition | `git grep -nE "(MoveDefinition\|UnitDefinition\|DecreeDefinition\|StatusDefinition)" Assets/ Docs/` | 0 匹配 in Assets/；3 引用 in Docs/03（line 201 / 465 / 492） | — | — | Docs/03 数据契约已写明 4 类 Definition 字段；Assets/ 内 0 个；Task 13 启动后由 ui-tools + architect 协同定义 |
| 8 | SampleScene + 全 Assets 清点 | `Get-ChildItem Assets -Recurse -File \| Group-Object Extension` | 32 个文件 = .asset×8 + .cs×2 + .inputactions×1 + .meta×20 + .png×1 + .unity×1 + .wlt×1；无 .prefab / 无 .controller / 无 .mixer / 无 .spriteatlas | 是 | 否 | 全模板；零业务资产；模板 TutorialInfo/ 含 Readme.cs + ReadmeEditor.cs + Readme.asset + Layout.wlt + Icons/URP.png |

### 8.3 Assets/ 整体结构与文件类型分布

**目录树**（取自 `Get-ChildItem Assets -Recurse -Directory`）：

```text
Assets/
├── InputSystem_Actions.inputactions          (41005 B)
├── Readme.asset                              (Template SO)
├── Scenes/
│   └── SampleScene.unity                     (11413 B)
├── Settings/
│   ├── DefaultVolumeProfile.asset            (23987 B)
│   ├── Mobile_Renderer.asset                 (1713 B)
│   ├── Mobile_RPAsset.asset                  (4672 B)
│   ├── PC_Renderer.asset                     (3439 B)
│   ├── PC_RPAsset.asset                      (4691 B)
│   ├── SampleSceneProfile.asset              (3703 B)
│   └── UniversalRenderPipelineGlobalSettings.asset (26897 B)
└── TutorialInfo/
    ├── Layout.wlt                            (URP Blank layout)
    └── Scripts/
        ├── Editor/
        │   └── ReadmeEditor.cs               (Template editor inspector)
        ├── Readme.cs                         (Template Readme SO)
        └── Icons/
            └── URP.png                       (24069 B)
```

**按扩展名分组的 Count（取证：Group-Object Extension）**：

```text
Ext           Count   TotalSize(KB)
.asset            8          68.6    (7 URP + 1 Readme)
.cs               2           6.8    (Readme + ReadmeEditor)
.inputactions     1            40.0   (InputSystem_Actions)
.meta            20           6.9    (一文件一 .meta，含空文件夹 .meta 4 个)
.png              1           23.5   (TutorialInfo/Icons/URP.png)
.unity            1           11.1   (SampleScene)
.wlt              1           15.8   (TutorialInfo/Layout.wlt)
```

合计 32 个文件 / 约 172 KB（不含 .meta）。**业务资产数 = 0**。

### 8.4 Library/ 残留与 .gitignore 覆盖验证

**Library/ 现状**：

```text
Test-Path Library                       → False
Test-Path Library/ScriptAssemblies      → False
```

注：本 worktree (`D:\AI-Worktrees\Xingyuan\ui-tools`) 不含 Library/；主工作区 `D:\UntiyProject\XingyuanCovenant` 在 qa Phase C-1 BatchMode 跑完后已生成 Library/，但仅存于主 worktree 且未推送到本 worktree。这是正常的 worktree 隔离行为，不构成审计差异。

**.gitignore 覆盖验证**：

```text
$ git check-ignore -v Library/
.gitignore:1:[Ll]ibrary/  Library/

$ exit=0
```

**结论**：`.gitignore` line 1 以 `[Ll]ibrary/` 模式覆盖 Library/（含 `Library/` 与 `library/` 两种命名），即便主工作区生成 Library/ 也不会污染本审计分支或 git 提交。同理覆盖 Temp/、obj/、Build/、Builds/、Logs/、UserSettings/、MemoryCaptures/、Recordings/、TestResults/。

### 8.5 ProjectSettings/ 完整性

**所有 .asset / .txt 文件清单**（取自 `Get-ChildItem ProjectSettings -File`）：

```text
AudioManager.asset                       413 B
ClusterInputManager.asset                114 B
DynamicsManager.asset                  1254 B
EditorBuildSettings.asset                371 B
EditorSettings.asset                   1687 B
GraphicsSettings.asset                 2957 B
InputManager.asset                     9731 B
MemorySettings.asset                   1192 B
MultiplayerManager.asset                157 B
NavMeshAreas.asset                     1308 B
PackageManagerSettings.asset           1157 B
Physics2DSettings.asset                2028 B
PhysicsCoreProjectSettings2D.asset      151 B
PresetManager.asset                     146 B
ProjectSettings.asset                 25517 B
ProjectVersion.txt                       83 B   ← m_EditorVersion: 6000.5.3f1
QualitySettings.asset                 3662 B
ShaderGraphSettings.asset               556 B
TagManager.asset                        657 B
TimeManager.asset                       202 B
UnityConnectSettings.asset             1063 B
URPProjectSettings.asset                461 B
VersionControlSettings.asset            188 B
VFXManager.asset                        308 B
XRSettings.asset                        158 B
```

合计 26 个文件 / 约 56 KB。**所有 Unity 6.5 LTS 标准 ProjectSettings 文件齐全**（含 6.5 新增 MemorySettings.asset / PhysicsCoreProjectSettings2D.asset）。

**关键字段取证**（`Select-String` ProjectSettings/ProjectSettings.asset）：

```text
productGUID:           db28ec0c4e884b048bda3ba517d6039c        (line 7)
companyName:           DefaultCompany                          (line 15)  ← I-A 信息项，待改
productName:           XingyuanCovenant                        (line 16)  ← 已设
activeInputHandler:    1                                       (line 929) ← 新 Input System
apiCompatibilityLevel: 6                                       (line 927) ← .NET Standard 2.1
gcIncremental:         1                                       (line 848)
applicationIdentifier: (空)                                   (line 171) ← I-B 信息项，待改
overrideDefaultApplicationIdentifier: 1                       (line 180)
AndroidMinSdkVersion:  26                                      (line 182)
```

**TagManager.asset** 现状（`Get-Content`）：

```text
tags:                []                                    ← 空（I-A 信息项）
layers:              [Default, TransparentFX, Ignore Raycast, (空), Water, UI]
m_SortingLayers:     [Default]
m_RenderingLayers:   [Default, Light Layer 1..7, (空)...]
```

URP 17.5.0 模板默认；Layer 7 = UI、8 = Water；按 Docs/03 后续可加 `BattleUnit` / `PhaseAlpha` / `PhaseBeta` / `Gravity` 等。

**GraphicsSettings.asset** 关键引用：

```text
m_CustomRenderPipeline: {fileID: 11400000, guid: 4b83569d67af61e458304325a23e5dfd, type: 2}
                                              ↑ Assets/Settings/PC_RPAsset.asset.meta guid
```

→ 默认 RP 已切到 PC_RPAsset；Mobile 走独立 RPAsset（见 Category 2）。

### 8.6 ui-tools 视角的「Task 02 / Task 15+ 进入准备度」

**模板脚手架完整**（无需立即改动，Task 02 启动后 G-F 删除）：

- ✅ `Assets/TutorialInfo/Scripts/Readme.cs`（31 行模板 Readme SO，附带 `Section[] sections` 与 `icon/title/loadedLayout`）
- ✅ `Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs`（Readme Inspector 自定义）
- ✅ `Assets/Readme.asset`（YAML 序列化，含 title="URP Empty Template"）
- ✅ `Assets/TutorialInfo/Layout.wlt`（Editor 窗口布局）
- ✅ `Assets/TutorialInfo/Icons/URP.png`（23.5 KB）

**URP 配置齐全**（GraphicsSettings → PC_RPAsset）：

- ✅ `Assets/Settings/PC_RPAsset.asset`（m_RenderScale=1，m_RenderingMode=2 = Forward+）
- ✅ `Assets/Settings/Mobile_RPAsset.asset`（m_RenderScale=0.8，m_RenderingMode=0 = Forward）
- ✅ `Assets/Settings/PC_Renderer.asset` + `Mobile_Renderer.asset`（m_RendererFeatures=[] 空）
- ✅ `Assets/Settings/DefaultVolumeProfile.asset`（23.6 KB，含 URP 默认 Volume）
- ✅ `Assets/Settings/SampleSceneProfile.asset`（3.6 KB，场景专属 Volume）
- ✅ `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`（26.2 KB，URP 全局设置）

**Input System 已切新**（Task 05+ 改造映射）：

- ✅ `Packages/manifest.json:7` `com.unity.inputsystem: 1.19.0`
- ✅ `ProjectSettings/ProjectSettings.asset:929` `activeInputHandler: 1`（新 Input System 独占，旧 Input Manager 关闭）
- ✅ `Assets/InputSystem_Actions.inputactions`（Player + UI 双地图，模板默认）

**业务 Presenter / UI / Data / Definition = 0 实现（预期）**：

- ❌ `Assets/Scripts/Presenter/` 不存在（Task 16 Planned Gap）
- ❌ `Assets/Scripts/UI/` 不存在（Task 18 Planned Gap）
- ❌ `Assets/Data/` 不存在（Task 13/14 Planned Gap）
- ❌ `Assets/StreamingAssets/` 不存在（Task 14 Planned Gap）
- ❌ `*.json` 业务数据 0 个（Task 14 Planned Gap）
- ❌ `MoveDefinition` / `UnitDefinition` / `DecreeDefinition` / `StatusDefinition` 0 个（Task 13 Planned Gap）

**后续 Task 主责归属（依据 Docs/04 Roadmap）**：

| Task | ui-tools 主责 | 备注 |
|------|---------------|------|
| Task 02 工程骨架 | 协同 architect（修改 ProjectSettings 极少） | 不修改 ProjectSettings（用户单独批准除外） |
| Task 05 Input | 主责 InputAction 资源改造 | 本 worktree 主改 |
| Task 13 Data | 主责 Definition JSON 容器 + ScriptableObject | 与 architect 共定接口 |
| Task 14 JSON | 主责 StreamingAssets 加载 + 校验 | 模板 json 工具就绪 |
| Task 15 Scene | 主责战斗场景新建 | 本 worktree 新增 |
| Task 16 Presenter | 主责 BoardPresenter / UnitPresenter / BattleHud | 模板无任何 Presenter |
| Task 18 UI | 主责 Canvas / Panel / HudButton | uGUI 2.5.0 包就绪 |

**Library/ 与 .gitignore 互洽**：BatchMode 后不会污染 git（见 8.4）。

### 8.7 ui-tools 给 Lead 的 READINESS 视角

**Task 02 启动后 ui-tools 待办**（用户裁决后落地）：

- **G-F**：删除 `Assets/TutorialInfo/` 整目录（模板清理；2 个 .cs + 1 个 .asset + 1 个 .wlt + 1 个 .png + 5 个 .meta）—— Task 02 内触发，Task 02 G-F 标签
- **G-G**：InputAction 资源在 Task 05+ 改造。当前 Player 地图保留 Move/Look（相机视角用），需要新增 Move（CellSelect）、PhaseFlip（Q）、Attack（确认）、DeployDecree（右键）等动作；模板动作 `Attack/Interact/Crouch/Jump/Previous/Next/Sprint` 不删，保留以避免 InputAction 资源破坏性修改（破坏 = 失 binding）
- **I-A**：`companyName` 改为 `XingyuanCovenant`（信息项，可一并处理）
- **I-B**：Android Bundle ID 改为 `com.xingyuan.covenant`（信息项，需用户在 Task 02 后裁决）
- **I-C**：未使用 4 项 Packages 候选清理（`com.unity.multiplayer.center` / `com.unity.timeline` / `com.unity.visualscripting` / `com.unity.ide.rider` —— 用户裁决后处理）

**Task 15 启动后 ui-tools 主责**：

- 新增战斗场景 `Assets/Scenes/BattleScene.unity`（8×10 网格 + 4 玩家单位 + 撤离目标点）
- 添加至 EditorBuildSettings（ProjectSettings/EditorBuildSettings.asset）
- 配套 Lighting / Volume Profile 复用 PC_RPAsset + SampleSceneProfile

**Task 16 / Task 18 启动后 ui-tools 主责**：

- 新增 `Assets/Scripts/Presenter/`（BoardPresenter / UnitPresenter / BattleHud）
- 新增 `Assets/Scripts/UI/`（HUDCanvas / ActionPanel / EndTurnButton / DecreeButton）
- 新增 `Assets/Scripts/Unity/`（Bootstrap.cs / GameLifetimeScope.cs / CameraRig.cs）
- 全部通过 `Starfall.Unity` asmdef 引用 Core + Data + UnityEngine，不引入第三方

**当前 ui-tools 风险与建议**：

- ⚠️ Risk-A：`Assets/Readme.asset` 关联 `TutorialInfo/Scripts/Readme.cs`；删除 TutorialInfo 时必须同步删除 Readme.asset，否则 YAML 引用悬空
- ⚠️ Risk-B：InputAction 资源若 Task 05 一次性大改（删除模板动作），InputActionAsset 重生成会导致 binding 全丢；建议保留模板动作、新增业务动作并通过 binding group 隔离
- ⚠️ Risk-C：`ProjectSettings/TagManager.asset` 仅 Default/Water/UI 三层（除内置 0–5），后续 BattleUnit / PhaseAlpha / PhaseBeta / Gravity 等需在 Task 15 前追加
- ⚠️ Risk-D：`Library/` 在主 worktree 存在但本 worktree 不存在；切换 worktree 时 Unity 需重新 import（首次打开 Editor 慢 1–2 分钟，可接受）

### 8.8 本节禁项自检（Phase C-3 ui-tools）

```text
- ✅ 未修改 Assets/ 下任何 .unity / .prefab / .asset / .cs / .meta / .inputactions
- ✅ 未修改 ProjectSettings/*.asset / ProjectVersion.txt
- ✅ 未修改 Packages/manifest.json / Packages/packages-lock.json
- ✅ 未创建新 .cs / .asmdef / .unity / .prefab / .asset / .inputactions
- ✅ 未修改 .agents/skills/ 任意文件
- ✅ 未安装新 Package
- ✅ 未删除任何文件
- ✅ 未运行 Unity BatchMode
- ✅ 未 Push / 未合并 / 未创建 PR / 未修改远程
- ✅ 未修改 .gitignore
- ✅ 唯一写操作：编辑 Docs/OPENCLAW_REPOSITORY_AUDIT.md 追加 Section 8
```

### 8.9 Section 8 与既有 Section 的关系

| Section | 作者 | 主题 | 与本节关系 |
|---------|------|------|----------|
| Section 1 | architect | 环境与版本 | 同：本节 8.5 复用 ProjectVersion.txt 6000.5.3f1 |
| Section 2 | architect | 程序集现状 | 同：Task 02 启动后由 architect 主写 ui-tools 协同 |
| Section 3 | architect | 资产与依赖 | 同：本节 8.3 复用 Assets 目录结构、8.5 复用 ProjectSettings |
| Section 4 | qa | 文档自洽性 | 同：本节 8.2 类别与 Docs/02 / Docs/03 对齐 |
| Section 5 | qa | 工程就绪与编译基线 | 同：5.7 BatchMode 已跑、5.8 Library/ 已生成 |
| Section 6 | qa | Task 01 → Task 02 交接 | 补充：8.7 给出 ui-tools 在 Task 02 内 5 项待办 |
| Section 7 | gameplay + Lead | 战斗代码只读审计 | 互补：7.x 审 Core 战斗代码，8.x 审 Unity 业务资产 |

本节与 Section 7 互补：Section 7 验证 `D:\AI-Worktrees\Xingyuan\gameplay` 侧的 Starfall.Core / Starfall.Data 实现尚未起步（合理）；本节验证 `D:\AI-Worktrees\Xingyuan\ui-tools` 侧的 Starfall.Unity / Presenter / UI / Data 容器实现尚未起步（合理）；Task 02 启动后两侧由 architect 主导新建 asmdef 脚手架，gameplay 与 ui-tools 各自填充 Core / Unity 实现。

---

## Section 9 — Lead Phase D 整合与 Task 02 READINESS 判定

> **作者**：`xingyuan-lead`（2026-07-12 20:25 GMT+8）
> **上下文**：Task 01 五个 Agent 分阶段提交均已完成。`agent/01-repository-audit` 分支现含 5 个 audit commit（base 8a3fb1f 不计）。
> **职责**：对 Section 1-8 做最终文档自洽性交叉验证、形成 Task 02 READINESS 判定、给出 BLOCKING USER DECISION 项与下一轮建议。

### 9.1 最终文档自洽性矩阵（Lead 汇总）

矩阵来源：§4.1（qa 出的 15 行 M1-M15）+ 本节新增 M16-M19（Section 7 / 8 / 9 的交叉引用）。

| # | 来源 | 目标 | 主题 | 是否一致 | 证据 |
|---|---|---|---|---|---|
| M1 | AGENTS.md §1 | Docs/01 §1 | MVP 差异化玩法 | ✅ | §4.1 |
| M2 | AGENTS.md §11 | Docs/01 §5 | 网格排序 y→x | ✅ | §4.1 |
| M3 | AGENTS.md §10 | Docs/02 §3 | asmdef 命名 + 依赖 | ✅（当前 0/5 已记录） | §4.1 |
| M4 | AGENTS.md §10.2/3 | Docs/02 §3 | Data→Core / Unity→{Core,Data,UnityEngine} | ✅（无越界，模板未引用业务层） | §4.1 |
| M5 | AGENTS.md §11 | Docs/02 §4 | GridPos IComparable / y→x | ✅（待 Task 03 实现） | §4.1 |
| M6 | AGENTS.md §12 | Docs/01 §2 | MVP 排除项 | ✅ | §4.1 |
| M7 | AGENTS.md §5.2-5.6 | 实际 Worktree | 5 个 Agent 路径 | ✅ | §4.1 + 本节实测 |
| M8 | AGENTS.md §9 | 实际分支命名 | `agent/<role>-bootstrap` | ✅ | §4.1 |
| M9 | AGENTS.md §17 | Docs/05 §12 | 报告 + 证据 | ✅ | §4.1 |
| M10 | AGENTS.md §17 | Docs/05 §13 | 状态枚举 vs 缺陷等级 | ⚠️ 措辞差异 | §4.1 |
| M11 | Docs/01 §2 vs ProjectVersion.txt | Unity 版本 | ❌ **不一致（BLOCKING USER DECISION U-A）** | §4.1 + §5.1 + §6.1(b) |
| M12 | Docs/02 §3 vs *.asmdef | asmdef 数量 | ❌ 0/5（Planned Gap — Task 02 G-A） | §4.1 + §6.1(c) |
| M13 | Docs/05 §4-5 vs Assets/Tests | 测试程序集 | ❌ 不存在（与 M12 同源） | §4.1 |
| M14 | AGENTS.md §11 | Docs/02 §4 GridPos | 排序约束 | ✅ | §4.1 |
| M15 | AGENTS.md §18 | Docs/04 §3 | ADR / KNOWN_LIMITATIONS 维护路径 | ⚠️ 待 Task 02 内交叉 | §4.1 |
| **M16** | §7.5（gameplay） | §8.5（ui-tools） | 模板脚手架识别（Readme.cs / ReadmeEditor.cs） | ✅ | 两节均识别为 URP Blank 模板，未触发业务越界；删除路径在 §8.7 Risk-A 同步列出 |
| **M17** | §7.2（gameplay 8 类） | §8.2（ui-tools 8 类） | 业务代码 + 业务资产 = 0 | ✅ | 两节矩阵均全 0 匹配，Task 01 基线一致 |
| **M18** | §5.7（qa BatchMode） | §8.4（ui-tools Library 检查） | Library/ 与 .gitignore 互洽 | ✅ | qa 跑出 70 DLLs + 4,608B Assembly-CSharp.dll + 10,240B Editor.dll；ui-tools 验证 Library/ 被 .gitignore L1 覆盖；qa 跑批所在 worktree（D:\AI-Worktrees\Xingyuan\qa）的 Library/ 为 gitignored 残留，不会污染 main |
| **M19** | §6.1 进入条件（qa 7 项） | §7.8 + §8.7（gameplay + ui-tools READINESS） | Task 02 启动多视角一致性 | ✅ | qa 7 项 + gameplay 2 项预确认（ADR-0001 + 5 个 asmdef）+ ui-tools 5 项待办（G-F / G-G / I-A / I-B / I-C）互不冲突且合并后形成 9 项联合进入条件 |

**结论**：15+4 = 19 行矩阵全部一致或可接受；**唯一 BLOCKING USER DECISION = M11**（Unity 版本）。

### 9.2 Task 02 READINESS 判定

#### 9.2.1 评分维度（5 维度）

| 维度 | 证据来源 | 状态 |
|---|---|---|
| 1. 文档交付完整性 | §1-§8 全部落地 + 已知偏差 + 附录 | ✅ PASS |
| 2. 真实编译基线 | §5.7 Unity BatchMode（exit 0 / 0 error / 0 warning / 90s import） | ✅ PASS |
| 3. 零业务代码越界 | §7.2 + §8.2 16 类全 0 匹配 + §5.6 Core 守卫 grep 仅模板 3 行命中 | ✅ PASS |
| 4. 多 Agent READINESS 一致性 | §6.1（qa）+ §7.8（gameplay）+ §8.7（ui-tools） | ✅ PASS |
| 5. 已知偏差分类完整 | 重新分类 § + M11-M13 重分类对照表 | ✅ PASS（仅 M11 BLOCKING） |

#### 9.2.2 READINESS 结论

```
TASK 02 READINESS: **READY WITH CONDITIONS**
```

**判定依据**：

- ✅ Task 01 范围内所有交付物（环境审计 + 真实编译基线 + 多视角资产审计 + 已知偏差分类）均已完成。
- ✅ 5 个 Agent（architect / qa / gameplay / ui-tools / lead）均无遗留返工项。
- ✅ Worktree 链 5 个 commit 干净，`agent/01-repository-audit` 分支可被 Task 02 分支直接基于派生。
- ⚠️ **唯一 BLOCKING 条件**：Unity 版本不一致（U-A / M11）。Task 02 必须在用户对 Unity 版本裁决后才允许启动。

**进入 Task 02 需满足的 9 项联合条件**（整合自 qa §6.1 + gameplay §7.8 + ui-tools §8.7）：

| # | 条件 | 来源 | 类别 |
|---|---|---|---|
| (a) | Task 01 审计 doc 已落地 | qa §6.1(a) | ✅ 已满足 |
| **(b)** | **用户裁决 Unity 版本（6000.5.3f1 vs 文档 6.3 LTS）** | qa §6.1(b) + §5.1 + 本节 §9.1 M11 | **🔴 BLOCKING USER DECISION** |
| (c) | 5 个 `Starfall.*` asmdef 由 architect 创建 | qa §6.1(c) + gameplay §7.8 + §9.1 M12 | 🟡 Planned Gap — Task 02 G-A |
| (d) | ADR-0001 + ADR-0002 由 architect 起草 | qa §6.1(d) + gameplay §7.8 | 🟡 Planned Gap — Task 02 G-B |
| (e) | ProjectSettings 默认值清理（companyName / bundle ID / scene 列表）由用户裁决 | qa §6.1(e) + ui-tools §8.7 I-A/I-B | 🟢 信息项，可在 Task 02 启动前裁决 |
| (f) | 未使用 4 项 Packages（timeline / visualscripting / multiplayer.center / ide.rider）由用户裁决 | qa §6.1(f) + ui-tools §8.7 I-C | 🟢 信息项，可在 Task 02 启动前裁决 |
| (g) | 新分支 `agent/02-project-skeleton` 由 architect 从 `agent/01-repository-audit` 派生 | qa §6.1(g) | 🟡 Planned Gap — Task 02 启动动作 |
| (h) | ADR-0001 含 FNV-1a 64 位哈希字段顺序（防 Task 04 口径漂移） | gameplay §7.8 | 🟡 Planned Gap — Task 02 内合并 |
| (i) | Task 02 内 Core 依赖守卫测试建立（`Starfall.Tests.EditMode` + 禁止符号扫描） | gameplay §7.7 + qa §6.3 + §9.1 M13 | 🟡 Planned Gap — Task 02 G-C |

**READY WITH CONDITIONS** 含义：Task 02 可在用户裁决条件 (b) 后立即启动；其余 8 项均为 Task 02 内部交付物，不阻塞启动但需在 Task 02 Gate 中验证。

### 9.3 BLOCKING USER DECISION 项详细说明

**U-A：Unity 版本不一致**

| 维度 | 声明 | 实测 |
|---|---|---|
| `Docs/01 §2` 技术平台 | `Unity 6.3 LTS` | — |
| `Docs/02 §1` 引用 | `Unity 6.3 LTS` | — |
| `ProjectSettings/ProjectVersion.txt` | — | `6000.5.3f1`（`c2eb47b3a2a9`） |

**实测确认（不靠 Unity 包兼容性矩阵；靠 §5.7 真实 BatchMode）**：

- URP 17.5.0 + 6000.5.3f1 编译 **run-and-pass**（exit 0 / 0 error / 0 warning）
- Assembly-CSharp.dll 4,608 bytes + Editor.dll 10,240 bytes 干净产出
- 70 个 DLL 在 `Library/ScriptAssemblies/` 完整生成

**3 个裁决选项**（来自 §5.1）：

| 选项 | 动作 | 风险 | 推荐度 |
|---|---|---|---|
| **A** | 文档更新：将 `Docs/01 §2` + `Docs/02 §1` 中 `Unity 6.3 LTS` 更正为 `Unity 6.5 (6000.5.3f1)` | 低（不改 ProjectSettings） | ⭐⭐⭐ 推荐（影响最小） |
| **B** | 版本升降：切换 Unity Editor 到 `Unity 6.3 LTS`（6000.3.x） | 高（ProjectVersion 重生成 + 模板 reimport + 可能触发 URP / Test Framework 版本冲突） | ⛔ 不推荐 |
| **C** | 维持现状并记录偏差：保留 `6000.5.3f1`，偏差记入 `docs/KNOWN_LIMITATIONS.md` | 中（后续 ADR / 文档需统一指向 6.5） | △ 可接受 |

**Lead 建议**：选项 A（文档更新）——理由：

1. BatchMode 已实证 URP 17.5.0 + 6000.5.3f1 编译 run-and-pass，无需为追逐「6.3 LTS」字样而回退 Unity Editor；
2. `com.unity.test-framework 1.7.0` / `com.unity.inputsystem 1.19.0` / `com.unity.render-pipelines.universal 17.5.0` 均已在 6000.5.3f1 下验证编译；
3. 修改 2 处文档字符串（`Docs/01` + `Docs/02`）远比重新安装 Unity Editor + 触发 1000+ 资产 reimport 经济；
4. 后续 ADR / 新文档可直接统一写 `Unity 6.5 (6000.5.3f1)`。

### 9.4 Task 01 总结

**已完成（5 个 audit commit）**：

```
db0d666 docs(audit): unity asset audit by xingyuan-ui-tools         290 lines
61b9e19 docs(audit): battle code audit (recovered from gameplay)    237 lines  (Lead 接管)
f0394fa docs(audit): batchmode baseline + reclassification by qa    310 lines  (含真实 BatchMode)
a6a8629 docs(audit): sections 4-6 by xingyuan-qa                   310 lines
b23285e docs(audit): sections 1-3 by xingyuan-architect            624 lines
                     Docs/OPENCLAW_REPOSITORY_AUDIT.md 共 1657 行新增（1 file changed）
```

**未完成 / 阻塞**：0 项；Task 01 范围内全部完成。

**关键数据点**：

- Unity Editor：6000.5.3f1（已实测）
- URP：17.5.0（已实测编译 run-and-pass）
- Test Framework：1.7.0（已就位）
- asmdef：0 / 5（Planned Gap G-A — Task 02）
- Assets/Scripts/：不存在（Planned Gap — Task 02 由 architect 创建）
- 业务代码：0（16 类 grep 全 0 匹配）
- 业务资产：0（template-only）
- 模板脚手架：2 个 .cs（Readme + ReadmeEditor）+ 1 个 .asset（Readme）+ 1 个 .wlt + 1 个 .png（Planned Gap G-F — Task 02 删除）
- Unity BatchMode：exit 0 / 0 error / 0 warning / 70 DLLs / 90s 首次 import

### 9.5 下一轮建议（待用户裁决）

1. **首选行动**：用户裁决 U-A（建议选项 A — 文档更新 `Unity 6.3 LTS` → `Unity 6.5 (6000.5.3f1)`，仅改 `Docs/01 §2` + `Docs/02 §1` 两处字符串）；
2. 用户裁决 U-A 后，Task 02 即可启动：
   - Lead 起草 Task 02 Task Package（asmdef + ADR-0001/0002 + Core 守卫测试 + ProjectSettings 默认值裁决 + Packages 清理裁决）；
   - architect 在 `agent/02-project-skeleton` 分支主责 5 个 asmdef 创建 + ADR 起草；
   - ui-tools 协同处理 ProjectSettings + TutorialInfo 删除 + Packages 候选清理；
   - qa 建立 Core 依赖守卫测试 + 真实 BatchMode compile + EditMode 测试运行；
   - gameplay 待命 Task 03 启动；
3. 关键风险（再述）：
   - U-A 未裁决前不允许进入 Task 02；
   - 删除 `Assets/TutorialInfo/` 须同步删除 `Assets/Readme.asset`（§8.7 Risk-A）；
   - InputAction 资源改造须增量修改保留 binding（§8.7 Risk-B）；
   - TagManager 仅 Default/Water/UI 三层，业务 Tag（BattleUnit/PhaseAlpha/PhaseBeta/Gravity）在 Task 03+ 追加（§8.7 Risk-C）；
   - main worktree 已存在 `Library/`（来自 qa 在 qa worktree 跑 BatchMode 的产物），但 Library/ 被 .gitignore L1 覆盖，且与 main worktree 隔离不会被 git 跟踪。

### 9.6 Lead 签收

```
Agent:    xingyuan-lead
Workspace: D:\UntiyProject\XingyuanCovenant（main worktree）
Branch:   agent/01-repository-audit（HEAD = 即将生成的 Lead commit）
Time:     2026-07-12 20:25 GMT+8
Mode:     read + edit Docs/OPENCLAW_REPOSITORY_AUDIT.md（追加 Section 9）
Forbid:   不修改业务代码 / ProjectSettings / Packages / Unity 版本 / .agents/skills
Push:     不 Push / 不合并 / 不开始 Task 02
```

**Lead Phase D 自检**：

- ✅ 19 行文档自洽性矩阵（含 Section 7/8 交叉）
- ✅ Task 02 READINESS 评分 5 维度全 PASS（除 U-A BLOCKING）
- ✅ 9 项联合进入条件整合自 3 个 Agent
- ✅ U-A 3 选项 + Lead 建议 A
- ✅ Task 01 总结 + 关键数据点 + 下一轮建议

**下一动作（等待用户裁决）**：等待用户对 U-A 的 3 选 1 裁决（A / B / C），以及是否批准进入 Task 02。

### 9.7 U-A Resolution（用户裁决后补记）

**裁决时间**：2026-07-12 20:46 GMT+8
**裁决选项**：A（文档更新）
**裁决依据**：URP 17.5.0 + 6000.5.3f1 编译已实测 run-and-pass（§5.7），无需回退 Unity Editor；改 2 处文档字符串远比重装 Editor + 资产 reimport 经济。

**实际变更**（本节对应 commit 由 xingyuan-lead 在本分支落地）：

| 文件 | 行号 | 旧 | 新 |
|---|---|---|---|
| `Docs/01_Project_Overview_and_GDD.md` | L33 | `Unity 6.3 LTS` | `Unity 6.5 (6000.5.3f1)` |
| `Docs/02_Technical_Development_Manual.md` | L6 | `Engine: Unity 6.3 LTS` | `Engine: Unity 6.5 (6000.5.3f1)` |

**U-A 状态**：🔴 → ✅ **RESOLVED**

**未变更的审计历史证据**（保留为 audit-time 快照）：

- §1.1 / §5.1 / §6.1(b) / §9.1 M11 仍记录「文档声明 6.3 LTS vs 实测 6.5」的不一致事实
- 这是审计方法的正确保留——审计 doc 是「审计时点的事实快照」，不应被事后改写以匹配新现实
- 当前状态以 §9.7 + §9.8 为准

### 9.8 U-B Resolution（用户裁决后补记）

**裁决时间**：2026-07-12 20:49 GMT+8
**裁决选项**：同 U-A Option A（用户明示「同 A」）
**依据**：AGENTS.md L18 与 Docs/01+02 同一性质不一致事实，避免文档间口径漂移。

**实际变更**：

| 文件 | 行号 | 旧 | 新 |
|---|---|---|---|
| `AGENTS.md` | L18 | `在 Unity 6.3 LTS + URP 中完成...` | `在 Unity 6.5 (6000.5.3f1) + URP 中完成...` |

**U-B 状态**：🔴 → ✅ **RESOLVED**（用户 20:49 显式批准）

**安全红线触发记录**：本变更触及「修改团队规则文件」边界，已由用户在 20:49 显式批准；按 `AGENTS.md §13.13`「修改系统、OpenClaw、Gateway、计划任务或代理配置」要求「先获得确认」，本次确认已落地。

**9 项联合进入条件最终状态**：

| # | 条件 | 状态 |
|---|---|---|
| (a) | Task 01 审计 doc 已落地 | ✅ 已满足 |
| (b) | 用户裁决 U-A（Unity 版本） | ✅ **已满足（20:46 GMT+8, Option A）** |
| (c) | 5 个 `Starfall.*` asmdef 由 architect 创建 | 🟡 Planned Gap — Task 02 G-A |
| (d) | ADR-0001 + ADR-0002 由 architect 起草 | 🟡 Planned Gap — Task 02 G-B |
| (e) | ProjectSettings 默认值清理 | 🟢 信息项，可启动前裁决 |
| (f) | 未使用 4 项 Packages 清理裁决 | 🟢 信息项，可启动前裁决 |
| (g) | `agent/02-project-skeleton` 分支由 architect 派生 | 🟡 Planned Gap，Task 02 启动动作 |
| (h) | ADR-0001 含 FNV-1a 64 位哈希字段顺序 | 🟡 Planned Gap，Task 02 内合并 |
| (i) | Core 依赖守卫测试建立 | 🟡 Planned Gap — Task 02 G-C |

**Task 02 READINESS 更新**：`READY WITH CONDITIONS` → `READY WITH CONDITIONS`（**U-A + U-B 均已解除；剩余 8 项均为 Task 02 内部交付物，不阻塞启动**）。用户已于 20:49 GMT+8 批准进入 Task 02；按 §6.1 + §9.2.2 联合条件立即起草 Task Package（不直接派单，待 Task Package 批准后再 spawn）。

## 已知偏差与建议（QA Phase C-1 重写）

> **重分类口径**（用户裁决 2026-07-12 18:51 GMT+8，已最终生效）：
> - **BLOCKING USER DECISION** — 仅保留 Unity 版本不一致（6000.5.3f1 vs 文档 Unity 6.3 LTS）；属「Task 02 启动前必须裁决」
> - **Planned Gap（附 Task 号）** — asmdef 缺失（Task 02）/ SampleScene（Task 15）/ Template 残留（Task 02）
> - **信息项** — TagManager 空 / Company / Bundle ID / 未使用 Packages / URP 配套软声明 / m_EnterPlayModeOptions / BatchMode license 噪声
> - **static-only 重声明** — BatchMode 本次跑的是「资产导入 + ScriptAssemblies 编译」非真正的 Player 构建；该维度仍属 static-only

### 🔴 BLOCKING USER DECISION（唯一保留的阻塞项）

- **U-A. Unity 版本不一致**（原 Major M-A）
  - 实际：`6000.5.3f1`（Unity 6.5 系列 patch 3）— `ProjectSettings/ProjectVersion.txt` L1
  - 文档：`Docs/01 §2` 技术平台 / `Docs/02 §1` 声明 `Unity 6.3 LTS`
  - 阻塞 Task 02：进入条件 §6.1(b)
  - 待裁决：A 文档更新 / B 版本升降 / C 维持并记录到 `docs/KNOWN_LIMITATIONS.md`
  - **BatchMode 实测确认**：Log L2 `Built from '6000.5/staging' branch; Version is '6000.5.3f1 (c2eb47b3a2a9) revision 12774215'`，实际编译基于 6.5；URP 17.5.0 与之配套编译成功（§5.7 全 run-and-pass）。

### 🟡 Planned Gap（依 Task 号跟踪，非阻塞）

- **G-A. 5 个 `Starfall.*` asmdef 全部缺失**（原 Major M-B，§6.1(c)）
  - 实际：0（`Get-ChildItem -Filter *.asmdef` 返回 0 条）
  - 期望（`Docs/02 §3`）：`Starfall.Core` / `Starfall.Data` / `Starfall.Unity` / `Starfall.Tests.EditMode` / `Starfall.Tests.PlayMode`
  - **Track** = Task 02 主交付物，由 architect 创建
  - **BatchMode 实测确认**：当前 0 asmdef 下 Assembly-CSharp.dll 仍能编译（§5.7 run-and-pass）；Task 02 后 5 个 asmdef 接管编译输出

- **G-B. ADR-0001 / ADR-0002 尚未起草**（原 Major M-C，§6.1(d)）
  - 实际：`Docs/ADR/` 目录无 ADR 实体（Task 01 不起草 ADR）
  - **Track** = Task 02 内 architect 起草
  - **范围**：ADR-0001 含 `BattleState` / `GridPos` / `BoardState` / `UnitState` / `TileState` / FNV-1a 64 位哈希字段顺序 / `BattleStateCloner` / `BattleStateComparer`；ADR-0002 含 `Presenter` 同步契约

- **G-C. Core 依赖守卫测试尚未建立**（原 5.6 结论）
  - 实际：`Starfall.Core` 程序集未建立 → 无可执行守卫对象
  - **Track** = Task 02 内由 architect + gameplay 联合建立 `Starfall.Tests.EditMode` 并写守卫测试（参见 §6.3 第 2 条）
  - **当前 grep 命中**：仅模板脚手架（`Assets/TutorialInfo/Scripts/Readme.cs : ScriptableObject` + `ReadmeEditor.cs : using UnityEditor`），非业务代码，不计入守卫范围

- **G-D. 业务场景与 Tag 系统尚未建立**（原 Minor m-a + m-d）
  - 实际：`TagManager.tags = []`；`EditorBuildSettings.m_Scenes` 仅含 `SampleScene.unity`
  - **Track** = 战斗场景 → Task 15；项目业务 Tag（`Unit` / `Anchor` / `Decree` / `Objective`） → Task 03+

- **G-E. URP 17.5.0 补丁级配套软声明**（原信息性 i-d）
  - 状态：Phase C-1 BatchMode 实证：URP 17.5.0 + 6000.5.3f1 编译成功（§5.7 0 error）。但「严格补丁级配套」未由 Unity 包兼容性矩阵直接验证。
  - **Track** = Task 02 启动后用 `Package Manager UI` 检查兼容性报告；如失败，按 U-A 联动处理

- **G-F. 模板脚手架残留**（原信息性 i-b）
  - 实际：`Assets/TutorialInfo/Scripts/{Readme.cs, ReadmeEditor.cs}` + `Assets/Readme.asset`
  - **Track** = Task 02 内由 ui-tools 删除（属模板清理）
  - **BatchMode 实测确认**：残留脚本能干净编译进入 Assembly-CSharp.dll（4608 bytes），不影响基线

- **G-G. 模板默认 InputAction 资源未改造**（原信息性 i-c）
  - 实际：`Assets/InputSystem_Actions.inputactions` 仅含 Player 地图（Move/Look/Attack/Interact/Crouch/Jump/Previous/Next/Sprint）
  - 与 `Docs/02 §17` 输入模式枚举（None / Move / PhaseFlip / Attack / DeployDecree）未建立映射
  - **Track** = Task 05+ 由 ui-tools 改造

### 🟢 信息项（Non-blocking，记录偏差即可）

- **I-A. `companyName` 仍为 `DefaultCompany`**（原 Minor m-b，§6.1(e)）
  - 不影响编译；影响最终 bundle metadata
  - Task 02 启动**前不要求裁决**；可在 Task 02 内一次性处理

- **I-B. Android Bundle ID 仍为模板默认**（原 Minor m-c，§6.1(e)）
  - `com.UnityTechnologies.com.unity.template.urpblank`
  - 不影响编译；影响 Android 构建
  - 同 I-A 处理路径

- **I-C. 未使用 Packages**（4 项候选清理，原 i-a，§6.1(f)）
  - `com.unity.timeline` 1.8.12 / `com.unity.visualscripting` 1.9.11 / `com.unity.multiplayer.center` 1.0.1 / `com.unity.ai.navigation` 2.0.13
  - MVP 不使用；与 `AGENTS.md §12` 排除项一致
  - **Track** = Task 02 候选（用户裁决后由 ui-tools 一次性清理，或保留至 Task 09 整治）
  - **BatchMode 实测确认**：这 4 项 DLL 均出现在 `Library/ScriptAssemblies/`（Unity.AI.Navigation.dll, Unity.Timeline.dll, Unity.VisualScripting.*.dll, Unity.Multiplayer.Center.*.dll），确认未被引用且仍产出 DLL；后续清理路径由用户裁决

- **I-D. URP 17.5.0 vs 6000.5.3f1 补丁级配套软声明**（原 i-d）
  - 同 §G-E，BatchMode 已实证编译成功，此条保留为「Task 02 启动后 Package Manager UI 验证」待办

- **I-E. `m_EnterPlayModeOptions: 0` 禁用 Domain Reload**（原 Minor m-e）
  - PlayMode 测试可能受静态缓存影响（`AGENTS.md §11` 确定性）
  - **Track** = Task 02 内 architect 决策

- **I-F. Unity 6 BatchMode 无 license 噪声**（新增 §5.7 N-1）
  - Log 出现 29 行 `[Licensing::Module]` + 1 行 `Curl error 42: Callback aborted`
  - 不构成项目错误；为环境属性
  - **Track** = QA grep 日志时过滤前 100 行 / 最后 30 行；不需修改

- **I-G. Test Framework 隐式 performance 子包**（新增 §5.7 N-2）
  - `com.unity.test-framework.performance` 自动包含在 `com.unity.test-framework` 1.7.0 内
  - **Track** = 仅在显式新建 `Unity.PerformanceTesting` 引用 asmdef 后才参与编译；当前 0 影响

### 🟣 static-only 重声明（Phase C-1 边界明确化）

- **业务代码编译 = run-and-pass**（§5.7）：BatchMode 实证 Assembly-CSharp.dll 4608 bytes + Editor.dll 10240 bytes 编译产出，0 compile error。
- **Player 构建 = static-only**（维度未跑）：本次 `-batchmode -quit -buildTarget StandaloneWindows64` 仅切换活跃 TargetGroup + 触发 ScriptAssemblies 编译，**未调用** `BuildPipeline.BuildPlayer` 也**未带** `-executeMethod <BuildMethod>`，因此无 Player 二进制（`.exe` / `.apk`）产物。需 Task 02 内补做完整 Player 构建以满足「端到端构建验证」。
- **EditMode / PlayMode 测试 = not-run**（§5.8）：0 项目测试被发现（0 asmdef + 0 Assets/Tests）；且 BatchMode `-quit` 不带 `-runTests`，即便存在测试也不会被执行。
- **措辞限制**：本审计文中不使用「编译通过」「已就绪」「0 错误」「已验证」等措辞指代 Player 构建维度；以上措辞均限定为「业务 C# 编译维度」「资产导入维度」「Domain Reload 维度」run-and-pass。「Player 完整构建端到端 pass」属 Task 02 后补做。

### 📊 重分类对照表

| 原分类 (Phase C) | 原 ID | 新分类 (C-1) | 新 ID | 处置 |
|---|---|---|---|---|
| Major | M-A（Unity 版本） | BLOCKING USER DECISION | U-A | §6.1(b) |
| Major | M-B（asmdef） | Planned Gap — Task 02 | G-A | §6.1(c) |
| Major | M-C（ADR） | Planned Gap — Task 02 | G-B | §6.1(d) |
| Minor | m-a（TagManager 空） | Planned Gap — Task 03+ | G-D | 跟随业务 Tag 任务 |
| Minor | m-b（companyName） | 信息项 | I-A | §6.1(e) |
| Minor | m-c（Bundle ID） | 信息项 | I-B | §6.1(e) |
| Minor | m-d（SampleScene-only） | Planned Gap — Task 15 | G-D | 跟随战斗场景任务 |
| Minor | m-e（EnterPlayMode） | 信息项 | I-E | Task 02 内决策 |
| 信息性 | i-a（依赖清理） | 信息项 | I-C | §6.1(f) |
| 信息性 | i-b（TutorialInfo） | Planned Gap — Task 02 | G-F | 模板清理 |
| 信息性 | i-c（InputAction） | Planned Gap — Task 05+ | G-G | 输入改造 |
| 信息性 | i-d（URP 配套） | Planned Gap — Task 02 | G-E | Package Manager UI 验证 |

---

## 附录 A：本次仅执行了只读命令清单（Phase A+B）

### Phase A 自检

### Phase A 自检

- `Get-Location`（PWD 自检）
- `git status --short`
- `git rev-parse --show-toplevel`
- `git branch --show-current`
- `git rev-parse origin/main`
- `git checkout -b agent/01-repository-audit origin/main` ← **唯一允许的写操作（创建分支）**
- `git branch --show-current`（创建后验证）
- `git log -1 --pretty=format:"%h %s"`（创建后验证）
- `git status --short`（创建后验证）

### Phase B 取证（全部只读）

- `Test-Path BOOTSTRAP.md`
- `Test-Path D:\UntiyProject\XingyuanCovenant\BOOTSTRAP.md`
- `Test-Path Assets/Scripts`
- `Get-Content ProjectSettings/ProjectVersion.txt`
- `Get-Content Packages/manifest.json`
- `Get-ChildItem ProjectSettings -Recurse -Filter *.asset`
- `Get-ChildItem -Path . -Recurse -Filter *.asmdef -ErrorAction SilentlyContinue`
- `Get-ChildItem Assets -Recurse -Directory`
- `Get-ChildItem Assets -Recurse -File -Include *.cs,*.unity,*.asset,*.prefab,*.inputactions`
- `Get-ChildItem Assets/Settings -File`
- `Get-Content ProjectSettings/GraphicsSettings.asset`
- `Get-Content ProjectSettings/ProjectSettings.asset`
- `Get-Content ProjectSettings/EditorBuildSettings.asset`
- `Get-Content ProjectSettings/EditorSettings.asset`
- `Get-Content ProjectSettings/URPProjectSettings.asset`
- `Get-Content ProjectSettings/TagManager.asset`
- `Get-Content ProjectSettings/QualitySettings.asset`
- `Get-Content Assets/InputSystem_Actions.inputactions`
- `Get-Content Docs/02_Technical_Development_Manual.md`
- `Get-Content -Encoding UTF8 Docs/02_Technical_Development_Manual.md`
- `[System.IO.File]::ReadAllBytes("Docs/02_Technical_Development_Manual.md")[0..2]`（验证 UTF-8 无 BOM）
- `Get-Content Docs/01_Project_Overview_and_GDD.md`
- `Get-Content Docs/04_Roadmap_and_Milestones.md`
- `Get-Content Docs/05_Test_and_Acceptance.md`
- `git ls-files Docs/`
- `git ls-files Assets/TutorialInfo`
- `git worktree list`
- `git grep -nE "class\s+(UnitState|BattleState|MonoBehaviour|ScriptableObject)" Assets/Scripts`（路径不存在 → 预期失败）
- `git grep -nE "class\s+(UnitState|BattleState|MonoBehaviour|ScriptableObject)" Assets/TutorialInfo/Scripts`（exit 1 = 无匹配）
- `git grep -nE "class\s+(UnitState|BattleState)" .`（exit 1 = 全仓库无匹配）
- `Get-Content Assets/TutorialInfo/Scripts/Readme.cs`（确认模板脚手架）

### Phase B 写入

- `git add Docs/OPENCLAW_REPOSITORY_AUDIT.md`
- `git status`（核对仅含此文件）
- `git commit -m "docs(audit): sections 1-3 by xingyuan-architect"` ← **唯一允许的 commit**
- `git rev-parse HEAD`（记录 SHA）

### Phase B 末尾分支切换

- `git checkout agent/architecture-bootstrap` ← **唯一允许的分支切换**
- `git branch --show-current`（验证）
- `git status --short`（验证）
- `git log agent/01-repository-audit -1 --pretty=format:"%h %s"`（验证另一分支保留）

### 禁项自检（未执行任何）

- ❌ `git push` / `git push origin ...`
- ❌ 创建 Pull Request / 合并 / 发布
- ❌ 修改 `ProjectSettings/` 任何 `.asset`（仅读取）
- ❌ 修改 `Packages/manifest.json`
- ❌ 修改 Unity 版本
- ❌ 创建任何 `.cs` 业务代码
- ❌ 创建任何 `.asmdef`
- ❌ 修改 `.agents/skills/` 下任何 `SKILL.md`
- ❌ 安装任何依赖 / 包
- ❌ 删除任何文件
- ❌ 跨 worktree 写入（仅在 architect worktree 内操作）

### Phase C-1 取证 + 写入（xingyuan-qa，2026-07-12 18:53 GMT+8）

**取证件（全部只读 + 仅一次 Unity 进程调用）**：

- `Test-Path Assets/Tests`  → False
- `(Get-ChildItem -Recurse -Filter *.asmdef | Measure-Object).Count`  → 0
- `Get-ChildItem -Recurse -Filter *.dll | Where-Object { $_.FullName -like "*Tests*" }`  → 空
- `Get-ChildItem -Recurse -Directory | Where-Object { $_.Name -like "Starfall*" }`  → 0
- `Select-String -Path Packages/manifest.json -Pattern "com.unity.test-framework"`  → L10 = `1.7.0`
- `git grep -nE "(using\s+UnityEngine|using\s+UnityEditor)" Assets/`  → 3 行匹配（均为 TutorialInfo 模板脚手架）
- `git grep -nE "class\s+\w+\s*:\s*(MonoBehaviour|ScriptableObject)" Assets/`  → 1 行匹配（`Readme : ScriptableObject`）
- `Select-String -Path Logs\unity-batchmode.log -Pattern "CompileError|Compile error|Compilation failed"`  → 0
- `Select-String -Path Logs\unity-batchmode.log -Pattern " warning:"`  → 0
- `Select-String -Path Logs\unity-batchmode.log -Pattern "Test Discovery|DiscoverTests|TestPlan"`  → 0
- `Get-ChildItem -Path Library\ScriptAssemblies -Filter Assembly-CSharp*.dll`  → Assembly-CSharp.dll 4608B + Editor.dll 10240B
- `(Get-ChildItem -Path Library\ScriptAssemblies -Filter *.dll | Measure-Object).Count`  → 70
- `git check-ignore -v Library/ Logs/ UserSettings/ Temp/`  → 全部 covered by `.gitignore` L1/L6/L7/L2

**BatchMode 运行（本次唯一的实质进程调用）**：

- 命令：`Start-Process Unity.exe -ArgumentList "-batchmode -nographics -quit -projectPath D:\AI-Worktrees\Xingyuan\qa -logFile D:\AI-Worktrees\Xingyuan\qa\Logs\unity-batchmode.log -buildTarget StandaloneWindows64"`
- 退出码：`0`（success）
- 日志路径：`D:\AI-Worktrees\Xingyuan\qa\Logs\unity-batchmode.log`
- 日志大小：`2,043,081` 字节（`1.95 MB`），`10,214` 行
- 进程 PID：30212
- 总耗时：约 3 分钟（首次资产导入 + 编译）

**写入件**：

- `git fetch . agent/01-repository-audit:agent/01-repository-audit`
- `git checkout agent/01-repository-audit`
- `git status`（验证仅 audit doc 可写）
- `edit Docs/OPENCLAW_REPOSITORY_AUDIT.md`（重写元信息头 + §5.5–§5.8 + §6.1 + §6.4 + 「已知偏差与建议」）
- `git add Docs/OPENCLAW_REPOSITORY_AUDIT.md`
- `git commit -m "docs(audit): batchmode baseline + reclassification by xingyuan-qa"` ← **Phase C-1 唯一允许的 commit**
- `git rev-parse HEAD`（记录 SHA）
- `git checkout agent/qa-bootstrap`（唯一允许的二次分支切换）

**Phase C-1 禁项自检（未执行任何）**：

- ❌ `git push` / 创建 PR / 合并
- ❌ 修改 `ProjectSettings/` 任何 `.asset`
- ❌ 修改 `Packages/manifest.json`
- ❌ 修改 Unity 版本
- ❌ 创建任何 `.cs` 业务代码
- ❌ 创建任何 `.asmdef`
- ❌ 安装新 Package 或修改依赖
- ❌ 删除文件
- ❌ 跨 worktree 写入（仅在 qa worktree 内操作）
- ❌ 修改 Unity Editor 内部状态（仅 `Start-Process` 调用 + 进程退出后的 gitignored 副作用）
- ❌ 描述 static-only 为编译通过（已严格区分 run-and-pass / run-and-fail / static-only / not-run / partial-pass）

---

## 附录 B：审计元数据

- **审计工作量分布**：
  - Phase A + B：`D:\AI-Worktrees\Xingyuan\architect`（xingyuan-architect，2026-07-12 上午）
  - Phase C：`D:\AI-Worktrees\Xingyuan\qa`（xingyuan-qa，2026-07-12 下午）
- **审计时所在 worktree HEAD**（Phase A 起点）：`8a3fb1f feat(skills): add Xingyuan OpenClaw audit skills`
- **本审计分支累计提交**：
  - `b23285e docs(audit): sections 1-3 by xingyuan-architect`
  - `a6a8629 docs(audit): sections 4-6 by xingyuan-qa`（Phase C 初版）
  - `Phase C-1 commit`（待 C8 后回填 SHA；本批改加 Section 5.5–5.8 + §6.1/6.4 重分类 + 「已知偏差」整节重写 + 元信息头 + 附录 A/C-1）
- **唯一额外的 Unity 调用一次**：
  - `C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe -batchmode -nographics -quit -projectPath D:\AI-Worktrees\Xingyuan\qa -logFile D:\AI-Worktrees\Xingyuan\qa\Logs\unity-batchmode.log -buildTarget StandaloneWindows64`
  - 退出码 0；日志 2.04 MB；运行一次。
- **下一阶段建议**：
  1. 用户对 §5.1 / §6.1(b) Unity 版本偏差作最终裁决（**BLOCKING USER DECISION**）
  2. architect 接手 Task 02：创建 5 个 `Starfall.*` asmdef + 起草 ADR-0001 / ADR-0002
  3. gameplay / ui-tools / qa 联合在 `agent/02-project-skeleton` 分支协同
  4. Task 02 启动后用 Package Manager UI 验证 URP 17.5.0 × 6000.5.3f1 补丁级配套（§G-E）
  5. Task 09 修整期对未使用 4 项 Packages 作最终清理裁决（§I-C）