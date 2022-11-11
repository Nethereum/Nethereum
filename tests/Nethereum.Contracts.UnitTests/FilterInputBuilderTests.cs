using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Linq;
using System.Numerics;
using Xunit;

namespace Nethereum.Contracts.UnitTests
{
    public class FilterInputBuilderTests
    {
        [Event("Transfer")]
        public class TransferEvent
        {
            [Parameter("address", "_from", 1, true)]
            public string From { get; set; }

            [Parameter("address", "_to", 2, true)] 
            public string To { get; set; }

            [Parameter("uint256", "_value", 3, true)]
            public BigInteger Value { get; set; }
            
            [Parameter("uint256", "_notIndexed", 3, false)]
            public uint NotIndexed { get; set; }

            public uint NotAnEventParameter { get; set; }
        }

        [Event("Transfer")]
        public class TransferEvent_ERC20
        {
            [Parameter("address", "_from", 1, true)]
            public string From { get; set; }

            [Parameter("address", "_to", 2, true)] 
            public string To { get; set; }

            [Parameter("uint256", "_value", 3, false)]
            public BigInteger Value { get; set; }
            
        }

        [Event("TransferEvent_WithEmptyParameterNames")]
        public class TransferEvent_WithEmptyParameterNames
        {
            [Parameter("address", "", 1, true)]
            public string From { get; set; }

            [Parameter("address", null, 2, true)] 
            public string To { get; set; }

            [Parameter("uint256", " ", 3, true)]
            public BigInteger Value { get; set; }
        }

        [Fact]
        public void Topic_Value_Array_Length_Always_Equals_Signature_Plus_Count_Of_Indexed_Parameters()
        {
            var filter = new FilterInputBuilder<TransferEvent_ERC20>().Build();
            Assert.Equal(3, filter.Topics.Length);

            var filterFrom = new FilterInputBuilder<TransferEvent_ERC20>()
                .AddTopic(tfr => tfr.From, "0xdfa70b70b41d77a7cdd8b878f57521d47c064d8c")
                .Build();

            Assert.Equal(3, filterFrom.Topics.Length);

            var filterTo = new FilterInputBuilder<TransferEvent_ERC20>()
                .AddTopic(tfr => tfr.To, "0xefa70b70b41d77a7cdd8b878f57521d47c064d8c")
                .Build();

            Assert.Equal(3, filterTo.Topics.Length);

            var filterFromAndTo = new FilterInputBuilder<TransferEvent_ERC20>()
                .AddTopic(tfr => tfr.From, "0xdfa70b70b41d77a7cdd8b878f57521d47c064d8c")
                .AddTopic(tfr => tfr.To, "0xefa70b70b41d77a7cdd8b878f57521d47c064d8c")
                .Build();

            Assert.Equal(3, filterFromAndTo.Topics.Length);


        }

        [Fact]
        public void Assigns_Event_Signature_To_Topic0()
        {
            var filter = new FilterInputBuilder<TransferEvent>().Build();

            var eventAbi = ABITypedRegistry.GetEvent<TransferEvent>();

            Assert.Equal(eventAbi.Sha3Signature.EnsureHexPrefix(), filter.Topics.FirstOrDefault());

            Assert.False(filter.IsTopicFiltered(1));
            Assert.False(filter.IsTopicFiltered(2));
            Assert.False(filter.IsTopicFiltered(3));
        }

        [Fact]
        public void Can_Assign_To_Topic1()
        {
            var from = "0xc14934679e71ef4d18b6ae927fe2b953c7fd9b91";

            var filter = new FilterInputBuilder<TransferEvent>()
                .AddTopic((t) => t.From, from)
                .Build();

            Assert.Equal("0x000000000000000000000000c14934679e71ef4d18b6ae927fe2b953c7fd9b91", 
                filter.GetFirstTopicValueAsString(1));

            Assert.False(filter.IsTopicFiltered(2));
            Assert.False(filter.IsTopicFiltered(3));
        }

        [Fact]
        public void Can_Assign_Many_Values_To_A_Topic()
        {
            var address1 = "0xc14934679e71ef4d18b6ae927fe2b953c7fd9b91";
            var address2 = "0xc24934679e71ef4d18b6ae927fe2b953c7fd9b91";

            var filter = new FilterInputBuilder<TransferEvent>()
                .AddTopic((t) => t.From, address1)
                .AddTopic((t) => t.From, address2)
                .Build();

            var topicValues = filter.GetTopicValues(1);

            Assert.Equal("0x000000000000000000000000c14934679e71ef4d18b6ae927fe2b953c7fd9b91", 
                topicValues[0].ToString());

            Assert.Equal("0x000000000000000000000000c24934679e71ef4d18b6ae927fe2b953c7fd9b91", 
                topicValues[1].ToString());
        }

        [Fact]
        public void Can_Assign_Many_Values_To_A_Topic_At_Once()
        {
            var address1 = "0xc14934679e71ef4d18b6ae927fe2b953c7fd9b91";
            var address2 = "0xc24934679e71ef4d18b6ae927fe2b953c7fd9b91";

            var filter = new FilterInputBuilder<TransferEvent>()
                .AddTopic((t) => t.From, new []{address1, address2})
                .Build();

            var topicValues = filter.GetTopicValues(1);

            Assert.Equal("0x000000000000000000000000c14934679e71ef4d18b6ae927fe2b953c7fd9b91", 
                topicValues[0].ToString());

            Assert.Equal("0x000000000000000000000000c24934679e71ef4d18b6ae927fe2b953c7fd9b91", 
                topicValues[1].ToString());
        }

        [Fact]
        public void Can_Assign_To_Topic2()
        {
            var to = "0xc14934679e71ef4d18b6ae927fe2b953c7fd9b91";

            var filter = new FilterInputBuilder<TransferEvent>()
                .AddTopic(template => template.To, to)
                .Build();

            Assert.False(filter.IsTopicFiltered(1));
            Assert.Equal("0x000000000000000000000000c14934679e71ef4d18b6ae927fe2b953c7fd9b91", 
                filter.GetFirstTopicValueAsString(2));
            Assert.False(filter.IsTopicFiltered(3));
        }

        [Fact]
        public void Can_Assign_To_Topic3()
        {
            var value = BigInteger.One;

            var filter = new FilterInputBuilder<TransferEvent>()
                .AddTopic(template => template.Value, value)
                .Build();

            Assert.False(filter.IsTopicFiltered(1));
            Assert.False(filter.IsTopicFiltered(2));
            Assert.Equal("0x0000000000000000000000000000000000000000000000000000000000000001", 
                filter.GetFirstTopicValueAsString(3));
        }


        [Fact]
        public void Can_Assign_To_Multiple_Topics()
        {
            var from = "0xc14934679e71ef4d18b6ae927fe2b953c7fd9b91";
            var to = "0xc14934679e71ef4d18b6ae927fe2b953c7fd9b92";
            var value = BigInteger.One;

            var filter = new FilterInputBuilder<TransferEvent>()
                .AddTopic(template => template.From, from)
                .AddTopic(template => template.To,  to)
                .AddTopic(template => template.Value, value)
                .Build();

            Assert.Equal("0x000000000000000000000000c14934679e71ef4d18b6ae927fe2b953c7fd9b91", 
                filter.GetFirstTopicValueAsString(1));

            Assert.Equal("0x000000000000000000000000c14934679e71ef4d18b6ae927fe2b953c7fd9b92", 
                filter.GetFirstTopicValueAsString(2));

            Assert.Equal("0x0000000000000000000000000000000000000000000000000000000000000001", 
                filter.GetFirstTopicValueAsString(3));
        }

        [Fact]
        public void When_Parameter_Name_Is_Empty_Uses_Order_To_Find_Topic()
        {
            var from = "0xc14934679e71ef4d18b6ae927fe2b953c7fd9b91";
            var to = "0xc14934679e71ef4d18b6ae927fe2b953c7fd9b92";
            var value = BigInteger.One;

            var filter = new FilterInputBuilder<TransferEvent_WithEmptyParameterNames>()
                .AddTopic(template => template.From, from)
                .AddTopic(template => template.To,  to)
                .AddTopic(template => template.Value, value)
                .Build();

            Assert.Equal("0x000000000000000000000000c14934679e71ef4d18b6ae927fe2b953c7fd9b91", 
                filter.GetFirstTopicValueAsString(1));

            Assert.Equal("0x000000000000000000000000c14934679e71ef4d18b6ae927fe2b953c7fd9b92", 
                filter.GetFirstTopicValueAsString(2));

            Assert.Equal("0x0000000000000000000000000000000000000000000000000000000000000001", 
                filter.GetFirstTopicValueAsString(3));
        }

        [Fact]
        public void Assigns_Specified_Contract_Addresses()
        {
            var ContractAddresses = new []
            {
                "0xC03cDD393C89D169bd4877d58f0554f320f21037",
                "0xD03cDD393C89D169bd4877d58f0554f320f21037"
            };

            var filter = new FilterInputBuilder<TransferEvent>().Build(ContractAddresses);

            Assert.True(filter.Address.SequenceEqual(ContractAddresses));
        }

        [Fact]
        public void Assigns_Specified_Contract_Address()
        {
            var contractAddress = 
                "0xC03cDD393C89D169bd4877d58f0554f320f21037";

            var filter = new FilterInputBuilder<TransferEvent>().Build(contractAddress);

            Assert.Single(filter.Address, contractAddress);
        }

        [Fact]
        public void Assigns_Specified_Block_Numbers()
        {
            var range = new BlockRange(15, 25);

            var filter = new FilterInputBuilder<TransferEvent>().Build(blockRange: range);

            Assert.Equal(range.From, filter.FromBlock.BlockNumber.Value);
            Assert.Equal(range.To, filter.ToBlock.BlockNumber.Value);
        }

        [Fact]
        public void When_Assigning_To_A_Non_Indexed_Property_It_Will_Throw_Argument_Exception()
        {
            var x = Assert.Throws<ArgumentException>(() => 
                new FilterInputBuilder<TransferEvent>()
                    .AddTopic((t) => t.NotIndexed, (uint)1));

            Assert.Equal("Property 'NotIndexed' does not represent a topic. The property must have a ParameterAttribute which is flagged as indexed", x.Message);
        }

        [Fact]
        public void When_Assigning_To_A_Property_Without_A_Parameter_Attribute_It_Will_Throw_Argument_Exception()
        {
            var x = Assert.Throws<ArgumentException>(() => 
                new FilterInputBuilder<TransferEvent>()
                    .AddTopic((t) => t.NotAnEventParameter, (uint)1));

            Assert.Equal("Property 'NotAnEventParameter' does not represent a topic. The property must have a ParameterAttribute which is flagged as indexed", x.Message);
                
        }
    }
}