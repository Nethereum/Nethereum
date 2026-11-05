using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class HostedServiceProcessExitCodeCollection
    {
        public const string Name = "HostedServiceProcessExitCode";
    }
}
