using System;

namespace Starfall.Core.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.2 地物标签位掩码（[Flags] int，共 22 个标签）。
    ///
    /// <para/>
    /// **角色**：在 <see cref="TerrainDefinition"/> 之外，给每个 <see cref="TileDefinition"/>
    /// 实例附加额外的语义标签（可叠加、可移除）。例如：
    /// 一个 <c>TerrainType.Plain</c> 的 tile 可以同时拥有
    /// <see cref="Spawnable"/> + <see cref="Deployable"/> + <see cref="Extraction"/>
    /// 标签；另一个同地形的 tile 可以完全无标签。
    ///
    /// <para/>
    /// **位分配**（22 个标签占 bit 0..21）：
    /// <list type="table">
    /// <listheader><term>位</term><description>标签</description></listheader>
    /// <item><term>0</term><description><see cref="Walkable"/></description></item>
    /// <item><term>1</term><description><see cref="Impassable"/></description></item>
    /// <item><term>2</term><description><see cref="PhaseFlippable"/></description></item>
    /// <item><term>3</term><description><see cref="PhaseLocked"/></description></item>
    /// <item><term>4</term><description><see cref="Destructible"/></description></item>
    /// <item><term>5</term><description><see cref="Collapsible"/></description></item>
    /// <item><term>6</term><description><see cref="Hazardous"/></description></item>
    /// <item><term>7</term><description><see cref="Bridge"/></description></item>
    /// <item><term>8</term><description><see cref="Void"/></description></item>
    /// <item><term>9</term><description><see cref="Wall"/></description></item>
    /// <item><term>10</term><description><see cref="AnchorNode"/></description></item>
    /// <item><term>11</term><description><see cref="Spawnable"/></description></item>
    /// <item><term>12</term><description><see cref="Deployable"/></description></item>
    /// <item><term>13</term><description><see cref="Interactable"/></description></item>
    /// <item><term>14</term><description><see cref="Extraction"/></description></item>
    /// <item><term>15</term><description><see cref="GuardObjective"/></description></item>
    /// <item><term>16</term><description><see cref="GuardSpawn"/></description></item>
    /// <item><term>17</term><description><see cref="Exit"/></description></item>
    /// <item><term>18</term><description><see cref="Patrol"/></description></item>
    /// <item><term>19</term><description><see cref="VisionBlocker"/></description></item>
    /// <item><term>20</term><description><see cref="ProjectileBlocker"/></description></item>
    /// <item><term>21</term><description><see cref="AudioSource"/></description></item>
    /// </list>
    ///
    /// <para/>
    /// **位分配约束**（AGENTS.md §11）：bit 0..21 一一对应上述 22 个标签。
    /// 任何序列化 / 哈希 / 网络协议都依赖此位序，**禁止重排或跳号**。
    /// </summary>
    [Flags]
    public enum TileTags : int
    {
        /// <summary>无标签（默认值）。</summary>
        None = 0,

        /// <summary>可站立 / 可通过；通常与 <see cref="TerrainDefinition.BlocksMovement"/> = false 一致。</summary>
        Walkable = 1 << 0,

        /// <summary>不可通过；通常与 <see cref="TerrainDefinition.BlocksMovement"/> = true 一致。</summary>
        Impassable = 1 << 1,

        /// <summary>可被相位翻转（仅在 <see cref="TerrainType.GateTile"/> 上有意义）。</summary>
        PhaseFlippable = 1 << 2,

        /// <summary>相位被锁定（任何相位翻转命令被拒绝）。</summary>
        PhaseLocked = 1 << 3,

        /// <summary>可被攻击 / 律令破坏（如木墙、可破坏障碍物）。</summary>
        Destructible = 1 << 4,

        /// <summary>可坍塌（如断桥坍塌后变为虚空）。</summary>
        Collapsible = 1 << 5,

        /// <summary>每回合造成伤害（与 <see cref="TerrainDefinition.HazardousDamagePerTurn"/> 配对）。</summary>
        Hazardous = 1 << 6,

        /// <summary>桥类地形（断桥 / 光桥 / 任何可通行的桥）。</summary>
        Bridge = 1 << 7,

        /// <summary>虚空标记（不可站立 / 不阻挡视线）。</summary>
        Void = 1 << 8,

        /// <summary>整墙标记（与 <see cref="TerrainType.Wall"/> 对应）。</summary>
        Wall = 1 << 9,

        /// <summary>锚点 node（玩家初始 / 撤离 / 防守目标）。</summary>
        AnchorNode = 1 << 10,

        /// <summary>可作为部署点（玩家单位初始位置候选）。</summary>
        Spawnable = 1 << 11,

        /// <summary>可部署（可主动向此 tile 派单位）。</summary>
        Deployable = 1 << 12,

        /// <summary>可交互（地图机关 / 终端 / 开关）。</summary>
        Interactable = 1 << 13,

        /// <summary>撤离点（防守目标的胜利条件之一）。</summary>
        Extraction = 1 << 14,

        /// <summary>防守目标（撤离阶段之前的防守胜利条件）。</summary>
        GuardObjective = 1 << 15,

        /// <summary>敌方 spawn 点（敌方单位刷新位置）。</summary>
        GuardSpawn = 1 << 16,

        /// <summary>地图出口（玩家撤离后的"逃出地图"出口）。</summary>
        Exit = 1 << 17,

        /// <summary>巡逻路径（敌方 AI 巡逻标记）。</summary>
        Patrol = 1 << 18,

        /// <summary>视线阻挡标记（与 <see cref="TerrainDefinition.BlocksVision"/> 等价）。</summary>
        VisionBlocker = 1 << 19,

        /// <summary>弹道阻挡标记（与 <see cref="TerrainDefinition.BlocksProjectile"/> 等价）。</summary>
        ProjectileBlocker = 1 << 20,

        /// <summary>声音源（用于音效 / 音频律令的触发位置）。</summary>
        AudioSource = 1 << 21,
    }
}