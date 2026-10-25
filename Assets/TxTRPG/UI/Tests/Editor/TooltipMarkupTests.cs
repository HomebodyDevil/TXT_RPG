using NUnit.Framework;
using TxTRPG.UI.Tooltips;

namespace TxTRPG.UI.Tests
{
    public sealed class TooltipMarkupTests
    {
        private static string Name(string id) => id == "kw.fire" ? "화염" : id == "kw.ice" ? "냉기" : null;

        private static string Link(string id, string text) =>
            $"<link=\"kw:{id}\"><b><u><color={TooltipMarkup.DefaultHighlightColor}>{text}</color></u></b></link>";

        [Test]
        public void Keyword_UsesTheDisplayNameOrTheGivenText()
        {
            var result = TooltipMarkup.Parse("{kw:kw.fire} 피해와 {kw:kw.ice|얼음 피해}", Name);
            Assert.That(result.Text, Is.EqualTo($"{Link("kw.fire", "화염")} 피해와 {Link("kw.ice", "얼음 피해")}"));
            Assert.That(result.KeywordIds, Is.EqualTo(new[] { "kw.fire", "kw.ice" }));
            Assert.That(result.UnknownKeywordIds, Is.Empty);
            Assert.That(result.HasUnclosedMarkup, Is.False);
        }

        [Test]
        public void Keyword_IdIsTrimmed()
        {
            Assert.That(TooltipMarkup.Parse("{kw: kw.fire }", Name).KeywordIds, Is.EqualTo(new[] { "kw.fire" }));
        }

        [Test]
        public void Braces_AreEscapedByDoubling()
        {
            Assert.That(TooltipMarkup.Parse("{{kw:kw.fire}} {x} }}", Name).Text, Is.EqualTo("{kw:kw.fire} {x} }"));
        }

        [Test]
        public void UnknownKeyword_IsPlainText()
        {
            var result = TooltipMarkup.Parse("{kw:kw.none} / {kw:kw.none|보이는 글}", Name);
            Assert.That(result.Text, Is.EqualTo("kw.none / 보이는 글"));
            Assert.That(result.UnknownKeywordIds, Is.EqualTo(new[] { "kw.none", "kw.none" }));
            Assert.That(result.KeywordIds, Is.Empty);
        }

        [Test]
        public void UnclosedMarkup_IsShownAsWritten()
        {
            var result = TooltipMarkup.Parse("앞 {kw:kw.fire|화염 뒤", Name);
            Assert.That(result.Text, Is.EqualTo("앞 {kw:kw.fire|화염 뒤"));
            Assert.That(result.HasUnclosedMarkup, Is.True);
        }

        [Test]
        public void OtherTags_PassThrough_AndHelpersFindIds()
        {
            Assert.That(TooltipMarkup.Parse("<i>기울임</i>", Name).Text, Is.EqualTo("<i>기울임</i>"));
            Assert.That(TooltipMarkup.FindKeywordIds("{kw:a} {kw:b|B} {{kw:c}}"), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(TooltipMarkup.KeywordIdOfLink("kw:a.b"), Is.EqualTo("a.b"));
            Assert.That(TooltipMarkup.KeywordIdOfLink("other"), Is.Empty);
            Assert.That(TooltipMarkup.Parse(null, Name).Text, Is.Empty);
        }
    }
}
