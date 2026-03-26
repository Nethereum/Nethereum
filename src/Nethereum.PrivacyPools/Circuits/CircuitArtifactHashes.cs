using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Nethereum.PrivacyPools
{
    public static class CircuitArtifactHashes
    {
        // SHA-256 hex digests for Privacy Pools circuit artifacts.
        // Matches the 0xbow SDK v1.2.0 artifact hash manifest, with compatibility
        // aliases for the legacy Nethereum file names.
        public static IReadOnlyDictionary<string, string> Default { get; } =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["commitment.wasm"] = "254d2130607182fd6fd1aee67971526b13cfe178c88e360da96dce92663828d8",
            ["commitment.vkey"] = "7d48b4eb3dedc12fb774348287b587f0c18c3c7254cd60e9cf0f8b3636a570d8",
            ["commitment_vk.json"] = "7d48b4eb3dedc12fb774348287b587f0c18c3c7254cd60e9cf0f8b3636a570d8",
            ["commitment.zkey"] = "494ae92d64098fda2a5649690ddc5821fcd7449ca5fe8ef99ee7447544d7e1f3",
            ["withdraw.wasm"] = "36cda22791def3d520a55c0fc808369cd5849532a75fab65686e666ed3d55c10",
            ["withdrawal.wasm"] = "36cda22791def3d520a55c0fc808369cd5849532a75fab65686e666ed3d55c10",
            ["withdraw.vkey"] = "666bd0983b20c1611543b04f7712e067fbe8cad69f07ada8a310837ff398d21e",
            ["withdrawal_vk.json"] = "666bd0983b20c1611543b04f7712e067fbe8cad69f07ada8a310837ff398d21e",
            ["withdraw.zkey"] = "2a893b42174c813566e5c40c715a8b90cd49fc4ecf384e3a6024158c3d6de677",
            ["withdrawal.zkey"] = "2a893b42174c813566e5c40c715a8b90cd49fc4ecf384e3a6024158c3d6de677",
        });
    }
}
