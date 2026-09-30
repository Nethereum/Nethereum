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
using Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition;

namespace Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition
{


    public partial class AppChainAnchorDeployment : AppChainAnchorDeploymentBase
    {
        public AppChainAnchorDeployment() : base(BYTECODE) { }
        public AppChainAnchorDeployment(string byteCode) : base(byteCode) { }
    }

    public class AppChainAnchorDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x6080604052348015600e575f5ffd5b505f80546101003302610100600160a81b03199091161790556125fb806100345f395ff3fe608060405234801561000f575f5ffd5b506004361061016d575f3560e01c80638456cb59116100d9578063b352618111610093578063d5d754eb1161006e578063d5d754eb146105bd578063e397a64f146105c6578063ed2552a514610606578063f2fde38b14610619575f5ffd5b8063b352618114610584578063b6246cb614610597578063b84a5a93146105aa575f5ffd5b80638456cb59146104da5780638c47a870146104e25780638da5cb5b146104f55780639a64c7f314610524578063ac146ff51461055e578063b2b5adc414610571575f5ffd5b80635801b8a41161012a5780635801b8a4146102db5780635c975abb146103945780636d57e8d21461039e5780636d7ed1bb1461040b578063701827c81461043557806373134f8e14610475575f5ffd5b80630b6b95b31461017157806317f902ec146101a85780631d2f37f4146101e057806324e3a62a146102ab57806326f3a0c9146102c05780633f4ba83a146102d3575b5f5ffd5b61019361017f366004612025565b60046020525f908152604090205460ff1681565b60405190151581526020015b60405180910390f35b6101d26101b6366004612054565b600760209081525f928352604080842090915290825290205481565b60405190815260200161019f565b6102506101ee366004612085565b600160208190525f9182526040909120805491810154600282015460038301546004909301546001600160401b03948516949293919092169160ff8082169161010081048216916001600160a01b036201000083041691600160b01b90041688565b604080516001600160401b03998a16815260208101989098529590971694860194909452606085019290925260ff90811660808501521660a08301526001600160a01b031660c082015290151560e08201526101000161019f565b6102be6102b93660046120b5565b61062c565b005b6102be6102ce3660046120ea565b610803565b6102be61098a565b6103496102e9366004612085565b6001600160401b039081165f9081526001602081905260409091209081015460028201546003830154600490930154919493169260ff8083169261010081048216926201000082046001600160a01b031692600160b01b90920490911690565b604080519788526001600160401b0390961660208801529486019390935260ff91821660608601521660808401526001600160a01b031660a0830152151560c082015260e00161019f565b5f5460ff16610193565b6103e16103ac366004612085565b6001600160401b039081165f908152600360208190526040909120805460018201546002830154929093015493169391929091565b604080516001600160401b039095168552602085019390935291830152606082015260800161019f565b6101d2610419366004612054565b600660209081525f928352604080842090915290825290205481565b6103e1610443366004612085565b600360208190525f918252604090912080546001820154600283015492909301546001600160401b0390911692919084565b6104b3610483366004612025565b60056020525f90815260409020546001600160a01b0381169060ff600160a01b8204811691600160a81b90041683565b604080516001600160a01b039094168452911515602084015215159082015260600161019f565b6102be6109c2565b6102be6104f03660046120ea565b6109f8565b5f5461050c9061010090046001600160a01b031681565b6040516001600160a01b03909116815260200161019f565b61050c610532366004612085565b6001600160401b03165f908152600160205260409020600401546201000090046001600160a01b031690565b61019361056c366004612112565b610b75565b6102be61057f3660046121d9565b610c80565b6102be61059236600461221f565b610dbe565b6101936105a5366004612269565b610ea6565b6102be6105b8366004612291565b610eca565b6101d261100081565b6105ee6105d4366004612308565b60026020525f90815260409020546001600160401b031681565b6040516001600160401b03909116815260200161019f565b6102be61061436600461231f565b611396565b6102be6106273660046123a5565b611b1f565b6001600160401b0382165f9081526001602052604090206004810154600160b01b900460ff166106775760405162461bcd60e51b815260040161066e906123c0565b60405180910390fd5b6001600160a01b0382166106c15760405162461bcd60e51b8152602060048201526011602482015270496e76616c696420617574686f7269747960781b604482015260640161066e565b600481810154604051634f4697a960e01b81526001600160401b038616928101929092523360248301526201000090046001600160a01b031690634f4697a990604401602060405180830381865afa15801561071f573d5f5f3e3d5ffd5b505050506040513d601f19601f8201168201806040525081019061074391906123e7565b8061075c57505f5461010090046001600160a01b031633145b6107995760405162461bcd60e51b815260206004820152600e60248201526d139bdd08185d5d1a1bdc9a5e995960921b604482015260640161066e565b6004810180546001600160a01b038481166201000081810262010000600160b01b031985161790945560405193909204169182906001600160401b038716907f1dcea67297f161516fc65a89f0a204ae1d10df50a1ac2a299d05e9c1820621ae905f90a450505050565b5f5461010090046001600160a01b031633146108315760405162461bcd60e51b815260040161066e90612402565b6001600160401b0382165f9081526001602052604090206004810154600160b01b900460ff166108735760405162461bcd60e51b815260040161066e906123c0565b600481015460ff6101009091048116908316116108c35760405162461bcd60e51b815260206004820152600e60248201526d43616e206f6e6c7920726169736560901b604482015260640161066e565b60ff8083165f908152600460205260409020541661091b5760405162461bcd60e51b815260206004820152601560248201527414d8da195b58481b9bdd081c9959da5cdd195c9959605a1b604482015260640161066e565b60048101805460ff84811661010081810261ff001985161790945560408051949093049091168084526020840191909152916001600160401b038616917f0295fd946d259276c52482e631808dc3fb21da22fb611bc90cdbd30a22df161491015b60405180910390a250505050565b5f5461010090046001600160a01b031633146109b85760405162461bcd60e51b815260040161066e90612402565b6109c0611beb565b565b5f5461010090046001600160a01b031633146109f05760405162461bcd60e51b815260040161066e90612402565b6109c0611c3c565b5f5461010090046001600160a01b03163314610a265760405162461bcd60e51b815260040161066e90612402565b6001600160401b0382165f9081526001602052604090206004810154600160b01b900460ff16610a685760405162461bcd60e51b815260040161066e906123c0565b600481015460ff90811690831611610ab35760405162461bcd60e51b815260206004820152600e60248201526d43616e206f6e6c7920726169736560901b604482015260640161066e565b60ff8083165f90815260056020526040902054600160a81b900416610b1a5760405162461bcd60e51b815260206004820152601b60248201527f50726f6f662073797374656d206e6f7420726567697374657265640000000000604482015260640161066e565b60048101805460ff84811660ff1983168117909355604080519190921680825260208201939093526001600160401b038616917f22bdf4139e97c137239fb0006775b5ac799a46d0033ada15bc44856d202d5fd4910161097c565b6001600160401b038089165f908152600760209081526040808320938b1683529290529081205480610be95760405162461bcd60e51b815260206004820152601760248201527f4e6f20616e63686f72206174207468697320626c6f636b000000000000000000604482015260640161066e565b6040516001600160c01b031960c08a901b1660208201526028810188905260488101879052606881018690525f90608801604051602081830303815290604052805190602001209050610c718585808060200260200160405190810160405280939291908181526020018383602002808284375f92019190915250869250859150611c789050565b9b9a5050505050505050505050565b5f5461010090046001600160a01b03163314610cae5760405162461bcd60e51b815260040161066e90612402565b60ff8084165f90815260056020526040902054600160a81b90041615610d0b5760405162461bcd60e51b8152602060048201526012602482015271105b1c9958591e481c9959da5cdd195c995960721b604482015260640161066e565b604080516060810182526001600160a01b038481168083528415156020808501828152600186880190815260ff8b165f8181526005855289902097518854935192511515600160a81b0260ff60a81b19931515600160a01b026001600160a81b03199095169190981617929092171694909417909455845191825292810192909252917f3e064887bd167e93c502292aab60bb6b9ecb9542a766fff1f13c382a63780443910160405180910390a2505050565b5f5461010090046001600160a01b03163314610dec5760405162461bcd60e51b815260040161066e90612402565b60ff8085165f908152600460205260409020541615610e425760405162461bcd60e51b8152602060048201526012602482015271105b1c9958591e481c9959da5cdd195c995960721b604482015260640161066e565b60ff84165f8181526004602052604090819020805460ff19166001179055517f6f48694a3dd0d6c1bf6ad4a7804b136cb6d0748a5dc1536a095ca9a66e80daba9061097c9086908690869092835260ff918216602084015216604082015260600190565b6001600160401b0382165f9081526003602052604090206002015481145b92915050565b5f5461010090046001600160a01b03163314610ef85760405162461bcd60e51b815260040161066e90612402565b5f876001600160401b031611610f425760405162461bcd60e51b815260206004820152600f60248201526e125b9d985b1a590818da185a5b9259608a1b604482015260640161066e565b85610f865760405162461bcd60e51b8152602060048201526014602482015273092dcecc2d8d2c840cecadccae6d2e640d0c2e6d60631b604482015260640161066e565b5f856001600160401b031611610fde5760405162461bcd60e51b815260206004820152601960248201527f47656e6573697320626c6f636b206d757374206265203e203000000000000000604482015260640161066e565b6001600160a01b0381166110285760405162461bcd60e51b8152602060048201526011602482015270496e76616c696420617574686f7269747960781b604482015260640161066e565b6001600160401b0387165f90815260016020526040902060040154600160b01b900460ff161561109a5760405162461bcd60e51b815260206004820152601a60248201527f436861696e496420616c72656164792072656769737465726564000000000000604482015260640161066e565b5f868152600260205260409020546001600160401b0316156110fe5760405162461bcd60e51b815260206004820152601a60248201527f47656e6573697320616c72656164792072656769737465726564000000000000604482015260640161066e565b604051806101000160405280886001600160401b03168152602001878152602001866001600160401b031681526020018581526020018460ff1681526020018360ff168152602001826001600160a01b031681526020016001151581525060015f896001600160401b03166001600160401b031681526020019081526020015f205f820151815f015f6101000a8154816001600160401b0302191690836001600160401b03160217905550602082015181600101556040820151816002015f6101000a8154816001600160401b0302191690836001600160401b03160217905550606082015181600301556080820151816004015f6101000a81548160ff021916908360ff16021790555060a08201518160040160016101000a81548160ff021916908360ff16021790555060c08201518160040160026101000a8154816001600160a01b0302191690836001600160a01b0316021790555060e08201518160040160166101000a81548160ff0219169083151502179055509050508660025f8881526020019081526020015f205f6101000a8154816001600160401b0302191690836001600160401b0316021790555060405180608001604052806001876112c7919061243a565b6001600160401b0390811682525f602080840182905260408085018a905260609485018390528c84168084526003808452938290208751815490871667ffffffffffffffff199091161781558784015160018201558783015160028201559686015196909301959095558451928a168352820188905260ff878116838601528616928201929092526001600160a01b0384166080820152915188927f35dc14a3b9e310a8dff813aed6a775749e53ceb0db8e3abd66138a0daf81ae20919081900360a00190a350505050505050565b61139e611c8d565b6110008111156113e25760405162461bcd60e51b815260206004820152600f60248201526e50726f6f6620746f6f206c6172676560881b604482015260640161066e565b5f6001816113f36020870187612085565b6001600160401b0316815260208101919091526040015f206004810154909150600160b01b900460ff166114395760405162461bcd60e51b815260040161066e906123c0565b60048101546201000090046001600160a01b031663ab1e991261145f6020870187612085565b6040516001600160e01b031960e084901b1681526001600160401b039091166004820152336024820152604401602060405180830381865afa1580156114a7573d5f5f3e3d5ffd5b505050506040513d601f19601f820116820180604052508101906114cb91906123e7565b6115085760405162461bcd60e51b815260206004820152600e60248201526d139bdd08185d5d1a1bdc9a5e995960921b604482015260640161066e565b80600101548460200135146115525760405162461bcd60e51b815260206004820152601060248201526f08ecadccae6d2e640dad2e6dac2e8c6d60831b604482015260640161066e565b600481015460ff1661156a60c0860160a08701612025565b60ff1610156115bb5760405162461bcd60e51b815260206004820152601860248201527f42656c6f77206d696e696d756d2070726f6f6620746965720000000000000000604482015260640161066e565b6004810154610100900460ff166115d860a0860160808701612025565b60ff1610156116295760405162461bcd60e51b815260206004820152601c60248201527f42656c6f77206d696e696d756d20616e63686f722076657273696f6e00000000604482015260640161066e565b60045f61163c60a0870160808801612025565b60ff908116825260208201929092526040015f2054166116975760405162461bcd60e51b81526020600482015260166024820152752ab735b737bbb71030b731b437b9103b32b939b4b7b760511b604482015260640161066e565b5f6003816116a86020880188612085565b6001600160401b03908116825260208201929092526040015f2080549092506116d391166001612459565b6001600160401b03166116ec6060870160408801612085565b6001600160401b0316146117375760405162461bcd60e51b815260206004820152601260248201527147617020696e20626c6f636b2072616e676560701b604482015260640161066e565b80600101548560e00135146117845760405162461bcd60e51b8152602060048201526013602482015272213937b5b2b71030b731b437b91031b430b4b760691b604482015260640161066e565b6117946060860160408701612085565b6001600160401b03166117ad6080870160608801612085565b6001600160401b031610156117f25760405162461bcd60e51b815260206004820152600b60248201526a456d7074792072616e676560a81b604482015260640161066e565b5f60058161180660c0890160a08a01612025565b60ff908116825260208083019390935260409182015f20825160608101845290546001600160a01b0381168252600160a01b81048316151594820194909452600160a81b90930416151590820181905290915061189c5760405162461bcd60e51b8152602060048201526014602482015273556e6b6e6f776e2070726f6f662073797374656d60601b604482015260640161066e565b80602001511561197657805f01516001600160a01b0316639649daae86866118cd8a87600201548860010154611cb0565b6040518463ffffffff1660e01b81526004016118eb93929190612478565b602060405180830381865afa158015611906573d5f5f3e3d5ffd5b505050506040513d601f19601f8201168201806040525081019061192a91906123e7565b6119765760405162461bcd60e51b815260206004820152601960248201527f50726f6f6620766572696669636174696f6e206661696c656400000000000000604482015260640161066e565b6119866080870160608801612085565b825467ffffffffffffffff19166001600160401b039190911617825560c086013560018301556101208601356002830155610140860135600383015561010086013560075f6119d860208a018a612085565b6001600160401b0316815260208101919091526040015f90812090611a0360808a0160608b01612085565b6001600160401b0316815260208101919091526040015f90812091909155611a3160c0880160a08901612025565b60ff161115611a9857611a4386611ec2565b60065f611a5360208a018a612085565b6001600160401b0316815260208101919091526040015f90812090611a7e60808a0160608b01612085565b6001600160401b0316815260208101919091526040015f20555b611aa86080870160608801612085565b6001600160401b0316611ac16060880160408901612085565b6001600160401b0316611ad76020890189612085565b6001600160401b03167f8e30da7928d000573a59a7c183dcbdcf08400557579f27199c1fac4288510a1089604051611b0f91906124f0565b60405180910390a4505050505050565b5f5461010090046001600160a01b03163314611b4d5760405162461bcd60e51b815260040161066e90612402565b6001600160a01b038116611b935760405162461bcd60e51b815260206004820152600d60248201526c24b73b30b634b21037bbb732b960991b604482015260640161066e565b5f80546001600160a01b03838116610100818102610100600160a81b0319851617855560405193049190911692909183917f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e091a35050565b611bf3611f7d565b5f805460ff191690557f5db9ee0a495bf2e6ff9c91a7834c1ba4fdd244a5e8aa4e537bd38aeae4b073aa335b6040516001600160a01b03909116815260200160405180910390a1565b611c44611c8d565b5f805460ff191660011790557f62e78cea01bee320cd4e420270b5ea74000d11b0c9f74754ebdbfc544b05a258611c1f3390565b5f82611c848584611f9f565b14949350505050565b5f5460ff16156109c05760405163d93c066560e01b815260040160405180910390fd5b60408051600b80825261018082019092526060915f91906020820161016080368337019050509050611ce56020860186612085565b6001600160401b0316815f81518110611d0057611d006125b1565b6020908102919091010152611d1b60a0860160808701612025565b60ff1681600181518110611d3157611d316125b1565b6020908102919091010152611d4c60c0860160a08701612025565b60ff1681600281518110611d6257611d626125b1565b6020908102919091010152611d7d6060860160408701612085565b6001600160401b031681600381518110611d9957611d996125b1565b6020908102919091010152611db46080860160608701612085565b6001600160401b031681600481518110611dd057611dd06125b1565b602002602001018181525050835f1c81600581518110611df257611df26125b1565b6020026020010181815250508461012001355f1c81600681518110611e1957611e196125b1565b602002602001018181525050825f1c81600781518110611e3b57611e3b6125b1565b6020026020010181815250508460c001355f1c81600881518110611e6157611e616125b1565b6020026020010181815250508461010001355f1c81600981518110611e8857611e886125b1565b6020026020010181815250508461014001355f1c81600a81518110611eaf57611eaf6125b1565b6020908102919091010152949350505050565b5f611ed06020830183612085565b6020830135611ee56060850160408601612085565b611ef56080860160608701612085565b604080516001600160401b039586166020820152908101939093529083166060830152909116608082015260c08381013560a083015260e0808501359183019190915261012080850135918301919091526101008085013590830152610140808501359183019190915201604051602081830303815290604052805190602001209050919050565b5f5460ff166109c057604051638dfc202b60e01b815260040160405180910390fd5b5f81815b8451811015611fd957611fcf82868381518110611fc257611fc26125b1565b6020026020010151611fe1565b9150600101611fa3565b509392505050565b5f818310611ffb575f828152602084905260409020612009565b5f8381526020839052604090205b9392505050565b803560ff81168114612020575f5ffd5b919050565b5f60208284031215612035575f5ffd5b61200982612010565b80356001600160401b0381168114612020575f5ffd5b5f5f60408385031215612065575f5ffd5b61206e8361203e565b915061207c6020840161203e565b90509250929050565b5f60208284031215612095575f5ffd5b6120098261203e565b6001600160a01b03811681146120b2575f5ffd5b50565b5f5f604083850312156120c6575f5ffd5b6120cf8361203e565b915060208301356120df8161209e565b809150509250929050565b5f5f604083850312156120fb575f5ffd5b6121048361203e565b915061207c60208401612010565b5f5f5f5f5f5f5f5f60e0898b031215612129575f5ffd5b6121328961203e565b975061214060208a0161203e565b965061214e60408a0161203e565b9550606089013594506080890135935060a0890135925060c08901356001600160401b0381111561217d575f5ffd5b8901601f81018b1361218d575f5ffd5b80356001600160401b038111156121a2575f5ffd5b8b60208260051b84010111156121b6575f5ffd5b989b979a50959850939692959194602001935050565b80151581146120b2575f5ffd5b5f5f5f606084860312156121eb575f5ffd5b6121f484612010565b925060208401356122048161209e565b91506040840135612214816121cc565b809150509250925092565b5f5f5f5f60808587031215612232575f5ffd5b61223b85612010565b93506020850135925061225060408601612010565b915061225e60608601612010565b905092959194509250565b5f5f6040838503121561227a575f5ffd5b6122838361203e565b946020939093013593505050565b5f5f5f5f5f5f5f60e0888a0312156122a7575f5ffd5b6122b08861203e565b9650602088013595506122c56040890161203e565b9450606088013593506122da60808901612010565b92506122e860a08901612010565b915060c08801356122f88161209e565b8091505092959891949750929550565b5f60208284031215612318575f5ffd5b5035919050565b5f5f5f838503610180811215612333575f5ffd5b610160811215612341575f5ffd5b508392506101608401356001600160401b0381111561235e575f5ffd5b8401601f8101861361236e575f5ffd5b80356001600160401b03811115612383575f5ffd5b866020828401011115612394575f5ffd5b939660209190910195509293505050565b5f602082840312156123b5575f5ffd5b81356120098161209e565b6020808252600d908201526c2ab735b737bbb71031b430b4b760991b604082015260600190565b5f602082840312156123f7575f5ffd5b8151612009816121cc565b6020808252600a908201526927b7363c9037bbb732b960b11b604082015260600190565b634e487b7160e01b5f52601160045260245ffd5b6001600160401b038281168282160390811115610ec457610ec4612426565b6001600160401b038181168382160190811115610ec457610ec4612426565b60408152826040820152828460608301375f606084830101525f601f19601f85011682016060810160608483030160208501528085518083526080840191506020870193505f92505b808310156124e457835182526020820191506020840193506001830192506124c1565b50979650505050505050565b610160810161250f826125028561203e565b6001600160401b03169052565b602083810135908301526125256040840161203e565b6001600160401b0316604083015261253f6060840161203e565b6001600160401b0316606083015261255960808401612010565b60ff16608083015261256d60a08401612010565b60ff1660a083015260c0838101359083015260e080840135908301526101008084013590830152610120808401359083015261014092830135929091019190915290565b634e487b7160e01b5f52603260045260245ffdfea2646970667358221220b8d7031be99563f7423bd9eb35971b5e82cff0275fe5b63e772c6f230808dbe464736f6c634300081c0033";
        public AppChainAnchorDeploymentBase() : base(BYTECODE) { }
        public AppChainAnchorDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class MaxProofSizeFunction : MaxProofSizeFunctionBase { }

    [Function("MAX_PROOF_SIZE", "uint256")]
    public class MaxProofSizeFunctionBase : FunctionMessage
    {

    }

    public partial class AnchorCommitmentsFunction : AnchorCommitmentsFunctionBase { }

    [Function("anchorCommitments", "bytes32")]
    public class AnchorCommitmentsFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
        [Parameter("uint64", "", 2)]
        public virtual ulong ReturnValue2 { get; set; }
    }

    public partial class AppChainsFunction : AppChainsFunctionBase { }

    [Function("appChains", typeof(AppChainsOutputDTO))]
    public class AppChainsFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class BlockHashesRootsFunction : BlockHashesRootsFunctionBase { }

    [Function("blockHashesRoots", "bytes32")]
    public class BlockHashesRootsFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
        [Parameter("uint64", "", 2)]
        public virtual ulong ReturnValue2 { get; set; }
    }

    public partial class ChainIdByGenesisFunction : ChainIdByGenesisFunctionBase { }

    [Function("chainIdByGenesis", "uint64")]
    public class ChainIdByGenesisFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class GetAppChainConfigFunction : GetAppChainConfigFunctionBase { }

    [Function("getAppChainConfig", typeof(GetAppChainConfigOutputDTO))]
    public class GetAppChainConfigFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
    }

    public partial class GetChainAuthorityFunction : GetChainAuthorityFunctionBase { }

    [Function("getChainAuthority", "address")]
    public class GetChainAuthorityFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
    }

    public partial class GetLatestAnchorFunction : GetLatestAnchorFunctionBase { }

    [Function("getLatestAnchor", typeof(GetLatestAnchorOutputDTO))]
    public class GetLatestAnchorFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
    }

    public partial class LatestAnchorFunction : LatestAnchorFunctionBase { }

    [Function("latestAnchor", typeof(LatestAnchorOutputDTO))]
    public class LatestAnchorFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
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

    public partial class ProofSystemsFunction : ProofSystemsFunctionBase { }

    [Function("proofSystems", typeof(ProofSystemsOutputDTO))]
    public class ProofSystemsFunctionBase : FunctionMessage
    {
        [Parameter("uint8", "", 1)]
        public virtual byte ReturnValue1 { get; set; }
    }

    public partial class RaiseMinimumAnchorVersionFunction : RaiseMinimumAnchorVersionFunctionBase { }

    [Function("raiseMinimumAnchorVersion")]
    public class RaiseMinimumAnchorVersionFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint8", "newFloor", 2)]
        public virtual byte NewFloor { get; set; }
    }

    public partial class RaiseMinimumProofSystemFunction : RaiseMinimumProofSystemFunctionBase { }

    [Function("raiseMinimumProofSystem")]
    public class RaiseMinimumProofSystemFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint8", "newFloor", 2)]
        public virtual byte NewFloor { get; set; }
    }

    public partial class RegisterAppChainFunction : RegisterAppChainFunctionBase { }

    [Function("registerAppChain")]
    public class RegisterAppChainFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("bytes32", "genesisHash", 2)]
        public virtual byte[] GenesisHash { get; set; }
        [Parameter("uint64", "genesisBlock", 3)]
        public virtual ulong GenesisBlock { get; set; }
        [Parameter("bytes32", "genesisStateRoot", 4)]
        public virtual byte[] GenesisStateRoot { get; set; }
        [Parameter("uint8", "minimumProofSystem", 5)]
        public virtual byte MinimumProofSystem { get; set; }
        [Parameter("uint8", "minimumAnchorVersion", 6)]
        public virtual byte MinimumAnchorVersion { get; set; }
        [Parameter("address", "authority", 7)]
        public virtual string Authority { get; set; }
    }

    public partial class RegisterProofSystemFunction : RegisterProofSystemFunctionBase { }

    [Function("registerProofSystem")]
    public class RegisterProofSystemFunctionBase : FunctionMessage
    {
        [Parameter("uint8", "proofSystem", 1)]
        public virtual byte ProofSystem { get; set; }
        [Parameter("address", "verifier", 2)]
        public virtual string Verifier { get; set; }
        [Parameter("bool", "requiresProof", 3)]
        public virtual bool RequiresProof { get; set; }
    }

    public partial class RegisterSchemaFunction : RegisterSchemaFunctionBase { }

    [Function("registerSchema")]
    public class RegisterSchemaFunctionBase : FunctionMessage
    {
        [Parameter("uint8", "version", 1)]
        public virtual byte Version { get; set; }
        [Parameter("bytes32", "hashFunction", 2)]
        public virtual byte[] HashFunction { get; set; }
        [Parameter("uint8", "trieType", 3)]
        public virtual byte TrieType { get; set; }
        [Parameter("uint8", "stateModel", 4)]
        public virtual byte StateModel { get; set; }
    }

    public partial class SchemaExistsFunction : SchemaExistsFunctionBase { }

    [Function("schemaExists", "bool")]
    public class SchemaExistsFunctionBase : FunctionMessage
    {
        [Parameter("uint8", "", 1)]
        public virtual byte ReturnValue1 { get; set; }
    }

    public partial class SetChainAuthorityFunction : SetChainAuthorityFunctionBase { }

    [Function("setChainAuthority")]
    public class SetChainAuthorityFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("address", "newAuthority", 2)]
        public virtual string NewAuthority { get; set; }
    }

    public partial class SubmitAnchorFunction : SubmitAnchorFunctionBase { }

    [Function("submitAnchor")]
    public class SubmitAnchorFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "a", 1)]
        public virtual AggregatedAnchor A { get; set; }
        [Parameter("bytes", "proof", 2)]
        public virtual byte[] Proof { get; set; }
    }

    public partial class TransferOwnershipFunction : TransferOwnershipFunctionBase { }

    [Function("transferOwnership")]
    public class TransferOwnershipFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }

    public partial class UnpauseFunction : UnpauseFunctionBase { }

    [Function("unpause")]
    public class UnpauseFunctionBase : FunctionMessage
    {

    }

    public partial class VerifyBlockInclusionFunction : VerifyBlockInclusionFunctionBase { }

    [Function("verifyBlockInclusion", "bool")]
    public class VerifyBlockInclusionFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint64", "anchorEndBlock", 2)]
        public virtual ulong AnchorEndBlock { get; set; }
        [Parameter("uint64", "blockNumber", 3)]
        public virtual ulong BlockNumber { get; set; }
        [Parameter("bytes32", "blockHash", 4)]
        public virtual byte[] BlockHash { get; set; }
        [Parameter("bytes32", "preStateRoot", 5)]
        public virtual byte[] PreStateRoot { get; set; }
        [Parameter("bytes32", "postStateRoot", 6)]
        public virtual byte[] PostStateRoot { get; set; }
        [Parameter("bytes32[]", "merkleProof", 7)]
        public virtual List<byte[]> MerkleProof { get; set; }
    }

    public partial class VerifyStateRootFunction : VerifyStateRootFunctionBase { }

    [Function("verifyStateRoot", "bool")]
    public class VerifyStateRootFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("bytes32", "stateRoot", 2)]
        public virtual byte[] StateRoot { get; set; }
    }

    public partial class MaxProofSizeOutputDTO : MaxProofSizeOutputDTOBase { }

    [FunctionOutput]
    public class MaxProofSizeOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class AnchorCommitmentsOutputDTO : AnchorCommitmentsOutputDTOBase { }

    [FunctionOutput]
    public class AnchorCommitmentsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class AppChainsOutputDTO : AppChainsOutputDTOBase { }

    [FunctionOutput]
    public class AppChainsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("bytes32", "genesisHash", 2)]
        public virtual byte[] GenesisHash { get; set; }
        [Parameter("uint64", "genesisBlock", 3)]
        public virtual ulong GenesisBlock { get; set; }
        [Parameter("bytes32", "genesisStateRoot", 4)]
        public virtual byte[] GenesisStateRoot { get; set; }
        [Parameter("uint8", "minimumProofSystem", 5)]
        public virtual byte MinimumProofSystem { get; set; }
        [Parameter("uint8", "minimumAnchorVersion", 6)]
        public virtual byte MinimumAnchorVersion { get; set; }
        [Parameter("address", "authority", 7)]
        public virtual string Authority { get; set; }
        [Parameter("bool", "registered", 8)]
        public virtual bool Registered { get; set; }
    }

    public partial class BlockHashesRootsOutputDTO : BlockHashesRootsOutputDTOBase { }

    [FunctionOutput]
    public class BlockHashesRootsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class ChainIdByGenesisOutputDTO : ChainIdByGenesisOutputDTOBase { }

    [FunctionOutput]
    public class ChainIdByGenesisOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class GetAppChainConfigOutputDTO : GetAppChainConfigOutputDTOBase { }

    [FunctionOutput]
    public class GetAppChainConfigOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "genesisHash", 1)]
        public virtual byte[] GenesisHash { get; set; }
        [Parameter("uint64", "genesisBlock", 2)]
        public virtual ulong GenesisBlock { get; set; }
        [Parameter("bytes32", "genesisStateRoot", 3)]
        public virtual byte[] GenesisStateRoot { get; set; }
        [Parameter("uint8", "minimumProofSystem", 4)]
        public virtual byte MinimumProofSystem { get; set; }
        [Parameter("uint8", "minimumAnchorVersion", 5)]
        public virtual byte MinimumAnchorVersion { get; set; }
        [Parameter("address", "authority", 6)]
        public virtual string Authority { get; set; }
        [Parameter("bool", "registered", 7)]
        public virtual bool Registered { get; set; }
    }

    public partial class GetChainAuthorityOutputDTO : GetChainAuthorityOutputDTOBase { }

    [FunctionOutput]
    public class GetChainAuthorityOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetLatestAnchorOutputDTO : GetLatestAnchorOutputDTOBase { }

    [FunctionOutput]
    public class GetLatestAnchorOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint64", "endBlock", 1)]
        public virtual ulong EndBlock { get; set; }
        [Parameter("bytes32", "endBlockHash", 2)]
        public virtual byte[] EndBlockHash { get; set; }
        [Parameter("bytes32", "postStateRoot", 3)]
        public virtual byte[] PostStateRoot { get; set; }
        [Parameter("bytes32", "manifestHash", 4)]
        public virtual byte[] ManifestHash { get; set; }
    }

    public partial class LatestAnchorOutputDTO : LatestAnchorOutputDTOBase { }

    [FunctionOutput]
    public class LatestAnchorOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint64", "endBlock", 1)]
        public virtual ulong EndBlock { get; set; }
        [Parameter("bytes32", "endBlockHash", 2)]
        public virtual byte[] EndBlockHash { get; set; }
        [Parameter("bytes32", "postStateRoot", 3)]
        public virtual byte[] PostStateRoot { get; set; }
        [Parameter("bytes32", "manifestHash", 4)]
        public virtual byte[] ManifestHash { get; set; }
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

    public partial class ProofSystemsOutputDTO : ProofSystemsOutputDTOBase { }

    [FunctionOutput]
    public class ProofSystemsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "verifier", 1)]
        public virtual string Verifier { get; set; }
        [Parameter("bool", "requiresProof", 2)]
        public virtual bool RequiresProof { get; set; }
        [Parameter("bool", "exists", 3)]
        public virtual bool Exists { get; set; }
    }











    public partial class SchemaExistsOutputDTO : SchemaExistsOutputDTOBase { }

    [FunctionOutput]
    public class SchemaExistsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }









    public partial class VerifyBlockInclusionOutputDTO : VerifyBlockInclusionOutputDTOBase { }

    [FunctionOutput]
    public class VerifyBlockInclusionOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class VerifyStateRootOutputDTO : VerifyStateRootOutputDTOBase { }

    [FunctionOutput]
    public class VerifyStateRootOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class AnchorSubmittedEventDTO : AnchorSubmittedEventDTOBase { }

    [Event("AnchorSubmitted")]
    public class AnchorSubmittedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "chainId", 1, true )]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint64", "startBlock", 2, true )]
        public virtual ulong StartBlock { get; set; }
        [Parameter("uint64", "endBlock", 3, true )]
        public virtual ulong EndBlock { get; set; }
        [Parameter("tuple", "anchor", 4, false )]
        public virtual AggregatedAnchor Anchor { get; set; }
    }

    public partial class AppChainRegisteredEventDTO : AppChainRegisteredEventDTOBase { }

    [Event("AppChainRegistered")]
    public class AppChainRegisteredEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "chainId", 1, true )]
        public virtual ulong ChainId { get; set; }
        [Parameter("bytes32", "genesisHash", 2, true )]
        public virtual byte[] GenesisHash { get; set; }
        [Parameter("uint64", "genesisBlock", 3, false )]
        public virtual ulong GenesisBlock { get; set; }
        [Parameter("bytes32", "genesisStateRoot", 4, false )]
        public virtual byte[] GenesisStateRoot { get; set; }
        [Parameter("uint8", "minimumProofSystem", 5, false )]
        public virtual byte MinimumProofSystem { get; set; }
        [Parameter("uint8", "minimumAnchorVersion", 6, false )]
        public virtual byte MinimumAnchorVersion { get; set; }
        [Parameter("address", "authority", 7, false )]
        public virtual string Authority { get; set; }
    }

    public partial class ChainAuthorityChangedEventDTO : ChainAuthorityChangedEventDTOBase { }

    [Event("ChainAuthorityChanged")]
    public class ChainAuthorityChangedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "chainId", 1, true )]
        public virtual ulong ChainId { get; set; }
        [Parameter("address", "oldAuthority", 2, true )]
        public virtual string OldAuthority { get; set; }
        [Parameter("address", "newAuthority", 3, true )]
        public virtual string NewAuthority { get; set; }
    }

    public partial class MinimumAnchorVersionRaisedEventDTO : MinimumAnchorVersionRaisedEventDTOBase { }

    [Event("MinimumAnchorVersionRaised")]
    public class MinimumAnchorVersionRaisedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "chainId", 1, true )]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint8", "oldFloor", 2, false )]
        public virtual byte OldFloor { get; set; }
        [Parameter("uint8", "newFloor", 3, false )]
        public virtual byte NewFloor { get; set; }
    }

    public partial class MinimumProofSystemRaisedEventDTO : MinimumProofSystemRaisedEventDTOBase { }

    [Event("MinimumProofSystemRaised")]
    public class MinimumProofSystemRaisedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "chainId", 1, true )]
        public virtual ulong ChainId { get; set; }
        [Parameter("uint8", "oldFloor", 2, false )]
        public virtual byte OldFloor { get; set; }
        [Parameter("uint8", "newFloor", 3, false )]
        public virtual byte NewFloor { get; set; }
    }

    public partial class OwnershipTransferredEventDTO : OwnershipTransferredEventDTOBase { }

    [Event("OwnershipTransferred")]
    public class OwnershipTransferredEventDTOBase : IEventDTO
    {
        [Parameter("address", "oldOwner", 1, true )]
        public virtual string OldOwner { get; set; }
        [Parameter("address", "newOwner", 2, true )]
        public virtual string NewOwner { get; set; }
    }

    public partial class PausedEventDTO : PausedEventDTOBase { }

    [Event("Paused")]
    public class PausedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, false )]
        public virtual string Account { get; set; }
    }

    public partial class ProofSystemRegisteredEventDTO : ProofSystemRegisteredEventDTOBase { }

    [Event("ProofSystemRegistered")]
    public class ProofSystemRegisteredEventDTOBase : IEventDTO
    {
        [Parameter("uint8", "proofSystem", 1, true )]
        public virtual byte ProofSystem { get; set; }
        [Parameter("address", "verifier", 2, false )]
        public virtual string Verifier { get; set; }
        [Parameter("bool", "requiresProof", 3, false )]
        public virtual bool RequiresProof { get; set; }
    }

    public partial class SchemaRegisteredEventDTO : SchemaRegisteredEventDTOBase { }

    [Event("SchemaRegistered")]
    public class SchemaRegisteredEventDTOBase : IEventDTO
    {
        [Parameter("uint8", "version", 1, true )]
        public virtual byte Version { get; set; }
        [Parameter("bytes32", "hashFunction", 2, false )]
        public virtual byte[] HashFunction { get; set; }
        [Parameter("uint8", "trieType", 3, false )]
        public virtual byte TrieType { get; set; }
        [Parameter("uint8", "stateModel", 4, false )]
        public virtual byte StateModel { get; set; }
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
}
