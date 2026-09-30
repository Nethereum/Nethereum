using System;

namespace Nethereum.AppChain.Anchoring.Finality
{
    public sealed class AnchorRecord
    {
        public ulong EndBlock { get; set; }

        public byte[] EndBlockHash { get; set; } = Array.Empty<byte>();

        public byte[] PostStateRoot { get; set; } = Array.Empty<byte>();
    }
}
