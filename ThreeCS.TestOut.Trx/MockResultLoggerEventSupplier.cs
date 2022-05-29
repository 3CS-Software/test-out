using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System;

namespace ThreeCS.TestOut.Trx
{
    /// <summary>
    /// Implements TestLoggerEvents in a way that they can just be invoked directly.
    /// </summary>
    /// <remarks>
    /// TODO: is there some other way to do this?.. I suppose if we plugin to dotnet test architecture, we wouldn't need this.
    /// </remarks>
    internal class MockResultLoggerEventSupplier : TestLoggerEvents
    {
        public void OnTestRunMessage(TestRunMessageEventArgs e) => TestRunMessage?.Invoke(this, e);
        public override event EventHandler<TestRunMessageEventArgs> TestRunMessage;

        public void OnTestRunStart(TestRunStartEventArgs e) => TestRunStart?.Invoke(this, e);
        public override event EventHandler<TestRunStartEventArgs> TestRunStart;

        public void OnTestResult(TestResultEventArgs e) => TestResult?.Invoke(this, e);
        public override event EventHandler<TestResultEventArgs> TestResult;

        public void OnTestRunComplete(TestRunCompleteEventArgs e) => TestRunComplete?.Invoke(this, e);
        public override event EventHandler<TestRunCompleteEventArgs> TestRunComplete;


        public override event EventHandler<DiscoveryStartEventArgs> DiscoveryStart;
        public override event EventHandler<TestRunMessageEventArgs> DiscoveryMessage;
        public override event EventHandler<DiscoveredTestsEventArgs> DiscoveredTests;
        public override event EventHandler<DiscoveryCompleteEventArgs> DiscoveryComplete;
    }
}
