namespace Nethereum.EVM.Execution
{
    public class EvmProgramExecution
    {
        public EvmProgramExecution() : this((IPrecompileProvider)null)
        {
        }

        public EvmProgramExecution(IPrecompileProvider precompileProvider)
            : this(new EvmPreCompiledContractsExecution(precompileProvider))
        {
        }

        public EvmProgramExecution(IPrecompiledContractsExecution precompiledContracts)
        {
            Arithmetic = new EvmArithmeticExecution();
            Bitwise = new EvmBitwiseExecution();
            CallingCreation = new EvmCallingCreationExecution(this);
            CallData = new  EvmCallInputDataExecution();
            CallInput = new EvmCallInputExecution();
            Code    = new EvmCodeExecution();
            StorageMemory = new EvmStorageMemoryExecution();
            PreCompiledContracts = precompiledContracts;
            BlockchainCurrentContractContext = new EvmBlockchainCurrentContractContextExecution();
            ReturnRevertLogExecution = new EvmReturnRevertLogExecution();
            StackFlowExecution = new EvmProgramStackFlowExecution();
        }
        public EvmProgramStackFlowExecution StackFlowExecution { get; set; }
        public EvmArithmeticExecution Arithmetic { get; protected set; }
        public EvmBitwiseExecution Bitwise { get; protected set; }
        public EvmCallingCreationExecution CallingCreation { get; protected set; }
        public EvmCallInputDataExecution  CallData { get; protected set; }
        public EvmCallInputExecution CallInput { get; protected set; }
        public EvmCodeExecution  Code { get; protected set; }
        public EvmStorageMemoryExecution StorageMemory { get; protected set; }
        public IPrecompiledContractsExecution PreCompiledContracts { get; protected set; }
        public EvmBlockchainCurrentContractContextExecution BlockchainCurrentContractContext { get; protected set; }

       public EvmReturnRevertLogExecution ReturnRevertLogExecution { get; protected set; }
    }
}