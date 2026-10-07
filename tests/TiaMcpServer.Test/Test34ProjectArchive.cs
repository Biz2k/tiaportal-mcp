using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>The rules of 'archive_project' and 'retrieve_project'. These tests do not connect to TIA Portal.</summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test34ProjectArchive
    {
        private const string ProjectFile = @"C:\Projects\Plant\Plant.ap21";

        private static (string Directory, string Name, string FullPath) Target(string? dir, string? name, string mode = "Compressed", bool dirExists = true, bool pathExists = false)
        {
            return ProjectArchiveRules.CheckArchiveTarget(dir, name, mode, ProjectFile, d => dirExists, p => pathExists);
        }

        [TestMethod]
        public void Test_3400_Mode_DefaultIsCompressed_AnyCaseAccepted()
        {
            Assert.AreEqual("Compressed", ProjectArchiveRules.ParseMode(null));
            Assert.AreEqual("Compressed", ProjectArchiveRules.ParseMode(" "));
            Assert.AreEqual("None", ProjectArchiveRules.ParseMode("NONE"));
            Assert.AreEqual("DiscardRestorableDataAndCompressed", ProjectArchiveRules.ParseMode("discardrestorabledataandcompressed"));
        }

        [TestMethod]
        public void Test_3401_Mode_Unknown_ListsTheValidOnes()
        {
            var ex = Assert.ThrowsException<PortalException>(() => ProjectArchiveRules.ParseMode("zip"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, ex.Code);

            foreach (var mode in ProjectArchiveRules.Modes)
            {
                StringAssert.Contains(ex.Message, mode);
            }
        }

        [TestMethod]
        public void Test_3402_Name_GetsTheExtensionOfTheProject_UnlessItHasOne()
        {
            Assert.AreEqual(@"C:\Backups\Plant_1.zap21", Target(@"C:\Backups", "Plant_1").FullPath);
            Assert.AreEqual(@"C:\Backups\Plant_1.zap21", Target(@"C:\Backups\", "Plant_1.zap21").FullPath);
            Assert.AreEqual(@"C:\Backups\Plant_1.zap20", Target(@"C:\Backups", "Plant_1.zap20").FullPath);
            Assert.AreEqual(@"C:\Backups\Plant.v2.zap21", Target(@"C:\Backups", "Plant.v2").FullPath);
        }

        [TestMethod]
        public void Test_3403_Name_FolderModes_KeepTheName()
        {
            Assert.AreEqual(@"C:\Backups\Plant_1", Target(@"C:\Backups", "Plant_1", "None").FullPath);
            Assert.IsFalse(ProjectArchiveRules.IsFile("None"));
            Assert.IsFalse(ProjectArchiveRules.IsFile("DiscardRestorableData"));
            Assert.IsTrue(ProjectArchiveRules.IsFile("DiscardRestorableDataAndCompressed"));
        }

        [TestMethod]
        public void Test_3404_Refused_BeforeTiaPortalIsAsked()
        {
            Assert.AreEqual(PortalErrorCode.InvalidParams, Assert.ThrowsException<PortalException>(() => Target(@"Backups", "x")).Code);
            Assert.AreEqual(PortalErrorCode.InvalidParams, Assert.ThrowsException<PortalException>(() => Target(null, "x")).Code);
            Assert.AreEqual(PortalErrorCode.InvalidParams, Assert.ThrowsException<PortalException>(() => Target(@"C:\Backups", " ")).Code);
            Assert.AreEqual(PortalErrorCode.InvalidParams, Assert.ThrowsException<PortalException>(() => Target(@"C:\Backups", @"sub\x")).Code);
            Assert.AreEqual(PortalErrorCode.NotFound, Assert.ThrowsException<PortalException>(() => Target(@"C:\Backups", "x", dirExists: false)).Code);

            var exists = Assert.ThrowsException<PortalException>(() => Target(@"C:\Backups", "x", pathExists: true));

            Assert.AreEqual(PortalErrorCode.InvalidState, exists.Code);
            StringAssert.Contains(exists.Message, @"C:\Backups\x.zap21");
        }

        [TestMethod]
        public void Test_3405_Retrieve_Accepted()
        {
            var r = ProjectArchiveRules.CheckRetrieve(@"C:\Backups\Plant.zap21", @"C:\Projects\Restored\", f => true, d => !d.EndsWith("Restored"), d => true);

            Assert.AreEqual(@"C:\Backups\Plant.zap21", r.Archive);
            Assert.AreEqual(@"C:\Projects\Restored", r.Directory);
            Assert.AreEqual(@"C:\Projects\Restored", ProjectArchiveRules.CheckRetrieve(@"C:\Backups\Plant.zap21", @"C:\Projects\Restored", f => true, d => true, d => true).Directory);
        }

        [TestMethod]
        public void Test_3406_Retrieve_Refused_WithTheReason()
        {
            Assert.AreEqual(PortalErrorCode.NotFound, Assert.ThrowsException<PortalException>(() => ProjectArchiveRules.CheckRetrieve(@"C:\Backups\x.zap21", @"C:\P\R", f => false, d => true, d => true)).Code);
            Assert.AreEqual(PortalErrorCode.InvalidParams, Assert.ThrowsException<PortalException>(() => ProjectArchiveRules.CheckRetrieve("x.zap21", @"C:\P\R", f => true, d => true, d => true)).Code);
            Assert.AreEqual(PortalErrorCode.InvalidParams, Assert.ThrowsException<PortalException>(() => ProjectArchiveRules.CheckRetrieve(@"C:\x.zap21", "R", f => true, d => true, d => true)).Code);
            Assert.AreEqual(PortalErrorCode.NotFound, Assert.ThrowsException<PortalException>(() => ProjectArchiveRules.CheckRetrieve(@"C:\x.zap21", @"C:\P\R", f => true, d => false, d => true)).Code);
            Assert.AreEqual(PortalErrorCode.InvalidState, Assert.ThrowsException<PortalException>(() => ProjectArchiveRules.CheckRetrieve(@"C:\x.zap21", @"C:\P\R", f => true, d => true, d => false)).Code);
        }
    }
}
