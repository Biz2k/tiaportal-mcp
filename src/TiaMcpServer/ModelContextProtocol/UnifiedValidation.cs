using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One thing TIA Portal's own validation says about a WinCC Unified object.</summary>
    public sealed class UnifiedFinding
    {
        public UnifiedFinding(string property, string text, bool isError)
        {
            Property = property ?? string.Empty;
            Text = (text ?? string.Empty).Trim();
            IsError = isError;
        }

        /// <summary>The property as the validation names it: "InitialValue", "Screen", "Left:Dynamization", "Dynamization".</summary>
        public string Property { get; }

        public string Text { get; }

        public bool IsError { get; }

        internal string Key => (IsError ? "E|" : "W|") + Property + "|" + Text;

        public override string ToString()
        {
            return string.IsNullOrEmpty(Property) ? Text : $"{Property}: {Text}";
        }
    }

    /// <summary>
    /// What a write did to the validation of an object. Openness stores many wrong values without a word - a screen, a
    /// graphic, a connection, a cycle that does not exist, an initial value outside the range of the data type - but
    /// the object's Validate() names them. The findings are read before the write and after it: an error that is new
    /// is the doing of this write and refuses it; what was there before, and warnings, are passed on as notes. An
    /// object just created is read right after its creation, so what TIA Portal misses on any new object of the kind
    /// (an alarm without a trigger tag) does not count against the values the caller gave.
    /// Pure logic, so that the tests reach it without TIA Portal.
    /// </summary>
    public static class UnifiedValidation
    {
        public sealed class Verdict
        {
            public List<string> Errors { get; } = new List<string>();

            public List<string> Notes { get; } = new List<string>();
        }

        public static Verdict Judge(IEnumerable<UnifiedFinding> before, IEnumerable<UnifiedFinding> after)
        {
            var known = new HashSet<string>(before.Select(f => f.Key), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var verdict = new Verdict();

            foreach (var finding in after)
            {
                if (!seen.Add(finding.Key))
                {
                    continue;
                }

                if (finding.IsError && !known.Contains(finding.Key))
                {
                    verdict.Errors.Add(finding.ToString());
                }
                else
                {
                    verdict.Notes.Add($"TIA Portal's validation {(finding.IsError ? "also reports, not caused by this write" : "warns")}: {finding}");
                }
            }

            return verdict;
        }

        /// <summary>The sentence a refused write is answered with.</summary>
        public static string Refusal(IReadOnlyCollection<string> errors)
        {
            return "TIA Portal's validation rejects the result (Openness stored the values without an error): " + string.Join(" | ", errors);
        }
    }
}
