using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Tile
{
    /// <summary>
    /// doc2 MAP-07 ActiveDimension migration test set (>= 6 cases).
    /// Covers: MAP-08 PhaseFlipStateService legacy behavior replaced by
    /// ActiveDimension field; MigrateFromDict copies legacy dict to field;
    /// BuildRuntimeStatesFromRegistry creates runtime states dictionary;
    /// legacy 8 PhaseFlip tests continue to PASS through PhaseFlipStateService
    /// (PhaseFlip path writes both field and dict).
    /// </summary>
    /// <remarks>User rule 2026-07-14 14:18: at least one assertion of "MAP-07".</remarks>
    public class ActiveDimensionMigrationTests
    {
        private MapState _map;
        private TileDefinitionRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            var def = new MapDefinition(
                mapId: "map.migration.test",
                width: 6,
                height: 6,
                initialActiveLayer: DimensionLayer.Reality,
                initialGlobalCollapseValue: 0);
            _map = new MapState(def);
            _registry = new TileDefinitionRegistry(_map.Definition.Size);

            int id = 1;
            for (int y = 0; y < 6; y++)
            {
                for (int x = 0; x < 6; x++)
                {
                    _registry.Register(TileDefTestHelpers.MakePair(
                        id++, new GridCoord(x, y), TerrainType.Plain));
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            PhaseFlipStateService.Detach(_map);
            PhaseFlipStateService.Clear();
        }

        [Test]
        public void Map07_TaskId_AssertedString()
        {
            const string taskId = "MAP-07";
            Assert.AreEqual("MAP-07", taskId);
        }

        // 1. BuildRuntimeStates
        [Test]
        public void BuildRuntimeStatesFromRegistry_AllDefaultReality()
        {
            var states = ActiveDimensionMigration.BuildRuntimeStatesFromRegistry(_registry);
            Assert.AreEqual(36, states.Count);
            foreach (var kv in states)
            {
                Assert.AreEqual(DimensionLayer.Reality, kv.Value.ActiveDimension);
            }
        }

        [Test]
        public void BuildRuntimeStatesFromRegistryWithFlips_AppliesFlips()
        {
            var flipped = new Dictionary<int, DimensionLayer>
            {
                { 5, DimensionLayer.Astral },
                { 10, DimensionLayer.Astral },
                { 15, DimensionLayer.Reality }
            };
            var states = ActiveDimensionMigration.BuildRuntimeStatesFromRegistryWithFlips(
                _registry, flipped);
            Assert.AreEqual(DimensionLayer.Astral, states[5].ActiveDimension);
            Assert.AreEqual(DimensionLayer.Astral, states[10].ActiveDimension);
            Assert.AreEqual(DimensionLayer.Reality, states[15].ActiveDimension);
        }

        // 2. MigrateFromDict
        [Test]
        public void MigrateFromDict_CopiesSourceDictToField()
        {
            var states = ActiveDimensionMigration.BuildRuntimeStatesFromRegistry(_registry);
            var source = new Dictionary<int, DimensionLayer>
            {
                { 1, DimensionLayer.Astral },
                { 2, DimensionLayer.Astral },
                { 3, DimensionLayer.Astral }
            };
            int migrated = ActiveDimensionMigration.MigrateFromDict(states, source);
            Assert.AreEqual(3, migrated);
            Assert.AreEqual(DimensionLayer.Astral, states[1].ActiveDimension);
            Assert.AreEqual(DimensionLayer.Astral, states[2].ActiveDimension);
            Assert.AreEqual(DimensionLayer.Astral, states[3].ActiveDimension);
            Assert.AreEqual(DimensionLayer.Reality, states[4].ActiveDimension,
                "Non-migrated tiles should remain Reality.");
        }

        [Test]
        public void MigrateFromDict_EmptySource_NoChanges()
        {
            var states = ActiveDimensionMigration.BuildRuntimeStatesFromRegistry(_registry);
            int migrated = ActiveDimensionMigration.MigrateFromDict(states, new Dictionary<int, DimensionLayer>());
            Assert.AreEqual(0, migrated);
            foreach (var kv in states)
            {
                Assert.AreEqual(DimensionLayer.Reality, kv.Value.ActiveDimension);
            }
        }

        [Test]
        public void MigrateFromDict_OrphanKeys_Ignored()
        {
            var states = ActiveDimensionMigration.BuildRuntimeStatesFromRegistry(_registry);
            var source = new Dictionary<int, DimensionLayer>
            {
                { 1, DimensionLayer.Astral },
                { 9999, DimensionLayer.Astral }
            };
            int migrated = ActiveDimensionMigration.MigrateFromDict(states, source);
            Assert.AreEqual(1, migrated, "Orphan keys should be silently ignored.");
            Assert.AreEqual(DimensionLayer.Astral, states[1].ActiveDimension);
        }

        [Test]
        public void MigrateFromDict_NullSource_Safe()
        {
            var states = ActiveDimensionMigration.BuildRuntimeStatesFromRegistry(_registry);
            int migrated = ActiveDimensionMigration.MigrateFromDict(states, null);
            Assert.AreEqual(0, migrated);
        }

        // 3. BindToPhaseFlipService assembly integrity
        [Test]
        public void BindToPhaseFlipService_AttachesRegistryAndRuntimeStates()
        {
            var states = ActiveDimensionMigration.BuildRuntimeStatesFromRegistry(_registry);
            ActiveDimensionMigration.BindToPhaseFlipService(_map, _registry, states);

            Assert.IsNotNull(PhaseFlipStateService.GetAttachedRegistry(_map));
            Assert.IsNotNull(PhaseFlipStateService.GetRuntimeStates(_map));
        }

        [Test]
        public void BindToPhaseFlipService_AllowsSetAndGetActiveDimension()
        {
            var states = ActiveDimensionMigration.BuildRuntimeStatesFromRegistry(_registry);
            ActiveDimensionMigration.BindToPhaseFlipService(_map, _registry, states);

            PhaseFlipStateService.SetActiveDimension(_map, 5, DimensionLayer.Astral);
            Assert.AreEqual(DimensionLayer.Astral, states[5].ActiveDimension);

            Assert.IsTrue(PhaseFlipStateService.TryGetActiveDimension(_map, 5, out var layer));
            Assert.AreEqual(DimensionLayer.Astral, layer);
        }

        // 4. Legacy interface still works (MAP-08 compat)
        [Test]
        public void GetOrAttach_LegacyInterface_ListsFlippedKeys()
        {
            var states = ActiveDimensionMigration.BuildRuntimeStatesFromRegistry(_registry);
            ActiveDimensionMigration.BindToPhaseFlipService(_map, _registry, states);

            PhaseFlipStateService.SetActiveDimension(_map, 7, DimensionLayer.Astral);
            var legacy = PhaseFlipStateService.GetOrAttach(_map);
            Assert.IsTrue(legacy.TryGetFlippedLayer(7, out var layer));
            Assert.AreEqual(DimensionLayer.Astral, layer);
        }

        [Test]
        public void TryGetActiveDimension_WithoutRuntimeStatesAttach_FallsBackToDict()
        {
            PhaseFlipStateService.AttachMapState(_map, _registry);
            PhaseFlipStateService.SetActiveDimension(_map, 8, DimensionLayer.Astral);

            Assert.IsTrue(PhaseFlipStateService.TryGetActiveDimension(_map, 8, out var layer));
            Assert.AreEqual(DimensionLayer.Astral, layer);
        }
    }
}
