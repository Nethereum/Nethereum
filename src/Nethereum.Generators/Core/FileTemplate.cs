using System;
using System.Linq;
using Nethereum.Generators.Core;

namespace Nethereum.Generators.CQS
{
    public abstract class FileTemplate
    {
        protected FileTemplate(IFileModel fileModel)
        {
            FileModel = fileModel;
        }

        public IFileModel FileModel { get; protected set; }

        public string GenerateNamespaceDependencies()
        {
            return string.Join(Environment.NewLine,
                FileModel.NamespaceDependencies.Distinct().Select(GenerateNamespaceDependency));
        }

        public abstract string GenerateNamespaceDependency(string namespaceName);
    }
}