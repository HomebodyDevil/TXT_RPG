using NUnit.Framework;
using TxTRPG.UI.Runes;
using TxTRPG.UI.Tooltips;
using UnityEngine;

namespace TxTRPG.UI.Tests
{
    public sealed class RuneTooltipCatalogTests
    {
        [Test]
        public void Lookup_TrimsIdsUsesTheFirstEntryAndValidates()
        {
            var first = ScriptableObject.CreateInstance<TooltipContent>();
            var second = ScriptableObject.CreateInstance<TooltipContent>();
            var catalog = ScriptableObject.CreateInstance<RuneTooltipCatalog>();
            try
            {
                catalog.SetEntries(new[] { new RuneTooltipCatalog.Entry(" rune.a ", first), new RuneTooltipCatalog.Entry("rune.b", null) });
                Assert.That(catalog.Validate(out var reason), Is.True, reason);
                Assert.That(catalog.TryGetContent("rune.a", out var content) && content == first, Is.True);
                Assert.That(catalog.TryGetContent("Rune.A", out _), Is.False, "case-sensitive");
                Assert.That(catalog.TryGetContent("rune.b", out _), Is.False, "no content");
                Assert.That(catalog.TryGetContent(null, out _), Is.False);
                catalog.SetEntries(new[] { new RuneTooltipCatalog.Entry("rune.a", first), new RuneTooltipCatalog.Entry("rune.a", second) });
                Assert.That(catalog.TryGetContent("rune.a", out content) && content == first, Is.True, "first entry wins");
                Assert.That(catalog.Validate(out reason), Is.False);
                Assert.That(reason, Does.Contain("duplicated"));
                catalog.SetEntries(new[] { new RuneTooltipCatalog.Entry("  ", first) });
                Assert.That(catalog.Validate(out reason), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(catalog);
            }
        }
    }

    public sealed class TooltipKeywordCatalogTests
    {
        [Test]
        public void Lookup_TrimsIdsAndValidates()
        {
            var a = ScriptableObject.CreateInstance<TooltipKeywordDefinition>();
            var b = ScriptableObject.CreateInstance<TooltipKeywordDefinition>();
            var catalog = ScriptableObject.CreateInstance<TooltipKeywordCatalog>();
            try
            {
                a.Configure(" kw.a ", "에이", null, new[] { TooltipBlock.Text("설명") });
                b.Configure("kw.b", " ", null, null);
                catalog.SetKeywords(new[] { a, b });
                Assert.That(catalog.Validate(out var reason), Is.True, reason);
                Assert.That(catalog.TryGet("kw.a", out var found) && found == a, Is.True);
                Assert.That(catalog.TryGet("KW.A", out _), Is.False);
                Assert.That(b.DisplayName, Is.EqualTo("kw.b"), "an empty name shows the id");
                Assert.That(a.CreateTooltipBlocks()[0].Kind, Is.EqualTo(TooltipBlockKind.Title));
                b.Configure("kw.a", "중복", null, null);
                Assert.That(catalog.Validate(out reason), Is.False);
                Assert.That(reason, Does.Contain("duplicated"));
                b.Configure("", "빈 ID", null, null);
                Assert.That(catalog.Validate(out reason), Is.False);
                catalog.SetKeywords(new TooltipKeywordDefinition[] { null });
                Assert.That(catalog.Validate(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
