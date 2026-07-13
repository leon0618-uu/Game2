using System.Collections.Generic;
using Starfall.Data.Definition;

namespace Starfall.Data.Validation
{
    public static class DefinitionValidator
    {
        public static void Validate(BattleDefinition def, string filePath)
        {
            if (def == null)
                throw new DefinitionException("BattleDefinition is null", filePath, "$", null);

            if (def.TurnNumber < 0)
                throw new DefinitionException("TurnNumber must be >= 0", filePath, "$.TurnNumber", def.TurnNumber);

            if (def.Board == null)
                throw new DefinitionException("Board is required", filePath, "$.Board", null);
            if (def.Board.Width <= 0 || def.Board.Width > 255)
                throw new DefinitionException("Width must be 1..255", filePath, "$.Board.Width", def.Board.Width);
            if (def.Board.Height <= 0 || def.Board.Height > 255)
                throw new DefinitionException("Height must be 1..255", filePath, "$.Board.Height", def.Board.Height);

            var seen = new HashSet<(int, int)>();
            for (int i = 0; i < def.Board.Tiles.Count; i++)
            {
                var t = def.Board.Tiles[i];
                if (t.X < 0 || t.X >= def.Board.Width)
                    throw new DefinitionException("Tile.X out of bounds", filePath, $"$.Board.Tiles[{i}].X", t.X);
                if (t.Y < 0 || t.Y >= def.Board.Height)
                    throw new DefinitionException("Tile.Y out of bounds", filePath, $"$.Board.Tiles[{i}].Y", t.Y);
                if (!System.Enum.TryParse<Starfall.Core.Model.TileState>(t.State, true, out _))
                    throw new DefinitionException("Tile.State invalid", filePath, $"$.Board.Tiles[{i}].State", t.State);
                if (!seen.Add((t.X, t.Y)))
                    throw new DefinitionException("Duplicate tile coordinate", filePath, $"$.Board.Tiles[{i}]", $"({t.X},{t.Y})");
            }

            var seenIds = new HashSet<int>();
            for (int i = 0; i < def.Units.Count; i++)
            {
                var u = def.Units[i];
                if (!seenIds.Add(u.UnitId))
                    throw new DefinitionException("Duplicate UnitId", filePath, $"$.Units[{i}].UnitId", u.UnitId);
                if (u.X < 0 || u.X >= def.Board.Width)
                    throw new DefinitionException("Unit.X out of bounds", filePath, $"$.Units[{i}].X", u.X);
                if (u.Y < 0 || u.Y >= def.Board.Height)
                    throw new DefinitionException("Unit.Y out of bounds", filePath, $"$.Units[{i}].Y", u.Y);
                if (u.Hp <= 0)
                    throw new DefinitionException("Unit.Hp must be > 0", filePath, $"$.Units[{i}].Hp", u.Hp);
                if (!System.Enum.TryParse<Starfall.Core.Model.Phase>(u.Phase, true, out _))
                    throw new DefinitionException("Unit.Phase invalid", filePath, $"$.Units[{i}].Phase", u.Phase);
                if (!System.Enum.TryParse<Starfall.Core.Model.Owner>(u.Owner, true, out _))
                    throw new DefinitionException("Unit.Owner invalid", filePath, $"$.Units[{i}].Owner", u.Owner);
            }

            // Task 19 关卡闭环字段校验（向后兼容：缺省 = 合法默认）
            if (def.GuardsRequired.HasValue)
            {
                if (def.GuardsRequired.Value < 1 || def.GuardsRequired.Value > 100)
                    throw new DefinitionException("GuardsRequired must be 1..100", filePath, "$.GuardsRequired", def.GuardsRequired.Value);
            }

            // ExitTile 必须两个坐标同时设置或同时缺省
            if (def.ExitTileX.HasValue != def.ExitTileY.HasValue)
                throw new DefinitionException("ExitTileX and ExitTileY must be set together", filePath, "$.ExitTile", $"({def.ExitTileX},{def.ExitTileY})");
            if (def.ExitTileX.HasValue)
            {
                if (def.ExitTileX.Value < 0 || def.ExitTileX.Value >= def.Board.Width)
                    throw new DefinitionException("ExitTileX out of bounds", filePath, "$.ExitTile.X", def.ExitTileX.Value);
                if (def.ExitTileY.Value < 0 || def.ExitTileY.Value >= def.Board.Height)
                    throw new DefinitionException("ExitTileY out of bounds", filePath, "$.ExitTile.Y", def.ExitTileY.Value);
            }
        }
    }
}