using System.Threading.Tasks;
using RazorLight;

namespace Nethereum.Generator.Console
{
    public class CodeGeneratorService
    {
        public static async Task<string> GenerateFileAsync(string templateName, object model, string fileNameOuput )
        {
             var engine = new RazorLightEngineBuilder()
                 .UseEmbeddedResourcesProject(typeof(Program))
                 .UseMemoryCachingProvider()
                 .Build();
            //Note: pass the name of the view without extension
            var result = await engine.CompileRenderAsync(templateName, model);

            using (var fileOutput = System.IO.File.CreateText(fileNameOuput))
            {
                fileOutput.Write(result);
                fileOutput.Flush();
            }
            return fileNameOuput;
        }
    }
}