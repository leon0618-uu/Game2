using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Tile.PhasePair
{
    /// <summary>
    /// doc2 MAP-07 双层配置校验结果。
    ///
    /// <para/>
    /// **字段**：
    /// <list type="bullet">
    /// <item><see cref="Valid"/>：true = 通过；false = 失败（<see cref="ErrorCode"/> + <see cref="BrokenTileIds"/>）。</item>
    /// <item><see cref="ErrorCode"/>：失败原因码（见下表）；成功时为 null。</item>
    /// <item><see cref="BrokenTileIds"/>：涉及错误的 tileId 列表，按 tileId 升序，副本不可变。</item>
    /// </list>
    /// <para/>
    /// **错误码**（doc2 §21.4）：
    /// <list type="table">
    /// <listheader><term>ErrorCode</term><description>含义</description></listheader>
    /// <item><term>PAIR_ASYMMETRIC</term><description>tileA 指向 tileB 但 tileB.PhasePairTileId != tileA（或 null）。</description></item>
    /// <item><term>PAIR_ORPHAN</term><description>PhasePairTileId 指向不存在的 tileId。</description></item>
    /// <item><term>FLIP_DESYNC</term><description>互链的 tile 其中一个 ActiveDimension ≠ pair 的 ActiveDimension。</description></item>
    /// </list>
    /// </summary>
    public readonly struct ValidationResult
    {
        public readonly bool Valid;
        public readonly string ErrorCode;
        public readonly IReadOnlyList<int> BrokenTileIds;

        public ValidationResult(bool valid, string errorCode, IReadOnlyList<int> brokenTileIds)
        {
            Valid = valid;
            ErrorCode = errorCode;
            BrokenTileIds = brokenTileIds ?? Array.Empty<int>();
        }

        /// <summary>构造成功结果。</summary>
        public static ValidationResult Ok()
            => new ValidationResult(true, null, Array.Empty<int>());

        /// <summary>构造失败结果（brokenTileIds 内部复制为升序只读列表）。</summary>
        public static ValidationResult Fail(string code, IEnumerable<int> brokenTileIds)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            var list = new List<int>();
            if (brokenTileIds != null)
            {
                foreach (var id in brokenTileIds)
                {
                    if (id >= 1 && !list.Contains(id)) list.Add(id);
                }
                list.Sort();
            }
            return new ValidationResult(false, code, list);
        }

        public override string ToString()
        {
            if (Valid) return "ValidationResult(OK)";
            return $"ValidationResult(Fail, code={ErrorCode}, broken=[{string.Join(",", BrokenTileIds)}])";
        }
    }

    /// <summary>
    /// doc2 MAP-07 双层配对 / flip 同步校验器。
    ///
    /// <para/>
    /// **三类校验**（可分别调用）：
    /// <list type="number">
    /// <item>**PAIR_ASYMMETRIC**：tileA 指向 tileB，但 tileB.PhasePairTileId != tileA。
    ///       只检验 registry 静态层，无需 runtime states。</item>
    /// <item>**PAIR_ORPHAN**：PhasePairTileId 指向不存在的 tileId。
    ///       只检验 registry 静态层，无需 runtime states。</item>
    /// <item>**FLIP_DESYNC**：配对 tile 互链，但当前 ActiveDimension 不一致。
    ///       需传入 <c>runtimeStates</c>（tileId → MapTileState）。
    ///       null/缺 entries 视作"默认 Reality"= 跳过该对（视为 OK）。</item>
    /// </list>
    /// <para/>
    /// **Valid 调用顺序**：<see cref="Validate"/> 按 ORPHAN → ASYMMETRIC → DESYNC 顺序
    /// 报告错误（首个失败即返回；多错误同时存在只报告首个）。
    /// <para/>
    /// **无 UnityEngine 引用**：纯 C#，符合 AGENTS.md §10.1。
    /// </summary>
    public static class CrossLayerValidator
    {
        /// <summary>
        /// 一站式校验：ORPHAN → ASYMMETRIC → DESYNC 顺序报告。
        /// <paramref name="runtimeStates"/> 可为 null（仅静态层校验）。
        /// </summary>
        public static ValidationResult Validate(
            MapState map,
            TileDefinitionRegistry registry,
            IReadOnlyDictionary<int, MapTileState> runtimeStates)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            // 1) ORPHAN
            var orphan = CheckOrphans(registry);
            if (!orphan.Valid) return orphan;

            // 2) ASYMMETRIC
            var asym = CheckAsymmetric(registry);
            if (!asym.Valid) return asym;

            // 3) FLIP_DESYNC
            if (runtimeStates != null)
            {
                var desync = CheckFlipSync(registry, runtimeStates);
                if (!desync.Valid) return desync;
            }

            return ValidationResult.Ok();
        }

        /// <summary>
        /// 仅检查 ORPHAN（PhasePairTileId 指向不存在 tileId）。
        /// </summary>
        public static ValidationResult CheckOrphans(TileDefinitionRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var allIds = new HashSet<int>();
            foreach (var def in registry.All())
            {
                if (def.TileId >= 1) allIds.Add(def.TileId);
            }
            var broken = new List<int>();
            foreach (var def in registry.All())
            {
                if (!def.PhasePairTileId.HasValue) continue;
                int pairId = def.PhasePairTileId.Value;
                if (pairId < 1) continue;
                if (pairId == def.TileId) continue;
                if (!allIds.Contains(pairId))
                    broken.Add(def.TileId);
            }
            return broken.Count == 0
                ? ValidationResult.Ok()
                : ValidationResult.Fail("PAIR_ORPHAN", broken);
        }

        /// <summary>
        /// 仅检查 ASYMMETRIC：tileA 指向 tileB，但 tileB 不指向 tileA。
        /// </summary>
        public static ValidationResult CheckAsymmetric(TileDefinitionRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var broken = new List<int>();
            var seen = new HashSet<long>(); // tileA*MAX+pairB 用于去重
            foreach (var def in registry.All())
            {
                if (!def.PhasePairTileId.HasValue) continue;
                int pairId = def.PhasePairTileId.Value;
                if (pairId < 1) continue;
                if (pairId == def.TileId) continue;

                long key = ((long)def.TileId * 1000003L) ^ (long)pairId;
                if (!seen.Add(key)) continue;

                if (!registry.TryGetById(pairId, out var pairDef))
                {
                    // 应被 ORPHAN 报告；这里不报，避免重复
                    continue;
                }
                if (!pairDef.PhasePairTileId.HasValue
                    || pairDef.PhasePairTileId.Value != def.TileId)
                {
                    broken.Add(def.TileId);
                    broken.Add(pairId);
                }
            }
            return broken.Count == 0
                ? ValidationResult.Ok()
                : ValidationResult.Fail("PAIR_ASYMMETRIC", broken);
        }

        /// <summary>
        /// 仅检查 FLIP_DESYNC：互链 tile 的 ActiveDimension 不一致。
        /// <para/>
        /// **规则**：仅看 <c>runtimeStates</c> 中已记录的 tile（未出现的视为默认 Reality）—
        /// 若 A 已显式设置（如 Astral）但 B 未显式设置（默认 Reality），视为 desync。
        /// </summary>
        public static ValidationResult CheckFlipSync(
            TileDefinitionRegistry registry,
            IReadOnlyDictionary<int, MapTileState> runtimeStates)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (runtimeStates == null) throw new ArgumentNullException(nameof(runtimeStates));
            var broken = new List<int>();
            var seen = new HashSet<long>();
            foreach (var def in registry.All())
            {
                if (!def.PhasePairTileId.HasValue) continue;
                int pairId = def.PhasePairTileId.Value;
                if (pairId < 1) continue;
                if (pairId == def.TileId) continue;

                long key = ((long)def.TileId * 1000003L) ^ (long)pairId;
                if (!seen.Add(key)) continue;

                if (!registry.TryGetById(pairId, out var pairDef)) continue;
                if (!pairDef.PhasePairTileId.HasValue
                    || pairDef.PhasePairTileId.Value != def.TileId) continue;

                if (!runtimeStates.TryGetValue(def.TileId, out var stateA)) continue;
                if (!runtimeStates.TryGetValue(pairId, out var stateB)) continue;

                if (stateA.ActiveDimension != stateB.ActiveDimension)
                {
                    broken.Add(def.TileId);
                    broken.Add(pairId);
                }
            }
            return broken.Count == 0
                ? ValidationResult.Ok()
                : ValidationResult.Fail("FLIP_DESYNC", broken);
        }

        /// <summary>便捷：按 <see cref="GridCoord"/> 取 mapState。</summary>
        public static GridCoord? GetCoordOrNull(TileDefinitionRegistry registry, int tileId)
        {
            if (registry == null) return null;
            if (registry.TryGetById(tileId, out var def)) return def.Coord;
            return null;
        }
    }
}
