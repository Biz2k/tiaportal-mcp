using System;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the values the WinCC Unified log tools take: durations, dates and times. These
    /// tests do not connect to TIA Portal.
    /// </summary>
    [TestClass]
    public class Test18UnifiedLogs
    {
        private static JsonElement Json(string text)
        {
            using var document = JsonDocument.Parse(text);

            return document.RootElement.Clone();
        }

        [TestMethod]
        public void Test_1800_Parse_ReadsDurationPartsAndLeavesTheRestAtZero()
        {
            var duration = UnifiedLogDuration.Parse(Json("{\"days\": 7, \"Hours\": 12}"), "Settings.LogTimePeriod");

            Assert.AreEqual(7u, duration.Days);
            Assert.AreEqual(12u, duration.Hours);
            Assert.AreEqual(0u, duration.Minutes);
            Assert.AreEqual(0u, duration.Seconds);
            Assert.AreEqual(0u, duration.Ticks);
        }

        [TestMethod]
        public void Test_1801_Parse_RefusesWhatIsNotADuration()
        {
            var number = Assert.ThrowsException<PortalException>(() => UnifiedLogDuration.Parse(Json("7"), "Settings.LogTimePeriod"));
            StringAssert.Contains(number.Message, "object");

            var part = Assert.ThrowsException<PortalException>(() => UnifiedLogDuration.Parse(Json("{\"weeks\": 1}"), "Settings.LogTimePeriod"));
            StringAssert.Contains(part.Message, "weeks");
            StringAssert.Contains(part.Message, "days, hours");

            Assert.ThrowsException<PortalException>(() => UnifiedLogDuration.Parse(Json("{\"days\": -1}"), "Settings.LogTimePeriod"));
            Assert.ThrowsException<PortalException>(() => UnifiedLogDuration.Parse(Json("{\"days\": \"7\"}"), "Settings.LogTimePeriod"));
        }

        [TestMethod]
        public void Test_1802_ConvertHmiValue_ReadsIsoDateAndTime()
        {
            var value = Portal.ConvertHmiValue(Json("\"2026-01-02T03:04:05\""), typeof(DateTime), "SegmentStartTime");

            Assert.AreEqual(new DateTime(2026, 1, 2, 3, 4, 5), value);
        }

        [TestMethod]
        public void Test_1803_ConvertHmiValue_ReadsTimeSpanAsTextOrSeconds()
        {
            Assert.AreEqual(TimeSpan.FromSeconds(30), Portal.ConvertHmiValue(Json("\"00:00:30\""), typeof(TimeSpan), "AggregationDelay"));
            Assert.AreEqual(TimeSpan.FromSeconds(90), Portal.ConvertHmiValue(Json("90"), typeof(TimeSpan), "AggregationDelay"));
            Assert.AreEqual(new TimeSpan(1, 2, 3, 4), Portal.ConvertHmiValue(Json("\"1.02:03:04\""), typeof(TimeSpan), "AggregationDelay"));
        }

        [TestMethod]
        public void Test_1804_ConvertHmiValue_SaysWhatDateAndTimeSpanShouldLookLike()
        {
            var date = Assert.ThrowsException<PortalException>(() => Portal.ConvertHmiValue(Json("\"tomorrow\""), typeof(DateTime), "SegmentStartTime"));
            StringAssert.Contains(date.Message, "SegmentStartTime");
            StringAssert.Contains(date.Message, "ISO");

            var span = Assert.ThrowsException<PortalException>(() => Portal.ConvertHmiValue(Json("\"soon\""), typeof(TimeSpan), "SmoothingMinTime"));
            StringAssert.Contains(span.Message, "SmoothingMinTime");
            StringAssert.Contains(span.Message, "00:00:30");
        }
    }
}
