using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Turns raw card description strings into TMP rich-text with the important
/// words emphasised, and figures out which keywords a card references without
/// anyone having to hand-fill a list.
///
/// Two things get wrapped in &lt;b&gt;&lt;/b&gt;:
///   1. Any <see cref="KeywordData"/> name that appears in the text. Every
///      keyword asset under Resources/<see cref="KeywordsResourceFolder"/> is
///      loaded once and matched as a whole word (case-insensitive).
///   2. A stat token (ATK / DMG / HP) together with an adjacent number on
///      either side: "5 ATK", "ATK 5", "+2 HP", "deal 3 DMG". A lone token
///      with no number is left alone.
///
/// The keyword scan also powers <see cref="GetKeywordsInCard"/>, so the keyword
/// info panels are populated straight from the description text.
/// </summary>
public static class CardTextFormatter
{
    // Folder under a Resources/ directory that holds the KeywordData assets.
    private const string KeywordsResourceFolder = "Keywords";

    // Stat abbreviations that get bolded alongside their number. Extend freely.
    private static readonly string[] StatTokens = { "ATK", "DMG", "HP" };

    private static bool _loaded;
    private static KeywordData[] _keywords;
    private static Regex _boldRegex;                 // combined keyword + stat matcher
    private static Dictionary<KeywordData, Regex> _perKeyword; // whole-word matcher per keyword

    /// <summary>
    /// Returns <paramref name="text"/> with keywords and stat tokens wrapped in
    /// &lt;b&gt; tags. Safe to call on null/empty and on text that already has tags.
    /// </summary>
    public static string Format(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        EnsureLoaded();
        if (_boldRegex == null) return text;
        return _boldRegex.Replace(text, m => $"<b>{m.Value}</b>");
    }

    /// <summary>
    /// Every keyword referenced anywhere in the card's descriptions, unioned
    /// with any keywords still assigned by hand on the card. Order: detected
    /// first (in asset order), then manual extras not already found.
    /// </summary>
    public static List<KeywordData> GetKeywordsInCard(FieldableCardType card)
    {
        var result = new List<KeywordData>();
        if (card == null) return result;

        EnsureLoaded();
        if (_keywords != null)
        {
            foreach (var kw in _keywords)
            {
                if (kw == null || !_perKeyword.TryGetValue(kw, out var rx)) continue;
                if (MatchesAny(rx, card.passiveDescription, card.effect1Description, card.effect2Description))
                    result.Add(kw);
            }
        }

        // Preserve any manually-dragged keywords that the text scan didn't catch.
        if (card.keywords != null)
            foreach (var kw in card.keywords)
                if (kw != null && !result.Contains(kw))
                    result.Add(kw);

        return result;
    }

    /// <summary>Drops the cache so freshly edited keyword assets are picked up.</summary>
    public static void Reload()
    {
        _loaded = false;
        _keywords = null;
        _boldRegex = null;
        _perKeyword = null;
    }

    private static bool MatchesAny(Regex rx, params string[] texts)
    {
        foreach (var t in texts)
            if (!string.IsNullOrEmpty(t) && rx.IsMatch(t))
                return true;
        return false;
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        _keywords = Resources.LoadAll<KeywordData>(KeywordsResourceFolder);
        _perKeyword = new Dictionary<KeywordData, Regex>();

        // Longest names first so e.g. "Cheap Shot" wins over a hypothetical "Cheap".
        var names = new List<string>();
        foreach (var kw in _keywords)
        {
            if (kw == null || string.IsNullOrWhiteSpace(kw.keywordName)) continue;
            string name = kw.keywordName.Trim();
            _perKeyword[kw] = new Regex($@"\b{Regex.Escape(name)}\b", RegexOptions.IgnoreCase);
            names.Add(name);
        }

        var parts = new List<string>();

        // Stat token with a number on either side.
        string stats = string.Join("|", StatTokens);
        parts.Add($@"(?<!\w)[+\-]?\d+\s*(?:{stats})\b");   // "5 ATK", "+2 HP"
        parts.Add($@"\b(?:{stats})\s*[+\-]?\d+(?!\w)");     // "ATK 5", "HP +2"

        // Keyword names, longest first, de-duplicated.
        foreach (var name in names.Distinct().OrderByDescending(n => n.Length))
            parts.Add($@"\b{Regex.Escape(name)}\b");

        _boldRegex = new Regex(string.Join("|", parts), RegexOptions.IgnoreCase);
    }
}
