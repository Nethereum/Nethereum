using Nethereum.Generators.Core;

namespace Nethereum.Generators.CQS
{
    public class RazorClassFileTemplate : ClassFileTemplate
    {
        public RazorClassFileTemplate(IClassModel classModel, IClassTemplate classTemplate) : base(classModel, classTemplate)
        {

        }
        public override string GenerateNamespaceDependency(string namespaceName)
        {
            return $@"@using {namespaceName}";
        }
        public override string GenerateFullClass()
        {
            return
                $@"{GenerateNamespaceDependencies()}
{SpaceUtils.NoTabs}
{SpaceUtils.NoTabs}{ClassTemplate.GenerateClass()}
{SpaceUtils.NoTabs}
";
        }
    }

}