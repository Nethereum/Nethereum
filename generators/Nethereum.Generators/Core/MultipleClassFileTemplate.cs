using System.Collections.Generic;
using Nethereum.Generators.Core;

namespace Nethereum.Generators.CQS
{
    public abstract class MultipleClassFileTemplate:FileTemplate
    {
        protected IEnumerable<IClassGenerator> ClassGenerators { get; }

        public MultipleClassFileTemplate(IEnumerable<IClassGenerator> classGenerators, IFileModel fileModel):base(fileModel)
        {
            ClassGenerators = classGenerators;
        }

        public abstract string GenerateFile();
    }
}