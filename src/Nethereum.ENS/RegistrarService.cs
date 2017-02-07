using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;

namespace Nethereum.ENS
{
    public class RegistrarService
    {
        public static string ABI =
            @"[{'constant':false,'inputs':[{'name':'_hash','type':'bytes32'}],'name':'releaseDeed','outputs':[],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'unhashedName','type':'string'}],'name':'invalidateName','outputs':[],'payable':false,'type':'function'},{'constant':true,'inputs':[{'name':'hash','type':'bytes32'},{'name':'owner','type':'address'},{'name':'value','type':'uint256'},{'name':'salt','type':'bytes32'}],'name':'shaBid','outputs':[{'name':'sealedBid','type':'bytes32'}],'payable':false,'type':'function'},{'constant':true,'inputs':[{'name':'','type':'bytes32'}],'name':'entries','outputs':[{'name':'status','type':'uint8'},{'name':'deed','type':'address'},{'name':'registrationDate','type':'uint256'},{'name':'value','type':'uint256'},{'name':'highestBid','type':'uint256'}],'payable':false,'type':'function'},{'constant':true,'inputs':[],'name':'ens','outputs':[{'name':'','type':'address'}],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'_hash','type':'bytes32'}],'name':'transferRegistrars','outputs':[],'payable':false,'type':'function'},{'constant':true,'inputs':[{'name':'','type':'bytes32'}],'name':'sealedBids','outputs':[{'name':'','type':'address'}],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'_hash','type':'bytes32'},{'name':'newOwner','type':'address'}],'name':'transfer','outputs':[],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'_hash','type':'bytes32'}],'name':'finalizeAuction','outputs':[],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'_hash','type':'bytes32'},{'name':'_owner','type':'address'},{'name':'_value','type':'uint256'},{'name':'_salt','type':'bytes32'}],'name':'unsealBid','outputs':[],'payable':false,'type':'function'},{'constant':true,'inputs':[],'name':'registryCreated','outputs':[{'name':'','type':'uint256'}],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'sealedBid','type':'bytes32'}],'name':'newBid','outputs':[],'payable':true,'type':'function'},{'constant':false,'inputs':[{'name':'seal','type':'bytes32'}],'name':'cancelBid','outputs':[],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'_hashes','type':'bytes32[]'}],'name':'startAuctions','outputs':[],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'_hash','type':'bytes32'}],'name':'startAuction','outputs':[],'payable':false,'type':'function'},{'constant':true,'inputs':[],'name':'rootNode','outputs':[{'name':'','type':'bytes32'}],'payable':false,'type':'function'},{'inputs':[{'name':'_ens','type':'address'},{'name':'_rootNode','type':'bytes32'}],'type':'constructor'},{'anonymous':false,'inputs':[{'indexed':true,'name':'hash','type':'bytes32'},{'indexed':false,'name':'auctionExpiryDate','type':'uint256'}],'name':'AuctionStarted','type':'event'},{'anonymous':false,'inputs':[{'indexed':true,'name':'hash','type':'bytes32'},{'indexed':false,'name':'deposit','type':'uint256'}],'name':'NewBid','type':'event'},{'anonymous':false,'inputs':[{'indexed':true,'name':'hash','type':'bytes32'},{'indexed':true,'name':'owner','type':'address'},{'indexed':false,'name':'value','type':'uint256'},{'indexed':false,'name':'status','type':'uint8'}],'name':'BidRevealed','type':'event'},{'anonymous':false,'inputs':[{'indexed':true,'name':'hash','type':'bytes32'},{'indexed':true,'name':'owner','type':'address'},{'indexed':false,'name':'value','type':'uint256'},{'indexed':false,'name':'now','type':'uint256'}],'name':'HashRegistered','type':'event'},{'anonymous':false,'inputs':[{'indexed':true,'name':'hash','type':'bytes32'},{'indexed':false,'name':'value','type':'uint256'}],'name':'HashReleased','type':'event'},{'anonymous':false,'inputs':[{'indexed':true,'name':'hash','type':'bytes32'},{'indexed':true,'name':'name','type':'string'},{'indexed':false,'name':'value','type':'uint256'},{'indexed':false,'name':'now','type':'uint256'}],'name':'HashInvalidated','type':'event'}]";

        public static string BYTE_CODE =
            "0x6060604081815280611996833960a0905251608051600080546c0100000000000000000000000080850204600160a060020a03199091161790556001819055426004555050611944806100526000396000f3606060405236156100c45760e060020a60003504630230a07c81146100c957806315f733311461016b57806322ec1244146101f8578063267b6922146102115780633f15457f146102585780635ddae2831461026f578063615849361461031157806379ce9fac14610337578063983b94fb146103dc578063aefc8c7214610426578063b88eef5314610485578063ce92dced14610493578063df7cec28146104be578063e27fe50f146105bb578063ede8acdb14610629578063faff50a814610694575b610002565b3461000257600480356000818152600260209081526040808320805482518401859052825160e060020a638da5cb5b02815292516106a29786958895610100909404600160a060020a031693638da5cb5b9381840193909182900301818987803b156100025760325a03f1156100025750506040515133600160a060020a03908116911614159050806101615750805460ff16600214155b1561070557610002565b34610002576106a26004808035906020019082018035906020019191908080601f0160208091040260200160405190810160405280939291908181526020018383808284375094965050505050505060006000600661084784805160009060018381019184010182805b828410156114ca5750825160ff1660808110156114d3576001939093019261153b565b34610002576106a460043560243560443560643561044a565b34610002576002602081905260043560009081526040902080546001820154928201546003909201546106b69360ff831693610100909304600160a060020a031692909185565b34610002576106e9600054600160a060020a031681565b3461000257600480356000818152600260209081526040808320805482518401859052825160e060020a638da5cb5b02815292516106a29786958895610100909404600160a060020a031693638da5cb5b9381840193909182900301818987803b156100025760325a03f1156100025750506040515133600160a060020a03908116911614159050806103075750805460ff16600214155b15610aa157610002565b34610002576106e9600435600360205260009081526040902054600160a060020a031681565b3461000257600480356000818152600260209081526040808320805482518401859052825160e060020a638da5cb5b02815292516106a29760243596958895610100909404600160a060020a031693638da5cb5b9381840193909182900301818987803b156100025760325a03f1156100025750506040515133600160a060020a03908116911614159050806103d25750805460ff16600214155b15610bc957610002565b34610002576106a26004356000818152600260205260408120600181015490919042108061040c57506003820154155b8061041c5750815460ff16600114155b15610c9c57610002565b34610002576106a26004356024356044356064356000600060006000610e8d888888885b60408051948552600160a060020a0393909316606060020a026020850152603484019190915260548301525160749181900391909101902090565b34610002576106a460045481565b6106a2600435600081815260036020526040812054600160a060020a0316819011156112b757610002565b34610002576106a2600435600081815260036020526040902054600160a060020a031680158061054c575062093a80600c0263ffffffff1681600160a060020a03166305b344106000604051602001526040518160e060020a028152600401809050602060405180830381600087803b156100025760325a03f1156100025750506040515191909101421090505b806105b15750600081600160a060020a0316638da5cb5b6000604051602001526040518160e060020a028152600401809050602060405180830381600087803b156100025760325a03f11561000257505060405151600160a060020a03169190911190505b1561136a57610002565b3461000257604080516020600480358082013583810280860185019096528085526106a295929460249490939285019282918501908490808284375094965050505050505060005b815181101561136657611452828281518110156100025790602001906020020151610635565b34610002576106a26004355b6000818152600260205260409020805460ff16600114801561065a5750600181015442105b806106695750805460ff166002145b806106785750805460ff166003145b8061068a5750600454630784ce000142115b1561145a57610002565b34610002576106a460015481565b005b60408051918252519081900360200190f35b60408051958652600160a060020a0390941660208601528484019290925260608401526080830152519081900360a00190f35b60408051600160a060020a039092168252519081900360200190f35b6000858152600260205260409020805460018201549195506101009004600160a060020a031693506301e13380014210806107475750600454630f099c000142115b1561075157610002565b835460ff19168455600080546001546040805160e060020a6306ab59230281526004810192909252602482018990526044820184905251600160a060020a03909216926306ab59239260648084019382900301818387803b156100025760325a03f1156100025750505082600160a060020a031663bbe427716103e86040518260e060020a02815260040180828152602001915050600060405180830381600087803b156100025760325a03f115610002575050506002840154604080519182525186917f292b79b9246fa2c8e77d3fe195b251f9cb839d7d038e667c069ee7708c631e16919081900360200190a25050505050565b111561085257610002565b82604051808280519060200190808383829060006004602084601f0104600302600f01f150905001915050604051809103902091506002600050600083600019168152602001908152602001600020600050905060038160000160006101000a81548160ff021916908360f860020a908102040217905550600060009054906101000a9004600160a060020a0316600160a060020a03166306ab59236001600050548460006040518460e060020a0281526004018084600019168152602001836000191681526020018281526020019350505050600060405180830381600087803b156100025760325a03f11561000257505081546101009004600160a060020a0316159050610a1e578060000160019054906101000a9004600160a060020a0316600160a060020a03166313af4035336040518260e060020a0281526004018082600160a060020a03168152602001915050600060405180830381600087803b156100025760325a03f11561000257505081546040805160e060020a63bbe42771028152606460048201529051610100909204600160a060020a0316925063bbe4277191602480830192600092919082900301818387803b156100025760325a03f115610002575050505b82604051808280519060200190808383829060006004602084601f0104600302600f01f150905001915050604051809103902082600019167f1f9c649fe47e58bb60f4e52f0d90e4c47a526c9f90c5113df842c025970b66ad836002016000505442604051808381526020018281526020019250505060405180910390a3505050565b6000805460015460408051602090810185905281517f02571be300000000000000000000000000000000000000000000000000000000815260048101939093529051600160a060020a03909316936302571be3936024808501949192918390030190829087803b156100025760325a03f1156100025750506040515194505030600160a060020a039081169085161415610b3a57610002565b600085815260026020526040808220805482517ffaab9d39000000000000000000000000000000000000000000000000000000008152600160a060020a03898116600483015293519297506101009091049092169263faab9d39926024808201939182900301818387803b156100025760325a03f115610002575050835460ff19166003178455505050505050565b6000858152600260205260408082208054825160e060020a6313af4035028152600160a060020a0389811660048301529351929750610100909104909216926313af4035926024808201939182900301818387803b156100025760325a03f115610002575050600080546001546040805160e060020a6306ab59230281526004810192909252602482018a9052600160a060020a0389811660448401529051921693506306ab592392606480830193919282900301818387803b156100025760325a03f115610002575050505050505050565b815460ff191660029081178355820154610cca90662386f26fc100005b600081831115611546575081611549565b6002830155600080546001548454604080516020908101869052815160e060020a638da5cb5b0281529151600160a060020a03958616966306ab5923968b9561010090041693638da5cb5b93600480830194919391928390030190829087803b156100025760325a03f11561000257505060408051805160e060020a8702825260048201959095526024810193909352600160a060020a039093166044830152509051606480830192600092919082900301818387803b156100025760325a03f115610002575050825460028401546040805160e160020a637d8b34e5028152600481019290925251610100909204600160a060020a0316935083925063fb1669ca91602480830192600092919082900301818387803b156100025760325a03f1156100025750505080600160a060020a0316638da5cb5b6000604051602001526040518160e060020a028152600401809050602060405180830381600087803b156100025760325a03f115610002575050604080518051600286015482524260208301528251600160a060020a03909116935086927f0f0c27adfd84b60b6f456b0e87cdccb1e5fb9603991588d87fa99f5b6b61e670928290030190a3505050565b600081815260036020526040902054909450600160a060020a03169250821515610eb657610002565b6000848152600360205260408082208054600160a060020a0319169055805160e060020a6313af4035028152600160a060020a038a811660048301529151918616926313af40359260248084019382900301818387803b156100025760325a03f11561000257505050600088815260026020908152604080832060018101548251840185905282517f05b3441000000000000000000000000000000000000000000000000000000000815292519196506201517f190193600160a060020a038816936305b34410936004808201949293918390030190829087803b156100025760325a03f1156100025750506040515191909111905080610fba5750600182015442115b80610fcb5750662386f26fc1000086105b156110525782600160a060020a031663bbe42771600a6040518260e060020a02815260040180828152602001915050600060405180830381600087803b156100025760325a03f11561000257505060408051888152600060208201528151600160a060020a038b1693508b92600080516020611924833981519152928290030190a36112ad565b60038201548611156111915781546101009004600160a060020a0316156110d0575080546040805160e060020a63bbe427710281526103e760048201529051610100909204600160a060020a031691829163bbe4277191602480830192600092919082900301818387803b156100025760325a03f115610002575050505b6003820180546002840155869055815474ffffffffffffffffffffffffffffffffffffffff001916610100606060020a85810204021782556040805160e160020a637d8b34e5028152600481018890529051600160a060020a0385169163fb1669ca91602480830192600092919082900301818387803b156100025760325a03f11561000257505060408051888152600260208201528151600160a060020a038b1693508b92600080516020611924833981519152928290030190a36112ad565b600282015486111561122e57600282018690556040805160e060020a63bbe427710281526103e760048201529051600160a060020a0385169163bbe4277191602480830192600092919082900301818387803b156100025760325a03f11561000257505060408051888152600360208201528151600160a060020a038b1693508b92600080516020611924833981519152928290030190a36112ad565b82600160a060020a031663bbe427716103e76040518260e060020a02815260040180828152602001915050600060405180830381600087803b156100025760325a03f11561000257505060408051888152600460208201528151600160a060020a038b1693508b92600080516020611924833981519152928290030190a35b5050505050505050565b6040516103d480611550833901809050604051809103906000f08015610002576000838152600360209081526040918290208054600160a060020a031916606060020a858102041790558151348152915192935084927fdb578ec7204282ed3ffcec84ef9d2ca9adda7fb0c0b707010bad5cce9f18f41f9281900390910190a2604051600160a060020a038216903480156108fc02916000818181858888f19350505050151561136657610002565b5050565b80600160a060020a03166313af4035336040518260e060020a0281526004018082600160a060020a03168152602001915050600060405180830381600087803b156100025760325a03f1156100025750505080600160a060020a031663bbe4277160056040518260e060020a02815260040180828152602001915050600060405180830381600087803b156100025760325a03f1156100025750505060008281526003602090815260408083208054600160a060020a031916905580518381526005928101929092528051859260008051602061192483398151915292908290030190a35050565b600101610603565b600454611472904262093a8001906212750001610cb9565b6001808301829055825460ff19161782556000600283018190556003830155604080519182525183917f87e97e825a1d1fa0c54e1d36c7506c1dea8b1efd451fe68b000cf96f7cf40003919081900360200190a25050565b50949350505050565b60e08160ff1610156114eb576002939093019261153b565b60f08160ff161015611503576003939093019261153b565b60f88160ff16101561151b576004939093019261153b565b60fc8160ff161015611533576005939093019261153b565b600693909301925b6001909101906101d5565b50805b9291505056006060604052600080546c0100000000000000000000000033810204600160a060020a0319909116179055426001556002805460a060020a60ff02191674010000000000000000000000000000000000000000179055610372806100626000396000f36060604052361561006c5760e060020a600035046305b34410811461006e5780630b5ab3d51461007c57806313af4035146100895780632b20e397146100af5780638da5cb5b146100c6578063bbe42771146100dd578063faab9d3914610103578063fb1669ca14610129575b005b346100025761014a60015481565b346100025761006c610189565b346100025761006c60043560005433600160a060020a039081169116146101f557610002565b34610002576101a0600054600160a060020a031681565b34610002576101a0600254600160a060020a031681565b346100025761006c60043560005433600160a060020a0390811691161461026657610002565b346100025761006c60043560005433600160a060020a039081169116146102d657610002565b61006c60043560005433600160a060020a0390811691161461030b57610002565b60408051918252519081900360200190f35b6040517fbb2ce2f51803bba16bc85282b47deeea9a5c6223eabea1077be696b3f265cf1390600090a16102635b60025460a060020a900460ff16156101bc57610002565b60408051600160a060020a039092168252519081900360200190f35b600254604051600160a060020a039182169130163180156108fc02916000818181858888f19350505050156101f05761deadff5b610002565b6002805473ffffffffffffffffffffffffffffffffffffffff19166c010000000000000000000000008381020417905560408051600160a060020a038316815290517fa2ea9883a321a3e97b8266c2b078bfeec6d50c711ed71f874a90d500ae2eaf36916020908290030190a15b50565b60025460a060020a900460ff16151561027e57610002565b6002805474ff00000000000000000000000000000000000000001916905560405161dead906103e8600160a060020a03301631848203020480156108fc02916000818181858888f19350505050151561015c57610002565b600080546c010000000000000000000000008084020473ffffffffffffffffffffffffffffffffffffffff1990911617905550565b60025460a060020a900460ff16151561032357610002565b8030600160a060020a031631101561033a57610002565b600254604051600160a060020a039182169130163183900380156108fc02916000818181858888f19350505050151561026357610002567b6c4b278d165a6b33958f8ea5dfb00c8c9d4d0acf1985bef5d10786898bc3e7";

        private readonly Web3.Web3 web3;

        private readonly Contract contract;

        public RegistrarService(Web3.Web3 web3, string address)
        {
            this.web3 = web3;
            contract = web3.Eth.GetContract(ABI, address);
        }

        public Task<string> CancelBidAsync(string addressFrom, byte[] seal, HexBigInteger gas = null,
            HexBigInteger valueAmount = null)
        {
            var function = GetFunctionCancelBid();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, seal);
        }

        public static Task<string> DeployContractAsync(Web3.Web3 web3, string addressFrom, string _ens, byte[] _rootNode,
            HexBigInteger gas = null, HexBigInteger valueAmount = null)
        {
            return web3.Eth.DeployContract
                .SendRequestAsync(ABI, BYTE_CODE, addressFrom, gas, valueAmount, _ens, _rootNode);
        }

        public Task<string> EnsAsyncCall()
        {
            var function = GetFunctionEns();
            return function.CallAsync<string>();
        }

        public Task<EntriesDTO> EntriesAsyncCall(byte[] a)
        {
            var function = GetFunctionEntries();
            return function.CallDeserializingToObjectAsync<EntriesDTO>(a);
        }

        public Task<string> FinalizeAuctionAsync(string addressFrom, byte[] _hash, HexBigInteger gas = null,
            HexBigInteger valueAmount = null)
        {
            var function = GetFunctionFinalizeAuction();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, _hash);
        }

        public Event GetEventAuctionStarted()
        {
            return contract.GetEvent("AuctionStarted");
        }

        public Event GetEventBidRevealed()
        {
            return contract.GetEvent("BidRevealed");
        }

        public Event GetEventHashInvalidated()
        {
            return contract.GetEvent("HashInvalidated");
        }

        public Event GetEventHashRegistered()
        {
            return contract.GetEvent("HashRegistered");
        }

        public Event GetEventHashReleased()
        {
            return contract.GetEvent("HashReleased");
        }

        public Event GetEventNewBid()
        {
            return contract.GetEvent("NewBid");
        }

        public Function GetFunctionCancelBid()
        {
            return contract.GetFunction("cancelBid");
        }

        public Function GetFunctionEns()
        {
            return contract.GetFunction("ens");
        }

        public Function GetFunctionEntries()
        {
            return contract.GetFunction("entries");
        }

        public Function GetFunctionFinalizeAuction()
        {
            return contract.GetFunction("finalizeAuction");
        }

        public Function GetFunctionInvalidateName()
        {
            return contract.GetFunction("invalidateName");
        }

        public Function GetFunctionNewBid()
        {
            return contract.GetFunction("newBid");
        }

        public Function GetFunctionRegistryCreated()
        {
            return contract.GetFunction("registryCreated");
        }

        public Function GetFunctionReleaseDeed()
        {
            return contract.GetFunction("releaseDeed");
        }

        public Function GetFunctionRootNode()
        {
            return contract.GetFunction("rootNode");
        }

        public Function GetFunctionSealedBids()
        {
            return contract.GetFunction("sealedBids");
        }

        public Function GetFunctionShaBid()
        {
            return contract.GetFunction("shaBid");
        }

        public Function GetFunctionStartAuction()
        {
            return contract.GetFunction("startAuction");
        }

        public Function GetFunctionStartAuctions()
        {
            return contract.GetFunction("startAuctions");
        }

        public Function GetFunctionTransfer()
        {
            return contract.GetFunction("transfer");
        }

        public Function GetFunctionTransferRegistrars()
        {
            return contract.GetFunction("transferRegistrars");
        }

        public Function GetFunctionUnsealBid()
        {
            return contract.GetFunction("unsealBid");
        }

        public Task<string> InvalidateNameAsync(string addressFrom, string unhashedName, HexBigInteger gas = null,
            HexBigInteger valueAmount = null)
        {
            var function = GetFunctionInvalidateName();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, unhashedName);
        }

        public Task<string> NewBidAsync(string addressFrom, byte[] sealedBid, HexBigInteger gas = null,
            HexBigInteger valueAmount = null)
        {
            var function = GetFunctionNewBid();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, sealedBid);
        }

        public Task<BigInteger> RegistryCreatedAsyncCall()
        {
            var function = GetFunctionRegistryCreated();
            return function.CallAsync<BigInteger>();
        }

        public Task<string> ReleaseDeedAsync(string addressFrom, byte[] _hash, HexBigInteger gas = null,
            HexBigInteger valueAmount = null)
        {
            var function = GetFunctionReleaseDeed();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, _hash);
        }

        public Task<byte[]> RootNodeAsyncCall()
        {
            var function = GetFunctionRootNode();
            return function.CallAsync<byte[]>();
        }

        public Task<string> SealedBidsAsyncCall(byte[] a)
        {
            var function = GetFunctionSealedBids();
            return function.CallAsync<string>(a);
        }

        public Task<byte[]> ShaBidAsyncCall(byte[] hash, string owner, BigInteger value, byte[] salt)
        {
            var function = GetFunctionShaBid();
            return function.CallAsync<byte[]>(hash, owner, value, salt);
        }

        public Task<string> StartAuctionAsync(string addressFrom, byte[] _hash, HexBigInteger gas = null,
            HexBigInteger valueAmount = null)
        {
            var function = GetFunctionStartAuction();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, _hash);
        }

        public Task<string> StartAuctionsAsync(string addressFrom, byte[][] _hashes, HexBigInteger gas = null,
            HexBigInteger valueAmount = null)
        {
            var function = GetFunctionStartAuctions();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, _hashes);
        }

        public Task<string> TransferAsync(string addressFrom, byte[] _hash, string newOwner, HexBigInteger gas = null,
            HexBigInteger valueAmount = null)
        {
            var function = GetFunctionTransfer();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, _hash, newOwner);
        }

        public Task<string> TransferRegistrarsAsync(string addressFrom, byte[] _hash, HexBigInteger gas = null,
            HexBigInteger valueAmount = null)
        {
            var function = GetFunctionTransferRegistrars();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, _hash);
        }

        public Task<string> UnsealBidAsync(string addressFrom, byte[] _hash, string _owner, BigInteger _value,
            byte[] _salt, HexBigInteger gas = null, HexBigInteger valueAmount = null)
        {
            var function = GetFunctionUnsealBid();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, _hash, _owner, _value, _salt);
        }
    }

    [FunctionOutput]
    public class EntriesDTO
    {
        [Parameter("uint8", "status", 1)]
        public byte Status { get; set; }

        [Parameter("address", "deed", 2)]
        public string Deed { get; set; }

        [Parameter("uint256", "registrationDate", 3)]
        public BigInteger RegistrationDate { get; set; }

        [Parameter("uint256", "value", 4)]
        public BigInteger Value { get; set; }

        [Parameter("uint256", "highestBid", 5)]
        public BigInteger HighestBid { get; set; }
    }

    public class AuctionStartedEventDTO
    {
        [Parameter("bytes32", "hash", 1, true)]
        public byte[] Hash { get; set; }

        [Parameter("uint256", "auctionExpiryDate", 2, false)]
        public BigInteger AuctionExpiryDate { get; set; }
    }

    public class NewBidEventDTO
    {
        [Parameter("bytes32", "hash", 1, true)]
        public byte[] Hash { get; set; }

        [Parameter("uint256", "deposit", 2, false)]
        public BigInteger Deposit { get; set; }
    }

    public class BidRevealedEventDTO
    {
        [Parameter("bytes32", "hash", 1, true)]
        public byte[] Hash { get; set; }

        [Parameter("address", "owner", 2, true)]
        public string Owner { get; set; }

        [Parameter("uint256", "value", 3, false)]
        public BigInteger Value { get; set; }

        [Parameter("uint8", "status", 4, false)]
        public byte Status { get; set; }
    }

    public class HashRegisteredEventDTO
    {
        [Parameter("bytes32", "hash", 1, true)]
        public byte[] Hash { get; set; }

        [Parameter("address", "owner", 2, true)]
        public string Owner { get; set; }

        [Parameter("uint256", "value", 3, false)]
        public BigInteger Value { get; set; }

        [Parameter("uint256", "now", 4, false)]
        public BigInteger Now { get; set; }
    }

    public class HashReleasedEventDTO
    {
        [Parameter("bytes32", "hash", 1, true)]
        public byte[] Hash { get; set; }

        [Parameter("uint256", "value", 2, false)]
        public BigInteger Value { get; set; }
    }

    public class HashInvalidatedEventDTO
    {
        [Parameter("bytes32", "hash", 1, true)]
        public byte[] Hash { get; set; }

        [Parameter("string", "name", 2, true)]
        public string Name { get; set; }

        [Parameter("uint256", "value", 3, false)]
        public BigInteger Value { get; set; }

        [Parameter("uint256", "now", 4, false)]
        public BigInteger Now { get; set; }
    }
}