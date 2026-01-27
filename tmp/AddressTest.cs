using System;
using Nethereum.Util;

class Test {
    static void Main() {
        // Test 1: Precompile 0x01 in different formats
        var precompile1 = "0x0000000000000000000000000000000000000001";
        var precompile1NoPrefix = "0000000000000000000000000000000000000001";
        var precompile1Short = "0x1";
        
        Console.WriteLine($"Test 1a: '{precompile1}'.IsTheSameAddress('{precompile1NoPrefix}') = {precompile1.IsTheSameAddress(precompile1NoPrefix)}");
        Console.WriteLine($"Test 1b: '{precompile1}'.IsTheSameAddress('{precompile1Short}') = {precompile1.IsTheSameAddress(precompile1Short)}");
        
        // Test 2: Different precompiles shouldn't match
        var precompile2 = "0x0000000000000000000000000000000000000002";
        Console.WriteLine($"Test 2: '{precompile1}'.IsTheSameAddress('{precompile2}') = {precompile1.IsTheSameAddress(precompile2)}");
        
        // Test 3: Normalized comparison
        var normalized = AddressUtil.Current.ConvertToValid20ByteAddress(precompile1NoPrefix).ToLower();
        Console.WriteLine($"Test 3: Normalized '{precompile1NoPrefix}' = '{normalized}'");
        Console.WriteLine($"Test 3b: '{normalized}'.IsTheSameAddress('{precompile1}') = {normalized.IsTheSameAddress(precompile1)}");
    }
}
