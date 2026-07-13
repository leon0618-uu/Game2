using System.IO;
using System.Text.Json;
using Starfall.Data.Definition;

namespace Starfall.Data.Loading
{
    public static class JsonBattleLoader
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static BattleDefinition Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new DefinitionException("File not found", filePath, "$", null);
            string text = File.ReadAllText(filePath);
            BattleDefinition def;
            try
            {
                def = JsonSerializer.Deserialize<BattleDefinition>(text, Options);
            }
            catch (JsonException ex)
            {
                throw new DefinitionException("JSON parse error: " + ex.Message, filePath, "$", null, ex);
            }
            if (def == null)
                throw new DefinitionException("Deserialized to null", filePath, "$", null);
            return def;
        }
    }
}