using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace Nethereum.Generators.Nuget.Test.EIP20v2.DTO
{
    [Event("Log")]
    public class LogEventDTO
    {
        [Parameter("uint8", "level", 1, true )]
        public byte Level {get; set;}
        [Parameter("string", "message", 2, false )]
        public string Message {get; set;}
    }
}
