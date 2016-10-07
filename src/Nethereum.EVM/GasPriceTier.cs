using System;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.EVM
{
    //0, 2, 3, 5, 8, 10, 20, 0}
    public enum GasPriceTier
    {
	    ZeroTier = 0,   // 0, Zero
        BaseTier,       // 2, Quick
        VeryLowTier,    // 3, Fastest
        LowTier,        // 5, Fast
        MidTier,        // 8, Mid
        HighTier,       // 10, Slow
        ExtTier,        // 20, Ext
        SpecialTier,    // multiparam or otherwise special
        InvalidTier     // Invalid.
    }
}
