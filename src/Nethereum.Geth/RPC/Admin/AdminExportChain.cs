using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Admin
{
    /// <Summary>
    /// ExportChain exports the current blockchain into a local file,
    /// or a range of blocks if first and last are non-nil
    /// </Summary>
    public class AdminExportChain : RpcRequestResponseHandler<bool>, IAdminExportChain
    {
        public AdminExportChain(IClient client) : base(client, ApiMethods.admin_exportChain.ToString())
        {
        }

        public RpcRequest BuildRequest(string file, object id = null)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            return base.BuildRequest(id, file);
        }

        public Task<bool> SendRequestAsync(string file, object id = null)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            return base.SendRequestAsync(id, file);
        }

        public RpcRequest BuildRequest(string file, long first, object id = null)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            return base.BuildRequest(id, file, first);
        }

        public Task<bool> SendRequestAsync(string file, long first, object id = null)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            return base.SendRequestAsync(id, file, first);
        }

        public RpcRequest BuildRequest(string file, long first, long last, object id = null)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            return base.BuildRequest(id, file, first, last);
        }

        public Task<bool> SendRequestAsync(string file, long first, long last, object id = null)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            return base.SendRequestAsync(id, file, first, last);
        }
    }
}