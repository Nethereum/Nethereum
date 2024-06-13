using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using System;
using System.Diagnostics;

namespace Nethereum.Generators.MudService
{
    public class MudServiceFSharpTemplate : ClassTemplateBase<MudServiceModel>
    {
        public MudServiceFSharpTemplate(MudServiceModel model) : base(model)
        {
            
            ClassFileTemplate = new FSharpClassFileTemplate(Model, this);
        }
        public override string GenerateClass()
        {
            return string.Empty;
           
//            return
//                $@"
//{SpaceUtils.One__Tab}type {Model.GetTypeName()} (web3: Web3, contractAddress: string) =
//{SpaceUtils.One__Tab}

//{SpaceUtils.One__Tab}

//{SpaceUtils.One__Tab}

//{SpaceUtils.One__Tab}";

        }
    }
}