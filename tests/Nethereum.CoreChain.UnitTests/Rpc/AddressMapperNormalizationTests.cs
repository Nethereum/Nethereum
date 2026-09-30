using System.Collections.Generic;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Mappers;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class AddressMapperNormalizationTests
    {
        private const string Checksummed = "0xdAC17F958D2ee523a2206206994597C13D831ec7";
        private const string Canonical = "0xdac17f958d2ee523a2206206994597c13d831ec7";

        [Fact]
        public void Given_AccessListItem_When_ToRPCAccessList_Then_AddressCanonical()
        {
            var list = new List<AccessListItem> { new AccessListItem(Checksummed, new List<byte[]> { new byte[32] }) };

            var rpc = list.ToRPCAccessList();

            Assert.Equal(Canonical, rpc[0].Address);
        }

        [Fact]
        public void Given_Authorisation_When_ToRPCAuthorisation_Then_AddressCanonical()
        {
            var signed = new List<Authorisation7702Signed>
            {
                new Authorisation7702Signed(new EvmUInt256(1UL), Checksummed, new EvmUInt256(0UL),
                    new byte[32], new byte[32], new byte[] { 0 })
            };

            var rpc = signed.ToRPCAuthorisation();

            Assert.Equal(Canonical, rpc[0].Address);
        }
    }
}
