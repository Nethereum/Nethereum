namespace Nethereum.Freezer
{
    public readonly struct ItemRange
    {
        public ushort FileNumber { get; }
        public uint Start { get; }
        public uint Length { get; }

        public ItemRange(ushort fileNumber, uint start, uint length)
        {
            FileNumber = fileNumber;
            Start = start;
            Length = length;
        }
    }
}
