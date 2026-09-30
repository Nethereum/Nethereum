namespace Nethereum.EVM.Hardforks.Policies
{
    public abstract class EmptyAccountPolicy
    {
        public static readonly EmptyAccountPolicy Persist = new PersistPolicy();

        public static readonly EmptyAccountPolicy Eip161Clear = new Eip161ClearPolicy();

        public abstract bool DeletesEmpties { get; }

        private sealed class PersistPolicy : EmptyAccountPolicy
        {
            public override bool DeletesEmpties => false;
        }

        private sealed class Eip161ClearPolicy : EmptyAccountPolicy
        {
            public override bool DeletesEmpties => true;
        }
    }
}
