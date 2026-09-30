using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class LegacyFailureClassifier
    {
        private readonly ITestOutputHelper _output;
        private readonly GethEvmRunner _gethRunner;

        public LegacyFailureClassifier(ITestOutputHelper output)
        {
            _output = output;
            _gethRunner = new GethEvmRunner();
        }

        public async Task<List<FailureClassification>> ClassifyCategoryAsync(string categoryDir, string fork)
        {
            var results = new List<FailureClassification>();
            if (!Directory.Exists(categoryDir)) return results;

            foreach (var file in Directory.GetFiles(categoryDir, "*.json"))
            {
                var perFile = await ClassifyFileAsync(file, fork);
                results.AddRange(perFile);
            }
            return results;
        }

        public async Task<List<FailureClassification>> ClassifyFileAsync(string filePath, string fork)
        {
            var results = new List<FailureClassification>();
            var runner = new GeneralStateTestRunner(_output, targetHardfork: fork);
            TestResult fileResult;
            try
            {
                fileResult = await runner.RunTestWithExecutorAsync(filePath, specificDataIndex: null, captureTraces: false);
            }
            catch (Exception ex)
            {
                results.Add(new FailureClassification
                {
                    FilePath = filePath,
                    Fork = fork,
                    Class = FailureClass.RUNNER_EXCEPTION,
                    Detail = ex.Message
                });
                return results;
            }

            foreach (var r in fileResult.Results)
            {
                if (r.Passed || r.Skipped) continue;

                var classification = await ClassifySubTestAsync(filePath, r.DataIndex, r.GasIndex, r.ValueIndex, fork);
                classification.TestName = r.TestName;
                results.Add(classification);
            }
            return results;
        }

        public async Task<FailureClassification> ClassifySubTestAsync(string filePath, int dataIndex, int gasIndex, int valueIndex, string fork)
        {
            var c = new FailureClassification
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                Fork = fork,
                DataIndex = dataIndex,
                GasIndex = gasIndex,
                ValueIndex = valueIndex
            };

            var runner = new GeneralStateTestRunner(_output, targetHardfork: fork);
            TestResult nethResult;
            try
            {
                nethResult = await runner.RunTestWithExecutorAsync(filePath, specificDataIndex: dataIndex, captureTraces: true);
            }
            catch (Exception ex)
            {
                c.Class = FailureClass.RUNNER_EXCEPTION;
                c.Detail = ex.Message;
                return c;
            }

            var single = nethResult.Results.FirstOrDefault(r => r.DataIndex == dataIndex && r.GasIndex == gasIndex && r.ValueIndex == valueIndex)
                         ?? nethResult.Results.FirstOrDefault();

            if (single == null)
            {
                c.Class = FailureClass.NETH_SKIPPED;
                c.Detail = "No matching sub-test in re-run";
                return c;
            }
            if (single.Skipped)
            {
                c.Class = FailureClass.NETH_SKIPPED;
                c.Detail = single.SkipReason;
                return c;
            }
            if (single.Passed)
            {
                c.Class = FailureClass.NOW_PASSES;
                return c;
            }
            c.ExpectedStateRoot = single.ExpectedStateRoot;
            c.ActualStateRoot = single.ActualStateRoot;

            if (single.Traces == null || single.Traces.Count == 0)
            {
                c.Class = FailureClass.NETH_NO_TRACE;
                c.Detail = "Runner produced no trace — cannot classify";
                return c;
            }

            GethEvmResult gethResult;
            try
            {
                gethResult = await _gethRunner.RunStateTestAsync(filePath, dataIndex, gasIndex, valueIndex, fork);
            }
            catch (Exception ex)
            {
                c.Class = FailureClass.GETH_FAILED;
                c.Detail = ex.Message;
                return c;
            }
            if (!gethResult.Success || gethResult.Steps == null || gethResult.Steps.Count == 0)
            {
                c.Class = FailureClass.GETH_FAILED;
                c.Detail = gethResult.Error ?? "No steps captured";
                return c;
            }

            var comparer = new TraceComparer();
            var nethSteps = comparer.NormalizeNethTrace(single.Traces);
            var validator = new TraceValidator();
            var validation = validator.Validate(gethResult.Steps, nethSteps);
            c.GethStepCount = gethResult.Steps.Count;
            c.NethStepCount = nethSteps.Count;

            if (validation.IsValid)
            {
                c.Class = FailureClass.POST_EVM_DIVERGENCE;
                c.Detail = $"Traces match ({nethSteps.Count} steps); divergence is post-execution";
            }
            else
            {
                c.Class = FailureClass.EVM_OPCODE_DIFF;
                c.FirstDiffStep = validation.FirstMismatch?.StepIndex ?? 0;
                c.FirstDiffOpcode = validation.FirstMismatch?.GethStep?.Op ?? validation.FirstMismatch?.NethStep?.Op;
                c.FirstDiffField = validation.FirstMismatch?.Field;
                c.GethValue = validation.FirstMismatch?.GethValue;
                c.NethValue = validation.FirstMismatch?.NethValue;
                c.Detail = $"Step {c.FirstDiffStep} {c.FirstDiffOpcode}: {c.FirstDiffField} geth={c.GethValue} neth={c.NethValue}";
            }
            return c;
        }
    }

    public enum FailureClass
    {
        EVM_OPCODE_DIFF,
        POST_EVM_DIVERGENCE,
        NOW_PASSES,
        NETH_SKIPPED,
        NETH_NO_TRACE,
        GETH_FAILED,
        RUNNER_EXCEPTION
    }

    public class FailureClassification
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string TestName { get; set; }
        public string Fork { get; set; }
        public int DataIndex { get; set; }
        public int GasIndex { get; set; }
        public int ValueIndex { get; set; }
        public FailureClass Class { get; set; }
        public int FirstDiffStep { get; set; }
        public string FirstDiffOpcode { get; set; }
        public string FirstDiffField { get; set; }
        public string GethValue { get; set; }
        public string NethValue { get; set; }
        public string ExpectedStateRoot { get; set; }
        public string ActualStateRoot { get; set; }
        public int GethStepCount { get; set; }
        public int NethStepCount { get; set; }
        public string Detail { get; set; }

        public string ToCsv() => string.Join(",",
            CsvEscape(Fork),
            CsvEscape(FileName),
            DataIndex, GasIndex, ValueIndex,
            Class.ToString(),
            FirstDiffStep,
            CsvEscape(FirstDiffOpcode),
            CsvEscape(FirstDiffField),
            CsvEscape(GethValue),
            CsvEscape(NethValue),
            GethStepCount, NethStepCount,
            CsvEscape(Detail));

        public static string CsvHeader() => "Fork,FileName,DataIndex,GasIndex,ValueIndex,Class,FirstDiffStep,FirstDiffOpcode,FirstDiffField,GethValue,NethValue,GethStepCount,NethStepCount,Detail";

        private static string CsvEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
