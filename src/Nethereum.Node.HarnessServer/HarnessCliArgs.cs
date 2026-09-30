using System;

namespace Nethereum.Node.HarnessServer
{
    public static class HarnessCliArgs
    {
        public static HarnessServerOptions Parse(string[] args)
        {
            var options = new HarnessServerOptions();

            for (var i = 0; i < args.Length; i++)
            {
                var (name, inlineValue) = SplitFlag(args[i]);

                string ValueFor(string flagName)
                {
                    if (inlineValue != null) return inlineValue;
                    if (i + 1 < args.Length) return args[++i];
                    throw new ArgumentException($"Missing value for {flagName}");
                }

                switch (name)
                {
                    case "--datadir":
                        options.DataDir = ValueFor(name);
                        break;
                    case "--http.addr":
                        options.HttpAddr = ValueFor(name);
                        break;
                    case "--http.port":
                        options.HttpPort = int.Parse(ValueFor(name));
                        break;
                    case "--authrpc.addr":
                        options.AuthRpcAddr = ValueFor(name);
                        break;
                    case "--authrpc.port":
                        options.AuthRpcPort = int.Parse(ValueFor(name));
                        break;
                    case "--authrpc.jwtsecret":
                        options.JwtSecretPath = ValueFor(name);
                        break;
                    case "--genesis":
                        options.GenesisPath = ValueFor(name);
                        break;
                    case "--verbose":
                        options.Verbose = true;
                        break;
                }
            }

            return options;
        }

        private static (string Name, string? InlineValue) SplitFlag(string arg)
        {
            var eq = arg.IndexOf('=');
            return eq < 0 ? (arg, null) : (arg.Substring(0, eq), arg.Substring(eq + 1));
        }
    }
}
