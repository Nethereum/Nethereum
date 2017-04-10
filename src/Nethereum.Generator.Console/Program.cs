using System;

namespace Nethereum.Generator.Console
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                return new App().Execute(args);
            }
            catch (Exception ex)
            {
                System.Console.Write(ex);
                return 1;
            }
        }
    }
}
