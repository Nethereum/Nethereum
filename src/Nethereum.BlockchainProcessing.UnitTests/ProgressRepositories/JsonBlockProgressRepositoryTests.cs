using Nethereum.BlockchainProcessing.ProgressRepositories;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.BlockchainProcessing.UnitTests.ProgressRepositories
{
    public class JsonBlockProgressRepositoryTests
    {
        private string CreateJsonFilePath()
        {
            return Path.ChangeExtension(Path.GetTempFileName(), "json");
        }

        [Fact]
        public async Task Persists_And_Returns_Last_Block_Processed()
        {
            var filePath = CreateJsonFilePath();
            DeleteFile(filePath);
            try
            {
                JsonBlockProgressRepository repo = CreateRepository(filePath);

                Assert.Null(await repo.GetLastBlockNumberProcessedAsync());

                await repo.UpsertProgressAsync((ulong)1);
                Assert.Equal(1, await repo.GetLastBlockNumberProcessedAsync());

                //recreate repo - should read existing file
                repo = CreateRepository(filePath);

                Assert.Equal(1, await repo.GetLastBlockNumberProcessedAsync());

            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Fact]
        public async Task Last_Block_Processed_Can_Be_Initialised()
        {
            var filePath = CreateJsonFilePath();
            DeleteFile(filePath);
            try
            {
                JsonBlockProgressRepository repo = CreateRepository(filePath, 10);

                Assert.Equal(10, await repo.GetLastBlockNumberProcessedAsync());

                await repo.UpsertProgressAsync(11);

                Assert.Equal(11, await repo.GetLastBlockNumberProcessedAsync());
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Fact]
        public async Task When_Initial_Last_Block_Processed_Is_Less_Than_Repo_Returns_Repo_Value()
        {
            var filePath = CreateJsonFilePath();
            DeleteFile(filePath);
            try
            {
                JsonBlockProgressRepository repo = CreateRepository(filePath);
                await repo.UpsertProgressAsync(11);

                repo = CreateRepository(filePath, 10);
                Assert.Equal(11, await repo.GetLastBlockNumberProcessedAsync());
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        private static JsonBlockProgressRepository CreateRepository(string filePath, BigInteger? lastBlockProcessed = null)
        {
            return new JsonBlockProgressRepository(
                async () => await Task.FromResult(File.Exists(filePath)),
                async (json) => await File.WriteAllTextAsync(filePath, json),
                async () => await File.ReadAllTextAsync(filePath),
                lastBlockProcessed);
        }

        private static void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
