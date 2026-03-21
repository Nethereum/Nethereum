namespace Nethereum.PrivacyPools.PrivacyPoolSimple.ContractDefinition
{
    public partial class PrivacyPoolSimpleDeployment
    {
        public void LinkLibraries(string poseidonT3Address, string poseidonT4Address)
        {
            var bytecode = ByteCode;
            // cc5cc6be = keccak256("../../node_modules/poseidon-solidity/PoseidonT3.sol:PoseidonT3")
            bytecode = bytecode.Replace("__$cc5cc6be2a21b973d664926dadcce79f17$__", poseidonT3Address.Replace("0x", "").ToLower());
            // f6445d80 = keccak256("../../node_modules/poseidon-solidity/PoseidonT4.sol:PoseidonT4")
            bytecode = bytecode.Replace("__$f6445d80c6f958e74d2ce02826cd200ff5$__", poseidonT4Address.Replace("0x", "").ToLower());
            ByteCode = bytecode;
        }
    }
}
