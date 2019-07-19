using Nethereum.BlockchainProcessing.LogProcessing;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Nethereum.BlockchainProcessing.IntegrationTests.LogProcessing
{
    public class BlockRangeRequestStrategyTests
    {
        [Fact]
        public void When_Range_Is_Within_Limits_Returns_Requested_Block()
        {
            var strategy = new BlockRangeRequestStrategy(10);
            Assert.Equal(10, strategy.GeBlockNumberToRequestTo(1, 10));
        }

        [Fact]
        public void When_Range_Exceeds_Limits_Returns_Requested_Block()
        {
            var strategy = new BlockRangeRequestStrategy(10);
            Assert.Equal(10, strategy.GeBlockNumberToRequestTo(1, 100));
        }

        [Fact]
        public void Adjusts_Max_Relative_To_Retries()
        {
            var strategy = new BlockRangeRequestStrategy(10);
            Assert.Equal(10, strategy.GeBlockNumberToRequestTo(1, 100, 1));
            Assert.Equal(5, strategy.GeBlockNumberToRequestTo(1, 100, 2));
            Assert.Equal(2, strategy.GeBlockNumberToRequestTo(1, 100, 3));
        }

        [Fact]
        public void At_Minimum_Will_Return_The_Next_Block()
        {
            var strategy = new BlockRangeRequestStrategy(10);
            //minimum
            Assert.Equal(2, strategy.GeBlockNumberToRequestTo(1, 100, 4));
        }
    }
}
