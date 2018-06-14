namespace Nethereum.Generators.ProtocolBuffers.ABIToProto.CoreProto
{
    public interface ISolidityToProtoBufTypeConverter
    {
        string Convert(string abiType);
    }
}