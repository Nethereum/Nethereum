
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI
{
    public interface ISignaturePromptService
    {
        Task<string> PromptSignatureAsync(string message);
    }
}
