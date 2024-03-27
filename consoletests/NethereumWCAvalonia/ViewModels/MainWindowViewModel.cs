using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Nethereum.ABI.EIP712;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Chain;
using Nethereum.RPC.HostWallet;
using Nethereum.Signer.EIP712;
using Nethereum.WalletConnect;
using Nethereum.Web3;
using QRCoder;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletConnectSharp.Core;
using WalletConnectSharp.Sign;
using WalletConnectSharp.Sign.Models;
using WalletConnectSharp.Storage;

namespace NethereumWCAvalonia.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {

            OnInitCommand = ReactiveCommand.CreateFromTask(InitAsync);
            OnSignedTypedDataCommand = ReactiveCommand.CreateFromTask(SignTypedDataAsync);
            OnSwitchChainCommand = ReactiveCommand.CreateFromTask(SwitchChainAsync);
            OnAddChainCommand = ReactiveCommand.CreateFromTask(AddEthereumChainAsync);
            OnPersonalSignCommand = ReactiveCommand.CreateFromTask(PersonalSignAsync);
            Connected = false;

        }

        public ICommand OnInitCommand { get; }
        public ICommand OnSignedTypedDataCommand { get; }
        public ICommand OnSwitchChainCommand { get; }
        public ICommand OnAddChainCommand { get; }

        public ICommand OnPersonalSignCommand { get; }


        WalletConnectSignClient client;

        private Bitmap? _qrCode; 

        public Bitmap? QRCode
        {
            get
            {
                return _qrCode;
            }
            set
            {

                this.RaiseAndSetIfChanged(ref _qrCode, value);
            }
        }


        private bool _connected;

        public bool Connected
        {
            get
            {
                return _connected;
            }
            set
            {

                this.RaiseAndSetIfChanged(ref _connected, value);
            }
        }

        private string? _address;

        public string? Address
        {
            get
            {
                return _address;
            }
            set
            {

                this.RaiseAndSetIfChanged(ref _address, value);
            }
        }

        private string? _chainId; 

        public string? ChainId
        {
            get
            {
                return _chainId;
            }
            set
            {

                this.RaiseAndSetIfChanged(ref _chainId, value);
            }
        }

        NethereumWalletConnectService walletConnectService;
        WalletConnectConnectedSession walletConnectConnectedSession;
        NethereumWalletConnectHostProvider walletConnectHostProvider;
        public string _response;
        public string _recoveredAccount;

        public string? Response
        {
            get
            {
                return _response;
            }
            set
            {

                this.RaiseAndSetIfChanged(ref _response, value);
            }
        }

        public string? RecoveredAccount
        {
            get
            {
                return _recoveredAccount;
            }
            set
            {

                this.RaiseAndSetIfChanged(ref _recoveredAccount, value);
            }
        }

        public async Task InitAsync()
        {
            try
            {
                var options = new SignClientOptions()
                {
                    ProjectId = "",
                    Metadata = new Metadata()
                    {
                        Description = "An example project to showcase WalletConnectSharpv2",
                        Icons = new[] { "https://walletconnect.com/meta/favicon.ico" },
                        Name = "WC Example",
                        Url = "https://walletconnect.com"
                    },
                    Storage = new InMemoryStorage()
                    
                };
                var connectionOptions = NethereumWalletConnectService.GetDefaultConnectOptions();

                if (client == null)
                {
                    
                    client = await WalletConnectSignClient.Init(options);
                }

                walletConnectService = new NethereumWalletConnectService(client);
                walletConnectHostProvider = new NethereumWalletConnectHostProvider(walletConnectService); // This needs to be initialise straight away to hook up the events

                walletConnectHostProvider.SelectedAccountChanged += async (address) =>
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Address = address;
                    });
                };

                walletConnectHostProvider.NetworkChanged += async (chainId) =>
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ChainId = chainId.ToString();
                    });
                };


                var connectionUri = await walletConnectService.InitialiseConnectionAndGetQRUriAsync(connectionOptions);

                if (!string.IsNullOrEmpty(connectionUri))
                {
                    using MemoryStream ms = new();
                    QRCodeGenerator qrCodeGenerate = new();
                    var qrCodeData = qrCodeGenerate.CreateQrCode(connectionUri, QRCodeGenerator.ECCLevel.Q);
                    var qrCodePng = new PngByteQRCode(qrCodeData);
                    var bytes = qrCodePng.GetGraphic(20);
                    var memoryStream = new MemoryStream(bytes);

                    QRCode = Bitmap.DecodeToHeight(memoryStream, 300);

                    var selectedAddress = await walletConnectService.WaitForConnectionApprovalAndGetSelectedAccountAsync();
                    Connected = true;

                    walletConnectConnectedSession = walletConnectService.GetWalletConnectConnectedSession();
                   
                }
            }
            catch (Exception ex)
            {
                Response = ex.Message;
                Debug.WriteLine(ex.ToString());
            }   

        }



        public async Task SignTypedDataAsync()
        {
            try
            {

                var web3 = await walletConnectHostProvider.GetWeb3Async();

                var typedData = GetMailTypedDefinition();

                var mail = new Mail
                {
                    From = new Person
                    {
                        Name = "Cow",
                        Wallets = new List<string> { "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826", "0xDeaDbeefdEAdbeefdEadbEEFdeadbeEFdEaDbeeF" }
                    },
                    To = new List<Person>
                {
                    new Person
                    {
                        Name = "Bob",
                        Wallets = new List<string> { "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB", "0xB0BdaBea57B0BDABeA57b0bdABEA57b0BDabEa57", "0xB0B0b0b0b0b0B000000000000000000000000000" }
                    }
                },
                    Contents = "Hello, Bob!"
                };

                typedData.Domain.ChainId = 1;
                typedData.SetMessage(mail);



                Response = await web3.Eth.AccountSigning.SignTypedDataV4.SendRequestAsync(typedData.ToJson());
                RecoveredAccount = new Eip712TypedDataSigner().RecoverFromSignatureV4(typedData, Response);
            }
            catch (Exception ex)
            {
                Response = ex.Message;
                Debug.WriteLine(ex.ToString());
            }

        }

        public async Task SwitchChainAsync()
        {
            try
            {
                var web3 = await walletConnectHostProvider.GetWeb3Async();

                var response = await web3.Eth.HostWallet.SwitchEthereumChain.SendRequestAsync(new SwitchEthereumChainParameter()
                {
                    ChainId = 1.ToHexBigInteger()
                });
               
                Response = response;
                
            }
            catch(Exception ex)
            {
                Response = ex.Message;
                Debug.WriteLine(ex.ToString());
            }

        }

        public async Task PersonalSignAsync()
        {
            try
            {
                //var web3 = new Web3();
                //web3.Client.OverridingRequestInterceptor = new NethereumWalletConnectInterceptor(walletConnectService);

                var web3 = await walletConnectHostProvider.GetWeb3Async();

                var response = await web3.Eth.AccountSigning.PersonalSign.SendRequestAsync(new HexUTF8String("Hello World"));
                Response = response;
            }
            catch (Exception ex)
            {
                Response = ex.Message;
                Debug.WriteLine(ex.ToString());
            }

        }

        public async Task AddEthereumChainAsync()
        {
            try
            {
                //var web3 = new Web3();
                //web3.Client.OverridingRequestInterceptor = new NethereumWalletConnectInterceptor(walletConnectService);

                var web3 = await walletConnectHostProvider.GetWeb3Async();

                var chainFeature = ChainDefaultFeaturesServicesRepository.GetDefaultChainFeature(Nethereum.Signer.Chain.Optimism);
                var addParameter = chainFeature.ToAddEthereumChainParameter();
                var response = await web3.Eth.HostWallet.AddEthereumChain.SendRequestAsync(addParameter);
               
                Response = response;
                
            }
            catch (Exception ex)
            {
                Response = ex.Message;
                Debug.WriteLine(ex.ToString());
            }

        }

        //The generic EIP712 Typed schema defintion for this message
        public TypedData<Domain> GetMailTypedDefinition()
        {
            return new TypedData<Domain>
            {
                Domain = new Domain
                {
                    Name = "Ether Mail",
                    Version = "1",
                    ChainId = 1,
                    VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
                },
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(Group), typeof(Mail), typeof(Person)),
                PrimaryType = nameof(Mail),
            };
        }

    }
}
