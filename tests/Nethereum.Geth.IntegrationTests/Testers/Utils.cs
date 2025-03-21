using System.Collections.Generic;
using System.Linq;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.Geth.RPC.Debug.Tracers;
using Nethereum.Hex.HexTypes;

namespace Nethereum.Geth.IntegrationTests.Testers;

public class Utils
{
    public static string GetCustomTracerCode()
    {
        return
            "retVal: []," +
            "step: function(log,db) {this.retVal.push(log.getPC() + \":\" + log.op.toString())}," +
            "fault: function(log,db) {this.retVal.push(\"FAULT: \" + JSON.stringify(log))}," +
            "result: function(ctx,db) {return this.retVal}";

    }

    public static TracingCallOptions GetBaseTracingCallConfig()
    {
        return new TracingCallOptions()
        {
            BlockOverridesDto = new BlockOverridesDto()
            {
                FeeRecipient = "0xdEad000000000000000000000000000000000000",
                GasLimit = new HexBigInteger(new System.Numerics.BigInteger(100000000))
            },
            StateOverrides = new Dictionary<string, StateOverrideDto>()
            {
                {
                    "0xDeaD00000000000000000000000000000000BEEf", new StateOverrideDto()
                    {
                        Balance = new HexBigInteger("0xde0b6b3a7640000")
                    }
                },
            },
            TxIndex = new HexBigInteger(new System.Numerics.BigInteger(50)),
            Timeout = "1m",
            Reexec = 128,
        };
    }
    
    public static IEnumerable<object[]> GetBooleanCombinations(int count)
    {
        int totalCombinations = 1 << count; // 2^count

        for (int i = 0; i < totalCombinations; i++)
        {
            var combination = new bool[count];
            for (int j = 0; j < count; j++)
            {
                combination[j] = (i & (1 << j)) != 0;
            }
            yield return combination.Cast<object>().ToArray();
        }
    }
}