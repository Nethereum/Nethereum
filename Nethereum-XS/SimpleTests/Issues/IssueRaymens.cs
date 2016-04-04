using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleTests.Issues
{
    [TestClass]

    public class IssueRaymens
    {

        private string abi = @"[{
    ""anonymous"": false,
    ""inputs"": [
      {
        ""indexed"": false,
        ""name"": ""newOwner"",
        ""type"": ""address""
      }
    ],
    ""name"": ""OwnerAdded"",
    ""type"": ""event""
  }]";


        [TestMethod]
        public void Test()
        {
            var web3 = new Web3("http://192.168.2.211:8545");
            var contract = web3.Eth.GetContract(abi, "0xa3969327661Ad9632638b8fe8d5dEF6ceFd94738");
            var e = contract.GetEvent("OwnerAdded");
            var filterId = e.CreateFilterAsync(new Nethereum.RPC.Eth.DTOs.BlockParameter(667852)).Result;
            var changes = e.GetAllChanges<OwnerAdded>(filterId).Result;
        }
// changes.Length == 0

public class OwnerAdded
        {
            [Parameter("address", "newOwner", 1, false)]
            public string NewOwner { get; set; }
        }
    }
        

    }

