using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.IntegrationTests.MudTest.Tables;
using Nethereum.Mud.Repositories.EntityFramework;
using Nethereum.Mud.TableRepository;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.Mud.IntegrationTests
{
    public class TablePredicateBuilderTests
    {
        [Fact]
        public void ShouldCreateEfSqlHexPredicateWithAndEqualOrEqualAndNotEqual()
        {
            var predicateBuilder = new TablePredicateBuilder<ItemTableRecord, ItemTableRecord.ItemKey, ItemTableRecord.ItemValue>("0xABC123");

            // Define conditions using both AndEqual, OrEqual, and AndNotEqual
            var predicate = predicateBuilder
                .AndEqual(x => x.Id, 1)         // AND key0 = '0x1'
                .AndEqual(x => x.Id, 2)         // AND key0 = '0x2'
                .OrEqual(x => x.Id, 3)          // OR key0 = '0x3'
                .AndNotEqual(x => x.Id, 4)      // AND key0 != '0x4'
                .Expand();                      // Finalize the predicate

            // Use SqlPredicateBuilder to build the final SQL from the predicate
            var sqlBuilder = new EFSqlHexPredicateBuilder();
            var sql = sqlBuilder.BuildSql(predicate);

            // Assert the SQL matches the expected format
            Assert.Equal(
                @"(tableid = @p0 AND address = @p1 AND key0 = @p2) AND 
(tableid = @p3 AND address = @p4 AND key0 = @p5) OR 
(tableid = @p6 AND address = @p7 AND key0 = @p8) AND 
(tableid = @p9 AND address = @p10 AND key0 != @p11)".Replace("\r\n", ""),
                sql.Sql);

            var tableId = predicateBuilder.TableResourceIdEncoded.ToHex(true);

            Assert.Equal(12, sql.Parameters.Count);
            Assert.Equal(tableId, sql.Parameters["@p0"]);
            Assert.Equal(tableId, sql.Parameters["@p3"]);
            Assert.Equal(tableId, sql.Parameters["@p6"]);
            Assert.Equal(tableId, sql.Parameters["@p9"]);

            Assert.Equal("0xabc123", sql.Parameters["@p1"]);
            Assert.Equal("0xabc123", sql.Parameters["@p4"]);
            Assert.Equal("0xabc123", sql.Parameters["@p7"]);
            Assert.Equal("0xabc123", sql.Parameters["@p10"]);

            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000001", sql.Parameters["@p2"]);
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000002", sql.Parameters["@p5"]);
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000003", sql.Parameters["@p8"]);
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000004", sql.Parameters["@p11"]);
        }


        [Fact]
        public void ShouldCreateJsonPredicateWithAndEqualOrEqualAndNotEqual()
        {
            var predicateBuilder = new TablePredicateBuilder<ItemTableRecord, ItemTableRecord.ItemKey, ItemTableRecord.ItemValue>("0xABC123");

            // Define conditions using both AndEqual, OrEqual, and AndNotEqual
            var predicate = predicateBuilder
                .AndEqual(x => x.Id, 1)         // AND key0 = '0x1'
                .AndEqual(x => x.Id, 2)         // AND key0 = '0x2'
                .OrEqual(x => x.Id, 3)          // OR key0 = '0x3'
                .AndNotEqual(x => x.Id, 4)      // AND key0 != '0x4'
                .Expand();                      // Finalize the predicate

            var predicateJson = JsonConvert.SerializeObject(predicate);

            var expectedJson = @"{""CombineOperator"":""AND"",
                                    ""Conditions"":
                                                [{""Key"":""key0"",
                                                 ""AbiType"":""uint32"",
                                                 ""HexValue"":""0000000000000000000000000000000000000000000000000000000000000001"",
                                                 ""PropertyName"":""Id"",
                                                 ""Order"":1,
                                                 ""ComparisonOperator"":""="",
                                                 ""Name"":""id"",
                                                 ""TableId"":""0x746200000000000000000000000000004974656d000000000000000000000000"",
                                                 ""Address"":""0xABC123"",
                                                 ""UnionOperator"":""AND""},
                                                 {""Key"":""key0"",""AbiType"":""uint32"",""HexValue"":""0000000000000000000000000000000000000000000000000000000000000002"",""PropertyName"":""Id"",""Order"":1,""ComparisonOperator"":""="",""Name"":""id"",""TableId"":""0x746200000000000000000000000000004974656d000000000000000000000000"",""Address"":""0xABC123"",""UnionOperator"":""AND""},
                                                 {""Key"":""key0"",""AbiType"":""uint32"",""HexValue"":""0000000000000000000000000000000000000000000000000000000000000003"",""PropertyName"":""Id"",""Order"":1,""ComparisonOperator"":""="",""Name"":""id"",""TableId"":""0x746200000000000000000000000000004974656d000000000000000000000000"",""Address"":""0xABC123"",""UnionOperator"":""OR""},
                                                 {""Key"":""key0"",""AbiType"":""uint32"",""HexValue"":""0000000000000000000000000000000000000000000000000000000000000004"",""PropertyName"":""Id"",""Order"":1,""ComparisonOperator"":""!="",""Name"":""id"",""TableId"":""0x746200000000000000000000000000004974656d000000000000000000000000"",""Address"":""0xABC123"",""UnionOperator"":""AND""}],
                                    ""Groups"":[]}";
                
            Assert.True(JToken.DeepEquals(JObject.Parse(expectedJson), JObject.Parse(predicateJson)));
        }
    }

}
