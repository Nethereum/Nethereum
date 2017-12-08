using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Nethereum.Web3;

namespace Nethereum.Desktop.Sample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void BtnGetAccoutns_OnClick(object sender, RoutedEventArgs e)
        {
            var web3 = new Web3.Web3("https://mainnet.infura.io");
            var balance = await web3.Eth.GetBalance.SendRequestAsync("0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe");
            this.Dispatcher.Invoke(() => { txtAccounts.Text = Web3.Web3.Convert.FromWei(balance.Value).ToString(); });

        }
    }
}
