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
    }
}
