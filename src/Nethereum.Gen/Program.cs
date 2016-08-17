using System;
using System.Linq;
//using Nethereum.ABI.FunctionEncoding;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;

namespace Nethereum.Gen
{
    /*
    class MainClass
    {
        public static void Main(string[] args)
        {
            //GenerateAugur();
            try
            {
                //if (args[0] == "--?" || args[0] == "--help")
                //{
                //    Console.WriteLine("Just call using as parameters: abi contractName");
                //}
                //else
                //{

                    //string abi = args[0];
                    //string contractName = args[1];

                    var abi =
                        @"[{""constant"":false,""inputs"":[{""name"":""registeredAddress"",""type"":""address""}],""name"":""unregister"",""outputs"":[],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""registeredAddress"",""type"":""address""}],""name"":""register"",""outputs"":[],""type"":""function""},{""constant"":true,""inputs"":[{""name"":"""",""type"":""address""}],""name"":""records"",""outputs"":[{""name"":""registeredAddress"",""type"":""address""},{""name"":""owner"",""type"":""address""},{""name"":""time"",""type"":""uint256""},{""name"":""Id"",""type"":""uint256""}],""type"":""function""},{""constant"":true,""inputs"":[{""name"":"""",""type"":""uint256""}],""name"":""workRegistered"",""outputs"":[{""name"":"""",""type"":""address""}],""type"":""function""},{""constant"":true,""inputs"":[],""name"":""numRecords"",""outputs"":[{""name"":"""",""type"":""uint256""}],""type"":""function""},{""constant"":true,""inputs"":[],""name"":""maxId"",""outputs"":[{""name"":"""",""type"":""uint256""}],""type"":""function""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""registeredAddress"",""type"":""address""},{""indexed"":true,""name"":""id"",""type"":""uint256""},{""indexed"":true,""name"":""owner"",""type"":""address""},{""indexed"":false,""name"":""time"",""type"":""uint256""}],""name"":""Registered"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""registeredAddress"",""type"":""address""},{""indexed"":true,""name"":""id"",""type"":""uint256""}],""name"":""Unregistered"",""type"":""event""}]";
                    var contractName = "ContractRegistry";

                    var generator = new ContractServiceGenerator();
                    var fileName = contractName + "Service.cs";
                    var genContract = generator.ContractGen(abi, contractName, "Nethereum.ContractRegistry");
                    var fileOutput = System.IO.File.CreateText(fileName);
                    fileOutput.Write(genContract);
                    fileOutput.Flush();

                    Console.WriteLine("Generated " + fileName);
               // }
            }
            catch (Exception ex)
            {
              Console.WriteLine(ex.Message);   
            }
        }

        private static void GenerateAugur()
        {
            var augurJsonLocation = "contractsInfo.json";
            dynamic[] augurContracts = JsonConvert.DeserializeObject<dynamic[]>(File.ReadAllText(augurJsonLocation));
            Console.WriteLine(augurContracts.Length);
            foreach (var contract in augurContracts)
            {
                //var contract = augurContracts[0];
                var functionsABI = new List<dynamic>();
                //Console.WriteLine(contract.functions.Length);
                foreach (dynamic function in contract.functions)
                {
                    try
                    {
                        functionsABI.Add(function.abi);
                    }
                    catch
                    {
                    }
                }
                var abiJ = JsonConvert.SerializeObject(functionsABI.ToArray());
                Console.WriteLine(abiJ);
                var contractName = contract.name.ToString();
                Console.WriteLine(contractName);
                var generator = new ContractServiceGenerator();
                var fileName = generator.MakeFirstCharUpper(contractName) + "Service.cs";
                var genContract = generator.ContractGen(abiJ, contractName, "Augur");
                var fileOutput = System.IO.File.CreateText(fileName);
                fileOutput.Write(genContract);
                fileOutput.Flush();
                fileOutput.Close();
                Console.WriteLine("Generated " + fileName);
            }
        }
    }
  
    */
}



