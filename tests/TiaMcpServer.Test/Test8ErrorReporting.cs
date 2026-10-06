using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for what a failing tool tells the client, and for attribute values surviving JSON
    /// serialization. These tests do not connect to TIA Portal and do not open a project.
    /// </summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test8ErrorReporting
    {
        [TestInitialize]
        public void ClassInit()
        {
            // Helper references Siemens.Engineering types, so the resolver has to be in place.
            Engineering.TiaMajorVersion = Settings.TiaMajorVersion;
            Openness.Initialize(Engineering.TiaMajorVersion);
        }

        [TestMethod]
        public void Test_800_OperationRun_KeepsTheReasonInTheMessage()
        {
            // Act
            var pex = Assert.ThrowsException<PortalException>(() =>
                Operation.Run<int>(null, "CreateFB", PortalErrorCode.CreateFailed,
                    () => throw new InvalidOperationException("Block number 0 is not valid"),
                    ("groupPath", "Tests")));

            // Assert
            Assert.AreEqual(PortalErrorCode.CreateFailed, pex.Code);
            StringAssert.StartsWith(pex.Message, "CreateFB failed: ");
            StringAssert.Contains(pex.Message, "Block number 0 is not valid");
        }

        [TestMethod]
        public void Test_801_OperationRun_PassesPortalExceptionThroughUnchanged()
        {
            // Act
            var pex = Assert.ThrowsException<PortalException>(() =>
                Operation.Run<int>(null, "GetBlock", PortalErrorCode.ExportFailed,
                    () => throw new PortalException(PortalErrorCode.NotFound, "Block not found at 'A/B'.")));

            // Assert
            Assert.AreEqual(PortalErrorCode.NotFound, pex.Code);
            Assert.AreEqual("Block not found at 'A/B'.", pex.Message);
        }

        [TestMethod]
        public void Test_802_Describe_SkipsReflectionWrappersAndRepeats()
        {
            // Arrange
            var inner = new InvalidOperationException("The interface is not connected to a subnet");
            var wrapped = new TargetInvocationException(new Exception("Call failed", inner));

            // Act
            var text = ErrorText.Describe(wrapped);

            // Assert
            Assert.AreEqual("Call failed -> The interface is not connected to a subnet", text);
            Assert.AreEqual("Same", ErrorText.Describe(new Exception("Same", new Exception("Same"))));
        }

        [TestMethod]
        public void Test_803_ToolError_NamesCodeContextAndCause()
        {
            // Arrange
            var pex = Assert.ThrowsException<PortalException>(() =>
                Operation.Run<int>(null, "DownloadToPlc", PortalErrorCode.InvalidState,
                    () => throw new TimeoutException("No reachable device"),
                    ("softwarePath", "PLC_1")));

            // Act
            var message = McpServer.ToolError(pex).Message;

            // Assert
            StringAssert.Contains(message, "DownloadToPlc failed: No reachable device");
            StringAssert.Contains(message, "code: InvalidState");
            StringAssert.Contains(message, "softwarePath: 'PLC_1'");
            StringAssert.Contains(message, "cause: TimeoutException");
            Assert.IsFalse(message.Contains("__logged"), "Bookkeeping keys must not reach the client");
        }

        [TestMethod]
        public void Test_804_ToJsonSafe_MakesAttributeValuesSerializable()
        {
            // Arrange
            var file = new FileInfo(Path.Combine(Path.GetTempPath(), "Project1.ap21"));
            var values = new List<object?>
            {
                file,
                new DirectoryInfo(Path.GetTempPath()),
                CultureInfo.GetCultureInfo("en-US"),
                DayOfWeek.Monday,
                double.NaN,
                new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
                new object[] { 1, "two", new FileInfo(file.FullName) },
                null
            };

            // Act
            var safe = values.ConvertAll(v => TiaMcpServer.ModelContextProtocol.Helper.ToJsonSafe(v));
            var json = JsonSerializer.Serialize(safe);

            // Assert
            Assert.AreEqual(file.FullName, safe[0]);
            Assert.AreEqual("en-US", safe[2]);
            Assert.AreEqual("Monday", safe[3]);
            Assert.AreEqual("NaN", safe[4]);
            Assert.IsNull(safe[7]);
            StringAssert.Contains(json, "two");
        }

        [TestMethod]
        public void Test_805_BatchError_SharedCode_OneBracketAtTheEnd()
        {
            var failed = new List<BatchFailure>
            {
                new BatchFailure("update 'Tag_1'", "Tag 'Tag_1' does not exist.", PortalErrorCode.NotFound),
                new BatchFailure("delete 'Tag_2'", "Tag 'Tag_2' does not exist.", PortalErrorCode.NotFound)
            };

            var (message, code) = BatchErrorText.Compose(failed, 3, "Nothing was changed.");

            Assert.AreEqual(PortalErrorCode.NotFound, code);
            StringAssert.StartsWith(message, "2 of 3 action(s) failed. Nothing was changed. update 'Tag_1': Tag 'Tag_1' does not exist. | delete 'Tag_2'");
            Assert.IsFalse(message.Contains("[") || message.Contains("code:"), "the code is the one the batch exception carries, not repeated per action");
        }

        [TestMethod]
        public void Test_806_BatchError_MixedCodes_EachActionNamesItsOwn()
        {
            var failed = new List<BatchFailure>
            {
                new BatchFailure("create 'A'", "Name is empty.", PortalErrorCode.InvalidParams),
                new BatchFailure("update 'B'", "No such tag.", PortalErrorCode.NotFound),
                new BatchFailure("delete 'C'", "Openness refused.", null)
            };

            var (message, code) = BatchErrorText.Compose(failed, 3, "Rolled back.");

            Assert.AreEqual(PortalErrorCode.InvalidParams, code);
            StringAssert.Contains(message, "update 'B' [NotFound]: No such tag.");
            StringAssert.Contains(message, "create 'A' [InvalidParams]: Name is empty.");
            StringAssert.Contains(message, "delete 'C' [InvalidParams]: Openness refused.");
        }

        [TestMethod]
        public void Test_808_RuntimeLanguageName_AcceptsTheNameAndTheCultureCode()
        {
            var languages = new[] { "English (United States)", "Russian (Russia)", "Ukrainian (Ukraine)" };

            Assert.AreEqual("English (United States)", RuntimeLanguageName.Find("en-US", languages));
            Assert.AreEqual("Russian (Russia)", RuntimeLanguageName.Find("ru-ru", languages));
            Assert.AreEqual("Ukrainian (Ukraine)", RuntimeLanguageName.Find("Ukrainian (Ukraine)", languages));
            Assert.AreEqual("Russian (Russia)", RuntimeLanguageName.Find("russian (russia)", languages));
            Assert.IsNull(RuntimeLanguageName.Find("de-DE", languages), "a language the HMI does not have");
            Assert.IsNull(RuntimeLanguageName.Find("German", languages));
            StringAssert.Contains(RuntimeLanguageName.Describe(languages), "Russian (Russia) (ru-RU)");
        }

        [TestMethod]
        public void Test_807_ErrorText_ForAction_HasNoBracket()
        {
            var ex = new PortalException(PortalErrorCode.NotFound, "Tag 'X' not found.");
            ex.Data["softwarePath"] = "HMI_1/HMI_RT_1";

            Assert.AreEqual("Tag 'X' not found.", ErrorText.ForAction(ex));
            StringAssert.Contains(ErrorText.ForClient(ex), "[code: NotFound");
        }
    }
}
