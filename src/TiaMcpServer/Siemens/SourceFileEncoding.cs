using System.IO;
using System.Text;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// TIA Portal reads an external source file (.scl, .db, .udt, .awl) as UTF-8 only when it starts with a byte order
    /// mark; without one it takes the bytes for the Windows ANSI code page, and every non-ASCII character - a Cyrillic
    /// name, a degree sign - arrives in the project garbled (seen 2026-10-07: "ШВВ3-U1" in the code of
    /// 'plc_replace_source'). So no file goes to Openness as it is: it is decoded here and handed over as a UTF-8 copy
    /// with the mark. No Openness in this class.
    /// </summary>
    public static class SourceFileEncoding
    {
        /// <summary>A mark wins; without one the bytes are UTF-8 when they are valid UTF-8, else ANSI - what TIA Portal itself would have assumed.</summary>
        public static string Decode(byte[] bytes)
        {
            if (StartsWith(bytes, 0xEF, 0xBB, 0xBF))
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }

            if (StartsWith(bytes, 0xFF, 0xFE))
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }

            if (StartsWith(bytes, 0xFE, 0xFF))
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default.GetString(bytes);
            }
        }

        /// <summary>Writes the text as UTF-8 with the mark.</summary>
        public static void Write(string path, string text)
        {
            File.WriteAllText(path, text, new UTF8Encoding(true));
        }

        /// <summary>Copies a file into a directory under its own name - TIA Portal tells the kinds of source apart by the extension - as UTF-8 with the mark.</summary>
        public static string WriteCopy(string sourcePath, string targetDirectory)
        {
            var target = Path.Combine(targetDirectory, Path.GetFileName(sourcePath));

            Write(target, Decode(File.ReadAllBytes(sourcePath)));

            return target;
        }

        private static bool StartsWith(byte[] bytes, params byte[] mark)
        {
            if (bytes.Length < mark.Length)
            {
                return false;
            }

            for (var i = 0; i < mark.Length; i++)
            {
                if (bytes[i] != mark[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
