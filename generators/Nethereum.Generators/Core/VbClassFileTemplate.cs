using Nethereum.Generators.Core;

namespace Nethereum.Generators.CQS
{
    public class VbClassFileTemplate : ClassFileTemplate
    {

        public VbClassFileTemplate(IClassModel classModel, IClassTemplate classTemplate) : base(classModel, classTemplate)
        {

        }

        public override string GenerateNamespaceDependency(string namespaceName)
        {
            return $@"{SpaceUtils.NoTabs}Imports {namespaceName}";
        }

        public override string GenerateFullClass()
        {
            return
                $@"{GenerateNamespaceDependencies()}
{SpaceUtils.NoTabs}Namespace {ClassModel.Namespace}
{SpaceUtils.NoTabs}
{SpaceUtils.NoTabs}{ClassTemplate.GenerateClass()}
{SpaceUtils.NoTabs}
{SpaceUtils.NoTabs}End Namespace
";
        }
    }
}