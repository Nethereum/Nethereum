namespace Nethereum.EVM.SourceInfo
{
   
    public class SourceMap
    {
        public int Position { get; set; }
        public int Length { get; set; }
        public int SourceFile { get; set; }
        public string JumpType { get; set; }
        public int ModifierDepth { get; set; }
    }
}