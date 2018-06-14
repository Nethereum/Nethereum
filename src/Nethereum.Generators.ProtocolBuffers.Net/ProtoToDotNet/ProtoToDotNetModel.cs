using Nethereum.Generators.Core;

namespace Nethereum.Generators.ProtocolBuffers.Net.ProtoToDotNet
{
    public class ProtoToDotNetModel : TypeMessageModel
    {
        public string ProtoFileName { get; }

        public ProtoToDotNetModel(string protoFileName, string name, CodeGenLanguage language) : base(
            "", name, "")
        {
            ProtoFileName = protoFileName;
            CodeGenLanguage = language;
        }
    }
}