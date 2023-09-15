using Nethereum.ABI.EIP712;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.WalletConnect;
using Nethereum.WalletConnect.Requests;
using Nethereum.Web3;
using QRCoder;
using System.Net.NetworkInformation;
using WalletConnectSharp.Core;
using WalletConnectSharp.Core.Models.Pairing;
using WalletConnectSharp.Sign;
using WalletConnectSharp.Sign.Models;
using WalletConnectSharp.Sign.Models.Engine;


namespace WCNethereum
{


    public class SimpleExample
    {
        public string Name
        {
            get { return "simple_example"; }
        }

        public async Task Execute(params string[] args)
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

            var client = await WalletConnectSignClient.Init(options);

           
            var connectData = await client.Connect(NethereumWalletConnectService.GetDefaultConnectOptions());

            Console.WriteLine(connectData.Uri);
            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(connectData.Uri, QRCodeGenerator.ECCLevel.Q);
            PngByteQRCode qRCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeBytes = qRCode.GetGraphic(20);
            File.WriteAllBytes("qr.png", qrCodeBytes);

            var session = await connectData.Approval;

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


            try
            {
                var response = await web3.Eth.AccountSigning.SignTypedDataV4.SendRequestAsync(typedData.ToJson());
                var recoveredAccount = new Eip712TypedDataSigner().RecoverFromSignatureV4(typedData, response);
                Console.WriteLine(recoveredAccount);
            }
            catch (Exception ex)
            {
                var x = ex;
            }

          

            while (true)
            {
                await Task.Delay(2000);
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




  

