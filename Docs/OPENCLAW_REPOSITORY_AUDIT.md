# 《星渊誓约》OpenClaw 仓库与开发环境审计（Task 01）

> 文档语言：中文（用户批准决策 Q1）
> 文档语言：Markdown
> 本文档对应 Task 01 任务包 §2 中的 1.x 取证项；仅覆盖 Section 1–3（环境与版本 / 程序集现状 / 资产与依赖）。Section 4–6 由后续阶段补全。

## 元信息

- 任务包版本：v0（2026-07-12 用户批准）
- 审计日期：2026-07-12
- 负责 Agent（Phase A+B）：xingyuan-architect
- 工作区：`D:\AI-Worktrees\Xingyuan\architect`
- 分支：`agent/01-repository-audit`（自 `origin/main@8a3fb1fc7bbacf10858d992b112c5d2f1102a53b` 派生）
- 测试基线：**static-only**（未启动 Unity Editor 跑批，未调用编译 / 资源导入 / 测试运行）
- 写入范围：仅 `Docs/OPENCLAW_REPOSITORY_AUDIT.md`（Phase B 唯一允许的 commit）
- 关联提交：origin/main HEAD = `8a3fb1f feat(skills): add Xingyuan OpenClaw audit skills`

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

## 已知偏差与建议（暂列，待 Section 4–6 补全）

> 本节为 Phase A+B 范围内已发现但未决议的项；Phase C/D 由 QA / architect 后续阶段补全。

### 偏差清单

1. **Unity 版本不一致**（Major）：
   - 实际：`6000.5.3f1`（Unity 6.5 系列）
   - 文档：`Docs/01 §2` & `Docs/02 §1` 声明 `Unity 6.3 LTS`
   - 建议：在 Task 02 内由 architect + 用户裁决；可能动作 = 文档更新 / 版本升降（不在 Task 01 范围）。

2. **5 个 `Starfall.*` asmdef 全部缺失**（Major）：
   - 实际：0
   - 期望（`Docs/02 §3`）：`Starfall.Core` / `Starfall.Data` / `Starfall.Unity` / `Starfall.Tests.EditMode` / `Starfall.Tests.PlayMode`
   - 建议：建立 5 个 asmdef + 测试依赖图（任务范围 = Task 02+ 的架构工作）。

3. **战斗 Tag / Layer 尚未在 `TagManager.asset` 中声明**（Minor）：
   - 实际：`tags: []`
   - 建议：补 Unit / Anchor / Decree / Objective 等项目 Tag（属 ui-tools / gameplay 范围）。

4. **构建场景仅 1 条**（Minor）：
   - 实际：`Assets/Scenes/SampleScene.unity`（模板默认）
   - 建议：追加战斗主场景（属 ui-tools / gameplay 范围）。

5. **公司名 / Android Bundle ID 仍为模板默认**（Minor）：
   - 实际：`companyName=DefaultCompany` / `com.UnityTechnologies.com.unity.template.urpblank`
   - 建议：用户决策后由 ui-tools 修改（属 ProjectSettings 修改，须用户批准）。

6. **可能的依赖清理候选**（Minor / 信息性）：
   - `com.unity.timeline` / `com.unity.visualscripting` / `com.unity.multiplayer.center` / `com.unity.ai.navigation`
   - 当前不在 MVP 用途上；按 `AGENTS.md §12` 联机 / 复杂行为树排除；NavMesh 与 `Docs/02 §9` BFS Pathfinder 不一致
   - 建议：作为可清理项列入「Packages/ 整治」候选；不在 Task 01 范围修改 `manifest.json`。

7. **未启动 Unity Editor 编译 / 测试运行**（信息性，已声明）：
   - 本审计为 static-only；如需 run-and-pass 编译证据，须在 Task 02 内补。
   - 本文档中所有「可通过」「已就绪」均指静态检查可达，不含运行时验证。

### 交接建议（architect → Task 02 architect + QA）

- **Task 02 内的 architect 应起草**：
  - ADR-0001 `Starfall.Core` 数据模型与确定性哈希契约（覆盖 `BattleState` / `GridPos` / `BoardState` / `UnitState` / `TileState` / FNV-1a 64 位哈希字段顺序 / `BattleStateCloner` / `BattleStateComparer`）。
  - ADR-0002 `Presenter` 同步契约（`BoardPresenter` / `UnitPresenterRegistry` / `BattleHud` 不持有第二真值；`PresentationEvent` 失败不改 Core；Command 成功后才播放表现）。
  - 由 Task 02 内的 architect 起草；本任务（Task 01）仅记录建议，不起草实体文件。

- **QA 在 Section 4 应补充**：
  - 文档自洽性矩阵（`AGENTS.md` ↔ `Docs/01`–`Docs/05` ↔ 本审计）
  - §1.1 版本不一致条目纳入风险登记
  - §2.2 asmdef 缺失条目纳入 M1 任务清单

- **QA 在 Section 5 应补充**：
  - 工程就绪编译基线（明示 static-only → run-and-pass 的差距）
  - EditMode / PlayMode 测试当前不可执行的原因（无 asmdef）

- **QA 在 Section 6 应补充**：
  - Task 01 → Task 02 交接 Brief（含本节偏差清单）

---

## 附录 A：本次仅执行了只读命令清单（Phase A+B）

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

---

## 附录 B：审计元数据

- **审计工作区**：`D:\AI-Worktrees\Xingyuan\architect`
- **审计时所在 worktree HEAD**：`8a3fb1f feat(skills): add Xingyuan OpenClaw audit skills`
- **本次 commit**（待 B5 完成后回填）：见 Phase B 报告
- **下一阶段建议**：
  1. QA 接手 Section 4–6（任务包 §3）
  2. architect 在 Task 02 内起草 ADR-0001 / ADR-0002
  3. 用户对 §1.1 版本偏差做最终裁决
  4. 用户对 §3.2 依赖清理候选做最终裁决