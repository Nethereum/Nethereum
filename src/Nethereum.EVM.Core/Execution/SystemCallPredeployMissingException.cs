namespace Nethereum.EVM.Execution
{
    public class SystemCallPredeployMissingException : SystemCallFailedException
    {
        public SystemCallPredeployMissingException(string targetAddress)
            : base(targetAddress, "no_code_at_predeploy",
                $"No code at request predeploy {targetAddress}; the block is invalid")
        {
        }
    }
}
