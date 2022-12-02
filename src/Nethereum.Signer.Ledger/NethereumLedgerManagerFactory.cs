
using Device.Net;
using Ledger.Net;

namespace Nethereum.Ledger
{
    public class NethereumLedgerManagerFactory : ILedgerManagerFactory
    {
        public IManagesLedger GetNewLedgerManager(IDevice ledgerHidDevice, ICoinUtility coinUtility, ErrorPromptDelegate errorPrompt)
        {
            return new LedgerManager(new LedgerManagerTransportUpgrade(ledgerHidDevice), coinUtility, errorPrompt);
        }
    }
}
