using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.WalletConnect;
using QRCoder;
using WalletConnectSharp.Sign;
using WalletConnectSharp.Sign.Models;
using WalletConnectSharp.Core;
using System.Drawing.Imaging;
using Nethereum.Web3;
using Nethereum.Signer.EIP712;
using Nethereum.ABI.EIP712;
using Avalonia.Media.Imaging;
using System.Windows.Input;
using Avalonia.Threading;

namespace NethereumGodotAvalonia.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		public MainWindowViewModel()
		{

			OnInitCommand = ReactiveCommand.CreateFromTask(InitAsync);
			OnSignedTypedDataCommand = ReactiveCommand.CreateFromTask(SignTypedDataAsync);
			Connected = false;

		}

		public ICommand OnInitCommand { get; }
		public ICommand OnSignedTypedDataCommand { get; }


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
			var options = new SignClientOptions()
			{
				ProjectId = "",
				Metadata = new Metadata()
				{
					Description = "An example project to showcase WalletConnectSharpv2",
					Icons = new[] { "https://walletconnect.com/meta/favicon.ico" },
					Name = "WC Example",
					Url = "https://walletconnect.com"
				}
			};


			if (client == null)
			{
				client = await WalletConnectSignClient.Init(options);
			}

			var connectData = await client.Connect(NethereumWalletConnectService.GetDefaultConnectOptions());
			if (!string.IsNullOrEmpty(connectData.Uri))
			{
				using MemoryStream ms = new();
				QRCodeGenerator qrCodeGenerate = new();
				var qrCodeData = qrCodeGenerate.CreateQrCode(connectData.Uri, QRCodeGenerator.ECCLevel.Q);
				var qrCodePng = new PngByteQRCode(qrCodeData);
				var bytes = qrCodePng.GetGraphic(20);
				var memoryStream = new MemoryStream(bytes);

				QRCode = Bitmap.DecodeToHeight(memoryStream, 300);

				await connectData.Approval;
				Connected = true;
				walletConnectService = new NethereumWalletConnectService(client);
				walletConnectConnectedSession = walletConnectService.GetWalletConnectConnectedSession();
				Address = walletConnectConnectedSession.Address;
				ChainId = walletConnectConnectedSession.ChainId;
			}

		}



		public async Task SignTypedDataAsync()
		{
			var web3 = new Web3();
			web3.Client.OverridingRequestInterceptor = new NethereumWalletConnectInterceptor(client);

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
