using Nethereum.Generators.CQS;
using System;
using System.Diagnostics;
using System.IO;

namespace Nethereum.Generators.ProtocolBuffers.Net.ProtoToDotNet.CSharp
{
    public class ProtoToCSharpTemplate: ClassTemplateBase<ProtoToDotNetModel>
    {
        public ProtoToCSharpTemplate(ProtoToDotNetModel model):base(model)
        {
            ClassFileTemplate = new ProtoClassFileTemplate(model, this);
        }

        public string GetPathProtoC()
        {
            return @"C:\Users\info\.nuget\packages\google.protobuf.tools\3.5.1\tools\windows_x64\protoc";
        }

        public override string GenerateClass()
        {
            var protocPath = ProtoToDotNetConfiguration.PathToProtoC;

            var sourceProto = Path.GetFileName(Model.ProtoFileName);
            var protoPath = Path.GetDirectoryName(Model.ProtoFileName);
            var outputFolder = Path.GetTempPath();
            var cSharpFileExtension = ".g.cs";

            var processArgs = $"--proto_path={protoPath} --csharp_out={outputFolder} --csharp_opt=file_extension={cSharpFileExtension} {sourceProto}";

            if(protoPath.IndexOf(" ") > 0)
                throw new Exception("Spaces in the path to the source proto file is not supported");

            try
            {
                using (var process = new Process
                {
                    StartInfo = new ProcessStartInfo(protocPath, processArgs)
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                })
                {
                    process.Start();

                    var error = process.StandardError.ReadToEnd();

                    if (!string.IsNullOrEmpty(error))
                        throw new Exception(error);

                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"An error occurred calling protoc to generate csharp code from a protobuf file. Message:{ex.Message},  Protoc path:{protocPath}.  Protoc args: {processArgs}.", ex);
            }

            var generatedFilePath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(sourceProto) + cSharpFileExtension);

            var generatedContent = File.ReadAllText(generatedFilePath);
            File.Delete(generatedFilePath);
            return generatedContent;
        }

    }
}
