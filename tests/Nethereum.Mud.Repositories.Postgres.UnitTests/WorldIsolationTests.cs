using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.EncodingDecoding;

namespace Nethereum.Mud.Repositories.Postgres.UnitTests
{
    public class WorldIsolationTests
    {
        private const string WorldA = "0x1111111111111111111111111111111111111111";
        private const string WorldB = "0x2222222222222222222222222222222222222222";
        private static readonly byte[] TableId = "0x0000000000000000000000000000000000000000000000000000000000000abc".HexToByteArray();
        private static readonly List<byte[]> Key = new() { "0x0000000000000000000000000000000000000000000000000000000000000001".HexToByteArray() };

        private static EncodedValues Values(byte value) => new EncodedValues
        {
            StaticData = new[] { value },
            EncodedLengths = new byte[0],
            DynamicData = new byte[0]
        };

        [Fact]
        public async Task SetRecord_WithTheSameTableAndKeyInTwoWorlds_KeepsBothRecords()
        {
            using var database = new SqliteMudDatabase();
            using var context = database.CreateContext();
            var repository = new MudPostgresStoreRecordsTableRepository(context);

            await repository.SetRecordAsync(TableId, Key, Values(1), WorldA, 1, 0);
            await repository.SetRecordAsync(TableId, Key, Values(2), WorldB, 2, 0);

            using var verify = database.CreateContext();
            var verifier = new MudPostgresStoreRecordsTableRepository(verify);
            var a = await verifier.GetRecordAsync(TableId.ToHex(true), Key[0].ToHex(true), WorldA);
            var b = await verifier.GetRecordAsync(TableId.ToHex(true), Key[0].ToHex(true), WorldB);
            Assert.NotNull(a);
            Assert.NotNull(b);
            Assert.Equal(new byte[] { 1 }, a.StaticData);
            Assert.Equal(new byte[] { 2 }, b.StaticData);
        }

        [Fact]
        public async Task DeleteRecord_InOneWorld_LeavesTheSameTableAndKeyInAnotherWorldUntouched()
        {
            using var database = new SqliteMudDatabase();
            using var context = database.CreateContext();
            var repository = new MudPostgresStoreRecordsTableRepository(context);
            await repository.SetRecordAsync(TableId, Key, Values(1), WorldA, 1, 0);
            await repository.SetRecordAsync(TableId, Key, Values(2), WorldB, 2, 0);

            await repository.DeleteRecordAsync(TableId, Key, WorldB, 3, 0);

            using var verify = database.CreateContext();
            var verifier = new MudPostgresStoreRecordsTableRepository(verify);
            var a = await verifier.GetRecordAsync(TableId.ToHex(true), Key[0].ToHex(true), WorldA);
            var b = await verifier.GetRecordAsync(TableId.ToHex(true), Key[0].ToHex(true), WorldB);
            Assert.False(a.IsDeleted);
            Assert.True(b.IsDeleted);
        }
    }
}
