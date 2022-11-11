using System.Collections.Generic;
using Nethereum.Generators.Core;

namespace Nethereum.Generators.CQS
{
    public abstract class ClassFileTemplate:FileTemplate
    {
        public IClassModel ClassModel { get; }
        public IClassTemplate ClassTemplate { get; }

        protected ClassFileTemplate(IClassModel classModel, IClassTemplate classTemplate):base(classModel)
        {
            ClassModel = classModel;
            ClassTemplate = classTemplate;
        }  

        public abstract string GenerateFullClass();
       
    }
}