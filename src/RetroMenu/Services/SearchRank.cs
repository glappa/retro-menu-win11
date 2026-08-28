using System;

namespace RetroMenu.Services
{
    /// <summary>
    /// Shared scoring so every search list orders its hits the same way, and so the
    /// entry the user almost certainly meant ends up as the best match.
    /// </summary>
    public static class SearchRank
    {
        private static readonly char[] WordBreaks = { ' ', '-', '_', '.', '(', ')', ',', ':' };

        public static int Of(string name, string query)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(query)) return 0;

            if (name.Equals(query, StringComparison.CurrentCultureIgnoreCase)) return 6;
            if (name.StartsWith(query, StringComparison.CurrentCultureIgnoreCase)) return 5;

            var words = name.Split(WordBreaks, StringSplitOptions.RemoveEmptyEntries);

            // Word start, so "stu" finds "Android Studio".
            foreach (var word in words)
            {
                if (word.StartsWith(query, StringComparison.CurrentCultureIgnoreCase)) return 4;
            }

            // Initials, so "vsc" finds "Visual Studio Code".
            if (query.Length >= 2 && MatchesInitials(words, query)) return 3;

            return name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ? 1 : 0;
        }

        private static bool MatchesInitials(string[] words, string query)
        {
            if (words.Length < query.Length) return false;

            int at = 0;
            foreach (var word in words)
            {
                if (at >= query.Length) break;
                if (word.Length == 0) continue;
                if (char.ToUpperInvariant(word[0]) == char.ToUpperInvariant(query[at])) at++;
            }

            return at == query.Length;
        }
    }
}
