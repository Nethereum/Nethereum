using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.CoreProto;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.Models;

namespace Nethereum.Generators.ProtocolBuffers.ABIToProto.Templates
{
    public class ContractABIToProtoTemplate : ClassTemplateBase<ContractABIToProtoModel>
    {
        public string Namespace { get; }

        public ContractABIToProtoTemplate(ContractABIToProtoModel model, string @namespace) : base(model)
        {
            Namespace = @namespace;
            ClassFileTemplate = new StubClassFileTemplate(model, this);
        }

        private string JavaPackage => $"com.{Namespace?.ToLower() ?? "undefined.namespace"}";

        public virtual IEnumerable<string> GetProtoImports()
        {
            return new string[0];
        }

        public override string GenerateClass()
        {

            var header = $@"{SpaceUtils.NoTabs}syntax = ""proto3"";
{SpaceUtils.NoTabs}package {Namespace};
{CreateProtoImportDeclarations()}
{SpaceUtils.NoTabs}option csharp_namespace = ""{Namespace}"";
{SpaceUtils.NoTabs}option java_package = ""{JavaPackage}"";
{ SpaceUtils.NoTabs}option java_multiple_files = true;
{SpaceUtils.NoTabs}";

            var content = new StringBuilder(header);
            content.Append(Model.ConstructorGenerator.GenerateClass());
            foreach (var functionGenerator in Model.FunctionGenerators)
            {
                content.Append(functionGenerator.GenerateClass());
            }
            foreach (var eventGenerator in Model.EventGenerators)
            {
                content.Append(eventGenerator.GenerateClass());
            }
            return content.ToString();
        }

        private string CreateProtoImportDeclarations()
        {
            var imports = GetProtoImports();
            if (!imports.Any())
                return string.Empty;

            var stringBuilder = new StringBuilder();
            foreach (var import in imports)
            {
                stringBuilder.AppendLine($"{SpaceUtils.NoTabs}import \"{import}\";");
            }

            return stringBuilder.ToString();
        }
    }
}