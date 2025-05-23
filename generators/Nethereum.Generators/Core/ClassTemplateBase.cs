using Nethereum.Generators.Core;

namespace Nethereum.Generators.CQS
{

    public abstract class ClassTemplateBase: IClassTemplate
    {
        protected ClassFileTemplate ClassFileTemplate { get; set; }

        protected IClassModel ClassModel { get; }

        public abstract string GenerateClass();

        protected ClassTemplateBase(IClassModel model)
        {
            ClassModel = model;
        }

        public string GenerateFullClass()
        {
            return ClassFileTemplate.GenerateFullClass();
        }
    }

}