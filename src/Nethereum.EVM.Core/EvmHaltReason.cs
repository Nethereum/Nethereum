using Nethereum.Documentation;
namespace Nethereum.EVM
{
    [NethereumDocExample(DocSection.EvmSimulator, "exceptional-halt", "Why a frame halted exceptionally")]
    public enum EvmHaltReason : byte
    {
        None = 0,
        ExceptionalHalt,
        OutOfGas,
        StackUnderflow,
        StackOverflow,
        InvalidOpcode,
        InvalidJumpDestination,
        WriteInStaticContext,
        OutOfBoundsRead,
        InvalidParameter
    }
}
