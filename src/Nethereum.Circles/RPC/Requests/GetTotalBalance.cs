using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Circles.RPC.Requests
{
    /// <summary>
    /// Executes the Circles `circles_getTotalBalance` RPC method.
    /// </summary>
    public class GetTotalBalance : RpcRequestResponseHandler<string>
    {
        public GetTotalBalance(IClient client) : base(client, "circles_getTotalBalance") { }

        /// <summary>
        /// Sends a request to get the total balance of an avatar.
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

