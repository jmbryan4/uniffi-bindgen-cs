// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Xunit.v3;

namespace UniffiCS.BindingTests;

public class TestDiagnosticTestStartEnd
{
    // Helper: get the _starts dictionary from the attribute via reflection.
    private static ConcurrentDictionary<IXunitTest, long> GetStarts(DiagnosticTestStartEndAttribute attr) =>
        (ConcurrentDictionary<IXunitTest, long>)typeof(DiagnosticTestStartEndAttribute)
            .GetField("_starts", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(attr)!;

    // Helper: get the current xUnit test as IXunitTest (the v2-shim exposes Test as ITest
    // at compile time, but the runtime object implements IXunitTest).
    private static IXunitTest CurrentTest() =>
        (IXunitTest)(object)TestContext.Current.Test!;

    private static MethodInfo AnyMethod() =>
        typeof(TestDiagnosticTestStartEnd).GetMethod(nameof(DictionaryIsEmptyAfterEachCycle))!;

    // Verifies that each Before()/After() pair leaves the dictionary empty — no state
    // leaks between consecutive invocations of the same attribute instance.
    [Fact]
    public void DictionaryIsEmptyAfterEachCycle()
    {
        var attr = new DiagnosticTestStartEndAttribute();
        var starts = GetStarts(attr);
        var test = CurrentTest();
        var method = AnyMethod();

        Assert.Empty(starts);

        // First cycle
        attr.Before(method, test);
        Assert.Single(starts);
        Thread.Sleep(30);
        attr.After(method, test);
        Assert.Empty(starts);

        // Second cycle — the dictionary must remain clean, with no accumulated state
        attr.Before(method, test);
        Assert.Single(starts);
        Thread.Sleep(15);
        attr.After(method, test);
        Assert.Empty(starts);
    }

    // Verifies the elapsed time stored per-invocation is a fresh snapshot, not a
    // cumulative value from previous cycles.
    [Fact]
    public void ElapsedIsResetOnEachInvocation()
    {
        var attr = new DiagnosticTestStartEndAttribute();
        var starts = GetStarts(attr);
        var test = CurrentTest();
        var method = AnyMethod();

        // First Before(): store timestamp
        attr.Before(method, test);
        Thread.Sleep(60);
        long ts1 = starts[test]; // timestamp captured at first Before()
        attr.After(method, test);

        // Gap between invocations
        Thread.Sleep(30);

        // Second Before(): must store a NEW timestamp, not carry over from first
        attr.Before(method, test);
        long ts2 = starts[test]; // timestamp captured at second Before()
        attr.After(method, test);

        // ts2 must be strictly after ts1 (i.e., it was freshly taken at the second call,
        // not inherited). The gap between them is at least the 30ms sleep above.
        Assert.True(
            ts2 > ts1,
            $"Second Before() timestamp ({ts2}) should be after first Before() timestamp ({ts1}).");

        // The elapsed from ts2 through After() should be ~15ms (just the second sleep),
        // not ~105ms (cumulative). We can't read After()'s reported value, but we can
        // verify that the timestamp delta between the two Befores is positive and
        // corresponds to at least the sleep gap.
        var gapMs = Stopwatch.GetElapsedTime(ts1, ts2).TotalMilliseconds;
        Assert.True(gapMs >= 25, $"Gap between first and second Before() should be ≥ 25ms, was {gapMs:F1}ms");
    }

    // Smoke test: 100 simulated invocations leave no dictionary entries behind.
    [Fact]
    public void DictionaryStaysEmptyAcrossHundredSimulatedInvocations()
    {
        var attr = new DiagnosticTestStartEndAttribute();
        var starts = GetStarts(attr);
        var test = CurrentTest();
        var method = AnyMethod();

        for (int i = 0; i < 100; i++)
        {
            attr.Before(method, test);
            attr.After(method, test);
            Assert.Empty(starts);
        }
    }
}
