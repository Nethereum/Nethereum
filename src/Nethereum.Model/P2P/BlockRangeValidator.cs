namespace Nethereum.Model.P2P
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "model-peer-limits", "BlockRangeValidator")]
    public static class BlockRangeValidator
    {
        public static string Violation(ulong earliestBlock, ulong latestBlock, byte[] latestBlockHash)
        {
            if (earliestBlock > latestBlock)
                return "earliest > latest";
            if (latestBlockHash == null || IsZero(latestBlockHash))
                return "zero latest hash";
            return null;
        }

        private static bool IsZero(byte[] hash)
        {
            foreach (var b in hash)
                if (b != 0) return false;
            return true;
        }
    }
}
