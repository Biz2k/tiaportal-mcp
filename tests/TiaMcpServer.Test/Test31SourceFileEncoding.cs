using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Text;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [TestCategory("NoTia")]
    public class Test31SourceFileEncoding
    {
        private const string Text = "// ШВВ3-U1 °C їЄ";

        [TestMethod]
        public void Test_3101_Decode_ReadsUtf8WithAndWithoutMark()
        {
            Assert.AreEqual(Text, SourceFileEncoding.Decode(new UTF8Encoding(false).GetBytes(Text)));
            Assert.AreEqual(Text, SourceFileEncoding.Decode(new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(Text)).ToArray()));
        }

        [TestMethod]
        public void Test_3102_Decode_ReadsUtf16ByItsMark()
        {
            Assert.AreEqual(Text, SourceFileEncoding.Decode(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Text)).ToArray()));
            Assert.AreEqual(Text, SourceFileEncoding.Decode(Encoding.BigEndianUnicode.GetPreamble().Concat(Encoding.BigEndianUnicode.GetBytes(Text)).ToArray()));
        }

        [TestMethod]
        public void Test_3103_Decode_TakesInvalidUtf8ForAnsi()
        {
            var bytes = new byte[] { (byte)'a', 0xB0, (byte)'b' };

            Assert.AreEqual(Encoding.Default.GetString(bytes), SourceFileEncoding.Decode(bytes));
            Assert.AreEqual(string.Empty, SourceFileEncoding.Decode(new byte[0]));
        }

        [TestMethod]
        public void Test_3104_WriteCopy_KeepsTheNameAndWritesOneMark()
        {
            var from = Path.Combine(Path.GetTempPath(), "TiaMcpServerTest_" + Path.GetRandomFileName());
            var to = Path.Combine(from, "copy");

            Directory.CreateDirectory(to);

            try
            {
                var source = Path.Combine(from, "Block.scl");

                File.WriteAllBytes(source, new UTF8Encoding(false).GetBytes(Text));

                var copy = SourceFileEncoding.WriteCopy(source, to);
                var bytes = File.ReadAllBytes(copy);

                Assert.AreEqual("Block.scl", Path.GetFileName(copy));
                CollectionAssert.AreEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
                Assert.AreNotEqual(0xEF, bytes[3]);
                Assert.AreEqual(Text, SourceFileEncoding.Decode(bytes));
            }
            finally
            {
                Directory.Delete(from, true);
            }
        }
    }
}
