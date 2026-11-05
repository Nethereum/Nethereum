using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Engine;
using Nethereum.CoreChain.UnitTests.Engine;
using Nethereum.CoreChain.UnitTests.Import;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs.Engine;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.S5.Round1
{
    public class S5R1EngineFollowerTests
    {
        [Fact]
        public async Task Given_a_peer_that_answers_a_header_request_with_a_header_of_another_hash_When_forkchoiceUpdated_names_that_hash_Then_the_forged_header_is_not_published_as_the_sync_target()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(SideChainHarness.RecipientA, 3);
            using var follower = await EngineFollowerHarness.CreateAsync();
            var requested = chain[0].Hash;
            follower.PeerHeaders[requested.ToHex(true)] = chain[2].Header;

            await follower.ForkchoiceAsync(requested);

            foreach (var published in follower.Sink.Published)
            {
                Assert.False(published.HeadNumber == 3UL && published.Head.SequenceEqual(requested),
                    "head " + published.Head.ToHex(true) + " was published with the number of a different block");
            }
        }

        [Fact]
        public async Task Given_a_peer_that_answers_a_finalized_header_request_with_a_header_of_another_hash_When_forkchoiceUpdated_Then_the_forged_finalized_header_is_not_published()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(SideChainHarness.RecipientA, 3);
            using var follower = await EngineFollowerHarness.CreateAsync();
            follower.PeerHeaders[chain[0].Hash.ToHex(true)] = chain[2].Header;
            follower.PeerHeaders[chain[1].Hash.ToHex(true)] = chain[1].Header;

            await follower.ForkchoiceAsync(chain[1].Hash, chain[0].Hash);

            Assert.DoesNotContain(follower.Sink.Published, p => p.Finalized != null && p.Finalized.SequenceEqual(chain[0].Hash));
        }

        private static async Task<(EngineFollowerHarness Follower, RpcContext Context)> NewContextAsync()
        {
            var follower = await EngineFollowerHarness.CreateAsync();
            var services = new ServiceCollection();
            services.AddSingleton<IEngineApiService>(follower.Service);
            var node = follower.Store.Node;
            var context = new RpcContext(node, node.Config.ChainId, services.BuildServiceProvider());
            return (follower, context);
        }

        [Fact]
        public async Task Given_a_valid_payload_and_expected_blob_versioned_hashes_that_do_not_match_its_transactions_When_engine_newPayloadV3_Then_the_status_is_INVALID()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(SideChainHarness.RecipientA, 1);
            var (follower, context) = await NewContextAsync();
            using (follower)
            {
                await follower.ForkchoiceAsync(follower.GenesisHash);
                var payload = EngineFollowerHarness.PayloadOf(chain[0]);
                var expected = new[] { "0x01" + new string('a', 62) };

                var response = await new EngineNewPayloadV3Handler().HandleAsync(
                    new RpcRequestMessage(1, "engine_newPayloadV3", payload, expected, EngineFollowerHarness.ZeroRoot), context);

                var status = Assert.IsType<PayloadStatusV1>(response.Result);
                Assert.Equal(EnginePayloadStatus.Invalid, status.Status);
            }
        }

        [Fact]
        public async Task Given_a_payload_with_a_transaction_that_does_not_decode_When_newPayload_Then_the_answer_is_an_INVALID_status_not_an_exception()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(SideChainHarness.RecipientA, 1);
            using var follower = await EngineFollowerHarness.CreateAsync();
            await follower.ForkchoiceAsync(follower.GenesisHash);
            var payload = EngineFollowerHarness.PayloadOf(chain[0]);
            payload.Transactions = new List<string> { "0x03ff" };

            var status = await follower.Service.NewPayloadAsync(payload, EngineFollowerHarness.ZeroRoot);

            Assert.Equal(EnginePayloadStatus.Invalid, status.Status);
        }
    }
}
