using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;

namespace Nethereum.Optimism.OVM_SequencerFeeVault.ContractDefinition
{


    public partial class OVM_SequencerFeeVaultDeployment : OVM_SequencerFeeVaultDeploymentBase
    {
        public OVM_SequencerFeeVaultDeployment() : base(BYTECODE) { }
        public OVM_SequencerFeeVaultDeployment(string byteCode) : base(byteCode) { }
    }

    public class OVM_SequencerFeeVaultDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "608060405234801561001057600080fd5b5060405161034b38038061034b83398101604081905261002f91610054565b600080546001600160a01b0319166001600160a01b0392909216919091179055610084565b60006020828403121561006657600080fd5b81516001600160a01b038116811461007d57600080fd5b9392505050565b6102b8806100936000396000f3fe6080604052600436106100385760003560e01c80633ccfd60b14610044578063d3e5792b1461005b578063d4ff92181461008a57600080fd5b3661003f57005b600080fd5b34801561005057600080fd5b506100596100c2565b005b34801561006757600080fd5b5061007767d02ab486cedc000081565b6040519081526020015b60405180910390f35b34801561009657600080fd5b506000546100aa906001600160a01b031681565b6040516001600160a01b039091168152602001610081565b67d02ab486cedc000047101561016a5760405162461bcd60e51b815260206004820152605760248201527f4f564d5f53657175656e6365724665655661756c743a2077697468647261776160448201527f6c20616d6f756e74206d7573742062652067726561746572207468616e206d6960648201527f6e696d756d207769746864726177616c20616d6f756e74000000000000000000608482015260a40160405180910390fd5b60008054604080516020810182528381529051631474f2a960e31b81526010602160991b019363a3a79548936101c99373deaddeaddeaddeaddeaddeaddeaddeaddead0000936001600160a01b039092169247929091906004016101fd565b600060405180830381600087803b1580156101e357600080fd5b505af11580156101f7573d6000803e3d6000fd5b50505050565b600060018060a01b03808816835260208188168185015286604085015263ffffffff8616606085015260a06080850152845191508160a085015260005b828110156102565785810182015185820160c00152810161023a565b8281111561026857600060c084870101525b5050601f01601f19169190910160c001969550505050505056fea26469706673582212209b90daa7c2bb57c7794ebcc324de3518076ef4de858d1871771c371d5bf2b68464736f6c634300080b0033";
        public OVM_SequencerFeeVaultDeploymentBase() : base(BYTECODE) { }
        public OVM_SequencerFeeVaultDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_l1FeeWallet", 1)]
        public virtual string L1FeeWallet { get; set; }
    }

    public partial class MIN_WITHDRAWAL_AMOUNTFunction : MIN_WITHDRAWAL_AMOUNTFunctionBase { }

    [Function("MIN_WITHDRAWAL_AMOUNT", "uint256")]
    public class MIN_WITHDRAWAL_AMOUNTFunctionBase : FunctionMessage
    {

    }

    public partial class L1FeeWalletFunction : L1FeeWalletFunctionBase { }

    [Function("l1FeeWallet", "address")]
    public class L1FeeWalletFunctionBase : FunctionMessage
    {

    }

    public partial class WithdrawFunction : WithdrawFunctionBase { }

    [Function("withdraw")]
    public class WithdrawFunctionBase : FunctionMessage
    {

    }

    public partial class MIN_WITHDRAWAL_AMOUNTOutputDTO : MIN_WITHDRAWAL_AMOUNTOutputDTOBase { }

    [FunctionOutput]
    public class MIN_WITHDRAWAL_AMOUNTOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class L1FeeWalletOutputDTO : L1FeeWalletOutputDTOBase { }

    [FunctionOutput]
    public class L1FeeWalletOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }


}
