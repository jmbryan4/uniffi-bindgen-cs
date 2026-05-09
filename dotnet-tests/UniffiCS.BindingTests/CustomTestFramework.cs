// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Xunit.Sdk;
using Xunit.v3;

[assembly: TestPipelineStartup(typeof(UniffiCS.BindingTests.DiagnosticTestPipelineStartup))]
[assembly: UniffiCS.BindingTests.DiagnosticTestStartEnd]

namespace UniffiCS.BindingTests;

// Applied at assembly scope: one instance is reused for every test. A shared Stopwatch
// would accumulate elapsed time across tests; a ConcurrentDictionary keyed by IXunitTest
// keeps state per invocation and is safe if parallelization is ever re-enabled.
public sealed class DiagnosticTestStartEndAttribute : BeforeAfterTestAttribute
{
    private readonly ConcurrentDictionary<IXunitTest, long> _starts = new();

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        _starts[test] = Stopwatch.GetTimestamp();
        TestContext.Current.SendDiagnosticMessage("STARTED: {0}.{1}", test.TestCase.TestClassName, test.TestCase.TestMethodName);
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        if (_starts.TryRemove(test, out long start))
        {
            var elapsed = Stopwatch.GetElapsedTime(start);
            TestContext.Current.SendDiagnosticMessage("FINISHED: {0}.{1} ({2:F3}s)", test.TestCase.TestClassName, test.TestCase.TestMethodName, elapsed.TotalSeconds);
        }
    }
}

public sealed class DiagnosticTestPipelineStartup : ITestPipelineStartup
{
    public ValueTask StartAsync(IMessageSink diagnosticMessageSink)
    {
        diagnosticMessageSink.OnMessage(new DiagnosticMessage { Message = "Using DiagnosticTestPipelineStartup" });
        return default;
    }

    public ValueTask StopAsync() => default;
}
