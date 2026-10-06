using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Finds the runtime language a caller means. The settings of a language are addressed by the name TIA Portal gives it
    /// ("English (United States)"), but a caller thinks in culture codes ("en-US"), which the rest of the tools use for
    /// texts. Both are accepted. Pure logic, tested without TIA Portal.
    /// </summary>
    public static class RuntimeLanguageName
    {
        /// <summary>The language of <paramref name="languages"/> that <paramref name="wanted"/> names, or null.</summary>
        public static string? Find(string wanted, IEnumerable<string> languages)
        {
            var all = languages.ToList();
            var text = (wanted ?? string.Empty).Trim();

            var exact = all.FirstOrDefault(l => l.Equals(text, StringComparison.OrdinalIgnoreCase));

            if (exact != null)
            {
                return exact;
            }

            CultureInfo culture;

            try
            {
                culture = CultureInfo.GetCultureInfo(text);
            }
            catch (CultureNotFoundException)
            {
                return null;
            }

            if (culture.IsNeutralCulture && culture.Name.Length == 0)
            {
                return null;
            }

            return all.FirstOrDefault(l => l.Equals(culture.EnglishName, StringComparison.OrdinalIgnoreCase))
                   ?? all.FirstOrDefault(l => l.Equals(culture.DisplayName, StringComparison.OrdinalIgnoreCase))
                   ?? all.FirstOrDefault(l => l.Equals(culture.NativeName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>For an error: each language with the culture code it stands for, when one is known.</summary>
        public static string Describe(IEnumerable<string> languages)
        {
            var codes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                if (!codes.ContainsKey(culture.EnglishName))
                {
                    codes[culture.EnglishName] = culture.Name;
                }
            }

            return string.Join(", ", languages.Select(l => codes.TryGetValue(l, out var code) ? $"{l} ({code})" : l));
        }
    }
}
