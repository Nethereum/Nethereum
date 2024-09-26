using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.EncodingDecoding;
using System.Numerics;


namespace Nethereum.Mud.TableRepository
{

    public class StoredRecord: EncodedValues
    {
        public int RowId { get; set; }
        public byte[] TableIdBytes { get; set; }
        public byte[] KeyBytes { get; set; }
        public byte[] Key0Bytes { get; set; }
        public byte[] Key1Bytes { get; set; }
        public byte[] Key2Bytes { get; set; }
        public byte[] Key3Bytes { get; set; }
        public byte[] AddressBytes { get; set; }

        public BigInteger? BlockNumber { get; set; }
        public int? LogIndex { get; set; }
        public bool IsDeleted { get; set; } = false;

        // Cached hex strings
        private string _tableIdHex;
        private string _keyHex;
        private string _key0Hex;
        private string _key1Hex;
        private string _key2Hex;
        private string _key3Hex;
        private string _addressHex;

        // Hex string properties for transport (exposed via getter)
        public string TableId
        {
            get => _tableIdHex ??= TableIdBytes?.ToHex(true);
            set
            {
                TableIdBytes = value.HexToByteArray();
                _tableIdHex = value;
            }
        }

        public string Key
        {
            get => _keyHex ??= KeyBytes?.ToHex(true);
            set
            {
                KeyBytes = value.HexToByteArray();
                _keyHex = value;
            }
        }

        public string Key0
        {
            get => _key0Hex ??= Key0Bytes?.ToHex(true);
            set
            {
                Key0Bytes = value.HexToByteArray();
                _key0Hex = value;
            }
        }

        public string Key1
        {
            get => _key1Hex ??= Key1Bytes?.ToHex(true);
            set
            {
                Key1Bytes = value.HexToByteArray();
                _key1Hex = value;
            }
        }

        public string Key2
        {
            get => _key2Hex ??= Key2Bytes?.ToHex(true);
            set
            {
                Key2Bytes = value.HexToByteArray();
                _key2Hex = value;
            }
        }

        public string Key3
        {
            get => _key3Hex ??= Key3Bytes?.ToHex(true);
            set
            {
                Key3Bytes = value.HexToByteArray();
                _key3Hex = value;
            }
        }

        public string Address
        {
            get => _addressHex ??= AddressBytes?.ToHex(true);
            set
            {
                AddressBytes = value.HexToByteArray();
                _addressHex = value;
            }
        }

        private string _staticDataHex;
        private string _encodedLengthsHex;
        private string _dynamicDataHex;

        public string StaticDataHex
        {
            get => _staticDataHex ??= StaticData?.ToHex(true);
            set
            {
                StaticData = value.HexToByteArray();
                _staticDataHex = value;
            }
        }

        public string EncodedLengthsHex
        {
            get => _encodedLengthsHex ??= EncodedLengths?.ToHex(true);
            set
            {
                EncodedLengths = value.HexToByteArray();
                _encodedLengthsHex = value;
            }
        }

        public string DynamicDataHex
        {
            get => _dynamicDataHex ??= DynamicData?.ToHex(true);
            set
            {
                DynamicData = value.HexToByteArray();
                _dynamicDataHex = value;
            }
        }
    }

}
