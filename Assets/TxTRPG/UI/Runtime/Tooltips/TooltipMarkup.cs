using System;
using System.Collections.Generic;
using System.Text;

namespace TxTRPG.UI.Tooltips
{
    // Result of TooltipMarkup.Parse: the TMP rich text to show and the keywords it links.
    public sealed class TooltipMarkupResult
    {
        public TooltipMarkupResult(string text, IReadOnlyList<string> keywordIds, IReadOnlyList<string> unknownKeywordIds, bool hasUnclosedMarkup)
        {
            Text = text ?? string.Empty;
            KeywordIds = keywordIds ?? Array.Empty<string>();
            UnknownKeywordIds = unknownKeywordIds ?? Array.Empty<string>();
            HasUnclosedMarkup = hasUnclosedMarkup;
        }

        public string Text { get; }
        // Linked keywords in text order (a keyword used twice appears twice).
        public IReadOnlyList<string> KeywordIds { get; }
        // Keyword IDs that the resolver did not know; they are shown as plain text.
        public IReadOnlyList<string> UnknownKeywordIds { get; }
        public bool HasUnclosedMarkup { get; }
    }

    // Tooltip text markup. {kw:id} and {kw:id|shown text} become a TMP link ("kw:id") around the highlighted text (bold,
    // underline and a color, so the highlight never relies on color alone); without shown text the keyword's display
    // name is used. {{ and }} show braces. An unknown ID shows its text unhighlighted; an unclosed {kw: is shown as
    // written. Other TMP tags pass through unchanged (the text is developer-authored).
    public static class TooltipMarkup
    {
        public const string LinkPrefix = "kw:";
        public const string DefaultHighlightColor = "#FFD27A";
        private const string KeywordOpen = "{kw:";

        // resolveDisplayName returns the keyword's display name, or null when the ID is unknown.
        public static TooltipMarkupResult Parse(string source, Func<string, string> resolveDisplayName, string highlightColor = DefaultHighlightColor)
        {
            if (string.IsNullOrEmpty(source)) return new TooltipMarkupResult(string.Empty, null, null, false);
            var builder = new StringBuilder(source.Length + 32);
            var keywords = new List<string>();
            var unknown = new List<string>();
            var unclosed = false;
            var i = 0;
            while (i < source.Length)
            {
                var c = source[i];
                if (c == '{' && i + 1 < source.Length && source[i + 1] == '{') { builder.Append('{'); i += 2; continue; }
                if (c == '}' && i + 1 < source.Length && source[i + 1] == '}') { builder.Append('}'); i += 2; continue; }
                if (c == '{' && string.CompareOrdinal(source, i, KeywordOpen, 0, KeywordOpen.Length) == 0)
                {
                    var end = source.IndexOf('}', i + KeywordOpen.Length);
                    if (end < 0)
                    {
                        unclosed = true;
                        builder.Append(source, i, source.Length - i);
                        break;
                    }
                    var inner = source.Substring(i + KeywordOpen.Length, end - i - KeywordOpen.Length);
                    var separator = inner.IndexOf('|');
                    var id = (separator >= 0 ? inner.Substring(0, separator) : inner).Trim();
                    var shown = separator >= 0 ? inner.Substring(separator + 1) : null;
                    var displayName = id.Length > 0 ? resolveDisplayName?.Invoke(id) : null;
                    if (displayName == null)
                    {
                        if (id.Length > 0) unknown.Add(id);
                        builder.Append(string.IsNullOrEmpty(shown) ? id : shown);
                    }
                    else
                    {
                        keywords.Add(id);
                        builder.Append("<link=\"").Append(LinkPrefix).Append(id).Append("\"><b><u><color=").Append(highlightColor).Append('>')
                            .Append(string.IsNullOrEmpty(shown) ? displayName : shown).Append("</color></u></b></link>");
                    }
                    i = end + 1;
                    continue;
                }
                builder.Append(c);
                i++;
            }
            return new TooltipMarkupResult(builder.ToString(), keywords, unknown, unclosed);
        }

        public static TooltipMarkupResult Parse(string source, TooltipKeywordCatalog catalog) =>
            Parse(source, id => catalog != null && catalog.TryGet(id, out var keyword) ? keyword.DisplayName : null);

        // Keyword IDs written in a text, without resolving them (Editor usage search, asset validation).
        public static IReadOnlyList<string> FindKeywordIds(string source)
        {
            var result = new List<string>();
            Parse(source, id => { result.Add(id); return id; });
            return result;
        }

        // "kw:id" link ID → keyword ID; empty for any other link.
        public static string KeywordIdOfLink(string linkId) =>
            linkId != null && linkId.StartsWith(LinkPrefix, StringComparison.Ordinal) ? linkId.Substring(LinkPrefix.Length) : string.Empty;
    }
}
