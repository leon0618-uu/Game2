using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 地块定义转换命令（修改 <see cref="TileDefinition"/> 的 3 个字段）。
    /// <para/>
    /// **修改范围**：仅 <see cref="TileDefinition.PhasePairTileId"/> / <see cref="TileDefinition.Tags"/>
    /// / <see cref="TileDefinition.Height"/> 3 个 immutable 字段；
    /// 不允许修改 <c>Terrain</c> / <c>BaseMoveCost</c> / <c>BlocksVision</c> 等
    /// 通过 <see cref="TerrainDefinition"/> 派生的字段（那些字段由 <c>TerrainRegistry</c>
    /// 集中控制；本阶段不允许 transform）。
    ///
    /// <para/>
    /// **实现路径**：
    /// <list type="bullet">
    /// <item>取得注册表中对应 <paramref name="tileId"/> 的 <see cref="TileDefinition"/>。</item>
    /// <item>构造一个新 <see cref="TileDefinition"/>（仅 3 字段变化，其余字段复制）。</item>
    /// <item>调 <see cref="TileDefinitionRegistry.Update"/> 原地替换。</item>
    /// </list>
    ///
    /// <para/>
    /// **失败条件**（不修改状态）：
    /// <list type="bullet">
    /// <item>tileId 未注册 → <c>"tile not found"</c>。</item>
    /// <item>PhasePairTileId ∈ {tileId} 自身（自配）→ <c>"phase pair cannot be self"</c>。</item>
    /// <item>Height 由 <see cref="HeightLevel"/> 构造自动 clamp（无需显式拒绝）。</item>
    /// <item>phasePairTileId 指向不存在的 tile → <c>"phase pair tile not found"</c>。</item>
    /// </list>
    /// </summary>
    public sealed class TransformTileCommand : IMapCommand
    {
        public int TileId { get; }
        public int? NewPhasePairTileId { get; }
        public TileTags? NewTags { get; }
        public HeightLevel? NewHeight { get; }

        public TransformTileCommand(
            int tileId,
            int? newPhasePairTileId = null,
            TileTags? newTags = null,
            HeightLevel? newHeight = null)
        {
            if (tileId < 1)
                throw new ArgumentOutOfRangeException(nameof(tileId), tileId,
                    "TileId must be >= 1.");
            TileId = tileId;
            NewPhasePairTileId = newPhasePairTileId;
            NewTags = newTags;
            NewHeight = newHeight;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            var registry = PhaseFlipStateService.GetAttachedRegistry(mapState);
            if (registry == null)
                return MapCommandResult.Fail("no tile registry attached");
            if (!registry.TryGetById(TileId, out var def))
                return MapCommandResult.Fail("tile not found");

            // ── 1) 校验 newPhasePairTileId ──
            if (NewPhasePairTileId.HasValue)
            {
                int pairId = NewPhasePairTileId.Value;
                if (pairId < 1)
                    return MapCommandResult.Fail("phase pair tile must be >= 1");
                if (pairId == TileId)
                    return MapCommandResult.Fail("phase pair cannot be self");
                if (!registry.TryGetById(pairId, out _))
                    return MapCommandResult.Fail("phase pair tile not found");
            }

            // ── 2) Height 范围由 <see cref="HeightLevel"/> 构造函数自动 clamp ──
            // 这里不做范围拒绝，依赖底层构造约束。

            // ── 3) 构造新 TileDefinition（仅 3 字段变化，其余复制） ──
            int? newPair = NewPhasePairTileId.HasValue ? NewPhasePairTileId : def.PhasePairTileId;
            TileTags newTags = NewTags.HasValue ? NewTags.Value : def.Tags;
            HeightLevel newHeight = NewHeight.HasValue ? NewHeight.Value : def.Height;

            var newDef = new TileDefinition(
                tileId: def.TileId,
                coord: def.Coord,
                terrainType: def.TerrainType,
                terrain: def.Terrain,
                height: newHeight,
                baseMoveCost: def.BaseMoveCost,
                blocksMovement: def.BlocksMovement,
                blocksVision: def.BlocksVision,
                blocksProjectile: def.BlocksProjectile,
                coverLevel: def.CoverLevel,
                coverDirections: def.CoverDirections,
                phasePairTileId: newPair,
                tags: newTags);

            // 先记录旧 def 以便 Undo；registry.Update 替换。
            _previousDef = def;
            registry.Update(TileId, newDef);

            _executed = true;
            var events = new List<MapEvent>(1) { MapEvent.TileChanged(def.Coord, $"transform:{TileId}") };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("TransformTileCommand.Undo called without prior Execute.");
            var registry = PhaseFlipStateService.GetAttachedRegistry(mapState);
            if (registry != null && _previousDef.HasValue)
            {
                registry.Update(TileId, _previousDef.Value);
            }
            _executed = false;
            _previousDef = null;
        }

        public int Version => 1;
        public string CommandId => $"transform-tile:{TileId}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private TileDefinition? _previousDef;

        public override string ToString()
            => $"TransformTileCommand(TileId={TileId}, Pair={NewPhasePairTileId?.ToString() ?? "-"}, Tags={NewTags?.ToString() ?? "-"}, H={NewHeight?.ToString() ?? "-"})";
    }
}
