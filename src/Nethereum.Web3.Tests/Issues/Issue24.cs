using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.Web3.Tests.Issues
{
    public class Issue24
    {
        //This was a problem on event type declaration, see the Mordern test

       
        [Fact]
        public void MordenTest()
        {
            string abi = @"[{""constant"":false,""inputs"":[{""name"":""multihash"",""type"":""bytes""}],""name"":""uploadBatch"",""outputs"":[{""name"":"""",""type"":""bool""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""_blockHeight"",""type"":""uint256""},{""name"":""_contentBy"",""type"":""address""},{""name"":""_changeCount"",""type"":""uint256""},{""name"":""_totalRexRewarded"",""type"":""uint256""}],""name"":""issueContentReward"",""outputs"":[],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""_address"",""type"":""address""}],""name"":""updateCoordinator"",""outputs"":[],""type"":""function""},{""constant"":true,""inputs"":[],""name"":""uploadCount"",""outputs"":[{""name"":"""",""type"":""uint256""}],""type"":""function""},{""inputs"":[{""name"":""_dataFeedCoordinatorAddress"",""type"":""address""},{""name"":""feedCode"",""type"":""bytes4""}],""type"":""constructor""},{""anonymous"":false,""inputs"":[{""indexed"":false,""name"":"""",""type"":""address""},{""indexed"":false,""name"":"""",""type"":""bytes""}],""name"":""BatchUploaded"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":false,""name"":""blockHeight"",""type"":""uint256""},{""indexed"":false,""name"":""contentBy"",""type"":""address""},{""indexed"":false,""name"":""changeCount"",""type"":""uint256""},{""indexed"":false,""name"":""totalRewards"",""type"":""uint256""}],""name"":""ContentRewarded"",""type"":""event""}]";
            string contractAddress = "0xe8d75008917c6a460473e62d5d4cefd3bbe4d85b";

            var web3 = new Web3("http://localhost:8545");
            var dataFeedContract = web3.Eth.GetContract(abi, contractAddress);

            //var filterAllContract = dataFeedContract.CreateFilterAsync().Result;
            //var logs = web3.Eth.Filters.GetFilterChangesForEthNewFilter.SendRequestAsync(filterAllContract).Result;

            Nethereum.Web3.Event e = dataFeedContract.GetEvent("BatchUploaded");
            var filterId = e.CreateFilterAsync(new Nethereum.RPC.Eth.DTOs.BlockParameter(500000)).Result;
            var changes = e.GetAllChanges<EventBatchUploaded>(filterId).Result;
        }

        //Issue on the original Event speficifing a string instead of an address type
        /*
          
          public class EventBatchUploaded
        {
            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("string", 1)]
            public string address { get; set; }

            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("bytes", 2)]
            public byte[] hashBytes { get; set; }
        }
        
        */ 
         

        public class EventBatchUploaded
        {
            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("address", 1)]
            public string address { get; set; }

            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("bytes", 2)]
            public string hashBytes { get; set; }
        }


        [Fact]
        public void BytesTest()
        {

            var original = "0x000000000000000000000000223aea5cf8c5a21859171edb2d01d2ceab0336780000000000000000000000000000000000000000000000000000000000000040000000000000000000000000000000000000000000000000000000000000002e516d5074633431505661375945585a7359524448586a6332525753474c47794b396774787a524e6543354b4e5641000000000000000000000000000000000000";

            var bytes = "000000000000000000000000000000000000000000000000000000000000002e516d5074633431505661375945585a7359524448586a6332525753474c47794b396774787a524e6543354b4e5641000000000000000000000000000000000000";

            var bytesType = new BytesType();
            var bytesArray = bytes.HexToByteArray();
            var decoded = (byte[])bytesType.Decode(bytesArray, typeof(byte[]));
            var stringValue = (string)bytesType.Decode(bytesArray, typeof(string));

        }

    }
}
