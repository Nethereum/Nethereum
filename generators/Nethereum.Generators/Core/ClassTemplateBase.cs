using Nethereum.Generators.Core;

namespace Nethereum.Generators.CQS
{

    public abstract class ClassTemplateBase<TModel> : IClassTemplate
        where TModel: IClassModel
    {
        protected ClassFileTemplate ClassFileTemplate { get; set; }

        protected TModel Model { get; }

        public abstract string GenerateClass();

        protected ClassTemplateBase(TModel model)
        {
            Model = model;
        }

        public string GenerateFullClass()
        {
            return ClassFileTemplate.GenerateFullClass();
        }
    }
}