using NUnit.Framework;
using Starfall.Data;
using Starfall.Data.Definition;
using Starfall.Data.Loading;
using Starfall.Data.Validation;

namespace Starfall.Tests.EditMode
{
    public class BattleSetupTests
    {
        private const string BattleJsonRelPath = "Assets/StreamingAssets/data/battle_default.json";

        private const string Battle8x10Json = @"{""TurnNumber"":0,""ActivePlayer"":""Player"",
            ""Board"":{""Width"":8,""Height"":10,""Tiles"":[]},
            ""Units"":[
                {""UnitId"":1,""X"":1,""Y"":1,""Hp"":2,""Phase"":""Light"",""Owner"":""Player""},
                {""UnitId"":2,""X"":2,""Y"":1,""Hp"":2,""Phase"":""Light"",""Owner"":""Player""},
                {""UnitId"":3,""X"":1,""Y"":2,""Hp"":2,""Phase"":""Light"",""Owner"":""Player""},
                {""UnitId"":4,""X"":2,""Y"":2,""Hp"":2,""Phase"":""Light"",""Owner"":""Player""}
            ]}";

        [Test]
        public void BattleDefaultJson_PathString_ContainsExpected()
        {
            Assert.IsTrue(BattleJsonRelPath.Contains("battle_default.json"));
            Assert.IsTrue(BattleJsonRelPath.Contains("StreamingAssets"));
        }

        [Test]
        public void BuildStateFromJson_8x10_MatchSpec()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"battle_{System.Guid.NewGuid():N}.json");
            System.IO.File.WriteAllText(path, Battle8x10Json);
            var def = JsonBattleLoader.Load(path);
            DefinitionValidator.Validate(def, path);
            var state = BattleStateBuilder.Build(def);
            Assert.AreEqual(8, state.Board.Width);
            Assert.AreEqual(10, state.Board.Height);
            Assert.AreEqual(4, state.Units.Count);
            Assert.AreEqual(2, state.Units[0].Hp);
            System.IO.File.Delete(path);
        }

        [Test]
        public void Validator_Rejects8x10_OutOfBoundsUnits()
        {
            var json = @"{""TurnNumber"":0,""ActivePlayer"":""Player"",
                ""Board"":{""Width"":8,""Height"":10,""Tiles"":[]},
                ""Units"":[{""UnitId"":1,""X"":10,""Y"":0,""Hp"":1,""Phase"":""Light"",""Owner"":""Player""}]}";
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"battle_oob_{System.Guid.NewGuid():N}.json");
            System.IO.File.WriteAllText(path, json);
            var def = JsonBattleLoader.Load(path);
            var ex = Assert.Throws<DefinitionException>(() => DefinitionValidator.Validate(def, path));
            Assert.IsTrue(ex.Message.Contains("out of bounds"));
            System.IO.File.Delete(path);
        }

        [Test]
        public void Validator_8x10WithObjective_Accepted()
        {
            var json = @"{""TurnNumber"":0,""ActivePlayer"":""Player"",
                ""Board"":{""Width"":8,""Height"":10,""Tiles"":[
                    {""X"":3,""Y"":4,""State"":""Objective""}
                ]},
                ""Units"":[]}";
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"battle_obj_{System.Guid.NewGuid():N}.json");
            System.IO.File.WriteAllText(path, json);
            var def = JsonBattleLoader.Load(path);
            Assert.DoesNotThrow(() => DefinitionValidator.Validate(def, path));
            System.IO.File.Delete(path);
        }
    }
}