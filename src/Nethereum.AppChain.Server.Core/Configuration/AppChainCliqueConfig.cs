using System;

namespace Nethereum.AppChain.Server.Configuration
{
    public sealed class AppChainCliqueConfig
    {
        public SignerKeyPair Signer { get; set; } = new SignerKeyPair();

        public string[] InitialSigners { get; set; } = Array.Empty<string>();

        public int PeriodSeconds { get; set; } = 15;

        public int EpochLength { get; set; } = 30000;
    }
}
