using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One LAD instruction as TIA Portal writes it in a SIMATIC SD document.</summary>
    public class LadInstruction
    {
        /// <summary>The name TIA Portal writes; it reads the aliases as well.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Other spellings TIA Portal accepts - mostly the name of the instruction in the help (SCALE_X for Scale).</summary>
        public List<string>? Aliases { get; set; }

        /// <summary>"Basic", "LAD" (basic instructions of the help) or "Extended".</summary>
        public string? Category { get; set; }

        public string? Description { get; set; }

        /// <summary>The instruction with all its pins; #b, #i, #r stand for operands.</summary>
        public string? Form { get; set; }

        /// <summary>The line to put before the instruction when it needs a data type, e.g. { S7_Templates := "SrcType := Int" }.</summary>
        public string? Template { get; set; }

        public string? Notes { get; set; }
    }

    /// <summary>
    /// The LAD instructions of TIA Portal V21 for S7-1500, as they are written in a document. There is no such list in
    /// the help or in the installation; this one was asked of TIA Portal itself, name by name
    /// (tools/lad-instruction-probe.ps1, docs/handoff/lad.md), and is shipped inside the server.
    /// </summary>
    public static class LadInstructions
    {
        public const string Syntax =
            "A LAD network is one or more rungs. 'RUNG wire#powerrail' starts at the power rail, the instructions follow one per line from left to right, 'END_RUNG' ends the rung. " +
            "Branches: a line 'wire#w1' marks a point of the rung; 'RUNG wire#w1' starts a further rung at that point (parallel outputs), 'END_RUNG wire#w1' ends a rung in that point " +
            "(parallel inputs - OR); the point is declared in the rung it lies on, at the place the branches meet. " +
            "Operands: #Local, \"Global\", \"DB\".Member, #Struct.Member, a bit of a word as .%X0, literals 5, 1.5, T#2s, 16#FF, TRUE. A name with other characters than ASCII letters, digits and '_' goes in quotes, also after #. " +
            "A contact or coil takes its operand in brackets: Contact( #Start ), Coil( \"Motor\" ). A box takes named pins: Mul( in1 := #a, in2 := 2, out => #b ) - ':=' for inputs, '=>' for outputs; " +
            "a pin that is not used is left empty ('et => ') or out. EN and ENO are not written: the place in the rung is the enable. " +
            "An instruction that works on several data types needs the type on a line before it: { S7_Templates := \"SrcType := Int\" }, a conversion { S7_Templates := \"[SrcType := Real, DestType := Int]\" }, " +
            "an IEC timer { S7_Templates := \"time_type := Time\" }, an IEC counter { S7_Templates := \"value_type := Int\" }; without it the compile says 'Please select a data type'. " +
            "IEC timers, counters and R_Trig / F_Trig are called through an instance: #Timer_0.TON( pt := T#2s, et => ) with 'Timer_0 : TON_TIME;' in the interface of the FB, or \"TimerDB\".TON( ... ). " +
            "A block is called by its name: \"FC_Name\"( in := #x ), \"InstanceDB\"( in := #x ), #MultiInstance( in := #x ). " +
            "A jump label is a line 'Label(M01)' before the first RUNG of the network; JumpCoil( M01 ) jumps to it. " +
            "Names written in upper case as in the help (ABS, SIN, MOD, AND) are refused: write Abs, Sin, Mod, And.";

        private static readonly Lazy<List<LadInstruction>> Loaded = new Lazy<List<LadInstruction>>(Load);

        public static IReadOnlyList<LadInstruction> All => Loaded.Value;

        private static List<LadInstruction> Load()
        {
            var assembly = typeof(LadInstructions).GetTypeInfo().Assembly;
            var resource = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("lad-instructions.tsv", StringComparison.OrdinalIgnoreCase));

            if (resource == null)
            {
                return new List<LadInstruction>();
            }

            using (var stream = assembly.GetManifestResourceStream(resource)!)
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return Parse(reader.ReadToEnd());
            }
        }

        /// <summary>Reads the table: name, aliases, internal name, form, notes, help (category | description), template.</summary>
        public static List<LadInstruction> Parse(string table)
        {
            var list = new List<LadInstruction>();

            foreach (var line in (table ?? string.Empty).Replace("\r\n", "\n").Split('\n').Skip(1))
            {
                var cells = line.Split('\t');

                if (cells.Length < 4 || cells[0].Trim().Length == 0)
                {
                    continue;
                }

                string Cell(int index) => cells.Length > index ? cells[index].Trim() : string.Empty;

                var help = Cell(5).Split(new[] { '|' }, 2);

                list.Add(new LadInstruction
                {
                    Name = Cell(0),
                    Aliases = Cell(1).Length == 0 ? null : Cell(1).Split(' ').Where(a => a.Length > 0).ToList(),
                    Form = Cell(3),
                    Notes = Cell(4).Length == 0 ? null : Cell(4),
                    Category = help.Length == 2 ? help[0].Trim() : "Basic",
                    Description = help.Length == 2 ? help[1].Trim() : Cell(5).Length == 0 ? null : Cell(5),
                    Template = Cell(6).Length == 0 ? null : Cell(6)
                });
            }

            return list;
        }

        /// <param name="filter">A regular expression or plain text, matched against name, aliases and description; empty matches all.</param>
        public static List<LadInstruction> Find(IEnumerable<LadInstruction> all, string? filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return all.ToList();
            }

            Regex pattern;

            try
            {
                pattern = new Regex(filter!.Trim(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            catch (ArgumentException)
            {
                pattern = new Regex(Regex.Escape(filter!.Trim()), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            return all
                .Where(i => pattern.IsMatch(i.Name) || (i.Aliases?.Any(a => pattern.IsMatch(a)) ?? false) || (i.Description != null && pattern.IsMatch(i.Description)))
                // A name or alias that is the filter itself first.
                .OrderBy(i => string.Equals(i.Name, filter!.Trim(), StringComparison.OrdinalIgnoreCase) || (i.Aliases?.Any(a => string.Equals(a, filter.Trim(), StringComparison.OrdinalIgnoreCase)) ?? false) ? 0 : 1)
                .ToList();
        }
    }

    public class ResponseLadInstructions : ResponseMessage
    {
        /// <summary>How a LAD network is written; given when no filter narrows the answer.</summary>
        public string? Syntax { get; set; }

        /// <summary>The names of all instructions by category; given instead of the items when there is no filter.</summary>
        public Dictionary<string, List<string>>? Names { get; set; }

        public IEnumerable<LadInstruction>? Items { get; set; }

        public int Count { get; set; }

        public int Total { get; set; }
    }
}
