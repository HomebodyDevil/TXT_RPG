using System;
using System.Collections.Generic;
using NUnit.Framework;
using TxTRPG.Gameplay.Items;
using TxTRPG.Gameplay.Players;

namespace TxTRPG.Gameplay.Tests
{
    public sealed class InventoryAndQuickItemPersistenceTests
    {
        [Test]
        public void InventoryAndLoadout_RejectCorruptedNegativeAndDuplicateData()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryState(new[] { new KeyValuePair<string, int>("potion", -1) }));
            Assert.Throws<ArgumentException>(() => new InventoryState(new[] { new KeyValuePair<string, int>("potion", 1), new KeyValuePair<string, int>("potion", 2) }));
            Assert.Throws<ArgumentException>(() => new QuickItemLoadout(5, new[] { new KeyValuePair<int, string>(0, "a"), new KeyValuePair<int, string>(0, "b") }));
        }

        [Test]
        public void VersionOneMigration_PreservesCharactersAndAddsEmptyItemState()
        {
            var migrated = PlayerSaveMigrator.Migrate(new PlayerSaveData
            {
                version = 1,
                activeCharacterInstanceId = "hero-1",
                characters = new List<TxTRPG.Gameplay.Characters.CharacterSaveData>()
            });
            Assert.That(migrated.version, Is.EqualTo(PlayerSaveData.CurrentVersion));
            Assert.That(migrated.inventory, Is.Empty);
            Assert.That(migrated.quickItems, Is.Empty);
        }
    }
}
