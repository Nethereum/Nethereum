namespace Nethereum.GSN.Exceptions
{
    public class GSNLowBalanceException : GSNException
    {
        public GSNLowBalanceException(string message) : base(message) { }
    }
}
