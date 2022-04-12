using System;
using System.Collections.Generic;

namespace Nethereum.Contracts.Standards.ERC20.TokenList
{
    public class Root
    {
        public string Name { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string> Keywords { get; set; }
        public Version Version { get; set; }
        public List<Token> Tokens { get; set; }

    }
}