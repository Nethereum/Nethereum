using System.IO;
using System.Text;

namespace Nethereum.Generators.Tests.Common.TestData
{
    public static class TestEnvironment
    {
        public static string TempPath = Path.Combine(
            Path.GetTempPath(), 
            typeof(TestEnvironment).Assembly.GetName(false).Name);

        public static string WriteFileToFolder(string folder, string fileName, string fileContent)
        {
            var fullPath = Path.Combine(folder, fileName);
            var outputFolder = Path.GetDirectoryName(fullPath);

            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            File.WriteAllText(fullPath, fileContent, Encoding.UTF8);
            return fullPath;
        }
    }
}