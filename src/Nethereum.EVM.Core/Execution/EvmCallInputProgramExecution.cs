namespace Nethereum.EVM.Execution
{
    public class EvmCallInputExecution
    {
        public void CallValue(Program program)
        {
            var value = program.ProgramContext.Value;
            program.StackPush(value);
            program.Step();
        }

        public void Caller(Program program)
        {
            var address = program.ProgramContext.AddressCallerEncoded;
            program.StackPush(address);
            program.Step();
        }

        public void Origin(Program program)
        {
            var address = program.ProgramContext.AddressOriginEncoded;
            program.StackPush(address);
            program.Step();
        }
    }
}