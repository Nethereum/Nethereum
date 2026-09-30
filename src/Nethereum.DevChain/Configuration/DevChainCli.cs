using System;
using System.IO;
using System.Linq;
using Nethereum.ChainNode.Hosting.Configuration;

namespace Nethereum.DevChain.Configuration
{
    public static class DevChainCli
    {
        public static bool TryHandleAdvancedHelp(string[] args, TextWriter writer)
        {
            if (!args.Any(a => a == "--help-advanced")) return false;
            PrintAdvancedHelp(writer);
            return true;
        }

        public static void PrintAdvancedHelp(TextWriter writer)
        {
            writer.WriteLine($"Nethereum DevChain Server v{Nethereum.CoreChain.NodeVersion.Version}");
            writer.WriteLine("Expert DevChain:* settings — raw form only (or, where noted above, a friendly flag).");
            writer.WriteLine();
            writer.WriteLine("USAGE: nethereum-devchain --DevChain:<Path> <value> [...]");
            writer.WriteLine();

            ChainNodeConfigSurface.RenderAdvancedHelp(writer, "DevChain", ChainNodeKind.DevChain);

            writer.WriteLine();
            writer.WriteLine("Settings can also come from appsettings.json (DevChain section) or DevChain__ env vars.");
            writer.WriteLine("Run with --help for the everyday flags.");
        }
    }
}
