using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class RecordingAsyncStateRunnerSkipDisciplineTests
    {
        private const string SourceCategory = "stExample";
        private const string SourceFixture = "yulExample";
        private const string Hardfork = "Prague";
        private const string FixtureLabel = SourceFixture + "[0,0,0]";

        [Fact]
        public async Task Given_AFixtureThatThrows_When_TheCategoryRuns_Then_TheSuiteFails()
        {
            using (var corpus = SingleFixtureCorpus.WithPostEntryField("txbytes", "0xdeadbeef"))
            {
                var output = new CapturedOutput();
                var runner = new RecordingAsyncStateTestRunner(output, corpus.VectorsRoot);

                await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(
                    () => runner.RunCategoryAsync(corpus.CategoryName, Hardfork));

                var log = output.ToString();
                Assert.Contains("Failed: 1,", log);
                Assert.Contains("Skipped: 0,", log);
                Assert.Contains(FixtureLabel, log);
            }
        }

        [Fact]
        public async Task Given_AFixtureLegitimatelyOutOfScope_When_TheCategoryRuns_Then_ItIsSkippedWithoutFailing()
        {
            using (var corpus = SingleFixtureCorpus.WithPostEntryField("txbytes", null))
            {
                var output = new CapturedOutput();
                var runner = new RecordingAsyncStateTestRunner(output, corpus.VectorsRoot);

                await runner.RunCategoryAsync(corpus.CategoryName, Hardfork);

                var log = output.ToString();
                Assert.Contains("Failed: 0,", log);
                Assert.Contains("Skipped: 1,", log);
                Assert.Contains("OUT OF SCOPE: " + FixtureLabel, log);
            }
        }

        private sealed class SingleFixtureCorpus : IDisposable
        {
            public string VectorsRoot { get; private set; }
            public string CategoryName { get; private set; }

            public static SingleFixtureCorpus WithPostEntryField(string field, string value)
            {
                var sourcePath = Path.Combine(
                    RecordingAsyncStateTestRunner.GetTestVectorsPath(), SourceCategory, SourceFixture + ".json");
                Assert.True(File.Exists(sourcePath), $"Source fixture not found: {sourcePath}");

                var fixture = JObject.Parse(File.ReadAllText(sourcePath));
                var postEntry = (JObject)fixture.Properties().First().Value["post"][Hardfork][0];
                if (value == null)
                    postEntry.Remove(field);
                else
                    postEntry[field] = value;

                var corpus = new SingleFixtureCorpus
                {
                    VectorsRoot = Path.Combine(Path.GetTempPath(), "nethereum-recording-async-" + Guid.NewGuid().ToString("N")),
                    CategoryName = "stSkipDiscipline"
                };
                var categoryPath = Path.Combine(corpus.VectorsRoot, corpus.CategoryName);
                Directory.CreateDirectory(categoryPath);
                File.WriteAllText(Path.Combine(categoryPath, SourceFixture + ".json"), fixture.ToString());
                return corpus;
            }

            public void Dispose()
            {
                try { Directory.Delete(VectorsRoot, recursive: true); } catch (IOException) { }
            }
        }

        private sealed class CapturedOutput : ITestOutputHelper
        {
            private readonly StringBuilder _lines = new StringBuilder();

            public void WriteLine(string message) => _lines.AppendLine(message);

            public void WriteLine(string format, params object[] args) => _lines.AppendLine(string.Format(format, args));

            public override string ToString() => _lines.ToString();
        }
    }
}
