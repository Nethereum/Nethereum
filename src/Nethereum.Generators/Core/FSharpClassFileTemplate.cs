using Nethereum.Generators.Core;

namespace Nethereum.Generators.CQS
{
    public class FSharpClassFileTemplate : ClassFileTemplate
    {
        public FSharpClassFileTemplate(IClassModel classModel, IClassTemplate classTemplate) : base(classModel, classTemplate)
        {

        }

        public override string GenerateNamespaceDependency(string namespaceName)
        {
            return $@"{SpaceUtils.NoTabs}open {namespaceName}";
        }

        public override string GenerateFullClass()
        {
            return
                $@"{SpaceUtils.NoTabs}namespace {ClassModel.Namespace}
{SpaceUtils.NoTabs}
{GenerateNamespaceDependencies()}
{SpaceUtils.NoTabs}
{SpaceUtils.NoTabs}{ClassTemplate.GenerateClass()}
{SpaceUtils.NoTabs}
";
        }
    }
}