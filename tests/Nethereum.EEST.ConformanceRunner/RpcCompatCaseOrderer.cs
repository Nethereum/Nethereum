using System;
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class RpcCompatCaseOrderer : ITestCaseOrderer
    {
        public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
            where TTestCase : ITestCase
        {
            return testCases.OrderBy(t => CaseId(t), StringComparer.Ordinal);
        }

        private static string CaseId(ITestCase testCase) =>
            (testCase as IXunitTestCase)?.TestMethodArguments?.FirstOrDefault() as string ?? testCase.DisplayName;
    }
}
