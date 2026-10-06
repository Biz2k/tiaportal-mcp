using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for reading which objects an external source text declares - the check 'plc_replace_source' makes
    /// before TIA Portal generates anything. These tests do not connect to TIA Portal.
    /// </summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test27SourceDeclarations
    {
        [TestMethod]
        public void Test_2700_Read_FindsKindAndName()
        {
            var one = SourceDeclarations.Read("FUNCTION_BLOCK \"FB Pump 1\"\r\n{ S7_Optimized_Access := 'TRUE' }\r\nBEGIN\r\nEND_FUNCTION_BLOCK\r\n");

            Assert.AreEqual(1, one.Count);
            Assert.AreEqual("FB", one[0].Kind);
            Assert.AreEqual("FB Pump 1", one[0].Name);

            Assert.AreEqual("FC", SourceDeclarations.Read("FUNCTION \"Calc\" : Void\nBEGIN\nEND_FUNCTION")[0].Kind);
            Assert.AreEqual("OB", SourceDeclarations.Read("ORGANIZATION_BLOCK \"Main\"\nBEGIN\nEND_ORGANIZATION_BLOCK")[0].Kind);
            Assert.AreEqual("DB", SourceDeclarations.Read("  data_block Unquoted\nBEGIN\nEND_DATA_BLOCK")[0].Kind);
            Assert.AreEqual("Unquoted", SourceDeclarations.Read("  data_block Unquoted\nBEGIN\nEND_DATA_BLOCK")[0].Name);
            Assert.AreEqual("TYPE", SourceDeclarations.Read("TYPE \"UDT_Motor\"\nSTRUCT\nEND_STRUCT;\nEND_TYPE")[0].Kind);
        }

        [TestMethod]
        public void Test_2701_Read_CountsEveryObjectAndSkipsComments()
        {
            var text = "// FUNCTION \"InAComment\" : Void\n(* TYPE \"AlsoAComment\"\n*)\nTYPE \"A\"\nEND_TYPE\n\nFUNCTION_BLOCK \"B\"\nBEGIN\n  #x := 1; // END_FUNCTION_BLOCK\nEND_FUNCTION_BLOCK\n";
            var found = SourceDeclarations.Read(text);

            Assert.AreEqual(2, found.Count);
            Assert.AreEqual("A", found[0].Name);
            Assert.AreEqual("B", found[1].Name);
            Assert.AreEqual(0, SourceDeclarations.Read("just some text").Count);
            Assert.AreEqual(0, SourceDeclarations.Read(null).Count);
        }

        [TestMethod]
        public void Test_2702_KindOfBlockClass_MapsTheDataBlockClasses()
        {
            Assert.AreEqual("DB", SourceDeclarations.KindOfBlockClass("GlobalDB"));
            Assert.AreEqual("DB", SourceDeclarations.KindOfBlockClass("InstanceDB"));
            Assert.AreEqual("DB", SourceDeclarations.KindOfBlockClass("ArrayDB"));
            Assert.AreEqual("FB", SourceDeclarations.KindOfBlockClass("FB"));
        }
    }
}
