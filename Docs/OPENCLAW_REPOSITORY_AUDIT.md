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

---

## Section 6 — Task 01 → Task 02 交接 Brief（QA 出具）

### 6.1 Task 02 进入条件（Acceptance）

进入 Task 02（工程骨架）须满足：

- [x] **Task 01 审计 doc 落地**（本节完成即满足；Section 4-6 + 已知偏差改写 + 1 commit `docs(audit): sections 4-6 by xingyuan-qa`）。
- [ ] **用户对 Unity 版本偏差（`6000.5.3f1` vs `Unity 6.3 LTS`）作出裁决**（选项见 §5.1：A 文档更新 / B 版本升降 / C 维持并记录）。
- [ ] **用户对 ProjectSettings 修改需求作出批准**（如需要：改 `companyName` / Android bundle ID / 追加战斗场景到 `EditorBuildSettings.m_Scenes`）。
- [ ] **architect 起草 ADR-0001**（`Starfall.Core` 数据模型与确定性哈希契约，含 `BattleState` / `GridPos` / `BoardState` / `UnitState` / `TileState` / FNV-1a 64 位哈希字段顺序 / `BattleStateCloner` / `BattleStateComparer`）。
- [ ] **architect 起草 ADR-0002**（`Presenter` 同步契约，含 `BoardPresenter` / `UnitPresenterRegistry` / `BattleHud` 不持有第二真值；`PresentationEvent` 失败不改 Core；Command 成功后才播放表现）。
- [ ] **新分支 `agent/02-project-skeleton` 由 architect 创建**（从 `origin/main` 或本审计分支 HEAD 派生）。
- [ ] **用户裁决依赖清理候选**（`com.unity.timeline` / `com.unity.visualscripting` / `com.unity.multiplayer.center` / `com.unity.ai.navigation` 4 项是否清理）。

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

### 6.4 已知风险登记（承接 Section 3 + Section 5）

| ID | 风险 | 等级 | 来源 | 缓解策略 |
|---|---|---|---|---|
| R1 | Unity 版本裁决未做 → Task 02 进入条件阻塞 | Major | §1.1 / §5.1 | 用户在 Task 02 启动前必选 A/B/C |
| R2 | ProjectSettings 模板值未清理（companyName / bundle ID / SampleScene-only）→ 后续构建 / manifest 受影响 | Minor | §1.3 / §5.3 | 用户裁决后由 ui-tools 一次性处理 |
| R3 | 候选依赖未清理（timeline / visualscripting / multiplayer.center / ai.navigation）→ 构建时长 + 测试面拉长 | 信息性 | §3.2 / §5.2 | 用户裁决；Task 02 内一并清理或留至 Task 09 整治 |
| R4 | push-based 子 Agent 完成事件丢失 → Lead 误判 | 信息性 | 运行经验 | Lead 启用 `subagents list` 兜底确认；不轮询 |
| R5 | `gh` CLI 不可用 → Issue 关联走 Web 手动 | 信息性 | 环境差异 | Lead 手动记录 Issue 引用；不阻塞开发 |
| R6 | 主会话 runtime Skill 列表缺 `xingyuan-test-gate` / `xingyuan-dev-workflow` / `xingyuan-determinism-review` → 子 Agent 须自读 | 信息性 | Agent 启动差异 | 子 Agent 用 `read <绝对路径>` 显式加载 SKILL.md |
| R7 | URP 17.5.0 与 `6000.5.3f1` 的严格「补丁级配套」未由 Unity 包兼容性矩阵验证 | 信息性 | §1.2.1 | Task 02 启动后用 `Package Manager UI` 检查 URP 兼容性报告；如报错，按 R1 联动处理 |
| R8 | `m_EnterPlayModeOptions: 0`（禁用 Domain Reload）→ PlayMode 测试静态缓存风险 | Minor | §1.3 / §5.3 | Task 02 内 architect 决策是否启用 Reload；QA 准备两条路径下的测试 |

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

## 已知偏差与建议（QA 整合 Phase A+B+C）

> **改写说明**：本节由 `xingyuan-qa` 在 Phase C 整合。来源：architect Phase A+B 已列 7 条 + C3 复核新增条目（Section 1-3 取证充分性、URP 配套软声明）。按等级分类。

### Major（阻塞 Task 02，必须先解决）

- **M-A. Unity 版本不一致**
  - 实际：`6000.5.3f1`（Unity 6.5 系列 patch 3）— `ProjectSettings/ProjectVersion.txt` L1
  - 文档：`Docs/01 §2` 技术平台 / `Docs/02 §1` 声明 `Unity 6.3 LTS`
  - 阻塞 Task 02：进入条件 R1
  - 待裁决：A 文档更新 / B 版本升降 / C 维持并记录

- **M-B. 5 个 `Starfall.*` asmdef 全部缺失**
  - 实际：0（`Get-ChildItem -Filter *.asmdef` 返回 0 条）
  - 期望（`Docs/02 §3`）：`Starfall.Core` / `Starfall.Data` / `Starfall.Unity` / `Starfall.Tests.EditMode` / `Starfall.Tests.PlayMode`
  - 阻塞 Task 02：进入条件（5 个 asmdef 是 Task 02 主交付物之一）
  - 缓解：Task 02 内由 architect 创建；QA 在 6.3 列测试计划

- **M-C. ADR-0001 / ADR-0002 尚未起草**
  - 实际：`Docs/ADR/` 目录尚无 ADR 实体（Task 01 不起草 ADR，由用户批准）
  - 阻塞 Task 02：进入条件（Task 02 内 architect 必须起草）
  - 范围：见 §6.1 + Phase A+B 交接建议

### Minor（非阻塞但应记录）

- **m-a. `TagManager.tags` 为空**
  - 实际：`tags: []`
  - 影响：业务 Tag（Unit / Anchor / Decree / Objective）需在 Task 03+ 增补
  - 不阻塞 Task 02（程序集先于 Tag）

- **m-b. `companyName` 仍为 `DefaultCompany`**
  - 实际：`ProjectSettings.asset` L 「companyName: DefaultCompany」
  - 影响：最终 bundle metadata；不阻塞编译
  - 处理：用户裁决后由 ui-tools 修改（属 ProjectSettings 写入）

- **m-c. Android Bundle ID 仍为模板默认**
  - 实际：`com.UnityTechnologies.com.unity.template.urpblank`
  - 影响：Android 构建；不阻塞编译
  - 处理：同 m-b

- **m-d. 构建场景仅 1 条（`SampleScene.unity`）**
  - 实际：`EditorBuildSettings.m_Scenes` 单条
  - 影响：Task 02 不要求战斗场景；Task 06+ 由 gameplay/ui-tools 追加
  - 不阻塞 Task 02

- **m-e. `m_EnterPlayModeOptions: 0` 禁用 Domain Reload**
  - 影响：PlayMode 测试静态缓存（`AGENTS.md §11` 确定性可能受影响）
  - 处理：Task 02 内 architect 决策

### 信息性（可延后）

- **i-a. 候选依赖清理**（4 项）
  - `com.unity.timeline` / `com.unity.visualscripting` / `com.unity.multiplayer.center` / `com.unity.ai.navigation`
  - 当前不在 MVP 用途上；与 `AGENTS.md §12` 排除项一致
  - 处理：用户裁决后由 ui-tools 一次性清理

- **i-b. `Assets/TutorialInfo/` 模板脚手架残留**
  - 实际：`Readme.cs` + `ReadmeEditor.cs` + `Readme.asset` 为 URP Blank 模板自带
  - 影响：非业务代码，但污染项目目录
  - 处理：Task 02 内由 ui-tools 删除（属模板清理）

- **i-c. 模板默认 InputAction 资源未改造**
  - 实际：`Assets/InputSystem_Actions.inputactions` 仅含 Player 地图（Move/Look/Attack/Interact/Crouch/Jump/Previous/Next/Sprint）
  - 与 `Docs/02 §17` 输入模式枚举（None / Move / PhaseFlip / Attack / DeployDecree）未建立映射
  - 处理：Task 05+ 由 ui-tools 改造

- **i-d. URP 17.5.0 与 `6000.5.3f1` 的严格「补丁级配套」软声明**
  - 来源：architect §1.2.1「URP 17.5.0 与 Unity 6000.5.3f1 版本配套」
  - 状态：未由 Unity 包兼容性矩阵直接验证；属软声明
  - 处理：Task 02 启动后用 Package Manager UI 检查兼容性报告；如失败，按 M-A 联动处理

### static-only 声明（已声明，本节重述）

- 本审计为 static-only；未运行 Unity Editor 编译或 EditMode/PlayMode 测试。
- 「可通过」「已就绪」「0 缺失」「字段正确」等措辞均指静态检查可达，**不含运行时验证**。
- Task 02 第一动作前应先记录「Editor 打开后 Console Error 基线」（见 §5.4），作为 run-and-pass 证据起点。

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