namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the command line options. These tests do not connect to TIA Portal.
    /// </summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test12CliOptions
    {
        [TestMethod]
        public void Test_1200_ParseArgs_WritesByDefault()
        {
            var options = CliOptions.ParseArgs(new string[0]);

            Assert.IsTrue(options.AllowWrite, "Writing is on unless --read-only is passed");
            Assert.IsFalse(options.DebugTools);
            Assert.IsFalse(options.Doctor);
        }

        [TestMethod]
        public void Test_1201_ParseArgs_ReadOnlyTurnsWritingOff()
        {
            Assert.IsFalse(CliOptions.ParseArgs(new[] { "--read-only" }).AllowWrite);
            Assert.IsFalse(CliOptions.ParseArgs(new[] { "-READ-ONLY" }).AllowWrite, "Options are case-insensitive");
        }

        [TestMethod]
        public void Test_1202_ParseArgs_AllowWriteIsStillAccepted()
        {
            // Client configurations written for the earlier opt-in must keep starting.
            Assert.IsTrue(CliOptions.ParseArgs(new[] { "--allow-write" }).AllowWrite);
        }

        [TestMethod]
        public void Test_1203_ParseArgs_ReadsTheOtherOptions()
        {
            var options = CliOptions.ParseArgs(new[] { "--tia-major-version", "20", "--logging", "1", "--debug-tools", "--doctor" });

            Assert.AreEqual(20, options.TiaMajorVersion);
            Assert.AreEqual(1, options.Logging);
            Assert.IsTrue(options.DebugTools);
            Assert.IsTrue(options.Doctor);
        }
    }
}