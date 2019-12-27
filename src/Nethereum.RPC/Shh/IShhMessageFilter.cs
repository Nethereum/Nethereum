using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Nethereum.RPC.Shh.DTOs;
using Nethereum.RPC.Shh.MessageFilter;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.RPC.Shh
{
    public interface IShhMessageFilter
    {
        IShhNewMessageFilter NewMessageFilter { get; }
        IShhDeleteMessageFilter DeleteMessageFilter { get; }
        IShhGetFilterMessages GetFilterMessages { get; }
    }
}
