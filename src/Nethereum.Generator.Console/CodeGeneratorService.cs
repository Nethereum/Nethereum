using RazorLight;

namespace Nethereum.Generator.Console
{
    public class CodeGeneratorService
    {
        public static string GenerateFile(string templateName, object model, string fileNameOuput )
        {
            var engine = EngineFactory.CreateEmbedded(typeof(CodeGeneratorService));
            //Note: pass the name of the view without extension
            var result = engine.Parse(templateName, model);

            using (var fileOutput = System.IO.File.CreateText(fileNameOuput))
            {
                fileOutput.Write(result);
                fileOutput.Flush();
            }
            return fileNameOuput;
        }
    }
}