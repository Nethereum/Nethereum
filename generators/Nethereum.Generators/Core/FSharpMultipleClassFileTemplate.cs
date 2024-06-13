using System.Collections.Generic;
using Nethereum.Generators.Core;

namespace Nethereum.Generators.CQS
{
    public class FSharpMultipleClassFileTemplate : MultipleClassFileTemplate
    {
        public FSharpMultipleClassFileTemplate(IEnumerable<IClassGenerator> classGenerators, IFileModel fileModel) : base(classGenerators, fileModel)
        {

        }

        public override string GenerateNamespaceDependency(string namespaceName)
        {
            return $@"{SpaceUtils.NoTabs}open {namespaceName}";
        }

        public override string GenerateFile()
        {
            return
                $@"{SpaceUtils.NoTabs}namespace {FileModel.Namespace}
{SpaceUtils.NoTabs}
{GenerateNamespaceDependencies()}
{SpaceUtils.NoTabs}
{SpaceUtils.NoTabs}{GenerateAll()}
{SpaceUtils.NoTabs}
";
        }

        protected string GenerateAll()
        {
            var result = "";
            foreach (var classGenerator in ClassGenerators)
            {
                result = result +
                         $@"{SpaceUtils.One__Tab}
{SpaceUtils.One__Tab}
{classGenerator.GenerateClass()}";
            }

            return result;
        }
    }
}