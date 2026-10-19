using NUnit.Framework;
using TxTRPG.Application.Encounters;
using TxTRPG.Gameplay.Encounters;
using UnityEngine;

namespace TxTRPG.Application.Tests
{
    public sealed class EncounterResultFormatterTests
    {
        private EncounterElementCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<EncounterElementCatalog>();
            catalog.SetEntries(new[]
            {
                new EncounterElementCatalog.Entry("rune", "룬"),
                new EncounterElementCatalog.Entry("relic", "유물")
            });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(catalog);

        [Test]
        public void SingleElement_FormatsAsNameColonKind()
        {
            var result = EncounterRollResult.Occurrence(EncounterKind.Combat, new[] { "rune" });
            Assert.That(EncounterResultFormatter.Format(result, catalog), Is.EqualTo("룬 : 전투"));
        }

        [Test]
        public void SeveralElements_FollowCatalogOrderWithCommaSeparator()
        {
            var result = EncounterRollResult.Occurrence(EncounterKind.Rest, new[] { "relic", "rune" });
            Assert.That(EncounterResultFormatter.Format(result, catalog), Is.EqualTo("룬, 유물 : 휴식"));
        }

        [Test]
        public void UnknownId_IsShownAsTheId()
        {
            var result = EncounterRollResult.Occurrence(EncounterKind.Event, new[] { "mystery", "rune" });
            Assert.That(EncounterResultFormatter.Format(result, catalog), Is.EqualTo("룬, mystery : 이벤트"));
            Assert.That(EncounterResultFormatter.Format(result, null), Is.EqualTo("mystery, rune : 이벤트"));
        }

        [Test]
        public void KindNames_AreKorean()
        {
            Assert.That(EncounterResultFormatter.KindName(EncounterKind.Combat), Is.EqualTo("전투"));
            Assert.That(EncounterResultFormatter.KindName(EncounterKind.Event), Is.EqualTo("이벤트"));
            Assert.That(EncounterResultFormatter.KindName(EncounterKind.Rest), Is.EqualTo("휴식"));
        }

        [Test]
        public void NotOccurred_UsesDebugMessages()
        {
            Assert.That(EncounterResultFormatter.Format(EncounterRollResult.NotOccurred(EncounterRollOutcome.ChanceFailed), catalog),
                Is.EqualTo("이벤트 없음 (발생 확률 판정 실패)"));
            Assert.That(EncounterResultFormatter.Format(EncounterRollResult.NotOccurred(EncounterRollOutcome.NoElementsSelected), catalog),
                Is.EqualTo("선택된 요소가 없습니다"));
            Assert.That(EncounterResultFormatter.Format(EncounterRollResult.NotOccurred(EncounterRollOutcome.NoKindAvailable), catalog),
                Is.EqualTo("종류 가중치가 모두 0입니다"));
        }
    }
}
