namespace Nethereum.EVM.Hardforks.Policies
{
    public abstract class CodePrefixPolicy
    {
        public static readonly CodePrefixPolicy Permissive = new PermissivePolicy();

        public static readonly CodePrefixPolicy Eip3541RejectEf = new Eip3541RejectEfPolicy();

        public abstract bool RejectsEfPrefix { get; }

        private sealed class PermissivePolicy : CodePrefixPolicy
        {
            public override bool RejectsEfPrefix => false;
        }

        private sealed class Eip3541RejectEfPolicy : CodePrefixPolicy
        {
            public override bool RejectsEfPrefix => true;
        }
    }
}
