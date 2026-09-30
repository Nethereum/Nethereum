using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Integration
{
    [Trait("Category", "Integration")]
    public class GethEth68Tests
    {
        private string GetEnode() =>
            Environment.GetEnvironmentVariable("GETH_ENODE")
            ?? throw new SkipException("Set GETH_ENODE to run integration tests");

        private string GetRpcUrl() =>
            Environment.GetEnvironmentVariable("GETH_RPC_URL") ?? "http://127.0.0.1:8545";

        [Fact]
        public async Task ExchangeStatus_WithGeth()
        {
            var web3 = new Nethereum.Web3.Web3(GetRpcUrl());
            var genesis = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber
                .SendRequestAsync(BlockParameter.CreateEarliest());
            var genesisHash = genesis.BlockHash.HexToByteArray();
            var netVersion = await web3.Net.Version.SendRequestAsync();
            var networkId = ulong.Parse(netVersion);

            var enode = GetEnode();
            var connector = new StaticPeerConnector();
            var conn = await connector.ConnectAsync(enode);
            var ethOffset = conn.GetCapabilityOffset("eth");

            var localStatus = new Eth68StatusMessage
            {
                ProtocolVersion = 68,
                NetworkId = networkId,
                TotalDifficulty = BigInteger.One,
                BestHash = genesisHash,
                GenesisHash = genesisHash,
                ForkHash = ForkId.ComputeHash(genesisHash, Array.Empty<ulong>()),
                ForkNext = 0
            };

            await conn.SendMessageAsync(
                ethOffset + Eth68MessageIds.Status,
                Eth68StatusMessageEncoder.Encode(localStatus));

            var (msgId, payload) = await conn.ReceiveMessageAsync();
            Assert.Equal(ethOffset + Eth68MessageIds.Status, msgId);

            var remoteStatus = Eth68StatusMessageEncoder.Decode(payload);
            Assert.Equal(68, remoteStatus.ProtocolVersion);
            Assert.Equal(genesisHash, remoteStatus.GenesisHash);

            await conn.DisconnectAsync();
        }

        [Fact]
        public async Task GetBlockHeaders_FromGeth()
        {
            var web3 = new Nethereum.Web3.Web3(GetRpcUrl());
            var genesis = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber
                .SendRequestAsync(BlockParameter.CreateEarliest());
            var genesisHash = genesis.BlockHash.HexToByteArray();
            var netVersion = await web3.Net.Version.SendRequestAsync();
            var networkId = ulong.Parse(netVersion);

            var enode = GetEnode();
            var connector = new StaticPeerConnector();
            var conn = await connector.ConnectAsync(enode);
            var ethOffset = conn.GetCapabilityOffset("eth");

            var localStatus = new Eth68StatusMessage
            {
                ProtocolVersion = 68,
                NetworkId = networkId,
                TotalDifficulty = BigInteger.One,
                BestHash = genesisHash,
                GenesisHash = genesisHash,
                ForkHash = ForkId.ComputeHash(genesisHash, Array.Empty<ulong>()),
                ForkNext = 0
            };

            await conn.SendMessageAsync(
                ethOffset + Eth68MessageIds.Status,
                Eth68StatusMessageEncoder.Encode(localStatus));
            await conn.ReceiveMessageAsync();

            var getHeaders = new GetBlockHeadersMessage
            {
                RequestId = 1,
                StartBlock = 0,
                Limit = 5,
                Skip = 0,
                Reverse = false
            };

            await conn.SendMessageAsync(
                ethOffset + Eth68MessageIds.GetBlockHeaders,
                GetBlockHeadersMessageEncoder.Encode(getHeaders));

            var (headersMsgId, headersPayload) = await conn.ReceiveMessageAsync();
            Assert.Equal(ethOffset + Eth68MessageIds.BlockHeaders, headersMsgId);

            var headersMsg = BlockHeadersMessageEncoder.Decode(headersPayload);
            Assert.Equal(1ul, headersMsg.RequestId);
            Assert.True(headersMsg.Headers.Count > 0);

            await conn.DisconnectAsync();
        }
    }
}
