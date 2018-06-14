using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.ProtocolBuffers.Net.ProtoToDotNet.CSharp;
using System;

namespace Nethereum.Generators.ProtocolBuffers.Net.ProtoToDotNet
{
    public class ProtoToDotNetGenerator: ClassGeneratorBase<ClassTemplateBase<ProtoToDotNetModel>, ProtoToDotNetModel>
    {
        public ProtoToDotNetGenerator(string inputProtoFile, string name, CodeGenLanguage language)
        {
            ClassModel = new ProtoToDotNetModel(inputProtoFile, name, language);

            if (language != CodeGenLanguage.CSharp)
                throw new ArgumentException("Invalid language, only CSharp is supported currently");

            ClassTemplate = new ProtoToCSharpTemplate(ClassModel);
        }
    }
}
