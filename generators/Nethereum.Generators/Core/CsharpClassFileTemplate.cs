using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Generators.Core;

namespace Nethereum.Generators.CQS
{
    public class CSharpClassFileTemplate:ClassFileTemplate
    {

        public CSharpClassFileTemplate(IClassModel classModel, IClassTemplate classTemplate):base(classModel, classTemplate)
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
{SpaceUtils.NoTabs}
{SpaceUtils.NoTabs}namespace {ClassModel.Namespace}
{SpaceUtils.NoTabs}{{
{SpaceUtils.NoTabs}{ClassTemplate.GenerateClass()}
{SpaceUtils.NoTabs}}}
";
        }
    }

}