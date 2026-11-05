using System;

namespace Nethereum.CoreChain.Hosting
{
    public static class HostArguments
    {
        public const string ReloadConfigOnChangeOff = "--hostBuilder:reloadConfigOnChange=false";

        public static string[] WithoutConfigReload(string[] args)
        {
            args ??= Array.Empty<string>();
            var result = new string[args.Length + 1];
            Array.Copy(args, result, args.Length);
            result[args.Length] = ReloadConfigOnChangeOff;
            return result;
        }
    }
}
