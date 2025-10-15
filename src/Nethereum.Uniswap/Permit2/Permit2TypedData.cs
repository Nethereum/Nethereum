using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Uniswap.Core.Permit2.ContractDefinition;
using System.Numerics;

namespace Nethereum.Uniswap.Permit2
{
    [Struct("EIP712Domain")]
    public class DomainWithNameChainIdAndVerifyingContract : IDomain
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }

        [Parameter("uint256", "chainId", 2)]
        public virtual BigInteger? ChainId { get; set; }

        [Parameter("address", "verifyingContract", 3)]
        public virtual string VerifyingContract { get; set; }

    }

    public class Permit2TypedData
    {
        public static TypedData<DomainWithNameChainIdAndVerifyingContract> GetPermitSingleTypeDefinition(BigInteger chainId, string verifyingContract)
        {
            return new TypedData<DomainWithNameChainIdAndVerifyingContract>
            {
                Domain = new DomainWithNameChainIdAndVerifyingContract
                {
                    Name = "Permit2",
                    ChainId = chainId,
                    VerifyingContract = verifyingContract
                },
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(DomainWithNameChainIdAndVerifyingContract), typeof(PermitSingle), typeof(PermitDetails)),
                PrimaryType = "PermitSingle",
            };
        }

        public static TypedData<DomainWithNameChainIdAndVerifyingContract> GetPermitBatchTypeDefinition(BigInteger chainId, string verifyingContract)
        {
            return new TypedData<DomainWithNameChainIdAndVerifyingContract>
            {
                Domain = new DomainWithNameChainIdAndVerifyingContract
                {
                    Name = "Permit2",
                    
                    ChainId = chainId,
                    VerifyingContract = verifyingContract
                },
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(DomainWithNameChainIdAndVerifyingContract), typeof(PermitBatch), typeof(PermitDetails)),
                PrimaryType = "PermitBatch",
            };
        }

        public static TypedData<DomainWithNameChainIdAndVerifyingContract> GetPermitTransferFromTypeDefinition(BigInteger chainId, string verifyingContract)
        {
            return new TypedData<DomainWithNameChainIdAndVerifyingContract>
            {
                Domain = new DomainWithNameChainIdAndVerifyingContract
                {
                    Name = "Permit2",
          
                    ChainId = chainId,
                    VerifyingContract = verifyingContract
                },
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(DomainWithNameChainIdAndVerifyingContract), typeof(PermitTransferFrom), typeof(TokenPermissions)),
                PrimaryType = "PermitTransferFrom",
            };
        }

        public static TypedData<DomainWithNameChainIdAndVerifyingContract> GetPermitBatchTransferFromTypeDefinition(BigInteger chainId, string verifyingContract)
        {
            return new TypedData<DomainWithNameChainIdAndVerifyingContract>
            {
                Domain = new DomainWithNameChainIdAndVerifyingContract
                {
                    Name = "Permit2",
              
                    ChainId = chainId,
                    VerifyingContract = verifyingContract
                },
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(DomainWithNameChainIdAndVerifyingContract), typeof(PermitBatchTransferFrom), typeof(TokenPermissions)),
                PrimaryType = "PermitBatchTransferFrom",
            };
        }


    }
}
