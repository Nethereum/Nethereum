using System;
using System.Linq;
using Nethereum.Generators.Core;

namespace Nethereum.Generators.CQS
{
    public abstract class ClassFileTemplate
    {
        public IClassModel ClassModel { get; }
        public IClassTemplate ClassTemplate { get; }

        protected ClassFileTemplate(IClassModel classModel, IClassTemplate classTemplate)
        {
            ClassModel = classModel;
            ClassTemplate = classTemplate;
        }
        public string GenerateNamespaceDependencies()
        {
            return string.Join(Environment.NewLine,
                ClassModel.NamespaceDependencies.Distinct().Select(GenerateNamespaceDependency));
        }

        public abstract string GenerateNamespaceDependency(string namespaceName);

        public abstract string GenerateFullClass();
       
    }
}