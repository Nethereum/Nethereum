namespace Nethereum.EVM.SourceInfo
{
   
    public class SourceMap
    {
        /// <summary>
        /// byte-offset to the start of the range in the source file
        /// </summary>
        public int Position { get; set; }
        /// <summary>
        ///  length of the source range
        /// </summary>
        public int Length { get; set; }
        /// <summary>
        /// Source index (ie file)
        /// </summary>
        public int SourceFile { get; set; }
        /// <summary>
        ///i (into a function), o (returns from a function),.- (is a regular jump as part of e.g. a loop)
        /// </summary>
        public string JumpType { get; set; }
        /// <summary>
        /// Denotes the “modifier depth”. This depth is increased whenever the placeholder statement (_) is entered in a modifier and decreased when it is left again. 
        /// </summary>
        public int ModifierDepth { get; set; }
    }
}