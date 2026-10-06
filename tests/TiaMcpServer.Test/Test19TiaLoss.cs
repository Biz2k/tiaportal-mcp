using System;
using System.Reflection;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for recognising a TIA Portal that is gone after an Openness call. The exception
    /// types are stand-ins with the names Openness uses: the real ones need the Siemens
    /// assemblies. These tests do not connect to TIA Portal.
    /// </summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test19TiaLoss
    {
        // Same name as Siemens.Engineering.NonRecoverableException.
        private class NonRecoverableException : Exception
        {
            public NonRecoverableException(string message) : base(message)
            {
            }
        }

        private class DerivedFatal : NonRecoverableException
        {
            public DerivedFatal(string message) : base(message)
            {
            }
        }

        private const string DisposedText = "Access to a disposed object of type 'Siemens.Engineering.Project' is not possible.";

        [TestMethod]
        public void Test_1900_Classify_NonRecoverableExceptionIsFatal()
        {
            Assert.AreEqual(TiaLoss.Kind.Fatal, TiaLoss.Classify(new NonRecoverableException("The call cannot be recovered from.")));
        }

        [TestMethod]
        public void Test_1901_Classify_FindsItBehindTheReflectionWrappers()
        {
            var wrapped = new TargetInvocationException(new InvalidOperationException("outer", new NonRecoverableException("inner")));

            Assert.AreEqual(TiaLoss.Kind.Fatal, TiaLoss.Classify(wrapped));
            Assert.AreEqual(TiaLoss.Kind.Fatal, TiaLoss.Classify(new AggregateException(new NotSupportedException(), new DerivedFatal("derived"))));
        }

        [TestMethod]
        public void Test_1902_Classify_DisposedObjectIsRecognisedByItsText()
        {
            Assert.AreEqual(TiaLoss.Kind.Disposed, TiaLoss.Classify(new InvalidOperationException(DisposedText)));
            Assert.AreEqual(TiaLoss.Kind.Disposed, TiaLoss.Classify(new Exception("wrapper", new ObjectDisposedException("Project", DisposedText))));
        }

        [TestMethod]
        public void Test_1903_Classify_FatalWinsOverDisposed()
        {
            var both = new InvalidOperationException(DisposedText, new NonRecoverableException("fatal"));

            Assert.AreEqual(TiaLoss.Kind.Fatal, TiaLoss.Classify(both));
        }

        [TestMethod]
        public void Test_1904_Classify_OrdinaryFailuresAreLeftAlone()
        {
            Assert.AreEqual(TiaLoss.Kind.None, TiaLoss.Classify(null));
            Assert.AreEqual(TiaLoss.Kind.None, TiaLoss.Classify(new InvalidOperationException("The property cannot be set")));
            Assert.AreEqual(TiaLoss.Kind.None, TiaLoss.Classify(new PortalException(PortalErrorCode.NotFound, "Screen 'A' not found.")));
        }

        [TestMethod]
        public void Test_1905_Classify_SurvivesAnEndlessChain()
        {
            var ex = new Exception("0");

            for (var i = 1; i < 50; i++)
            {
                ex = new Exception(i.ToString(), ex);
            }

            Assert.AreEqual(TiaLoss.Kind.None, TiaLoss.Classify(ex));
        }

        [TestMethod]
        public void Test_1906_GoneMessage_SaysWhatHappenedAndWhatToDo()
        {
            var fatal = TiaLoss.GoneMessage(TiaLoss.Kind.Fatal);

            StringAssert.Contains(fatal, "closed by the call");
            StringAssert.Contains(fatal, "Unsaved changes");
            StringAssert.Contains(fatal, "'connect'");

            var gone = TiaLoss.GoneMessage(TiaLoss.Kind.Disposed);

            StringAssert.Contains(gone, "no longer running");
            StringAssert.Contains(gone, "'connect'");
        }

        [TestMethod]
        public void Test_1907_ClosedProjectMessage_PointsAtGetState()
        {
            var text = TiaLoss.ClosedProjectMessage();

            StringAssert.Contains(text, "still running");
            StringAssert.Contains(text, "'get_state'");
        }
    }
}
