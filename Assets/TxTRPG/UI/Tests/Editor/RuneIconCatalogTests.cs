using NUnit.Framework;
using TxTRPG.UI.Runes;
using UnityEngine;

namespace TxTRPG.UI.Tests
{
    public sealed class RuneIconCatalogTests
    {
        private RuneIconCatalog catalog;
        private Texture2D texture;
        private Sprite first;
        private Sprite second;

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<RuneIconCatalog>();
            texture = new Texture2D(4, 4);
            first = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
            second = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void TryGetIcon_TrimsIdsAndIsCaseSensitive()
        {
            catalog.SetEntries(new[] { new RuneIconCatalog.Entry("  rune.attack ", first) });
            Assert.That(catalog.Validate(out _), Is.True);
            Assert.That(catalog.TryGetIcon(" rune.attack", out var icon), Is.True);
            Assert.That(icon, Is.SameAs(first));
            Assert.That(catalog.TryGetIcon("RUNE.ATTACK", out _), Is.False);
            Assert.That(catalog.TryGetIcon(null, out _), Is.False);
            Assert.That(catalog.TryGetIcon("  ", out _), Is.False);
        }

        [Test]
        public void Validate_ReportsEmptyAndDuplicateIds_AndLookupUsesTheFirstEntry()
        {
            catalog.SetEntries(new[] { new RuneIconCatalog.Entry("rune.heal", first), new RuneIconCatalog.Entry("rune.heal ", second) });
            Assert.That(catalog.Validate(out var reason), Is.False);
            Assert.That(reason, Does.Contain("duplicated"));
            Assert.That(catalog.TryGetIcon("rune.heal", out var icon), Is.True);
            Assert.That(icon, Is.SameAs(first));

            catalog.SetEntries(new[] { new RuneIconCatalog.Entry(" ", first) });
            Assert.That(catalog.Validate(out reason), Is.False);
            Assert.That(reason, Does.Contain("empty"));
        }

        [Test]
        public void EntryWithoutSprite_MeansNoIcon()
        {
            catalog.SetEntries(new[] { new RuneIconCatalog.Entry("rune.attack", null), new RuneIconCatalog.Entry("rune.heal", second) });
            Assert.That(catalog.Validate(out _), Is.True);
            Assert.That(catalog.TryGetIcon("rune.attack", out var icon), Is.False);
            Assert.That(icon, Is.Null);
            Assert.That(catalog.TryGetIcon("rune.unknown", out _), Is.False);
        }
    }
}
