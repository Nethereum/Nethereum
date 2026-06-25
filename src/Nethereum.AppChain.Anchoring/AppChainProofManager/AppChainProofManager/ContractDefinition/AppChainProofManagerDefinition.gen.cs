using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;
using Nethereum.AppChain.Anchoring.AppChainProofManager.ContractDefinition;

namespace Nethereum.AppChain.Anchoring.AppChainProofManager.ContractDefinition
{


    public partial class AppChainProofManagerDeployment : AppChainProofManagerDeploymentBase
    {
        public AppChainProofManagerDeployment() : base(BYTECODE) { }
        public AppChainProofManagerDeployment(string byteCode) : base(byteCode) { }
    }

    public class AppChainProofManagerDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60a060405234801561000f575f5ffd5b5060405161212738038061212783398101604081905261002e91610079565b60017f9b779b17422d0df92223018b32b4d1fa46e071723d6817e2486d003becc55f00556001600160a01b03166080525f8054610100600160a81b03191633610100021790556100a6565b5f60208284031215610089575f5ffd5b81516001600160a01b038116811461009f575f5ffd5b9392505050565b60805161203f6100e85f395f81816104d1015281816106180152818161119b01528181611290015281816116de015281816117b0015261189f015261203f5ff3fe608060405260043610610131575f3560e01c80638456cb59116100a8578063b1529cd01161006d578063b1529cd01461046d578063cbd14a7c14610482578063d1092bf1146104a1578063d3fb73b4146104c0578063e5583dea146104f3578063f3f4370314610512575f5ffd5b80638456cb59146103c1578063867fa521146103d5578063888115c6146104005780638da5cb5b14610416578063ab519f0214610451575f5ffd5b80633f4ba83a116100f95780633f4ba83a146102b55780635a84c588146102c95780635c975abb1461034d5780636030b87e1461036f57806366eb9cec1461038e5780636e2f253e146103a2575f5ffd5b8063051935ad1461013557806308978f8a146101dc57806309a7773f1461021557806313ab75581461022a57806330df028e14610249575b5f5ffd5b348015610140575f5ffd5b5061019b61014f366004611b43565b600360209081525f92835260408084209091529082529020546001600160a01b038116906001600160401b03600160a01b8204169060ff600160e01b8204811691600160e81b90041684565b604080516001600160a01b0390951685526001600160401b03909316602085015260ff90911691830191909152151560608201526080015b60405180910390f35b3480156101e7575f5ffd5b506102076101f6366004611b7a565b60016020525f908152604090205481565b6040519081526020016101d3565b610228610223366004611b43565b61053d565b005b348015610235575f5ffd5b50610228610244366004611c39565b6108f7565b348015610254575f5ffd5b5061019b610263366004611b43565b6001600160401b039182165f908152600360209081526040808320938516835292905220546001600160a01b03811692600160a01b82049092169160ff600160e01b8304811692600160e81b90041690565b3480156102c0575f5ffd5b506102286109b0565b3480156102d4575f5ffd5b506103216102e3366004611b43565b600460209081525f928352604080842090915290825290208054600182015460028301546003909301546001600160a01b0390921692909160ff1684565b604080516001600160a01b039095168552602085019390935291830152151560608201526080016101d3565b348015610358575f5ffd5b505f5460ff165b60405190151581526020016101d3565b34801561037a575f5ffd5b50610228610389366004611d06565b610a05565b348015610399575f5ffd5b50610228610b7f565b3480156103ad575f5ffd5b506102286103bc366004611d06565b610cbc565b3480156103cc575f5ffd5b50610228610dec565b3480156103e0575f5ffd5b506102076103ef366004611b7a565b60026020525f908152604090205481565b34801561040b575f5ffd5b5061020762278d0081565b348015610421575f5ffd5b505f546104399061010090046001600160a01b031681565b6040516001600160a01b0390911681526020016101d3565b34801561045c575f5ffd5b5061020768056bc75e2d6310000081565b348015610478575f5ffd5b50610207610e1081565b34801561048d575f5ffd5b5061035f61049c366004611b43565b610e3f565b3480156104ac575f5ffd5b506102286104bb366004611b43565b610e75565b3480156104cb575f5ffd5b506104397f000000000000000000000000000000000000000000000000000000000000000081565b3480156104fe575f5ffd5b5061022861050d366004611c39565b611069565b34801561051d575f5ffd5b5061020761052c366004611d44565b60056020525f908152604090205481565b61054561113d565b61054d611158565b6001600160401b0382165f90815260016020526040902054806105b75760405162461bcd60e51b815260206004820152601960248201527f50726f6f6620626f6e64206e6f7420636f6e666967757265640000000000000060448201526064015b60405180910390fd5b8034146105f85760405162461bcd60e51b815260206004820152600f60248201526e14d95b9908195e1858dd08189bdb99608a1b60448201526064016105ae565b6040516316006e2960e21b81526001600160401b03841660048201525f907f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031690635801b8a49060240160e060405180830381865afa158015610665573d5f5f3e3d5ffd5b505050506040513d601f19601f820116820180604052508101906106899190611d6e565b9650505050505050806106ce5760405162461bcd60e51b815260206004820152600d60248201526c2ab735b737bbb71031b430b4b760991b60448201526064016105ae565b6001600160401b038481165f90815260036020908152604080832093871683529290522054600160e81b900460ff161561073b5760405162461bcd60e51b815260206004820152600e60248201526d20b63932b0b23c90383937bb32b760911b60448201526064016105ae565b6001600160401b038085165f90815260046020908152604080832093871683529290522080546001600160a01b0316158061077a5750600381015460ff165b6107c65760405162461bcd60e51b815260206004820152601760248201527f5265717565737420616c72656164792070656e64696e6700000000000000000060448201526064016105ae565b6001600160401b0385165f90815260026020526040812054908190036107ec5750620151805b6040805160808101825233815234602082015290810161080c8342611ded565b81525f60209182018190526001600160401b03898116808352600484526040808420928b1680855292855292839020855181546001600160a01b0319166001600160a01b039091161781559385015160018501559184015160028401556060909301516003909201805460ff1916921515929092179091557f7ae432d8272a01cd1e47c0efb8426b720e7ae4d230cad4aa79f65b02caea686a33346108b18642611ded565b604080516001600160a01b03909416845260208401929092529082015260600160405180910390a3505050506108f360015f516020611fea5f395f51905f5255565b5050565b6108ff611158565b6109088b61117b565b6001600160a01b031663700bcefb8c336040518363ffffffff1660e01b8152600401610935929190611e0c565b602060405180830381865afa158015610950573d5f5f3e3d5ffd5b505050506040513d601f19601f820116820180604052508101906109749190611e2e565b6109905760405162461bcd60e51b81526004016105ae90611e47565b6109a38b8b8b8b8b8b8b8b8b8b8b61120c565b5050505050505050505050565b5f5461010090046001600160a01b031633146109fb5760405162461bcd60e51b815260206004820152600a60248201526927b7363c9037bbb732b960b11b60448201526064016105ae565b610a036114ea565b565b610a0e8261117b565b6001600160a01b0316634f4697a983336040518363ffffffff1660e01b8152600401610a3b929190611e0c565b602060405180830381865afa158015610a56573d5f5f3e3d5ffd5b505050506040513d601f19601f82011682018060405250810190610a7a9190611e2e565b610a965760405162461bcd60e51b81526004016105ae90611e47565b610e10811015610adb5760405162461bcd60e51b815260206004820152601060248201526f15da5b991bddc81d1bdbc81cda1bdc9d60821b60448201526064016105ae565b62278d00811115610b205760405162461bcd60e51b815260206004820152600f60248201526e57696e646f7720746f6f206c6f6e6760881b60448201526064016105ae565b6001600160401b0382165f81815260026020908152604091829020805490859055825181815291820185905292917f114b6b58df5de0ec8dc695a2eaa3a996128ab118d8f754a4fbad4a462059d1d491015b60405180910390a2505050565b610b8761113d565b335f9081526005602052604090205480610bd95760405162461bcd60e51b81526020600482015260136024820152724e6f7468696e6720746f20776974686472617760681b60448201526064016105ae565b335f818152600560205260408082208290555190919083908381818185875af1925050503d805f8114610c27576040519150601f19603f3d011682016040523d82523d5f602084013e610c2c565b606091505b5050905080610c6f5760405162461bcd60e51b815260206004820152600f60248201526e151c985b9cd9995c8819985a5b1959608a1b60448201526064016105ae565b60405182815233907f0d41118e36df44efb77a471fc49fb9c0be0406d802ef95520e9fbf606e65b4559060200160405180910390a25050610a0360015f516020611fea5f395f51905f5255565b610cc58261117b565b6001600160a01b0316634f4697a983336040518363ffffffff1660e01b8152600401610cf2929190611e0c565b602060405180830381865afa158015610d0d573d5f5f3e3d5ffd5b505050506040513d601f19601f82011682018060405250810190610d319190611e2e565b610d4d5760405162461bcd60e51b81526004016105ae90611e47565b68056bc75e2d63100000811115610d965760405162461bcd60e51b815260206004820152600d60248201526c084dedcc840e8dede40d0d2ced609b1b60448201526064016105ae565b6001600160401b0382165f81815260016020908152604091829020805490859055825181815291820185905292917f39647a92ce3d5d9b787c36c141ac2894c02fa9f6510b523a36b366a50b7472a49101610b72565b5f5461010090046001600160a01b03163314610e375760405162461bcd60e51b815260206004820152600a60248201526927b7363c9037bbb732b960b11b60448201526064016105ae565b610a0361153b565b6001600160401b038281165f90815260036020908152604080832093851683529290522054600160e81b900460ff165b92915050565b610e7d61113d565b6001600160401b038083165f90815260046020908152604080832093851683529290522080546001600160a01b0316610ee55760405162461bcd60e51b815260206004820152600a602482015269139bc81c995c5d595cdd60b21b60448201526064016105ae565b600381015460ff1615610f2e5760405162461bcd60e51b8152602060048201526011602482015270105b1c9958591e48199d5b199a5b1b1959607a1b60448201526064016105ae565b80600201544211610f755760405162461bcd60e51b81526020600482015260116024820152702bb4b73237bb9039ba34b6361037b832b760791b60448201526064016105ae565b60018082018054835460038501805460ff19169094179093555f918290556001600160a01b03909216808252600560205260408220805491928492610fbb908490611ded565b90915550506040518281526001600160a01b038216907f6de6fe586196fa05b73b973026c5fda3968a2933989bff3a0b6bd57644fab6069060200160405180910390a2604080516001600160a01b0383168152602081018490526001600160401b0380871692908816917fb04fcacf93241ad2ee83d78579cd7849ae6ced5db718d995ada53a915ae88931910160405180910390a35050506108f360015f516020611fea5f395f51905f5255565b61107161113d565b611079611158565b6110828b61117b565b6001600160a01b031663700bcefb8c336040518363ffffffff1660e01b81526004016110af929190611e0c565b602060405180830381865afa1580156110ca573d5f5f3e3d5ffd5b505050506040513d601f19601f820116820180604052508101906110ee9190611e2e565b61110a5760405162461bcd60e51b81526004016105ae90611e47565b61111d8b8b8b8b8b8b8b8b8b8b8b61120c565b6111278b8b611577565b6109a360015f516020611fea5f395f51905f5255565b61114561168b565b60025f516020611fea5f395f51905f5255565b5f5460ff1615610a035760405163d93c066560e01b815260040160405180910390fd5b604051639a64c7f360e01b81526001600160401b03821660048201525f907f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031690639a64c7f390602401602060405180830381865afa1580156111e8573d5f5f3e3d5ffd5b505050506040513d601f19601f82011682018060405250810190610e6f9190611e6f565b6001600160401b038b81165f908152600360209081526040808320938e1683529290522054600160e81b900460ff16156112795760405162461bcd60e51b815260206004820152600e60248201526d20b63932b0b23c90383937bb32b760911b60448201526064016105ae565b60405163ac146ff560e01b81526001600160a01b037f0000000000000000000000000000000000000000000000000000000000000000169063ac146ff5906112d3908e908d908f908e908e908e908e908e90600401611e8a565b602060405180830381865afa1580156112ee573d5f5f3e3d5ffd5b505050506040513d601f19601f820116820180604052508101906113129190611e2e565b6113545760405162461bcd60e51b8152602060048201526013602482015272213637b1b5903737ba1034b71030b731b437b960691b60448201526064016105ae565b6113648b8b8a8a8a8888886116ba565b6040518060800160405280336001600160a01b03168152602001426001600160401b031681526020018260ff1681526020016001151581525060035f8d6001600160401b03166001600160401b031681526020019081526020015f205f8c6001600160401b03166001600160401b031681526020019081526020015f205f820151815f015f6101000a8154816001600160a01b0302191690836001600160a01b031602179055506020820151815f0160146101000a8154816001600160401b0302191690836001600160401b031602179055506040820151815f01601c6101000a81548160ff021916908360ff1602179055506060820151815f01601d6101000a81548160ff021916908315150217905550905050896001600160401b03168b6001600160401b03167fac7c1a73d078284acb726137a1790b43b51750bb3169e09749b4035de679547b33846040516114d59291906001600160a01b0392909216825260ff16602082015260400190565b60405180910390a35050505050505050505050565b6114f2611afa565b5f805460ff191690557f5db9ee0a495bf2e6ff9c91a7834c1ba4fdd244a5e8aa4e537bd38aeae4b073aa335b6040516001600160a01b03909116815260200160405180910390a1565b611543611158565b5f805460ff191660011790557f62e78cea01bee320cd4e420270b5ea74000d11b0c9f74754ebdbfc544b05a25861151e3390565b6001600160401b038083165f90815260046020908152604080832093851683529290522080546001600160a01b0316158015906115b95750600381015460ff16155b156116865760038101805460ff19166001908117909155810180545f918290553382526005602052604082208054919283926115f6908490611ded565b909155505060405181815233907f6de6fe586196fa05b73b973026c5fda3968a2933989bff3a0b6bd57644fab6069060200160405180910390a28154604080513381526001600160a01b0390921660208301526001600160401b0385811692908716917f83a27e626d3fd480218ae772bfc67e43268a341dd2b547fa26ce0334bbb90779910160405180910390a3505b505050565b5f516020611fea5f395f51905f5254600203610a0357604051633ee5aeb560e01b815260040160405180910390fd5b604051633989a7c760e11b815260ff821660048201525f9081906001600160a01b037f000000000000000000000000000000000000000000000000000000000000000016906373134f8e90602401606060405180830381865afa158015611723573d5f5f3e3d5ffd5b505050506040513d601f19601f820116820180604052508101906117479190611f07565b9250509150806117905760405162461bcd60e51b8152602060048201526014602482015273556e6b6e6f776e2070726f6f662073797374656d60601b60448201526064016105ae565b6040516316006e2960e21b81526001600160401b038b1660048201525f907f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031690635801b8a49060240160e060405180830381865afa1580156117fd573d5f5f3e3d5ffd5b505050506040513d601f19601f820116820180604052508101906118219190611d6e565b50505093505050508060ff168460ff16101561187f5760405162461bcd60e51b815260206004820152601860248201527f42656c6f77206d696e696d756d2070726f6f662074696572000000000000000060448201526064016105ae565b6040516316006e2960e21b81526001600160401b038c1660048201525f907f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031690635801b8a49060240160e060405180830381865afa1580156118ec573d5f5f3e3d5ffd5b505050506040513d601f19601f820116820180604052508101906119109190611d6e565b50505050505090505f60066001600160401b0381111561193257611932611f49565b60405190808252806020026020018201604052801561195b578160200160208202803683370190505b509050815f1c815f8151811061197357611973611f5d565b6020026020010181815250508b6001600160401b03168160018151811061199c5761199c611f5d565b6020026020010181815250508a5f1c816002815181106119be576119be611f5d565b602002602001018181525050895f1c816003815181106119e0576119e0611f5d565b602002602001018181525050885f1c81600481518110611a0257611a02611f5d565b6020026020010181815250508560ff1681600581518110611a2557611a25611f5d565b6020908102919091010152604051634b24ed5760e11b81526001600160a01b03861690639649daae90611a60908b908b908690600401611f71565b602060405180830381865afa158015611a7b573d5f5f3e3d5ffd5b505050506040513d601f19601f82011682018060405250810190611a9f9190611e2e565b611aeb5760405162461bcd60e51b815260206004820152601960248201527f50726f6f6620766572696669636174696f6e206661696c65640000000000000060448201526064016105ae565b50505050505050505050505050565b5f5460ff16610a0357604051638dfc202b60e01b815260040160405180910390fd5b6001600160401b0381168114611b30575f5ffd5b50565b8035611b3e81611b1c565b919050565b5f5f60408385031215611b54575f5ffd5b8235611b5f81611b1c565b91506020830135611b6f81611b1c565b809150509250929050565b5f60208284031215611b8a575f5ffd5b8135611b9581611b1c565b9392505050565b5f5f83601f840112611bac575f5ffd5b5081356001600160401b03811115611bc2575f5ffd5b6020830191508360208260051b8501011115611bdc575f5ffd5b9250929050565b5f5f83601f840112611bf3575f5ffd5b5081356001600160401b03811115611c09575f5ffd5b602083019150836020828501011115611bdc575f5ffd5b60ff81168114611b30575f5ffd5b8035611b3e81611c20565b5f5f5f5f5f5f5f5f5f5f5f6101208c8e031215611c54575f5ffd5b8b35611c5f81611b1c565b9a50611c6d60208d01611b33565b9950611c7b60408d01611b33565b985060608c0135975060808c0135965060a08c0135955060c08c01356001600160401b03811115611caa575f5ffd5b611cb68e828f01611b9c565b90965094505060e08c01356001600160401b03811115611cd4575f5ffd5b611ce08e828f01611be3565b9094509250611cf490506101008d01611c2e565b90509295989b509295989b9093969950565b5f5f60408385031215611d17575f5ffd5b8235611d2281611b1c565b946020939093013593505050565b6001600160a01b0381168114611b30575f5ffd5b5f60208284031215611d54575f5ffd5b8135611b9581611d30565b80518015158114611b3e575f5ffd5b5f5f5f5f5f5f5f60e0888a031215611d84575f5ffd5b87516020890151909750611d9781611b1c565b604089015160608a01519197509550611daf81611c20565b6080890151909450611dc081611c20565b60a0890151909350611dd181611d30565b9150611ddf60c08901611d5f565b905092959891949750929550565b80820180821115610e6f57634e487b7160e01b5f52601160045260245ffd5b6001600160401b039290921682526001600160a01b0316602082015260400190565b5f60208284031215611e3e575f5ffd5b611b9582611d5f565b6020808252600e908201526d139bdd08185d5d1a1bdc9a5e995960921b604082015260600190565b5f60208284031215611e7f575f5ffd5b8151611b9581611d30565b6001600160401b03891681526001600160401b03881660208201526001600160401b03871660408201528560608201528460808201528360a082015260e060c08201528160e08201525f60018060fb1b03831115611ee6575f5ffd5b8260051b808561010085013791909101610100019998505050505050505050565b5f5f5f60608486031215611f19575f5ffd5b8351611f2481611d30565b9250611f3260208501611d5f565b9150611f4060408501611d5f565b90509250925092565b634e487b7160e01b5f52604160045260245ffd5b634e487b7160e01b5f52603260045260245ffd5b60408152826040820152828460608301375f606084830101525f601f19601f85011682016060810160608483030160208501528085518083526080840191506020870193505f92505b80831015611fdd5783518252602082019150602084019350600183019250611fba565b5097965050505050505056fe9b779b17422d0df92223018b32b4d1fa46e071723d6817e2486d003becc55f00a2646970667358221220975975594566962ffbd8b5433a336b64607bc9b1736084cccf5d8f3d372c59f564736f6c634300081c0033";
        public AppChainProofManagerDeploymentBase() : base(BYTECODE) { }
        public AppChainProofManagerDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "anchorContract", 1)]
        public virtual string AnchorContract { get; set; }
    }

    public partial class MaxProofBondFunction : MaxProofBondFunctionBase { }

    [Function("MAX_PROOF_BOND", "uint256")]
    public class MaxProofBondFunctionBase : FunctionMessage
    {

    }

    public partial class MaxProofWindowFunction : MaxProofWindowFunctionBase { }

    [Function("MAX_PROOF_WINDOW", "uint256")]
    public class MaxProofWindowFunctionBase : FunctionMessage
    {

    }

    public partial class MinProofWindowFunction : MinProofWindowFunctionBase { }

    [Function("MIN_PROOF_WINDOW", "uint256")]
    public class MinProofWindowFunctionBase : FunctionMessage
    {

    }

    public partial class AnchorFunction : AnchorFunctionBase { }

    [Function("anchor", "address")]
    public class AnchorFunctionBase : FunctionMessage
    {

    }

    public partial class BlockProofsFunction : BlockProofsFunctionBase { }

    [Function("blockProofs", typeof(BlockProofsOutputDTO))]
    public class BlockProofsFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
        [Parameter("uint64", "", 2)]
        public virtual ulong ReturnValue2 { get; set; }
    }

    public partial class ClaimProofTimeoutFunction : ClaimProofTimeoutFunctionBase { }

    [Function("claimProofTimeout")]
    public class ClaimProofTimeoutFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint64", "blockNumber", 2)]
        public virtual ulong BlockNumber { get; set; }
    }

    public partial class FulfillBlockProofFunction : FulfillBlockProofFunctionBase { }

    [Function("fulfillBlockProof")]
    public class FulfillBlockProofFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint64", "blockNumber", 2)]
        public virtual ulong BlockNumber { get; set; }
        [Parameter("uint64", "anchorEndBlock", 3)]
        public virtual ulong AnchorEndBlock { get; set; }
        [Parameter("bytes32", "blockHash", 4)]
        public virtual byte[] BlockHash { get; set; }
        [Parameter("bytes32", "preStateRoot", 5)]
        public virtual byte[] PreStateRoot { get; set; }
        [Parameter("bytes32", "postStateRoot", 6)]
        public virtual byte[] PostStateRoot { get; set; }
        [Parameter("bytes32[]", "merkleProof", 7)]
        public virtual List<byte[]> MerkleProof { get; set; }
        [Parameter("bytes", "zkProof", 8)]
        public virtual byte[] ZkProof { get; set; }
        [Parameter("uint8", "proofSystem", 9)]
        public virtual byte ProofSystem { get; set; }
    }

    public partial class GetBlockProofFunction : GetBlockProofFunctionBase { }

    [Function("getBlockProof", typeof(GetBlockProofOutputDTO))]
    public class GetBlockProofFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint64", "blockNumber", 2)]
        public virtual ulong BlockNumber { get; set; }
    }

    public partial class IsBlockProvenFunction : IsBlockProvenFunctionBase { }

    [Function("isBlockProven", "bool")]
    public class IsBlockProvenFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint64", "blockNumber", 2)]
        public virtual ulong BlockNumber { get; set; }
    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class PauseFunction : PauseFunctionBase { }

    [Function("pause")]
    public class PauseFunctionBase : FunctionMessage
    {

    }

    public partial class PausedFunction : PausedFunctionBase { }

    [Function("paused", "bool")]
    public class PausedFunctionBase : FunctionMessage
    {

    }

    public partial class PendingWithdrawalsFunction : PendingWithdrawalsFunctionBase { }

    [Function("pendingWithdrawals", "uint256")]
    public class PendingWithdrawalsFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ProofBondFunction : ProofBondFunctionBase { }

    [Function("proofBond", "uint256")]
    public class ProofBondFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class ProofRequestsFunction : ProofRequestsFunctionBase { }

    [Function("proofRequests", typeof(ProofRequestsOutputDTO))]
    public class ProofRequestsFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
        [Parameter("uint64", "", 2)]
        public virtual ulong ReturnValue2 { get; set; }
    }

    public partial class ProofWindowFunction : ProofWindowFunctionBase { }

    [Function("proofWindow", "uint256")]
    public class ProofWindowFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class RequestBlockProofFunction : RequestBlockProofFunctionBase { }

    [Function("requestBlockProof")]
    public class RequestBlockProofFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint64", "blockNumber", 2)]
        public virtual ulong BlockNumber { get; set; }
    }

    public partial class SetProofBondFunction : SetProofBondFunctionBase { }

    [Function("setProofBond")]
    public class SetProofBondFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint256", "newBond", 2)]
        public virtual BigInteger NewBond { get; set; }
    }

    public partial class SetProofWindowFunction : SetProofWindowFunctionBase { }

    [Function("setProofWindow")]
    public class SetProofWindowFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint256", "newWindow", 2)]
        public virtual BigInteger NewWindow { get; set; }
    }

    public partial class SubmitBlockProofFunction : SubmitBlockProofFunctionBase { }

    [Function("submitBlockProof")]
    public class SubmitBlockProofFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint64", "blockNumber", 2)]
        public virtual ulong BlockNumber { get; set; }
        [Parameter("uint64", "anchorEndBlock", 3)]
        public virtual ulong AnchorEndBlock { get; set; }
        [Parameter("bytes32", "blockHash", 4)]
        public virtual byte[] BlockHash { get; set; }
        [Parameter("bytes32", "preStateRoot", 5)]
        public virtual byte[] PreStateRoot { get; set; }
        [Parameter("bytes32", "postStateRoot", 6)]
        public virtual byte[] PostStateRoot { get; set; }
        [Parameter("bytes32[]", "merkleProof", 7)]
        public virtual List<byte[]> MerkleProof { get; set; }
        [Parameter("bytes", "zkProof", 8)]
        public virtual byte[] ZkProof { get; set; }
        [Parameter("uint8", "proofSystem", 9)]
        public virtual byte ProofSystem { get; set; }
    }

    public partial class UnpauseFunction : UnpauseFunctionBase { }

    [Function("unpause")]
    public class UnpauseFunctionBase : FunctionMessage
    {

    }

    public partial class WithdrawBondFunction : WithdrawBondFunctionBase { }

    [Function("withdrawBond")]
    public class WithdrawBondFunctionBase : FunctionMessage
    {

    }

    public partial class MaxProofBondOutputDTO : MaxProofBondOutputDTOBase { }

    [FunctionOutput]
    public class MaxProofBondOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class MaxProofWindowOutputDTO : MaxProofWindowOutputDTOBase { }

    [FunctionOutput]
    public class MaxProofWindowOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class MinProofWindowOutputDTO : MinProofWindowOutputDTOBase { }

    [FunctionOutput]
    public class MinProofWindowOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class AnchorOutputDTO : AnchorOutputDTOBase { }

    [FunctionOutput]
    public class AnchorOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class BlockProofsOutputDTO : BlockProofsOutputDTOBase { }

    [FunctionOutput]
    public class BlockProofsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "prover", 1)]
        public virtual string Prover { get; set; }
        [Parameter("uint64", "timestamp", 2)]
        public virtual ulong Timestamp { get; set; }
        [Parameter("uint8", "proofSystem", 3)]
        public virtual byte ProofSystem { get; set; }
        [Parameter("bool", "verified", 4)]
        public virtual bool Verified { get; set; }
    }





    public partial class GetBlockProofOutputDTO : GetBlockProofOutputDTOBase { }

    [FunctionOutput]
    public class GetBlockProofOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "prover", 1)]
        public virtual string Prover { get; set; }
        [Parameter("uint64", "timestamp", 2)]
        public virtual ulong Timestamp { get; set; }
        [Parameter("uint8", "proofSystem", 3)]
        public virtual byte ProofSystem { get; set; }
        [Parameter("bool", "verified", 4)]
        public virtual bool Verified { get; set; }
    }

    public partial class IsBlockProvenOutputDTO : IsBlockProvenOutputDTOBase { }

    [FunctionOutput]
    public class IsBlockProvenOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class PausedOutputDTO : PausedOutputDTOBase { }

    [FunctionOutput]
    public class PausedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class PendingWithdrawalsOutputDTO : PendingWithdrawalsOutputDTOBase { }

    [FunctionOutput]
    public class PendingWithdrawalsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class ProofBondOutputDTO : ProofBondOutputDTOBase { }

    [FunctionOutput]
    public class ProofBondOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class ProofRequestsOutputDTO : ProofRequestsOutputDTOBase { }

    [FunctionOutput]
    public class ProofRequestsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "requester", 1)]
        public virtual string Requester { get; set; }
        [Parameter("uint256", "bond", 2)]
        public virtual BigInteger Bond { get; set; }
        [Parameter("uint256", "deadline", 3)]
        public virtual BigInteger Deadline { get; set; }
        [Parameter("bool", "fulfilled", 4)]
        public virtual bool Fulfilled { get; set; }
    }

    public partial class ProofWindowOutputDTO : ProofWindowOutputDTOBase { }

    [FunctionOutput]
    public class ProofWindowOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }













    public partial class BlockProofSubmittedEventDTO : BlockProofSubmittedEventDTOBase { }

    [Event("BlockProofSubmitted")]
    public class BlockProofSubmittedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "chainId", 1, true )]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint64", "blockNumber", 2, true )]
        public virtual ulong BlockNumber { get; set; }
        [Parameter("address", "prover", 3, false )]
        public virtual string Prover { get; set; }
        [Parameter("uint8", "proofSystem", 4, false )]
        public virtual byte ProofSystem { get; set; }
    }

    public partial class BondCreditedEventDTO : BondCreditedEventDTOBase { }

    [Event("BondCredited")]
    public class BondCreditedEventDTOBase : IEventDTO
    {
        [Parameter("address", "recipient", 1, true )]
        public virtual string Recipient { get; set; }
        [Parameter("uint256", "amount", 2, false )]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class BondWithdrawnEventDTO : BondWithdrawnEventDTOBase { }

    [Event("BondWithdrawn")]
    public class BondWithdrawnEventDTOBase : IEventDTO
    {
        [Parameter("address", "recipient", 1, true )]
        public virtual string Recipient { get; set; }
        [Parameter("uint256", "amount", 2, false )]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class PausedEventDTO : PausedEventDTOBase { }

    [Event("Paused")]
    public class PausedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, false )]
        public virtual string Account { get; set; }
    }

    public partial class ProofBondChangedEventDTO : ProofBondChangedEventDTOBase { }

    [Event("ProofBondChanged")]
    public class ProofBondChangedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "chainId", 1, true )]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint256", "oldBond", 2, false )]
        public virtual BigInteger OldBond { get; set; }
        [Parameter("uint256", "newBond", 3, false )]
        public virtual BigInteger NewBond { get; set; }
    }

    public partial class ProofRequestExpiredEventDTO : ProofRequestExpiredEventDTOBase { }

    [Event("ProofRequestExpired")]
    public class ProofRequestExpiredEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "chainId", 1, true )]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint64", "blockNumber", 2, true )]
        public virtual ulong BlockNumber { get; set; }
        [Parameter("address", "requester", 3, false )]
        public virtual string Requester { get; set; }
        [Parameter("uint256", "compensation", 4, false )]
        public virtual BigInteger Compensation { get; set; }
    }

    public partial class ProofRequestFulfilledEventDTO : ProofRequestFulfilledEventDTOBase { }

    [Event("ProofRequestFulfilled")]
    public class ProofRequestFulfilledEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "chainId", 1, true )]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint64", "blockNumber", 2, true )]
        public virtual ulong BlockNumber { get; set; }
        [Parameter("address", "prover", 3, false )]
        public virtual string Prover { get; set; }
        [Parameter("address", "requester", 4, false )]
        public virtual string Requester { get; set; }
    }

    public partial class ProofRequestedEventDTO : ProofRequestedEventDTOBase { }

    [Event("ProofRequested")]
    public class ProofRequestedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "chainId", 1, true )]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint64", "blockNumber", 2, true )]
        public virtual ulong BlockNumber { get; set; }
        [Parameter("address", "requester", 3, false )]
        public virtual string Requester { get; set; }
        [Parameter("uint256", "bond", 4, false )]
        public virtual BigInteger Bond { get; set; }
        [Parameter("uint256", "deadline", 5, false )]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class ProofWindowChangedEventDTO : ProofWindowChangedEventDTOBase { }

    [Event("ProofWindowChanged")]
    public class ProofWindowChangedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "chainId", 1, true )]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint256", "oldWindow", 2, false )]
        public virtual BigInteger OldWindow { get; set; }
        [Parameter("uint256", "newWindow", 3, false )]
        public virtual BigInteger NewWindow { get; set; }
    }

    public partial class UnpausedEventDTO : UnpausedEventDTOBase { }

    [Event("Unpaused")]
    public class UnpausedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, false )]
        public virtual string Account { get; set; }
    }

    public partial class EnforcedPauseError : EnforcedPauseErrorBase { }
    [Error("EnforcedPause")]
    public class EnforcedPauseErrorBase : IErrorDTO
    {
    }

    public partial class ExpectedPauseError : ExpectedPauseErrorBase { }
    [Error("ExpectedPause")]
    public class ExpectedPauseErrorBase : IErrorDTO
    {
    }

    public partial class ReentrancyGuardReentrantCallError : ReentrancyGuardReentrantCallErrorBase { }
    [Error("ReentrancyGuardReentrantCall")]
    public class ReentrancyGuardReentrantCallErrorBase : IErrorDTO
    {
    }
}
