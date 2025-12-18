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

namespace Nethereum.Circles.Contracts.InflationaryCircles.ContractDefinition
{


    public partial class InflationaryCirclesDeployment : InflationaryCirclesDeploymentBase
    {
        public InflationaryCirclesDeployment() : base(BYTECODE) { }
        public InflationaryCirclesDeployment(string byteCode) : base(byteCode) { }
    }

    public class InflationaryCirclesDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x610260604052681800000000000000006080908152682ffec785b3614dbacc60a0526847fc56a0fe9220851660c052685ff8ad61c531c51ed460e0526877f3cbd7ea10a8394961010052688fedb2134f3060fc826101205268a7e66023d5c3bb8c4e6101405268bfddd6195e2ec38ca76101605268d7d41403c806cea59c6101805268efc919f2f2128706a76101a052690107bce7f6ba49f5e9836101c05269011faf7e1efdd68e14726101e052690137a0dc7b9913365c016102005269014f91031c678c54243d610220526901677ff21143ffd5e163610240526100e890600d90600f6103b1565b503480156100f4575f80fd5b505f5b600e60ff8216116101725761011767fff2fae779633d1e60ff831661018b565b60158260ff16600f811061012d5761012d61046c565b600291828204019190066010026101000a8154816001600160801b030219169083600f0b6001600160801b03160217905550808061016a90610480565b9150506100f7565b50601f80546001600160a01b03191660011790556104aa565b5f805f84600f0b1280156101a25750826001166001145b90505f8085600f0b126101b557846101b9565b845f035b6001600160801b03169050600160801b68010000000000000000821161025357603f82901b91505b841561024b5760018516156101f6578102607f1c5b908002607f1c90600285161561020c578102607f1c5b908002607f1c906004851615610222578102607f1c5b908002607f1c906008851615610238578102607f1c5b60049490941c93908002607f1c906101e1565b60401c61036e565b603f6c010000000000000000000000008310156102765760209290921b91601f19015b600160701b83101561028e5760109290921b91600f19015b600160781b8310156102a65760089290921b91600719015b6001607c1b8310156102be5760049290921b91600319015b6001607e1b8310156102d65760029290921b91600119015b6001607f1b8310156102ed5760019290921b915f19015b5f5b86156103585760408210610301575f80fd5b600187161561032757918302607f1c918101600160801b83111561032757600192831c92015b928002607f1c9260019190911b90600160801b841061034c57600193841c9391909101905b600187901c96506102ef565b60408110610364575f80fd5b6040039190911c90505b5f8361037a578161037e565b815f035b905060016001607f1b0319811280159061039f575060016001607f1b038113155b6103a7575f80fd5b9695505050505050565b600883019183908215610448579160200282015f5b8382111561041357835183826101000a8154816001600160801b030219169083600f0b6001600160801b031602179055509260200192601001602081600f010492830192600103026103c6565b80156104465782816101000a8154906001600160801b030219169055601001602081600f01049283019260010302610413565b505b50610454929150610458565b5090565b5b80821115610454575f8155600101610459565b634e487b7160e01b5f52603260045260245ffd5b5f60ff821660ff81036104a157634e487b7160e01b5f52601160045260245ffd5b60010192915050565b612069806104b75f395ff3fe608060405234801561000f575f80fd5b50600436106101dc575f3560e01c80637c234d0111610109578063a457c2d71161009e578063dd62ed3e1161006e578063dd62ed3e1461044d578063de0e9a3e14610485578063e8c6f90914610498578063f23a6e61146104ab575f80fd5b8063a457c2d7146103e8578063a9059cbb146103fb578063bc197c811461040e578063d505accf1461043a575f80fd5b80638ff12b6d116100d95780638ff12b6d1461039157806395d89b41146103bc57806396ef1444146103c457806399342aa3146103d5575f80fd5b80637c234d01146103405780637cf637cd146103495780637ecebe001461036357806384b0196e14610376575f80fd5b80633644e5151161017f5780635aef7de61161014f5780635aef7de6146102d057806370a08231146102e35780637421c1d71461030b57806377b8b1c71461032b575f80fd5b80633644e51514610277578063365a86fc1461027f57806339509351146102aa5780634eb7221a146102bd575f80fd5b806318160ddd116101ba57806318160ddd1461023057806323b872dd14610242578063253dd0b514610255578063313ce56714610268575f80fd5b806301ffc9a7146101e057806306fdde0314610208578063095ea7b31461021d575b5f80fd5b6101f36101ee366004611811565b6104be565b60405190151581526020015b60405180910390f35b6102106104f4565b6040516101ff919061186d565b6101f361022b36600461189a565b61058c565b601d545b6040519081526020016101ff565b6101f36102503660046118c2565b6105a1565b610234610263366004611911565b6105c2565b604051601281526020016101ff565b6102346105f4565b601f54610292906001600160a01b031681565b6040516001600160a01b0390911681526020016101ff565b6101f36102b836600461189a565b610602565b602054610292906001600160a01b031681565b602154610292906001600160a01b031681565b6102346102f136600461193b565b6001600160a01b03165f908152601e602052604090205490565b61031e610319366004611a14565b61063b565b6040516101ff9190611a8f565b61033e610339366004611aa1565b6106fd565b005b610234600c5481565b61023461035736600461193b565b6001600160a01b031690565b61023461037136600461193b565b61084f565b61037e61086c565b6040516101ff9796959493929190611ae1565b6103a461039f366004611b50565b6108ae565b6040516001600160401b0390911681526020016101ff565b6102106108cb565b6021546001600160a01b0316610234565b6102346103e3366004611911565b610969565b6101f36103f636600461189a565b610986565b6101f361040936600461189a565b6109cd565b61042161041c366004611bdf565b6109d9565b6040516001600160e01b031990911681526020016101ff565b61033e610448366004611c81565b610a2e565b61023461045b366004611cee565b6001600160a01b039182165f908152600b6020908152604080832093909416825291909152205490565b61033e610493366004611b50565b610b64565b61031e6104a6366004611a14565b610c44565b6104216104b9366004611d16565b610cf1565b5f6001600160e01b03198216630271189760e51b14806104ee57506301ffc9a760e01b6001600160e01b03198316145b92915050565b60205460215460405162cc244960e11b81526001600160a01b03918216600482015260609291909116906301984892906024015f60405180830381865afa158015610541573d5f803e3d5ffd5b505050506040513d5f823e601f3d908101601f191682016040526105689190810190611d75565b6040516020016105789190611e00565b604051602081830303815290604052905090565b5f610598338484610dc5565b50600192915050565b5f6105ad843384610e78565b6105b8848484610ef2565b5060019392505050565b5f806105e06801000d05c213d3f237846001600160401b0316610fb1565b90506105ec81856111e0565b949350505050565b5f6105fd611244565b905090565b335f818152600b602090815260408083206001600160a01b038716845290915281205490916105b890856106368685611e36565b610dc5565b60605f61065967fff2fae779633d1e846001600160401b0316610fb1565b90505f84516001600160401b0381111561067557610675611954565b60405190808252806020026020018201604052801561069e578160200160208202803683370190505b5090505f5b85518110156106f4576106cf838783815181106106c2576106c2611e49565b60200260200101516111e0565b8282815181106106e1576106e1611e49565b60209081029190910101526001016106a3565b50949350505050565b601f546001600160a01b03161561072757604051635bbc3edf60e01b815260040160405180910390fd5b6001600160a01b0383166107565760405163d82c8fc960e01b8152600b60048201526024015b60405180910390fd5b6001600160a01b0382166107805760405163d82c8fc960e01b8152600c600482015260240161074d565b6001600160a01b0381166107aa5760405163d82c8fc960e01b8152600d600482015260240161074d565b601f80546001600160a01b038086166001600160a01b0319928316811790935560218054858316908416179055602080549186169190921617815560408051637c234d0160e01b81529051637c234d01926004808401939192918290030181865afa15801561081b573d5f803e3d5ffd5b505050506040513d601f19601f8201168201806040525081019061083f9190611e5d565b600c5561084a611275565b505050565b6001600160a01b0381165f908152600a60205260408120546104ee565b5f6060805f805f606061087d6112ba565b6108856112cb565b604080515f80825260208201909252600f60f81b9b939a50919850469750309650945092509050565b5f62015180600c54836108c19190611e74565b6104ee9190611e87565b6040805180820182526002815261732d60f01b6020808301919091525460215492516354371abb60e11b81526001600160a01b0393841660048201526060939091169063a86e3576906024015f60405180830381865afa158015610931573d5f803e3d5ffd5b505050506040513d5f823e601f3d908101601f191682016040526109589190810190611d75565b604051602001610578929190611ea6565b5f806105e067fff2fae779633d1e846001600160401b0316610fb1565b335f908152600b602090815260408083206001600160a01b03861684529091528120548083106109c0576109bb33855f610dc5565b6105b8565b6105b83385858403610dc5565b5f610598338484610ef2565b601f545f9060e4906001600160a01b03163314610a135760405162c14c0760e81b815233600482015260ff8216602482015260440161074d565b60405163435c329f60e11b81525f600482015260240161074d565b83421115610a525760405163313c898160e11b81526004810185905260240161074d565b5f7f6e71edae12b1b97f4d1f60370fef10105fa2faae0126114a169c64845d6126c9888888610a9d8c6001600160a01b03165f908152600a6020526040902080546001810190915590565b6040805160208101969096526001600160a01b0394851690860152929091166060840152608083015260a082015260c0810186905260e0016040516020818303038152906040528051906020012090505f610af7826112dc565b90505f610b0682878787611308565b9050896001600160a01b0316816001600160a01b031614610b4d576040516325c0072360e11b81526001600160a01b0380831660048301528b16602482015260440161074d565b610b588a8a8a610dc5565b50505050505050505050565b610b6e3382611334565b5f610b7c826103e3426108ae565b601f5460215460408051637921219560e11b81523060048201523360248201526001600160a01b0392831660448201526064810185905260a060848201525f60a482018190529151949550919092169263f242432a9260c48084019391929182900301818387803b158015610bef575f80fd5b505af1158015610c01573d5f803e3d5ffd5b505060408051858152602081018590523393507f0d6f2c98313d3d3c2af8590212f72f4012b7ff15b817e0eff73263e2b61f58a392500160405180910390a25050565b60605f610c636801000d05c213d3f237846001600160401b0316610fb1565b90505f84516001600160401b03811115610c7f57610c7f611954565b604051908082528060200260200182016040528015610ca8578160200160208202803683370190505b5090505f5b85518110156106f457610ccc838783815181106106c2576106c2611e49565b828281518110610cde57610cde611e49565b6020908102919091010152600101610cad565b601f545f9060e3906001600160a01b03163314610d2b5760405162c14c0760e81b815233600482015260ff8216602482015260440161074d565b6021546001600160a01b03168514610d5f576040516363b25d3160e01b8152600481018690525f602482015260440161074d565b5f610d6a87866113dd565b60408051828152602081018890529192506001600160a01b038916917f7fd0e413cd76b45c220350a9c340fdac96b5420d2aceaf9460eb5e012dd4f4f8910160405180910390a25063f23a6e6160e01b979650505050505050565b6001600160a01b038316610dee5760405163e602df0560e01b81525f600482015260240161074d565b6001600160a01b038216610e1757604051634a1406b160e11b81525f600482015260240161074d565b6001600160a01b038381165f818152600b602090815260408083209487168084529482529182902085905590518481527f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b92591015b60405180910390a3505050565b6001600160a01b038084165f908152600b60209081526040808320938616835292905220545f198114610eec5781811015610edf57604051637dc7a0d960e11b81526001600160a01b0384166004820152602481018290526044810183905260640161074d565b610eec8484848403610dc5565b50505050565b6001600160a01b0383165f908152601e602052604090205481811015610f445760405163391434e360e21b81526001600160a01b0385166004820152602481018290526044810183905260640161074d565b6001600160a01b038085165f818152601e602052604080822086860390559286168082529083902080548601905591517fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef90610fa39086815260200190565b60405180910390a350505050565b5f805f84600f0b128015610fc85750826001166001145b90505f8085600f0b12610fdb5784610fdf565b845f035b6001600160801b03169050600160801b68010000000000000000821161107957603f82901b91505b841561107157600185161561101c578102607f1c5b908002607f1c906002851615611032578102607f1c5b908002607f1c906004851615611048578102607f1c5b908002607f1c90600885161561105e578102607f1c5b60049490941c93908002607f1c90611007565b60401c61118b565b603f600160601b8310156110935760209290921b91601f19015b600160701b8310156110ab5760109290921b91600f19015b600160781b8310156110c35760089290921b91600719015b6001607c1b8310156110db5760049290921b91600319015b6001607e1b8310156110f35760029290921b91600119015b6001607f1b83101561110a5760019290921b915f19015b5f5b8615611175576040821061111e575f80fd5b600187161561114457918302607f1c918101600160801b83111561114457600192831c92015b928002607f1c9260019190911b90600160801b841061116957600193841c9391909101905b600187901c965061110c565b60408110611181575f80fd5b6040039190911c90505b5f83611197578161119b565b815f035b90506f7fffffffffffffffffffffffffffffff1981128015906111ce57506f7fffffffffffffffffffffffffffffff8113155b6111d6575f80fd5b9695505050505050565b5f815f036111ef57505f6104ee565b5f83600f0b12156111fe575f80fd5b600f83900b6001600160801b038316810260401c90608084901c026001600160c01b0381111561122c575f80fd5b60401b811981111561123c575f80fd5b019392505050565b6003545f906001600160a01b031630148015611261575060025446145b1561126d575060015490565b6105fd61145a565b6112b860405180604001604052806007815260200166436972636c657360c81b815250604051806040016040528060028152602001613b1960f11b8152506114c2565b565b6006546060906105fd90600861151a565b6007546060906105fd90600961151a565b5f6104ee6112e8611244565b8360405161190160f01b8152600281019290925260228201526042902090565b5f805f80611318888888886115c3565b925092509250611328828261168b565b50909695505050505050565b6001600160a01b0382165f908152601e6020526040902054818110156113865760405163391434e360e21b81526001600160a01b0384166004820152602481018290526044810183905260640161074d565b6001600160a01b0383165f818152601e602090815260408083208686039055601d80548790039055518581529192917fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef9101610e6b565b5f806113ec83610263426108ae565b905080601d5f8282546113ff9190611e36565b90915550506001600160a01b0384165f818152601e60209081526040808320805486019055518481527fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef910160405180910390a39392505050565b600454600554604080517f8b73c3c69bb8fe3d512ecc4cf759cc79239f7b179b0ffacaa9a75d522b39400f60208201529081019290925260608201524660808201523060a08201525f9060c00160405160208183030381529060405280519060200120905090565b6114cd826008611747565b6006556114db816009611747565b60075581516020808401919091206004558151908201206005554660025561150161145a565b6001555050600380546001600160a01b03191630179055565b606060ff83146115345761152d83611770565b90506104ee565b81805461154090611eba565b80601f016020809104026020016040519081016040528092919081815260200182805461156c90611eba565b80156115b75780601f1061158e576101008083540402835291602001916115b7565b820191905f5260205f20905b81548152906001019060200180831161159a57829003601f168201915b505050505090506104ee565b5f80807f7fffffffffffffffffffffffffffffff5d576e7357a4501ddfe92f46681b20a08411156115fc57505f91506003905082611681565b604080515f808252602082018084528a905260ff891692820192909252606081018790526080810186905260019060a0016020604051602081039080840390855afa15801561164d573d5f803e3d5ffd5b5050604051601f1901519150506001600160a01b03811661167857505f925060019150829050611681565b92505f91508190505b9450945094915050565b5f82600381111561169e5761169e611ef2565b036116a7575050565b60018260038111156116bb576116bb611ef2565b036116d95760405163f645eedf60e01b815260040160405180910390fd5b60028260038111156116ed576116ed611ef2565b0361170e5760405163fce698f760e01b81526004810182905260240161074d565b600382600381111561172257611722611ef2565b03611743576040516335e2f38360e21b81526004810182905260240161074d565b5050565b5f60208351101561175b5761152d836117ad565b816117668482611f51565b5060ff90506104ee565b60605f61177c836117ea565b6040805160208082528183019092529192505f91906020820181803683375050509182525060208101929092525090565b5f80829050601f815111156117d7578260405163305a27a960e01b815260040161074d919061186d565b80516117e282612010565b179392505050565b5f60ff8216601f8111156104ee57604051632cd44ac360e21b815260040160405180910390fd5b5f60208284031215611821575f80fd5b81356001600160e01b031981168114611838575f80fd5b9392505050565b5f81518084528060208401602086015e5f602082860101526020601f19601f83011685010191505092915050565b602081525f611838602083018461183f565b80356001600160a01b0381168114611895575f80fd5b919050565b5f80604083850312156118ab575f80fd5b6118b48361187f565b946020939093013593505050565b5f805f606084860312156118d4575f80fd5b6118dd8461187f565b92506118eb6020850161187f565b9150604084013590509250925092565b80356001600160401b0381168114611895575f80fd5b5f8060408385031215611922575f80fd5b82359150611932602084016118fb565b90509250929050565b5f6020828403121561194b575f80fd5b6118388261187f565b634e487b7160e01b5f52604160045260245ffd5b604051601f8201601f191681016001600160401b038111828210171561199057611990611954565b604052919050565b5f82601f8301126119a7575f80fd5b813560206001600160401b038211156119c2576119c2611954565b8160051b6119d1828201611968565b92835284810182019282810190878511156119ea575f80fd5b83870192505b84831015611a09578235825291830191908301906119f0565b979650505050505050565b5f8060408385031215611a25575f80fd5b82356001600160401b03811115611a3a575f80fd5b611a4685828601611998565b925050611932602084016118fb565b5f815180845260208085019450602084015f5b83811015611a8457815187529582019590820190600101611a68565b509495945050505050565b602081525f6118386020830184611a55565b5f805f60608486031215611ab3575f80fd5b611abc8461187f565b9250611aca6020850161187f565b9150611ad86040850161187f565b90509250925092565b60ff60f81b8816815260e060208201525f611aff60e083018961183f565b8281036040840152611b11818961183f565b606084018890526001600160a01b038716608085015260a0840186905283810360c08501529050611b428185611a55565b9a9950505050505050505050565b5f60208284031215611b60575f80fd5b5035919050565b5f6001600160401b03821115611b7f57611b7f611954565b50601f01601f191660200190565b5f82601f830112611b9c575f80fd5b8135611baf611baa82611b67565b611968565b818152846020838601011115611bc3575f80fd5b816020850160208301375f918101602001919091529392505050565b5f805f805f60a08688031215611bf3575f80fd5b611bfc8661187f565b9450611c0a6020870161187f565b935060408601356001600160401b0380821115611c25575f80fd5b611c3189838a01611998565b94506060880135915080821115611c46575f80fd5b611c5289838a01611998565b93506080880135915080821115611c67575f80fd5b50611c7488828901611b8d565b9150509295509295909350565b5f805f805f805f60e0888a031215611c97575f80fd5b611ca08861187f565b9650611cae6020890161187f565b95506040880135945060608801359350608088013560ff81168114611cd1575f80fd5b9699959850939692959460a0840135945060c09093013592915050565b5f8060408385031215611cff575f80fd5b611d088361187f565b91506119326020840161187f565b5f805f805f60a08688031215611d2a575f80fd5b611d338661187f565b9450611d416020870161187f565b9350604086013592506060860135915060808601356001600160401b03811115611d69575f80fd5b611c7488828901611b8d565b5f60208284031215611d85575f80fd5b81516001600160401b03811115611d9a575f80fd5b8201601f81018413611daa575f80fd5b8051611db8611baa82611b67565b818152856020838501011115611dcc575f80fd5b8160208401602083015e5f91810160200191909152949350505050565b5f81518060208401855e5f93019283525090919050565b5f611e0b8284611de9565b662d45524332307360c81b81526007019392505050565b634e487b7160e01b5f52601160045260245ffd5b808201808211156104ee576104ee611e22565b634e487b7160e01b5f52603260045260245ffd5b5f60208284031215611e6d575f80fd5b5051919050565b818103818111156104ee576104ee611e22565b5f82611ea157634e487b7160e01b5f52601260045260245ffd5b500490565b5f6105ec611eb48386611de9565b84611de9565b600181811c90821680611ece57607f821691505b602082108103611eec57634e487b7160e01b5f52602260045260245ffd5b50919050565b634e487b7160e01b5f52602160045260245ffd5b601f82111561084a57805f5260205f20601f840160051c81016020851015611f2b5750805b601f840160051c820191505b81811015611f4a575f8155600101611f37565b5050505050565b81516001600160401b03811115611f6a57611f6a611954565b611f7e81611f788454611eba565b84611f06565b602080601f831160018114611fb1575f8415611f9a5750858301515b5f19600386901b1c1916600185901b178555612008565b5f85815260208120601f198616915b82811015611fdf57888601518255948401946001909101908401611fc0565b5085821015611ffc57878501515f19600388901b60f8161c191681555b505060018460011b0185555b505050505050565b80516020808301519190811015611eec575f1960209190910360031b1b1691905056fea2646970667358221220cdebfba6ac52df98f762c29a93d90dc16babdf66664a7e1243874fa900beb06064736f6c63430008190033";
        public InflationaryCirclesDeploymentBase() : base(BYTECODE) { }
        public InflationaryCirclesDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class DomainSeparatorFunction : DomainSeparatorFunctionBase { }

    [Function("DOMAIN_SEPARATOR", "bytes32")]
    public class DomainSeparatorFunctionBase : FunctionMessage
    {

    }

    public partial class AllowanceFunction : AllowanceFunctionBase { }

    [Function("allowance", "uint256")]
    public class AllowanceFunctionBase : FunctionMessage
    {
        [Parameter("address", "_owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("address", "_spender", 2)]
        public virtual string Spender { get; set; }
    }

    public partial class ApproveFunction : ApproveFunctionBase { }

    [Function("approve", "bool")]
    public class ApproveFunctionBase : FunctionMessage
    {
        [Parameter("address", "_spender", 1)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "_amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class AvatarFunction : AvatarFunctionBase { }

    [Function("avatar", "address")]
    public class AvatarFunctionBase : FunctionMessage
    {

    }

    public partial class BalanceOfFunction : BalanceOfFunctionBase { }

    [Function("balanceOf", "uint256")]
    public class BalanceOfFunctionBase : FunctionMessage
    {
        [Parameter("address", "_account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class CirclesIdentifierFunction : CirclesIdentifierFunctionBase { }

    [Function("circlesIdentifier", "uint256")]
    public class CirclesIdentifierFunctionBase : FunctionMessage
    {

    }

    public partial class ConvertBatchDemurrageToInflationaryValuesFunction : ConvertBatchDemurrageToInflationaryValuesFunctionBase { }

    [Function("convertBatchDemurrageToInflationaryValues", "uint256[]")]
    public class ConvertBatchDemurrageToInflationaryValuesFunctionBase : FunctionMessage
    {
        [Parameter("uint256[]", "_demurrageValues", 1)]
        public virtual List<BigInteger> DemurrageValues { get; set; }
        [Parameter("uint64", "_dayUpdated", 2)]
        public virtual ulong DayUpdated { get; set; }
    }

    public partial class ConvertBatchInflationaryToDemurrageValuesFunction : ConvertBatchInflationaryToDemurrageValuesFunctionBase { }

    [Function("convertBatchInflationaryToDemurrageValues", "uint256[]")]
    public class ConvertBatchInflationaryToDemurrageValuesFunctionBase : FunctionMessage
    {
        [Parameter("uint256[]", "_inflationaryValues", 1)]
        public virtual List<BigInteger> InflationaryValues { get; set; }
        [Parameter("uint64", "_day", 2)]
        public virtual ulong Day { get; set; }
    }

    public partial class ConvertDemurrageToInflationaryValueFunction : ConvertDemurrageToInflationaryValueFunctionBase { }

    [Function("convertDemurrageToInflationaryValue", "uint256")]
    public class ConvertDemurrageToInflationaryValueFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "_demurrageValue", 1)]
        public virtual BigInteger DemurrageValue { get; set; }
        [Parameter("uint64", "_dayUpdated", 2)]
        public virtual ulong DayUpdated { get; set; }
    }

    public partial class ConvertInflationaryToDemurrageValueFunction : ConvertInflationaryToDemurrageValueFunctionBase { }

    [Function("convertInflationaryToDemurrageValue", "uint256")]
    public class ConvertInflationaryToDemurrageValueFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "_inflationaryValue", 1)]
        public virtual BigInteger InflationaryValue { get; set; }
        [Parameter("uint64", "_day", 2)]
        public virtual ulong Day { get; set; }
    }

    public partial class DayFunction : DayFunctionBase { }

    [Function("day", "uint64")]
    public class DayFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "_timestamp", 1)]
        public virtual BigInteger Timestamp { get; set; }
    }

    public partial class DecimalsFunction : DecimalsFunctionBase { }

    [Function("decimals", "uint8")]
    public class DecimalsFunctionBase : FunctionMessage
    {

    }

    public partial class DecreaseAllowanceFunction : DecreaseAllowanceFunctionBase { }

    [Function("decreaseAllowance", "bool")]
    public class DecreaseAllowanceFunctionBase : FunctionMessage
    {
        [Parameter("address", "_spender", 1)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "_subtractedValue", 2)]
        public virtual BigInteger SubtractedValue { get; set; }
    }

    public partial class Eip712DomainFunction : Eip712DomainFunctionBase { }

    [Function("eip712Domain", typeof(Eip712DomainOutputDTO))]
    public class Eip712DomainFunctionBase : FunctionMessage
    {

    }

    public partial class HubFunction : HubFunctionBase { }

    [Function("hub", "address")]
    public class HubFunctionBase : FunctionMessage
    {

    }

    public partial class IncreaseAllowanceFunction : IncreaseAllowanceFunctionBase { }

    [Function("increaseAllowance", "bool")]
    public class IncreaseAllowanceFunctionBase : FunctionMessage
    {
        [Parameter("address", "_spender", 1)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "_addedValue", 2)]
        public virtual BigInteger AddedValue { get; set; }
    }

    public partial class InflationDayZeroFunction : InflationDayZeroFunctionBase { }

    [Function("inflationDayZero", "uint256")]
    public class InflationDayZeroFunctionBase : FunctionMessage
    {

    }

    public partial class NameFunction : NameFunctionBase { }

    [Function("name", "string")]
    public class NameFunctionBase : FunctionMessage
    {

    }

    public partial class NameRegistryFunction : NameRegistryFunctionBase { }

    [Function("nameRegistry", "address")]
    public class NameRegistryFunctionBase : FunctionMessage
    {

    }

    public partial class NoncesFunction : NoncesFunctionBase { }

    [Function("nonces", "uint256")]
    public class NoncesFunctionBase : FunctionMessage
    {
        [Parameter("address", "_owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class OnERC1155BatchReceivedFunction : OnERC1155BatchReceivedFunctionBase { }

    [Function("onERC1155BatchReceived", "bytes4")]
    public class OnERC1155BatchReceivedFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("address", "", 2)]
        public virtual string ReturnValue2 { get; set; }
        [Parameter("uint256[]", "", 3)]
        public virtual List<BigInteger> ReturnValue3 { get; set; }
        [Parameter("uint256[]", "", 4)]
        public virtual List<BigInteger> ReturnValue4 { get; set; }
        [Parameter("bytes", "", 5)]
        public virtual byte[] ReturnValue5 { get; set; }
    }

    public partial class OnERC1155ReceivedFunction : OnERC1155ReceivedFunctionBase { }

    [Function("onERC1155Received", "bytes4")]
    public class OnERC1155ReceivedFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("address", "_from", 2)]
        public virtual string From { get; set; }
        [Parameter("uint256", "_id", 3)]
        public virtual BigInteger Id { get; set; }
        [Parameter("uint256", "_amount", 4)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("bytes", "", 5)]
        public virtual byte[] ReturnValue5 { get; set; }
    }

    public partial class PermitFunction : PermitFunctionBase { }

    [Function("permit")]
    public class PermitFunctionBase : FunctionMessage
    {
        [Parameter("address", "_owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("address", "_spender", 2)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "_value", 3)]
        public virtual BigInteger Value { get; set; }
        [Parameter("uint256", "_deadline", 4)]
        public virtual BigInteger Deadline { get; set; }
        [Parameter("uint8", "_v", 5)]
        public virtual byte V { get; set; }
        [Parameter("bytes32", "_r", 6)]
        public virtual byte[] R { get; set; }
        [Parameter("bytes32", "_s", 7)]
        public virtual byte[] S { get; set; }
    }

    public partial class SetupFunction : SetupFunctionBase { }

    [Function("setup")]
    public class SetupFunctionBase : FunctionMessage
    {
        [Parameter("address", "_hub", 1)]
        public virtual string Hub { get; set; }
        [Parameter("address", "_nameRegistry", 2)]
        public virtual string NameRegistry { get; set; }
        [Parameter("address", "_avatar", 3)]
        public virtual string Avatar { get; set; }
    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceId", 1)]
        public virtual byte[] InterfaceId { get; set; }
    }

    public partial class SymbolFunction : SymbolFunctionBase { }

    [Function("symbol", "string")]
    public class SymbolFunctionBase : FunctionMessage
    {

    }

    public partial class ToTokenIdFunction : ToTokenIdFunctionBase { }

    [Function("toTokenId", "uint256")]
    public class ToTokenIdFunctionBase : FunctionMessage
    {
        [Parameter("address", "_avatar", 1)]
        public virtual string Avatar { get; set; }
    }

    public partial class TotalSupplyFunction : TotalSupplyFunctionBase { }

    [Function("totalSupply", "uint256")]
    public class TotalSupplyFunctionBase : FunctionMessage
    {

    }

    public partial class TransferFunction : TransferFunctionBase { }

    [Function("transfer", "bool")]
    public class TransferFunctionBase : FunctionMessage
    {
        [Parameter("address", "_to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class TransferFromFunction : TransferFromFunctionBase { }

    [Function("transferFrom", "bool")]
    public class TransferFromFunctionBase : FunctionMessage
    {
        [Parameter("address", "_from", 1)]
        public virtual string From { get; set; }
        [Parameter("address", "_to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_amount", 3)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class UnwrapFunction : UnwrapFunctionBase { }

    [Function("unwrap")]
    public class UnwrapFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "_amount", 1)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class ApprovalEventDTO : ApprovalEventDTOBase { }

    [Event("Approval")]
    public class ApprovalEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "spender", 2, true )]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "value", 3, false )]
        public virtual BigInteger Value { get; set; }
    }

    public partial class DepositInflationaryEventDTO : DepositInflationaryEventDTOBase { }

    [Event("DepositInflationary")]
    public class DepositInflationaryEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("uint256", "amount", 2, false )]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint256", "demurragedAmount", 3, false )]
        public virtual BigInteger DemurragedAmount { get; set; }
    }

    public partial class EIP712DomainChangedEventDTO : EIP712DomainChangedEventDTOBase { }

    [Event("EIP712DomainChanged")]
    public class EIP712DomainChangedEventDTOBase : IEventDTO
    {
    }

    public partial class TransferEventDTO : TransferEventDTOBase { }

    [Event("Transfer")]
    public class TransferEventDTOBase : IEventDTO
    {
        [Parameter("address", "from", 1, true )]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2, true )]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 3, false )]
        public virtual BigInteger Value { get; set; }
    }

    public partial class WithdrawInflationaryEventDTO : WithdrawInflationaryEventDTOBase { }

    [Event("WithdrawInflationary")]
    public class WithdrawInflationaryEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("uint256", "amount", 2, false )]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint256", "demurragedAmount", 3, false )]
        public virtual BigInteger DemurragedAmount { get; set; }
    }

    public partial class CirclesAmountOverflowError : CirclesAmountOverflowErrorBase { }

    [Error("CirclesAmountOverflow")]
    public class CirclesAmountOverflowErrorBase : IErrorDTO
    {
        [Parameter("uint256", "amount", 1)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint8", "code", 2)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesERC1155CannotReceiveBatchError : CirclesERC1155CannotReceiveBatchErrorBase { }

    [Error("CirclesERC1155CannotReceiveBatch")]
    public class CirclesERC1155CannotReceiveBatchErrorBase : IErrorDTO
    {
        [Parameter("uint8", "code", 1)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesErrorAddressUintArgsError : CirclesErrorAddressUintArgsErrorBase { }

    [Error("CirclesErrorAddressUintArgs")]
    public class CirclesErrorAddressUintArgsErrorBase : IErrorDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("uint256", "", 2)]
        public virtual BigInteger ReturnValue2 { get; set; }
        [Parameter("uint8", "", 3)]
        public virtual byte ReturnValue3 { get; set; }
    }

    public partial class CirclesErrorNoArgsError : CirclesErrorNoArgsErrorBase { }

    [Error("CirclesErrorNoArgs")]
    public class CirclesErrorNoArgsErrorBase : IErrorDTO
    {
        [Parameter("uint8", "", 1)]
        public virtual byte ReturnValue1 { get; set; }
    }

    public partial class CirclesErrorOneAddressArgError : CirclesErrorOneAddressArgErrorBase { }

    [Error("CirclesErrorOneAddressArg")]
    public class CirclesErrorOneAddressArgErrorBase : IErrorDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("uint8", "", 2)]
        public virtual byte ReturnValue2 { get; set; }
    }

    public partial class CirclesIdMustBeDerivedFromAddressError : CirclesIdMustBeDerivedFromAddressErrorBase { }

    [Error("CirclesIdMustBeDerivedFromAddress")]
    public class CirclesIdMustBeDerivedFromAddressErrorBase : IErrorDTO
    {
        [Parameter("uint256", "providedId", 1)]
        public virtual BigInteger ProvidedId { get; set; }
        [Parameter("uint8", "code", 2)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesInvalidCirclesIdError : CirclesInvalidCirclesIdErrorBase { }

    [Error("CirclesInvalidCirclesId")]
    public class CirclesInvalidCirclesIdErrorBase : IErrorDTO
    {
        [Parameter("uint256", "id", 1)]
        public virtual BigInteger Id { get; set; }
        [Parameter("uint8", "code", 2)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesInvalidParameterError : CirclesInvalidParameterErrorBase { }

    [Error("CirclesInvalidParameter")]
    public class CirclesInvalidParameterErrorBase : IErrorDTO
    {
        [Parameter("uint256", "parameter", 1)]
        public virtual BigInteger Parameter { get; set; }
        [Parameter("uint8", "code", 2)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesProxyAlreadyInitializedError : CirclesProxyAlreadyInitializedErrorBase { }
    [Error("CirclesProxyAlreadyInitialized")]
    public class CirclesProxyAlreadyInitializedErrorBase : IErrorDTO
    {
    }

    public partial class CirclesReentrancyGuardError : CirclesReentrancyGuardErrorBase { }

    [Error("CirclesReentrancyGuard")]
    public class CirclesReentrancyGuardErrorBase : IErrorDTO
    {
        [Parameter("uint8", "code", 1)]
        public virtual byte Code { get; set; }
    }

    public partial class ECDSAInvalidSignatureError : ECDSAInvalidSignatureErrorBase { }
    [Error("ECDSAInvalidSignature")]
    public class ECDSAInvalidSignatureErrorBase : IErrorDTO
    {
    }

    public partial class ECDSAInvalidSignatureLengthError : ECDSAInvalidSignatureLengthErrorBase { }

    [Error("ECDSAInvalidSignatureLength")]
    public class ECDSAInvalidSignatureLengthErrorBase : IErrorDTO
    {
        [Parameter("uint256", "length", 1)]
        public virtual BigInteger Length { get; set; }
    }

    public partial class ECDSAInvalidSignatureSError : ECDSAInvalidSignatureSErrorBase { }

    [Error("ECDSAInvalidSignatureS")]
    public class ECDSAInvalidSignatureSErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "s", 1)]
        public virtual byte[] S { get; set; }
    }

    public partial class ERC20InsufficientAllowanceError : ERC20InsufficientAllowanceErrorBase { }

    [Error("ERC20InsufficientAllowance")]
    public class ERC20InsufficientAllowanceErrorBase : IErrorDTO
    {
        [Parameter("address", "spender", 1)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "allowance", 2)]
        public virtual BigInteger Allowance { get; set; }
        [Parameter("uint256", "needed", 3)]
        public virtual BigInteger Needed { get; set; }
    }

    public partial class ERC20InsufficientBalanceError : ERC20InsufficientBalanceErrorBase { }

    [Error("ERC20InsufficientBalance")]
    public class ERC20InsufficientBalanceErrorBase : IErrorDTO
    {
        [Parameter("address", "sender", 1)]
        public virtual string Sender { get; set; }
        [Parameter("uint256", "balance", 2)]
        public virtual BigInteger Balance { get; set; }
        [Parameter("uint256", "needed", 3)]
        public virtual BigInteger Needed { get; set; }
    }

    public partial class ERC20InvalidApproverError : ERC20InvalidApproverErrorBase { }

    [Error("ERC20InvalidApprover")]
    public class ERC20InvalidApproverErrorBase : IErrorDTO
    {
        [Parameter("address", "approver", 1)]
        public virtual string Approver { get; set; }
    }

    public partial class ERC20InvalidReceiverError : ERC20InvalidReceiverErrorBase { }

    [Error("ERC20InvalidReceiver")]
    public class ERC20InvalidReceiverErrorBase : IErrorDTO
    {
        [Parameter("address", "receiver", 1)]
        public virtual string Receiver { get; set; }
    }

    public partial class ERC20InvalidSenderError : ERC20InvalidSenderErrorBase { }

    [Error("ERC20InvalidSender")]
    public class ERC20InvalidSenderErrorBase : IErrorDTO
    {
        [Parameter("address", "sender", 1)]
        public virtual string Sender { get; set; }
    }

    public partial class ERC20InvalidSpenderError : ERC20InvalidSpenderErrorBase { }

    [Error("ERC20InvalidSpender")]
    public class ERC20InvalidSpenderErrorBase : IErrorDTO
    {
        [Parameter("address", "spender", 1)]
        public virtual string Spender { get; set; }
    }

    public partial class ERC2612ExpiredSignatureError : ERC2612ExpiredSignatureErrorBase { }

    [Error("ERC2612ExpiredSignature")]
    public class ERC2612ExpiredSignatureErrorBase : IErrorDTO
    {
        [Parameter("uint256", "deadline", 1)]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class ERC2612InvalidSignerError : ERC2612InvalidSignerErrorBase { }

    [Error("ERC2612InvalidSigner")]
    public class ERC2612InvalidSignerErrorBase : IErrorDTO
    {
        [Parameter("address", "signer", 1)]
        public virtual string Signer { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
    }

    public partial class InvalidAccountNonceError : InvalidAccountNonceErrorBase { }

    [Error("InvalidAccountNonce")]
    public class InvalidAccountNonceErrorBase : IErrorDTO
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("uint256", "currentNonce", 2)]
        public virtual BigInteger CurrentNonce { get; set; }
    }

    public partial class InvalidShortStringError : InvalidShortStringErrorBase { }
    [Error("InvalidShortString")]
    public class InvalidShortStringErrorBase : IErrorDTO
    {
    }

    public partial class StringTooLongError : StringTooLongErrorBase { }

    [Error("StringTooLong")]
    public class StringTooLongErrorBase : IErrorDTO
    {
        [Parameter("string", "str", 1)]
        public virtual string Str { get; set; }
    }

    public partial class DomainSeparatorOutputDTO : DomainSeparatorOutputDTOBase { }

    [FunctionOutput]
    public class DomainSeparatorOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class AllowanceOutputDTO : AllowanceOutputDTOBase { }

    [FunctionOutput]
    public class AllowanceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class AvatarOutputDTO : AvatarOutputDTOBase { }

    [FunctionOutput]
    public class AvatarOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class BalanceOfOutputDTO : BalanceOfOutputDTOBase { }

    [FunctionOutput]
    public class BalanceOfOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class CirclesIdentifierOutputDTO : CirclesIdentifierOutputDTOBase { }

    [FunctionOutput]
    public class CirclesIdentifierOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class ConvertBatchDemurrageToInflationaryValuesOutputDTO : ConvertBatchDemurrageToInflationaryValuesOutputDTOBase { }

    [FunctionOutput]
    public class ConvertBatchDemurrageToInflationaryValuesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256[]", "", 1)]
        public virtual List<BigInteger> ReturnValue1 { get; set; }
    }

    public partial class ConvertBatchInflationaryToDemurrageValuesOutputDTO : ConvertBatchInflationaryToDemurrageValuesOutputDTOBase { }

    [FunctionOutput]
    public class ConvertBatchInflationaryToDemurrageValuesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256[]", "", 1)]
        public virtual List<BigInteger> ReturnValue1 { get; set; }
    }

    public partial class ConvertDemurrageToInflationaryValueOutputDTO : ConvertDemurrageToInflationaryValueOutputDTOBase { }

    [FunctionOutput]
    public class ConvertDemurrageToInflationaryValueOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class ConvertInflationaryToDemurrageValueOutputDTO : ConvertInflationaryToDemurrageValueOutputDTOBase { }

    [FunctionOutput]
    public class ConvertInflationaryToDemurrageValueOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class DayOutputDTO : DayOutputDTOBase { }

    [FunctionOutput]
    public class DayOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class DecimalsOutputDTO : DecimalsOutputDTOBase { }

    [FunctionOutput]
    public class DecimalsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint8", "", 1)]
        public virtual byte ReturnValue1 { get; set; }
    }



    public partial class Eip712DomainOutputDTO : Eip712DomainOutputDTOBase { }

    [FunctionOutput]
    public class Eip712DomainOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes1", "fields", 1)]
        public virtual byte[] Fields { get; set; }
        [Parameter("string", "name", 2)]
        public virtual string Name { get; set; }
        [Parameter("string", "version", 3)]
        public virtual string Version { get; set; }
        [Parameter("uint256", "chainId", 4)]
        public virtual BigInteger ChainId { get; set; }
        [Parameter("address", "verifyingContract", 5)]
        public virtual string VerifyingContract { get; set; }
        [Parameter("bytes32", "salt", 6)]
        public virtual byte[] Salt { get; set; }
        [Parameter("uint256[]", "extensions", 7)]
        public virtual List<BigInteger> Extensions { get; set; }
    }

    public partial class HubOutputDTO : HubOutputDTOBase { }

    [FunctionOutput]
    public class HubOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class InflationDayZeroOutputDTO : InflationDayZeroOutputDTOBase { }

    [FunctionOutput]
    public class InflationDayZeroOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class NameOutputDTO : NameOutputDTOBase { }

    [FunctionOutput]
    public class NameOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class NameRegistryOutputDTO : NameRegistryOutputDTOBase { }

    [FunctionOutput]
    public class NameRegistryOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class NoncesOutputDTO : NoncesOutputDTOBase { }

    [FunctionOutput]
    public class NoncesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class OnERC1155BatchReceivedOutputDTO : OnERC1155BatchReceivedOutputDTOBase { }

    [FunctionOutput]
    public class OnERC1155BatchReceivedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes4", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }







    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class SymbolOutputDTO : SymbolOutputDTOBase { }

    [FunctionOutput]
    public class SymbolOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ToTokenIdOutputDTO : ToTokenIdOutputDTOBase { }

    [FunctionOutput]
    public class ToTokenIdOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class TotalSupplyOutputDTO : TotalSupplyOutputDTOBase { }

    [FunctionOutput]
    public class TotalSupplyOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }






}
