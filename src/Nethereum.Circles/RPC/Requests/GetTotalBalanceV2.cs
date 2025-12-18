using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Circles.RPC.Requests
{
    /// <summary>
    /// Executes the Circles `circlesV2_getTotalBalance` RPC method.
    /// </summary>
    public class GetTotalBalanceV2 : RpcRequestResponseHandler<string>
    {
        public GetTotalBalanceV2(IClient client) : base(client, "circlesV2_getTotalBalance") { }

        /// <summary>
        /// Sends a request to get the total balance of an avatar using Circles V2.
        /// </summary>
        /// <param name="avatar">The avatar address.</param>
        /// <param name="asTimeCircles">Whether to return the balance as TimeCircles (default: true).</param>
        /// <param name="id">Optional request identifier.</param>
        /// <returns>The total balance as a string.</returns>
        public Task<string> SendRequestAsync(string avatar, bool asTimeCircles = true, object id = null)
        {
            if (string.IsNullOrEmpty(avatar))
                throw new ArgumentNullException(nameof(avatar));

            return base.SendRequestAsync(id, avatar, asTimeCircles);
        }
    }
}

