using System.Collections.Generic;
using NUnit.Framework;
using TxTRPG.Application.Items;
using TxTRPG.Content.Items;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TxTRPG.UI.Windows;

namespace TxTRPG.Application.Tests
{
    public sealed class InventoryProjectionTests
    {
        private readonly List<Object> created = new();
        [TearDown] public void TearDown() { foreach (var value in created) Object.DestroyImmediate(value); created.Clear(); }

        [TestCase(0, 1, 0)]
        [TestCase(11, 1, 11)]
        [TestCase(12, 1, 12)]
        [TestCase(13, 2, 1)]
        [TestCase(37, 4, 1)]
        public void Paged_ClampsAndSlicesStableResults(int count, int expectedPages, int expectedLastCount)
        {
            var definitions = CreateDefinitions(count);
            var result = InventoryProjectionBuilder.Build(definitions, _ => 1, "all", InventoryDisplayMode.Paged, 999, 12);
            Assert.That(result.PageCount, Is.EqualTo(expectedPages));
            Assert.That(result.Items.Count, Is.EqualTo(expectedLastCount));
        }

        [Test]
        public void CategoryFilter_DoesNotMutateSourceAndUsesMiscFallback()
        {
            var definitions = CreateDefinitions(3);
            definitions[0].SetCategoryForEditor("consumable");
            var quantities = new Dictionary<string, int> { [definitions[0].DefinitionId] = 2, [definitions[1].DefinitionId] = 1 };
            var result = InventoryProjectionBuilder.Build(definitions, id => quantities.TryGetValue(id, out var q) ? q : 0,
                "misc", InventoryDisplayMode.VerticalScroll, 0, 12);
            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(quantities[definitions[0].DefinitionId], Is.EqualTo(2));
        }

        [Test] public void Paged_RejectsInvalidItemsPerPage() => Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            InventoryProjectionBuilder.Build(new ItemDefinition[0], _ => 0, "all", InventoryDisplayMode.Paged, 0, 0));

        [Test]
        public void UnregisteredCategory_IsIncludedInMisc()
        {
            var definitions = CreateDefinitions(1); definitions[0].SetCategoryForEditor("removed-category");
            var result = InventoryProjectionBuilder.Build(definitions, _ => 1, "misc",
                InventoryDisplayMode.VerticalScroll, 0, 12, new[] { "all", "consumable", "misc" });
            Assert.That(result.Items.Count, Is.EqualTo(1));
        }

        [TestCase(InventoryDisplayMode.VerticalScroll, 0, 12, false, 12)]
        [TestCase(InventoryDisplayMode.VerticalScroll, 15, 12, false, 15)]
        [TestCase(InventoryDisplayMode.Paged, 2, 12, true, 12)]
        [TestCase(InventoryDisplayMode.Paged, 2, 12, false, 2)]
        public void GridSettings_CalculateDisplayCapacityWithoutTruncatingScrollResults(
            InventoryDisplayMode mode, int itemCount, int configuredSlots, bool fill, int expected)
        {
            var settings = new GridContentLayoutSettings
            {
                displayMode = mode,
                slotsPerPage = configuredSlots,
                minimumScrollSlots = configuredSlots,
                fillPageWithEmptySlots = fill
            };
            Assert.That(settings.CalculateDisplayCapacity(itemCount), Is.EqualTo(expected));
        }

        [Test]
        public void GridSettings_InvalidValuesUseSafeCopyWithoutMutatingSource()
        {
            var source = new GridContentLayoutSettings
            {
                displayMode = (InventoryDisplayMode)999,
                slotsPerPage = -4,
                minimumScrollSlots = 99999,
                columns = 0,
                columnPolicy = (InventoryColumnPolicy)999,
                cellSize = new Vector2(float.NaN, float.PositiveInfinity),
                spacing = new Vector2(-3, float.NaN),
                padding = null
            };
            var copy = GridContentLayoutSettings.CreateSafeCopy(source, out var usedFallback);
            Assert.That(usedFallback, Is.True);
            Assert.That(copy.displayMode, Is.EqualTo(InventoryDisplayMode.Paged));
            Assert.That(copy.columnPolicy, Is.EqualTo(InventoryColumnPolicy.AdaptiveUpToConfigured));
            Assert.That(copy.slotsPerPage, Is.EqualTo(1));
            Assert.That(copy.minimumScrollSlots, Is.EqualTo(GridContentLayoutSettings.MaximumSlots));
            Assert.That(copy.columns, Is.EqualTo(1));
            Assert.That(copy.cellSize, Is.EqualTo(new Vector2(72, 72)));
            Assert.That(copy.padding, Is.Not.Null);
            Assert.That(source.slotsPerPage, Is.EqualTo(-4));
            Assert.That(source.padding, Is.Null);
        }

        [Test]
        public void InventoryReferenceValidationReportsCatalogAndGridSeparately()
        {
            var gameObject = new GameObject("InventoryTest", typeof(RectTransform));
            created.Add(gameObject);
            var page = gameObject.AddComponent<InventoryGameWindowPage>();
            Assert.That(page.ValidateRequiredReferences(out var missingCatalog), Is.False);
            Assert.That(missingCatalog, Is.EqualTo("inventory.catalog-missing"));
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            created.Add(catalog);
            var serialized = new SerializedObject(page);
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(page.ValidateRequiredReferences(out var missingGrid), Is.False);
            Assert.That(missingGrid, Is.EqualTo("inventory.item-grid-missing"));
        }

        [Test]
        public void ModalRequest_ClonesGridSettingsAndKeepsTypedProvider()
        {
            var settings = new GridContentLayoutSettings { displayMode = InventoryDisplayMode.Paged, slotsPerPage = 9 };
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            created.Add(catalog);
            var provider = new InventoryModalDataProvider(null, catalog);
            var request = new ModalOpenRequest(GamePageIds.Inventory, ModalContentKind.ItemGrid, "Bag",
                settings, provider, "consumable", 3);
            settings.slotsPerPage = 2;
            Assert.That(request.ContentKind, Is.EqualTo(ModalContentKind.ItemGrid));
            Assert.That(request.DataProvider, Is.SameAs(provider));
            Assert.That(request.DataProvider.ProviderKey, Is.EqualTo("player-inventory"));
            Assert.That(((GridContentLayoutSettings)request.Configuration).slotsPerPage, Is.EqualTo(9));
            Assert.That(request.InitialCategoryId, Is.EqualTo("consumable"));
            Assert.That(request.InitialPage, Is.EqualTo(3));
        }

        [Test]
        public void InventoryRequest_RejectsWrongKindAndMissingProviderSeparately()
        {
            var gameObject = new GameObject("InventoryRequestTest", typeof(RectTransform));
            created.Add(gameObject);
            var page = gameObject.AddComponent<InventoryGameWindowPage>();
            page.ConfigureForEditor(GamePageIds.Inventory);

            var wrongKind = Assert.Throws<ModalRequestException>(() => page.ApplyRequest(
                new ModalOpenRequest(GamePageIds.Inventory, ModalContentKind.CustomContent, "Bag")));
            Assert.That(wrongKind.ErrorCode, Is.EqualTo("inventory.request-kind-mismatch"));

            var missingProvider = Assert.Throws<ModalRequestException>(() => page.ApplyRequest(
                new ModalOpenRequest(GamePageIds.Inventory, ModalContentKind.ItemGrid, "Bag")));
            Assert.That(missingProvider.ErrorCode, Is.EqualTo("inventory.provider-missing"));
        }

        [Test]
        public void InventoryRequest_MissingConfigurationUsesSafePagedDefault()
        {
            var gameObject = new GameObject("InventoryFallbackRequestTest", typeof(RectTransform));
            created.Add(gameObject);
            var page = gameObject.AddComponent<InventoryGameWindowPage>();
            page.ConfigureForEditor(GamePageIds.Inventory);
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            created.Add(catalog);
            page.ApplyRequest(new ModalOpenRequest(GamePageIds.Inventory, ModalContentKind.ItemGrid, "Bag",
                dataProvider: new InventoryModalDataProvider(null, catalog)));
            Assert.That(page.IsUsingFallbackGridSettings, Is.True);
            Assert.That(page.DisplayMode, Is.EqualTo(InventoryDisplayMode.Paged));
        }

        private List<ItemDefinition> CreateDefinitions(int count)
        {
            var result = new List<ItemDefinition>();
            for (var i = 0; i < count; i++)
            {
                var item = ScriptableObject.CreateInstance<ItemDefinition>(); created.Add(item);
                item.ConfigureForEditor($"item-{i:000}", $"Item {i:000}", string.Empty, ItemEffectKind.Healing, 1);
                result.Add(item);
            }
            return result;
        }
    }
}
