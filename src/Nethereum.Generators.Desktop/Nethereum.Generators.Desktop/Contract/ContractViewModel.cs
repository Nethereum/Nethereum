namespace Nethereum.Generators.Desktop
{
    public class ContractViewModel : ViewModelBase
    {
        private string abi;

        public string Abi
        {
            get { return abi; }
            set
            {
                if (value != abi)
                {
                    abi = value;
                    OnPropertyChanged();
                }
            }
        }

        private string byteCode;

        public string ByteCode
        {
            get { return byteCode; }
            set
            {
                if (value != byteCode)
                {
                    byteCode = value;
                    OnPropertyChanged();
                }
            }
        }

        private string contractName;

        public string ContractName
        {
            get { return contractName; }
            set
            {
                if (value != contractName)
                {
                    contractName = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}