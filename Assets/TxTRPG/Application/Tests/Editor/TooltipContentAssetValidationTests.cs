using System.IO;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Application.Editor;
using TxTRPG.Application.Runes;
using TxTRPG.UI.Runes;
using TxTRPG.UI.Tooltips;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Application.Tests
{
    // Read-only checks of the saved tooltip, keyword and test rune assets.
    public sealed class TooltipContentAssetValidationTests
    {
        private const string EventFolder = "Assets/TxTRPG/Application/Configuration/Events";

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        [Test]
        public void SavedCatalogs_PassValidation()
        {
            Assert.That(Load<TooltipKeywordCatalog>(TemporaryRuneTooltipApply.KeywordCatalogPath).Validate(out var reason), Is.True, reason);
            Assert.That(Load<RuneTooltipCatalog>(TemporaryRuneTooltipApply.RuneTooltipCatalogPath).Validate(out reason), Is.True, reason);
            Assert.That(Load<TemporaryTestRuneConfiguration>(TemporaryRuneTooltipApply.TestRuneConfigurationPath).Validate(out reason), Is.True, reason);
            Assert.That(Load<RuneIconCatalog>(TemporaryRuneGridApply.CatalogPath).Validate(out reason), Is.True, reason);
        }

        [Test]
        public void EveryKeywordMarkup_IsInTheKeywordCatalog()
        {
            var catalog = Load<TooltipKeywordCatalog>(TemporaryRuneTooltipApply.KeywordCatalogPath);
            var texts = AssetDatabase.FindAssets("t:TooltipContent").Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(path => AssetDatabase.LoadAssetAtPath<TooltipContent>(path).Blocks.Select(block => (path, block)))
                .Concat(AssetDatabase.FindAssets("t:TooltipKeywordDefinition").Select(AssetDatabase.GUIDToAssetPath)
                    .SelectMany(path => AssetDatabase.LoadAssetAtPath<TooltipKeywordDefinition>(path).Blocks.Select(block => (path, block))));
            foreach (var (path, block) in texts)
            {
                Assert.That(block.Kind, Is.Not.EqualTo(TooltipBlockKind.Unknown), path);
                if (block.Kind != TooltipBlockKind.Text) continue;
                var markup = TooltipMarkup.Parse(block.Content, catalog);
                Assert.That(markup.UnknownKeywordIds, Is.Empty, path);
                Assert.That(markup.HasUnclosedMarkup, Is.False, path);
            }
        }

        [Test]
        public void TestContent_UsesEveryBlockKindAndTheKeywordChainCycles()
        {
            var keywords = Load<TooltipKeywordCatalog>(TemporaryRuneTooltipApply.KeywordCatalogPath);
            var runeTooltips = Load<RuneTooltipCatalog>(TemporaryRuneTooltipApply.RuneTooltipCatalogPath);
            Assert.That(runeTooltips.TryGetContent(TemporaryTestRuneConfiguration.TooltipRuneId, out var content), Is.True);
            var kinds = content.Blocks.Select(block => block.Kind)
                .Concat(keywords.Keywords.SelectMany(keyword => keyword.Blocks.Select(block => block.Kind))).Distinct().ToArray();
            Assert.That(kinds, Is.SupersetOf(new[] { TooltipBlockKind.Title, TooltipBlockKind.Text, TooltipBlockKind.Image, TooltipBlockKind.Separator }));
            Assert.That(runeTooltips.TryGetContent(TemporaryTestRuneConfiguration.MissingRuneId, out _), Is.False, "the missing rune shows the fallback");
            Assert.That(Load<RuneIconCatalog>(TemporaryRuneGridApply.CatalogPath).TryGetIcon(TemporaryTestRuneConfiguration.MissingRuneId, out _), Is.False);
            string Next(string id) => TooltipMarkup.FindKeywordIds(keywords.Keywords.First(k => k.Id == id).Blocks.First(b => b.Kind == TooltipBlockKind.Text).Content).First();
            Assert.That(Next(TemporaryRuneTooltipApply.AlphaKeywordId), Is.EqualTo(TemporaryRuneTooltipApply.BetaKeywordId));
            Assert.That(Next(TemporaryRuneTooltipApply.BetaKeywordId), Is.EqualTo(TemporaryRuneTooltipApply.GammaKeywordId));
            Assert.That(Next(TemporaryRuneTooltipApply.GammaKeywordId), Is.EqualTo(TemporaryRuneTooltipApply.AlphaKeywordId));
        }

        // Catalog entries for definitions that no rune configuration defines are reported, not failed (deleting a test
        // rune kind leaves them until the catalogs are cleaned up).
        [Test]
        public void CatalogEntriesWithoutDefinition_AreReported()
        {
            var defined = AssetDatabase.FindAssets("t:TemporaryRuneConfiguration").Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(path => AssetDatabase.LoadAssetAtPath<TemporaryRuneConfiguration>(path).Definitions.Select(d => d.Id))
                .Concat(Load<TemporaryTestRuneConfiguration>(TemporaryRuneTooltipApply.TestRuneConfigurationPath).Definitions.Select(d => d.Id))
                .ToHashSet();
            var orphans = Load<RuneIconCatalog>(TemporaryRuneGridApply.CatalogPath).Entries.Select(e => e.DefinitionId)
                .Concat(Load<RuneTooltipCatalog>(TemporaryRuneTooltipApply.RuneTooltipCatalogPath).Entries.Select(e => e.DefinitionId))
                .Where(id => !defined.Contains(id)).Distinct().ToArray();
            if (orphans.Length > 0) Debug.LogWarning($"Rune catalogs have entries without a rune definition: {string.Join(", ", orphans)}");
            Assert.Pass(orphans.Length == 0 ? "no orphan entries" : $"orphan entries: {string.Join(", ", orphans)}");
        }

        [Test]
        public void ExplorationEvents_NeverGrantTestRunes()
        {
            var offenders = Directory.GetFiles(EventFolder, "*.asset", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains("definitionId: " + TemporaryTestRuneConfiguration.IdPrefix)).ToArray();
            Assert.That(offenders, Is.Empty);
        }
    }
}
