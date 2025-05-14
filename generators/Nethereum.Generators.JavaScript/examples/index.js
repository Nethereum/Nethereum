var codegen = require('../');

var contractByteCode =
    "0x00";
var abierc20 = '[{"inputs":[],"stateMutability":"nonpayable","type":"constructor"},{"inputs":[],"name":"ECDSAInvalidSignature","type":"error"},{"inputs":[{"internalType":"uint256","name":"length","type":"uint256"}],"name":"ECDSAInvalidSignatureLength","type":"error"},{"inputs":[{"internalType":"bytes32","name":"s","type":"bytes32"}],"name":"ECDSAInvalidSignatureS","type":"error"},{"inputs":[{"internalType":"address","name":"spender","type":"address"},{"internalType":"uint256","name":"allowance","type":"uint256"},{"internalType":"uint256","name":"needed","type":"uint256"}],"name":"ERC20InsufficientAllowance","type":"error"},{"inputs":[{"internalType":"address","name":"sender","type":"address"},{"internalType":"uint256","name":"balance","type":"uint256"},{"internalType":"uint256","name":"needed","type":"uint256"}],"name":"ERC20InsufficientBalance","type":"error"},{"inputs":[{"internalType":"address","name":"approver","type":"address"}],"name":"ERC20InvalidApprover","type":"error"},{"inputs":[{"internalType":"address","name":"receiver","type":"address"}],"name":"ERC20InvalidReceiver","type":"error"},{"inputs":[{"internalType":"address","name":"sender","type":"address"}],"name":"ERC20InvalidSender","type":"error"},{"inputs":[{"internalType":"address","name":"spender","type":"address"}],"name":"ERC20InvalidSpender","type":"error"},{"inputs":[{"internalType":"uint256","name":"deadline","type":"uint256"}],"name":"ERC2612ExpiredSignature","type":"error"},{"inputs":[{"internalType":"address","name":"signer","type":"address"},{"internalType":"address","name":"owner","type":"address"}],"name":"ERC2612InvalidSigner","type":"error"},{"inputs":[{"internalType":"address","name":"account","type":"address"},{"internalType":"uint256","name":"currentNonce","type":"uint256"}],"name":"InvalidAccountNonce","type":"error"},{"inputs":[],"name":"InvalidShortString","type":"error"},{"inputs":[{"internalType":"string","name":"str","type":"string"}],"name":"StringTooLong","type":"error"},{"anonymous":false,"inputs":[{"indexed":true,"internalType":"address","name":"owner","type":"address"},{"indexed":true,"internalType":"address","name":"spender","type":"address"},{"indexed":false,"internalType":"uint256","name":"value","type":"uint256"}],"name":"Approval","type":"event"},{"anonymous":false,"inputs":[],"name":"EIP712DomainChanged","type":"event"},{"anonymous":false,"inputs":[{"indexed":true,"internalType":"address","name":"from","type":"address"},{"indexed":true,"internalType":"address","name":"to","type":"address"},{"indexed":false,"internalType":"uint256","name":"value","type":"uint256"}],"name":"Transfer","type":"event"},{"inputs":[],"name":"DOMAIN_SEPARATOR","outputs":[{"internalType":"bytes32","name":"","type":"bytes32"}],"stateMutability":"view","type":"function"},{"inputs":[{"internalType":"address","name":"owner","type":"address"},{"internalType":"address","name":"spender","type":"address"}],"name":"allowance","outputs":[{"internalType":"uint256","name":"","type":"uint256"}],"stateMutability":"view","type":"function"},{"inputs":[{"internalType":"address","name":"spender","type":"address"},{"internalType":"uint256","name":"value","type":"uint256"}],"name":"approve","outputs":[{"internalType":"bool","name":"","type":"bool"}],"stateMutability":"nonpayable","type":"function"},{"inputs":[{"internalType":"address","name":"account","type":"address"}],"name":"balanceOf","outputs":[{"internalType":"uint256","name":"","type":"uint256"}],"stateMutability":"view","type":"function"},{"inputs":[],"name":"decimals","outputs":[{"internalType":"uint8","name":"","type":"uint8"}],"stateMutability":"view","type":"function"},{"inputs":[],"name":"eip712Domain","outputs":[{"internalType":"bytes1","name":"fields","type":"bytes1"},{"internalType":"string","name":"name","type":"string"},{"internalType":"string","name":"version","type":"string"},{"internalType":"uint256","name":"chainId","type":"uint256"},{"internalType":"address","name":"verifyingContract","type":"address"},{"internalType":"bytes32","name":"salt","type":"bytes32"},{"internalType":"uint256[]","name":"extensions","type":"uint256[]"}],"stateMutability":"view","type":"function"},{"inputs":[],"name":"name","outputs":[{"internalType":"string","name":"","type":"string"}],"stateMutability":"view","type":"function"},{"inputs":[{"internalType":"address","name":"owner","type":"address"}],"name":"nonces","outputs":[{"internalType":"uint256","name":"","type":"uint256"}],"stateMutability":"view","type":"function"},{"inputs":[{"internalType":"address","name":"owner","type":"address"},{"internalType":"address","name":"spender","type":"address"},{"internalType":"uint256","name":"value","type":"uint256"},{"internalType":"uint256","name":"deadline","type":"uint256"},{"internalType":"uint8","name":"v","type":"uint8"},{"internalType":"bytes32","name":"r","type":"bytes32"},{"internalType":"bytes32","name":"s","type":"bytes32"}],"name":"permit","outputs":[],"stateMutability":"nonpayable","type":"function"},{"inputs":[],"name":"symbol","outputs":[{"internalType":"string","name":"","type":"string"}],"stateMutability":"view","type":"function"},{"inputs":[],"name":"totalSupply","outputs":[{"internalType":"uint256","name":"","type":"uint256"}],"stateMutability":"view","type":"function"},{"inputs":[{"internalType":"address","name":"to","type":"address"},{"internalType":"uint256","name":"value","type":"uint256"}],"name":"transfer","outputs":[{"internalType":"bool","name":"","type":"bool"}],"stateMutability":"nonpayable","type":"function"},{"inputs":[{"internalType":"address","name":"from","type":"address"},{"internalType":"address","name":"to","type":"address"},{"internalType":"uint256","name":"value","type":"uint256"}],"name":"transferFrom","outputs":[{"internalType":"bool","name":"","type":"bool"}],"stateMutability":"nonpayable","type":"function"}]'
var abi = abierc20;
var contractName = "ERC20";
var baseNamespace = "Nethereum.Unity.Contracts.Standards";
var basePath = "codeGenNodeTest";

var mudTables = '{"tables":{"Counter":{"schema":{"value":"uint32"},"key":[]},"Item":{"schema":{"id":"uint32","price":"uint32","name":"string","description":"string","owner":"string"},"key":["id"]}}}';


var projectName = "MyProject";

//Csharp 0, Vb 1, Fsharp 3
//codegen.generateNetStandardClassLibrary(projectName, basePath, 0);

/*codegen.generateAllClasses(abi,
    contractByteCode,
    contractName,
    baseNamespace,
    basePath,
    0);
*/

//codegen.generateUnityRequests(abi, contractByteCode, contractName, baseNamespace, basePath);



codegen.generateMudTables(mudTables, baseNamespace, "Tables", basePath, 0);
codegen.generateMudService(abi,
    contractByteCode,
    contractName,
    baseNamespace,
    null,
    null,
    basePath,
    0);


var jsonGeneratorSetsExample1 = 
`[
{
    "paths": ["out/ERC20.sol/Standard_Token.json"],
        "generatorConfigs": [
            {
                "baseNamespace": "MyProject.Contracts",
                "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts",
                "codeGenLang": 0,
                "generatorType": "ContractDefinition"
            },
            {
                "baseNamespace": "MyProject.Contracts",
                "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Blazor",
                "codeGenLang": 0,
                "generatorType": "BlazorPageService"
            },
            {
                "baseNamespace": "MyProject.Contracts",
                "basePath": "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts",
                "codeGenLang": 0,
                "generatorType": "UnityRequest"
            }
        ]
},
{
    "paths": ["out/IncrementSystem.sol/IncrementSystem.json"],
        "generatorConfigs": [
            {
                "baseNamespace": "MyProject.Contracts.MyWorld.Systems",
                "basePath":  "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts/MyWorld/Systems",
                "codeGenLang": 0,
                "generatorType": "ContractDefinition",
                "mudNamespace": "MyWorld"
            },
            {
                "baseNamespace": "MyProject.Contracts.MyWorld.Systems",
                "basePath":  "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts/MyWorld/Systems",
                "codeGenLang": 0,
                "generatorType": "MudExtendedService",
                "mudNamespace": "MyWorld"
            }
        ]
},
{
    "paths": ["mudSingleNamespace/mud.config.ts"],
        "generatorConfigs": [
            {
                "baseNamespace": "MyProject.Contracts.MyWorld.Tables",
                "basePath":  "codeGenNodeTest/GeneratorSets/Example1/MyProject.Contracts/MyWorld/Tables",
                "generatorType": "MudTables",
                "mudNamespace": "MyWorld"
               
            }
        ]
}
]`;

var files = codegen.generateFilesFromConfigJsonString(jsonGeneratorSetsExample1, "examples/testAbi");
files.forEach(f => console.log(f));


var jsonGeneratorSetsExample2 =
    `[
{
    "paths": ["out/ERC20.sol/Standard_Token.json"],
        "generatorConfigs": [
            {
                "baseNamespace": "MyProject.Contracts",
                "basePath": "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts",
                "codeGenLang": 0,
                "sharedTypesNamespace": "SharedTypes",
                "sharedTypes": ["events", "errors"],
                "generatorType": "ContractDefinition"
            },
            {
                "baseNamespace": "MyProject.Contracts",
                "basePath": "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts",
                "codeGenLang": 0,
                "generatorType": "UnityRequest"
            }
        ]
},
{
    "paths": ["out/IncrementSystem.sol/IncrementSystem.json"],
        "generatorConfigs": [
            {
                "baseNamespace": "MyProject.Contracts.MyWorld1.Systems",
                "basePath":  "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts/MyWorld1/Systems",
                "codeGenLang": 0,
                "generatorType": "ContractDefinition",
                 "mudNamespace": "myworld1"
            },
            {
                "baseNamespace": "MyProject.Contracts.MyWorld1.Systems",
                "basePath":  "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts/MyWorld1/Systems",
                "codeGenLang": 0,
                "generatorType": "MudExtendedService",
                "mudNamespace": "myworld1"
            }
        ]
},
{
    "paths": ["mudMultipleNamespace/mud.config.ts"],
        "generatorConfigs": [
            {
                "baseNamespace": "MyProject.Contracts.MyWorld1.Tables",
                "basePath":  "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts/MyWorld1/Tables",
                "generatorType": "MudTables",
                "mudNamespace": "myworld1"
               
            }
        ]
},
{
    "paths": ["mudMultipleNamespace/mud.config.ts"],
        "generatorConfigs": [
            {
                "baseNamespace": "MyProject.Contracts.MyWorld2.Tables",
                "basePath":  "codeGenNodeTest/GeneratorSets/Example2/MyProject.Contracts/MyWorld2/Tables",
                "generatorType": "MudTables",
                "mudNamespace": "myworld2"

            }
        ]
}
]`;

files = codegen.generateFilesFromConfigJsonString(jsonGeneratorSetsExample2, "examples/testAbi");
files.forEach(f => console.log(f));


