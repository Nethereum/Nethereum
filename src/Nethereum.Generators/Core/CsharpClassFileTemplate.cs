using Nethereum.Generators.Core;

namespace Nethereum.Generators.CQS
{
    public class CsharpClassFileTemplate:ClassFileTemplate
    {

        public CsharpClassFileTemplate(IClassModel classModel, IClassTemplate classTemplate):base(classModel, classTemplate)
        {
         
        }

        public override string GenerateNamespaceDependency(string namespaceName)
        {
            return $@"{SpaceUtils.NoTabs}using {namespaceName};";
        }

        public override string GenerateFullClass()
        {
            return
                $@"{GenerateNamespaceDependencies()}
{SpaceUtils.NoTabs}namespace {ClassModel.Namespace}
{SpaceUtils.NoTabs}{{
{SpaceUtils.NoTabs}{ClassTemplate.GenerateClass()}
{SpaceUtils.NoTabs}}}
";
        }
    }
}