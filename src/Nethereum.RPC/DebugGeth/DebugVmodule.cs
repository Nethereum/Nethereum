using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.DebugGeth
{
    /// <Summary>
    ///     Sets the logging verbosity pattern.
    ///     Examples
    ///     If you want to see messages from a particular Go package (directory) and all subdirectories, use:
    ///     > debug.vmodule("eth/*=6")
    ///     If you want to restrict messages to a particular package (e.g. p2p) but exclude subdirectories, use:
    ///     > debug.vmodule("p2p=6")
    ///     If you want to see log messages from a particular source file, use
    ///     > debug.vmodule("server.go=6")
    ///     You can compose these basic patterns. If you want to see all output from peer.go in a package below eth
    ///     (eth/peer.go, eth/downloader/peer.go) as well as output from package p2p at level <= 5, use:
    ///     debug.vmodule("eth/*/peer.go=6,p2p=5")
    /// </Summary>
    public class DebugVmodule : RpcRequestResponseHandler<object>
    {
        public DebugVmodule(IClient client) : base(client, ApiMethods.debug_vmodule.ToString())
        {
        }

        public Task<object> SendRequestAsync(string pattern, object id = null)
        {
            return base.SendRequestAsync(id, pattern);
        }

        public RpcRequest BuildRequest(string pattern, object id = null)
        {
            return base.BuildRequest(id, pattern);
        }
    }
}