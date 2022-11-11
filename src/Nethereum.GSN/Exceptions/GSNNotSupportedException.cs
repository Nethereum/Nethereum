namespace Nethereum.GSN.Exceptions
{
    public class GSNNotSupportedException : GSNException
    {
        public GSNNotSupportedException()
            : base("Contract does not support Gas Station Network") { }
    }
}
