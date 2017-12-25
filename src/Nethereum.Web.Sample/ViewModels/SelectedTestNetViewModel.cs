using Nethereum.Web.Sample.Model;
using System.Collections.Generic;

namespace Nethereum.Web.Sample.ViewModels
{
    public class SelectedTestNetViewModel
    {
        public int SelectedTestnetId { get; set; }
        public List<TestNet> TestNetList { get; set; }
        public string EndpointUrl { get; set; }
        public List<TransactionViewModel> Transactions { get; set; }
    }
}