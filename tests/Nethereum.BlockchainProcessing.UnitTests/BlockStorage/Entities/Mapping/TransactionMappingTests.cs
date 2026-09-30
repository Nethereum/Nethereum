using System.Collections.Generic;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using Xunit;

namespace Nethereum.BlockchainProcessing.UnitTests.BlockStorage.Entities.Mapping
{
    public class TransactionMappingTests
    {
        [Fact]
        public void Maps_AuthorisationList_To_AuthorizationList_Json()
        {
            var source = new RPC.Eth.DTOs.Transaction
            {
                BlockNumber = new HexBigInteger(100),
                AuthorisationList = new List<RPC.Eth.DTOs.Authorisation>
                {
                    new RPC.Eth.DTOs.Authorisation
                    {
                        ChainId = new HexBigInteger(1),
                        Address = "0x5f236f062f16a9b19819c535127398df9a01d762",
                        Nonce = new HexBigInteger(0),
                        YParity = "0x0",
                        R = "0x01",
                        S = "0x02"
                    }
                }
            };

            var transaction = new Transaction();
            transaction.Map(source);

            Assert.NotNull(transaction.AuthorizationList);

            var deserialized = JsonConvert.DeserializeObject<List<RPC.Eth.DTOs.Authorisation>>(transaction.AuthorizationList);
            Assert.Single(deserialized);
            Assert.Equal(source.AuthorisationList[0].Address, deserialized[0].Address);
            Assert.Equal(source.AuthorisationList[0].R, deserialized[0].R);
            Assert.Equal(source.AuthorisationList[0].S, deserialized[0].S);
        }

        [Fact]
        public void Leaves_AuthorizationList_Null_When_Source_Has_No_Authorisations()
        {
            var source = new RPC.Eth.DTOs.Transaction
            {
                BlockNumber = new HexBigInteger(100)
            };

            var transaction = new Transaction();
            transaction.Map(source);

            Assert.Null(transaction.AuthorizationList);
        }
    }
}
