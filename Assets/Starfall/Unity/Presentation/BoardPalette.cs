using UnityEngine;
using Starfall.Core.Model;

namespace Starfall.Unity.Presentation
{
    /// <summary>
    /// 棋盘 / 单位 / 锚点 / HUD 的颜色映射表（AGENTS.md §11 确定性 — 纯函数）。
    /// 颜色固定值，禁止使用 UnityEngine.Random 或运行时依赖；
    /// 可在 EditMode 测试中直接断言。
    /// </summary>
    public static class BoardPalette
    {
        // === Tile colors ===
        public static readonly Color TileNormal = new Color(0.78f, 0.80f, 0.82f, 1f);   // 浅灰
        public static readonly Color TileBlocked = new Color(0.12f, 0.12f, 0.14f, 1f);  // 接近黑
        public static readonly Color TileHazard = new Color(0.85f, 0.30f, 0.20f, 1f);   // 红
        public static readonly Color TileObjective = new Color(0.30f, 0.85f, 0.40f, 1f); // 绿

        // === Active phase tint（叠加到 Normal 瓦片上的微调） ===
        public static readonly Color PhaseTintLight = new Color(1.00f, 1.00f, 1.00f, 1f);
        public static readonly Color PhaseTintDark = new Color(0.55f, 0.58f, 0.72f, 1f);

        // === Unit body colors ===
        public static readonly Color UnitPlayerLight = new Color(0.20f, 0.55f, 1.00f, 1f); // 亮蓝
        public static readonly Color UnitPlayerDark  = new Color(0.10f, 0.20f, 0.45f, 1f); // 深蓝
        public static readonly Color UnitEnemyLight  = new Color(1.00f, 0.55f, 0.20f, 1f); // 亮橙
        public static readonly Color UnitEnemyDark   = new Color(0.45f, 0.20f, 0.10f, 1f); // 深红棕

        // === Anchor polygon outline ===
        public static readonly Color AnchorPlayer = new Color(0.30f, 0.85f, 1.00f, 1f);
        public static readonly Color AnchorEnemy  = new Color(1.00f, 0.45f, 0.30f, 1f);
        public static readonly Color AnchorNeutral = new Color(0.85f, 0.85f, 0.30f, 1f);

        // === HUD ===
        public static readonly Color HudBackground = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color HudText = Color.white;
        public static readonly Color HudOutcomeOngoing = Color.white;
        public static readonly Color HudOutcomePlayerWins = new Color(0.4f, 1f, 0.4f, 1f);
        public static readonly Color HudOutcomeEnemyWins  = new Color(1f, 0.45f, 0.45f, 1f);
        public static readonly Color HudOutcomeDraw       = new Color(1f, 0.85f, 0.3f, 1f);

        // === Task 18 预览高亮（半透明叠加色，叠在 Tile 原色之上） ===
        // 合法移动落点：青蓝
        public static readonly Color HighlightLegalMove = new Color(0.20f, 0.85f, 0.95f, 0.45f);
        // 合法攻击目标：橘红（比 Enemy 单位色更亮以避免混淆）
        public static readonly Color HighlightAttackTarget = new Color(1.00f, 0.25f, 0.20f, 0.55f);
        // 坠落预览：警示紫
        public static readonly Color HighlightFallRisk = new Color(0.85f, 0.20f, 0.95f, 0.45f);
        // 伤害预览数字：暖白
        public static readonly Color DamagePreviewText = new Color(1.00f, 0.95f, 0.55f, 1f);
        // 状态条强调色
        public static readonly Color HudAccentPhase = new Color(0.55f, 0.85f, 1.00f, 1f);
        public static readonly Color HudAccentDamage = new Color(1.00f, 0.65f, 0.30f, 1f);

        /// <summary>根据 TileState 返回基础色（Phase 无关）。</summary>
        public static Color TileColor(TileState state)
        {
            switch (state)
            {
                case TileState.Blocked:   return TileBlocked;
                case TileState.Hazard:    return TileHazard;
                case TileState.Objective: return TileObjective;
                case TileState.Normal:
                default:                  return TileNormal;
            }
        }

        /// <summary>根据全局相位返回叠加 tint。</summary>
        public static Color PhaseTint(Phase phase)
        {
            return phase == Phase.Dark ? PhaseTintDark : PhaseTintLight;
        }

        /// <summary>根据 Phase + Owner 返回单位颜色。</summary>
        public static Color UnitColor(Phase phase, Owner owner)
        {
            bool dark = phase == Phase.Dark;
            if (owner == Owner.Player) return dark ? UnitPlayerDark : UnitPlayerLight;
            return dark ? UnitEnemyDark : UnitEnemyLight;
        }

        /// <summary>根据 anchor owner 字符串返回多边形描边色。</summary>
        public static Color AnchorColor(string owner)
        {
            if (owner == "Player") return AnchorPlayer;
            if (owner == "Enemy") return AnchorEnemy;
            return AnchorNeutral;
        }

        /// <summary>根据 BattleOutcome 返回 HUD 文本色。</summary>
        public static Color OutcomeColor(string outcome)
        {
            switch (outcome)
            {
                case "PlayerWins": return HudOutcomePlayerWins;
                case "EnemyWins":  return HudOutcomeEnemyWins;
                case "Draw":       return HudOutcomeDraw;
                case "Ongoing":
                default:           return HudOutcomeOngoing;
            }
        }
    }
}