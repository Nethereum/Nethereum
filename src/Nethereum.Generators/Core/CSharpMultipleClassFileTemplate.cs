using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Generators.Core;

namespace Nethereum.Generators.CQS
{
    public class CSharpMultipleClassFileTemplate : MultipleClassFileTemplate
    {

        public CSharpMultipleClassFileTemplate(IEnumerable<IClassGenerator> classGenerators, IFileModel fileModel) : base(classGenerators, fileModel)
        {

        }

        public override string GenerateNamespaceDependency(string namespaceName)
        {
            return $@"{SpaceUtils.NoTabs}using {namespaceName};";
        }

        public override string GenerateFile()
        {
            return
                $@"{GenerateNamespaceDependencies()}
{SpaceUtils.NoTabs}
{SpaceUtils.NoTabs}namespace {FileModel.Namespace}
{SpaceUtils.NoTabs}{{
{GenerateAll()}
{SpaceUtils.NoTabs}}}
";
        }

        protected string GenerateAll()
        {
            return string.Join($"{Environment.NewLine}{Environment.NewLine}", ClassGenerators.Select(x => x.GenerateClass()));
        }
    }

}