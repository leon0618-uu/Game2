using NUnit.Framework;
using Starfall.Core.Model;
using Starfall.Data;
using Starfall.Data.Definition;
using Starfall.Data.Loading;
using Starfall.Data.Validation;

namespace Starfall.Tests.EditMode
{
    public class DataLoadingTests
    {
        private const string ValidJson = @"{""TurnNumber"":0,""ActivePlayer"":""Player"",
            ""Board"":{""Width"":4,""Height"":4,""Tiles"":[
                {""X"":0,""Y"":0,""State"":""Normal""},
                {""X"":1,""Y"":0,""State"":""Blocked""}
            ]},
            ""Units"":[
                {""UnitId"":1,""X"":0,""Y"":1,""Hp"":10,""Phase"":""Light"",""Owner"":""Player""},
                {""UnitId"":2,""X"":3,""Y"":3,""Hp"":8,""Phase"":""Dark"",""Owner"":""Enemy""}
            ]}";

        [Test]
        public void JsonBattleLoader_LoadValid()
        {
            var path = WriteTemp(ValidJson);
            var def = JsonBattleLoader.Load(path);
            Assert.AreEqual(0, def.TurnNumber);
            Assert.AreEqual("Player", def.ActivePlayer);
            Assert.AreEqual(4, def.Board.Width);
            Assert.AreEqual(2, def.Board.Tiles.Count);
            Assert.AreEqual(2, def.Units.Count);
        }

        [Test]
        public void Validator_AcceptsValid()
        {
            var path = WriteTemp(ValidJson);
            var def = JsonBattleLoader.Load(path);
            Assert.DoesNotThrow(() => DefinitionValidator.Validate(def, path));
        }

        [Test]
        public void Validator_RejectsNegativeTurn()
        {
            var def = new BattleDefinition { TurnNumber = -1 };
            var ex = Assert.Throws<DefinitionException>(() =>
                DefinitionValidator.Validate(def, "test.json"));
            Assert.IsTrue(ex.Message.Contains("TurnNumber"));
        }

        [Test]
        public void Validator_RejectsDuplicateUnitId()
        {
            var json = @"{""TurnNumber"":0,""ActivePlayer"":""Player"",
                ""Board"":{""Width"":4,""Height"":4,""Tiles"":[]},
                ""Units"":[
                    {""UnitId"":1,""X"":0,""Y"":0,""Hp"":1,""Phase"":""Light"",""Owner"":""Player""},
                    {""UnitId"":1,""X"":1,""Y"":1,""Hp"":1,""Phase"":""Light"",""Owner"":""Player""}
                ]}";
            var path = WriteTemp(json);
            var def = JsonBattleLoader.Load(path);
            var ex = Assert.Throws<DefinitionException>(() => DefinitionValidator.Validate(def, path));
            Assert.IsTrue(ex.Message.Contains("Duplicate") && ex.Message.Contains("UnitId"));
        }

        [Test]
        public void Validator_RejectsOutOfBounds()
        {
            var json = @"{""TurnNumber"":0,""ActivePlayer"":""Player"",
                ""Board"":{""Width"":4,""Height"":4,""Tiles"":[]},
                ""Units"":[{""UnitId"":1,""X"":10,""Y"":0,""Hp"":1,""Phase"":""Light"",""Owner"":""Player""}]}";
            var path = WriteTemp(json);
            var def = JsonBattleLoader.Load(path);
            var ex = Assert.Throws<DefinitionException>(() => DefinitionValidator.Validate(def, path));
            Assert.IsTrue(ex.Message.Contains("out of bounds"));
        }

        [Test]
        public void BattleStateBuilder_BuildsMatch()
        {
            var path = WriteTemp(ValidJson);
            var def = JsonBattleLoader.Load(path);
            DefinitionValidator.Validate(def, path);
            var state = BattleStateBuilder.Build(def);
            Assert.AreEqual(0, state.TurnNumber);
            Assert.AreEqual(Owner.Player, state.ActivePlayer);
            Assert.AreEqual(4, state.Board.Width);
            Assert.AreEqual(2, state.Units.Count);
            Assert.AreEqual(1, state.Units[0].UnitId);
            Assert.AreEqual(new GridPos(0, 1), state.Units[0].Pos);
            Assert.AreEqual(Phase.Light, state.Units[0].Phase);
            Assert.AreEqual(Owner.Enemy, state.Units[1].Owner);
        }

        [Test]
        public void BattleStateBuilder_HashDeterministic()
        {
            var path = WriteTemp(ValidJson);
            var def = JsonBattleLoader.Load(path);
            DefinitionValidator.Validate(def, path);
            var s1 = BattleStateBuilder.Build(def);
            var s2 = BattleStateBuilder.Build(def);
            Assert.AreEqual(s1.PostStateHash, s2.PostStateHash);
        }

        private static string WriteTemp(string json)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"battle_test_{System.Guid.NewGuid():N}.json");
            System.IO.File.WriteAllText(path, json);
            return path;
        }
    }
}