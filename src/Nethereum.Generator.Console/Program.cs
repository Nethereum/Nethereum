using System.Linq;
using RazorLight;

namespace Nethereum.Generator.Console
{
    class Program
    {
        static void Main(string[] args)
        {

            var engine = EngineFactory.CreateEmbedded(typeof(ServiceModel));
         
            var contractByteCode =
              "0x6060604052604051602080610213833981016040528080519060200190919050505b806000600050819055505b506101d88061003b6000396000f360606040526000357c01000000000000000000000000000000000000000000000000000000009004806361325dbc1461004f578063c23f4e3e1461007b578063c6888fa1146100b05761004d565b005b61006560048080359060200190919050506100dc565b6040518082815260200191505060405180910390f35b61009a60048080359060200190919080359060200190919050506100f2565b6040518082815260200191505060405180910390f35b6100c66004808035906020019091905050610104565b6040518082815260200191505060405180910390f35b6000600060005054820290506100ed565b919050565b600081830290506100fe565b92915050565b600060006000505482029050805080827f51ae5c4fa89d1aa731ff280d425357e6e5c838c6fc8ed6ca0139ea31716bbd5760405180905060405180910390a360405180807f48656c6c6f20776f726c64000000000000000000000000000000000000000000815260200150600b019050604051809103902081837f74053123e4f45ba0f8cbf86301034a4ab00cdc75cd155a0df7c5d815bd97dcb533604051808273ffffffffffffffffffffffffffffffffffffffff16815260200191505060405180910390a48090506101d3565b91905056";

            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply1"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""},{""name"":""b"",""type"":""uint256""}],""name"":""multiply2"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""},{""name"":""e"",""type"":""uint256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""uint256""}],""type"":""constructor""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""a"",""type"":""uint256""},{""indexed"":true,""name"":""result"",""type"":""uint256""}],""name"":""Multiplied"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""a"",""type"":""uint256""},{""indexed"":true,""name"":""result"",""type"":""uint256""},{""indexed"":true,""name"":""sender"",""type"":""string""},{""indexed"":false,""name"":""hello"",""type"":""address""}],""name"":""MultipliedLog"",""type"":""event""}]";

            var model = new ServiceModel(abi, contractByteCode);
            
            //Note: pass the name of the view without extension
            var result = engine.Parse("Service", model);

            System.Console.WriteLine(result);

            var fileName = model.ContractName + "Service.cs";
             var fileOutput = System.IO.File.CreateText(fileName);
            fileOutput.Write(result);
            fileOutput.Flush();

            System.Console.ReadLine();


        }
    }
}