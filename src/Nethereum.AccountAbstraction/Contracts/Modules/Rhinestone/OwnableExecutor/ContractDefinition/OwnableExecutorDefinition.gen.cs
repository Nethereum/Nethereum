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
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableExecutor.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableExecutor.ContractDefinition
{


    public partial class OwnableExecutorDeployment : OwnableExecutorDeploymentBase
    {
        public OwnableExecutorDeployment() : base(BYTECODE) { }
        public OwnableExecutorDeployment(string byteCode) : base(byteCode) { }
    }

    public class OwnableExecutorDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60808060405234601557610bf3908161001a8239f35b5f80fdfe60806040526004361015610011575f80fd5b5f3560e01c806306fdde031461084a57806354fd4d50146108075780636d61fe701461070b5780637065cb481461063a5780638a91b0e314610574578063ccfdec8c1461053c578063d26cdce3146104d7578063d60b347f1461048a578063e50860031461039c578063ecd059611461037c578063fbe5ce0a146102545763fd8b84b11461009d575f80fd5b34610250576020366003190112610250576001600160a01b036100be6108e7565b16805f525f60205260405f20905f52600160205260405f20548015610241576100e681610985565b916100f46040519384610941565b81835261010082610985565b602084019290601f190136843760015f908152602083905260408120549093906001600160a01b03165b6001600160a01b0381169081151580610236575b8061022d575b1561017d5750806101558688610b95565b525f908152602084905260409020546001600160a01b03169361017790610977565b9361012a565b8690839087906001600160a01b03166001141580610224575b6101ea575b82919252604051918291602083019060208452518091526040830191905f5b8181106101c8575050500390f35b82516001600160a01b03168452859450602093840193909201916001016101ba565b5f198101818111610210576001600160a01b03906102089085610b95565b51505061019b565b634e487b7160e01b5f52601160045260245ffd5b50801515610196565b50838610610144565b50600182141561013e565b63f725081760e01b5f5260045ffd5b5f80fd5b346102505760403660031901126102505761026d6108e7565b6024356001600160a01b038116919082900361025057335f525f60205260405f209082158015610372575b610352576001600160a01b038181165f908152602084905260409020541683900361033f575f8381526020928352604080822080546001600160a01b0394851684528284208054959091166001600160a01b031995861617905580549093169092553381526001909252902080548015610210575f190190556040519081527fe594d081b4382713733fe631966432c9cea5199afb2db5c3c1931f9f9300367960203392a2005b82637c84ecfb60e01b5f5260045260245ffd5b637c84ecfb60e01b5f9081526001600160a01b0391909116600452602490fd5b5060018314610298565b346102505760203660031901126102505760206040516002600435148152f35b6103a5366108fd565b9160018060a01b031691825f525f6020526103c460405f203390610b59565b1561047b57610430925f926040516020810190600160f81b825285602182015285602282015285602682015285602a82015260208152610405604082610941565b519051906020811061046a575b506040516335a4725960e21b81529586948593849360048501610a83565b039134905af1801561045f5761044257005b61045d903d805f833e6104558183610941565b81019061099d565b005b6040513d5f823e3d90fd5b85199060200360031b1b1686610412565b631a27eac360e11b5f5260045ffd5b34610250576020366003190112610250576001600160a01b036104ab6108e7565b165f525f60205260405f2060015f52602052602060405f2060018060a01b039054161515604051908152f35b6104e0366108fd565b9160018060a01b031691825f525f6020526104ff60405f203390610b59565b1561047b57610430925f92604051602081019085825285602182015285602282015285602682015285602a82015260208152610405604082610941565b34610250576020366003190112610250576001600160a01b0361055d6108e7565b165f526001602052602060405f2054604051908152f35b346102505760203660031901126102505760043567ffffffffffffffff8111610250576105a59036906004016108b9565b5050335f525f60205260405f2060015f528060205260405f2060018060a01b039054165b6001600160a01b03811661060c57335f5260016020525f6040812055337f9d00629762554452d03c3b45626436df6ca1c3795d05d04df882f6db481b1be05f80a2005b6001600160a01b039081165f90815260208390526040902080546001600160a01b03198116909155166105c9565b34610250576020366003190112610250576106536108e7565b335f90815260208181526040808320600184529091529020546001600160a01b0316156106f8576001600160a01b0381169081156106e55761069f90335f525f60205260405f20610ab0565b335f52600160205260405f206106b58154610977565b90556040519081527fc82bdbbf677a2462f2a7e22e4ba9abd209496b69cd7b868b3b1d28f76e09a40a60203392a2005b5063b20f76e360e01b5f5260045260245ffd5b63f91bd6f160e01b5f523360045260245ffd5b346102505760203660031901126102505760043567ffffffffffffffff81116102505761073c9036906004016108b9565b601411610250573560601c80156107f557335f525f60205260405f2060015f528060205260405f2060018060a01b039054166107e65760015f5260205260405f2060016bffffffffffffffffffffffff60a01b825416179055335f525f6020526107a98160405f20610ab0565b335f526001602052600160405f20556040519081527f1cd4a6da6e6a6f4dc754cedd54ead3b9cd0e2f5804cda2ba60506c2899fb29df60203392a2005b6329e42f3360e11b5f5260045ffd5b63b20f76e360e01b5f5260045260245ffd5b34610250575f36600319011261025057610846604051610828604082610941565b60058152640312e302e360dc1b60208201526040519182918261088f565b0390f35b34610250575f3660031901126102505761084660405161086b604082610941565b600f81526e27bbb730b13632a2bc32b1baba37b960891b6020820152604051918291825b602060409281835280519182918282860152018484015e5f828201840152601f01601f1916010190565b9181601f840112156102505782359167ffffffffffffffff8311610250576020838186019501011161025057565b600435906001600160a01b038216820361025057565b906040600319830112610250576004356001600160a01b038116810361025057916024359067ffffffffffffffff82116102505761093d916004016108b9565b9091565b90601f8019910116810190811067ffffffffffffffff82111761096357604052565b634e487b7160e01b5f52604160045260245ffd5b5f1981146102105760010190565b67ffffffffffffffff81116109635760051b60200190565b6020818303126102505780519067ffffffffffffffff821161025057019080601f83011215610250578151916109d283610985565b926109e06040519485610941565b80845260208085019160051b830101918383116102505760208101915b838310610a0c57505050505090565b825167ffffffffffffffff811161025057820185603f820112156102505760208101519167ffffffffffffffff831161096357604051610a56601f8501601f191660200182610941565b8381526040838501018810610250575f602085819660408397018386015e830101528152019201916109fd565b91926060938192845260406020850152816040850152848401375f828201840152601f01601f1916010190565b6001600160a01b039091169081158015610b4f575b610b3c575f828152602082905260409020546001600160a01b0316610b295760015f8181526020929092526040808320805485855291842080546001600160a01b039093166001600160a01b03199384161790559190925280549091169091179055565b50631034f46960e21b5f5260045260245ffd5b50637c84ecfb60e01b5f5260045260245ffd5b5060018214610ac5565b6001600160a01b038216600114159182610b7257505090565b6001600160a01b039081165f908152602092909252604090912054161515919050565b8051821015610ba95760209160051b010190565b634e487b7160e01b5f52603260045260245ffdfea2646970667358221220229c6f3b4d8131153a2016b8e1e48d634af955154052352692369f3ac7fc52b764736f6c634300081c0033";
        public OwnableExecutorDeploymentBase() : base(BYTECODE) { }
        public OwnableExecutorDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class AddOwnerFunction : AddOwnerFunctionBase { }

    [Function("addOwner")]
    public class AddOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class ExecuteBatchOnOwnedAccountFunction : ExecuteBatchOnOwnedAccountFunctionBase { }

    [Function("executeBatchOnOwnedAccount")]
    public class ExecuteBatchOnOwnedAccountFunctionBase : FunctionMessage
    {
        [Parameter("address", "ownedAccount", 1)]
        public virtual string OwnedAccount { get; set; }
        [Parameter("bytes", "callData", 2)]
        public virtual byte[] CallData { get; set; }
    }

    public partial class ExecuteOnOwnedAccountFunction : ExecuteOnOwnedAccountFunctionBase { }

    [Function("executeOnOwnedAccount")]
    public class ExecuteOnOwnedAccountFunctionBase : FunctionMessage
    {
        [Parameter("address", "ownedAccount", 1)]
        public virtual string OwnedAccount { get; set; }
        [Parameter("bytes", "callData", 2)]
        public virtual byte[] CallData { get; set; }
    }

    public partial class GetOwnersFunction : GetOwnersFunctionBase { }

    [Function("getOwners", "address[]")]
    public class GetOwnersFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class IsInitializedFunction : IsInitializedFunctionBase { }

    [Function("isInitialized", "bool")]
    public class IsInitializedFunctionBase : FunctionMessage
    {
        [Parameter("address", "smartAccount", 1)]
        public virtual string SmartAccount { get; set; }
    }

    public partial class IsModuleTypeFunction : IsModuleTypeFunctionBase { }

    [Function("isModuleType", "bool")]
    public class IsModuleTypeFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "typeID", 1)]
        public virtual BigInteger TypeID { get; set; }
    }

    public partial class NameFunction : NameFunctionBase { }

    [Function("name", "string")]
    public class NameFunctionBase : FunctionMessage
    {

    }

    public partial class OnInstallFunction : OnInstallFunctionBase { }

    [Function("onInstall")]
    public class OnInstallFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "data", 1)]
        public virtual byte[] Data { get; set; }
    }

    public partial class OnUninstallFunction : OnUninstallFunctionBase { }

    [Function("onUninstall")]
    public class OnUninstallFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class OwnerCountFunction : OwnerCountFunctionBase { }

    [Function("ownerCount", "uint256")]
    public class OwnerCountFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class RemoveOwnerFunction : RemoveOwnerFunctionBase { }

    [Function("removeOwner")]
    public class RemoveOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "prevOwner", 1)]
        public virtual string PrevOwner { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
    }

    public partial class VersionFunction : VersionFunctionBase { }

    [Function("version", "string")]
    public class VersionFunctionBase : FunctionMessage
    {

    }







    public partial class GetOwnersOutputDTO : GetOwnersOutputDTOBase { }

    [FunctionOutput]
    public class GetOwnersOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address[]", "ownersArray", 1)]
        public virtual List<string> OwnersArray { get; set; }
    }

    public partial class IsInitializedOutputDTO : IsInitializedOutputDTOBase { }

    [FunctionOutput]
    public class IsInitializedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsModuleTypeOutputDTO : IsModuleTypeOutputDTOBase { }

    [FunctionOutput]
    public class IsModuleTypeOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class NameOutputDTO : NameOutputDTOBase { }

    [FunctionOutput]
    public class NameOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }





    public partial class OwnerCountOutputDTO : OwnerCountOutputDTOBase { }

    [FunctionOutput]
    public class OwnerCountOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class VersionOutputDTO : VersionOutputDTOBase { }

    [FunctionOutput]
    public class VersionOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ModuleInitializedEventDTO : ModuleInitializedEventDTOBase { }

    [Event("ModuleInitialized")]
    public class ModuleInitializedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "owner", 2, false )]
        public virtual string Owner { get; set; }
    }

    public partial class ModuleUninitializedEventDTO : ModuleUninitializedEventDTOBase { }

    [Event("ModuleUninitialized")]
    public class ModuleUninitializedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
    }

    public partial class OwnerAddedEventDTO : OwnerAddedEventDTOBase { }

    [Event("OwnerAdded")]
    public class OwnerAddedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "owner", 2, false )]
        public virtual string Owner { get; set; }
    }

    public partial class OwnerRemovedEventDTO : OwnerRemovedEventDTOBase { }

    [Event("OwnerRemoved")]
    public class OwnerRemovedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "owner", 2, false )]
        public virtual string Owner { get; set; }
    }

    public partial class InvalidOwnerError : InvalidOwnerErrorBase { }

    [Error("InvalidOwner")]
    public class InvalidOwnerErrorBase : IErrorDTO
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class LinkedlistAlreadyinitializedError : LinkedlistAlreadyinitializedErrorBase { }
    [Error("LinkedList_AlreadyInitialized")]
    public class LinkedlistAlreadyinitializedErrorBase : IErrorDTO
    {
    }

    public partial class LinkedlistEntryalreadyinlistError : LinkedlistEntryalreadyinlistErrorBase { }

    [Error("LinkedList_EntryAlreadyInList")]
    public class LinkedlistEntryalreadyinlistErrorBase : IErrorDTO
    {
        [Parameter("address", "entry", 1)]
        public virtual string Entry { get; set; }
    }

    public partial class LinkedlistInvalidentryError : LinkedlistInvalidentryErrorBase { }

    [Error("LinkedList_InvalidEntry")]
    public class LinkedlistInvalidentryErrorBase : IErrorDTO
    {
        [Parameter("address", "entry", 1)]
        public virtual string Entry { get; set; }
    }

    public partial class LinkedlistInvalidpageError : LinkedlistInvalidpageErrorBase { }
    [Error("LinkedList_InvalidPage")]
    public class LinkedlistInvalidpageErrorBase : IErrorDTO
    {
    }

    public partial class ModuleAlreadyInitializedError : ModuleAlreadyInitializedErrorBase { }

    [Error("ModuleAlreadyInitialized")]
    public class ModuleAlreadyInitializedErrorBase : IErrorDTO
    {
        [Parameter("address", "smartAccount", 1)]
        public virtual string SmartAccount { get; set; }
    }

    public partial class NotInitializedError : NotInitializedErrorBase { }

    [Error("NotInitialized")]
    public class NotInitializedErrorBase : IErrorDTO
    {
        [Parameter("address", "smartAccount", 1)]
        public virtual string SmartAccount { get; set; }
    }

    public partial class UnauthorizedAccessError : UnauthorizedAccessErrorBase { }
    [Error("UnauthorizedAccess")]
    public class UnauthorizedAccessErrorBase : IErrorDTO
    {
    }
}
