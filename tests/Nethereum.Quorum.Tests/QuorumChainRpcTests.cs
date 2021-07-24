using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.Quorum.Tests
{
    //Tests depend on the 7 nodes sample of Quorum
    public class QuorumChainRpcTests
    {
        [Fact]
        public async void ShouldReturnCanonicalHashForBlockNumber1()
        {
            var web3 = new Web3Quorum(DefaultSettings.GetDefaultUrl());
            var canonicalHash = await web3.Quorum.CanonicalHash.SendRequestAsync(1);
            Assert.Equal("0x8bb911238205c6d5e9841335c9c5aff3dfae4c0f6b0df28100737c2660a15f8d", canonicalHash);
        }

        [Fact]
        public async void ShouldReturnTrueWhenCallingIsBlockMakerForCoinbase()
        {
            var web3 = new Web3Quorum(DefaultSettings.GetDefaultUrl());
            var isBlockMaker = await web3.Quorum.IsBlockMaker.SendRequestAsync(await web3.Eth.CoinBase.SendRequestAsync());
            Assert.Equal(true, isBlockMaker);
        }

        [Fact]
        public async void ShouldReturnTrueWhenCallingIsVoterForCoinbase()
        {
            var web3 = new Web3Quorum(DefaultSettings.GetDefaultUrl());
            var isVoter = await web3.Quorum.IsVoter.SendRequestAsync(await web3.Eth.CoinBase.SendRequestAsync());
            Assert.Equal(true, isVoter);
        }

        [Fact]
        public async void ShouldBeAbleToMakeBlocksBypassingBlockStrategy()
        {
            //Node2 is a Block Maker
            //This might fail.. depending on the canonicalhash.. (quorum)            
            var web3 = new Web3Quorum(DefaultSettings.QuorumIPAddress + ":22001");
            var hash = await web3.Quorum.MakeBlock.SendRequestAsync();
            Assert.NotNull(hash);
        }

        [Fact]
        public async void ShouldBeAbleToVote()
        {
            //Node 5 can vote
            var web3 = new Web3Quorum(DefaultSettings.QuorumIPAddress + ":22004");
            var block =
                await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());

            var hash = await web3.Quorum.Vote.SendRequestAsync(block.BlockHash);
            Assert.NotNull(hash);
        }

        [Fact]
        public async void ShouldPauseResumeBlockMaker()
        {
            //Node 2 is a BlockMaker
       
            var web3 = new Web3Quorum(DefaultSettings.QuorumIPAddress + ":22001");
            await web3.Quorum.PauseBlockMaker.SendRequestAsync();
            await web3.Quorum.ResumeBlockMaker.SendRequestAsync();
        }

        [Fact]
        public async void ShouldGetNodeInfoOfBlockMaker()
        {
            var web3 = new Web3Quorum(DefaultSettings.QuorumIPAddress + ":22001");
            var nodeInfo = await web3.Quorum.NodeInfo.SendRequestAsync();
            Assert.Equal("active", nodeInfo.BlockMakeStratregy.Status);
        }
    }
}