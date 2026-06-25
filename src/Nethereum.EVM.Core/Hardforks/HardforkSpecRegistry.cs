namespace Nethereum.EVM.Hardforks
{
    /// <summary>
    /// Canonical registry of every <see cref="HardforkSpec"/> in mainnet
    /// activation order. Used by validation tests to enumerate all forks
    /// without reflection (Zisk-compatible — plain array of static
    /// readonly references).
    ///
    /// <para>Adding a new fork:</para>
    /// <list type="number">
    ///   <item>Create <c>NewForkSpec.cs</c> next to the others.</item>
    ///   <item>Add <c>NewForkSpec.Instance</c> to <see cref="All"/>.</item>
    ///   <item>Add the consumer accessor (e.g. <c>NewFork =&gt; FromSpec(NewForkSpec.Instance)</c>)
    ///   to <see cref="HardforkConfig"/>.</item>
    /// </list>
    ///
    /// <para>The spec-completeness test iterates <see cref="All"/> and
    /// asserts every required field is non-null on every spec. Because
    /// every property is <c>required</c>, the compiler already catches
    /// missing fields at the construction site; this test catches the
    /// pathological case of <c>null!</c> overrides or numeric invariant
    /// violations.</para>
    /// </summary>
    public static class HardforkSpecRegistry
    {
        /// <summary>
        /// Every Ethereum mainnet hardfork in chronological order.
        /// </summary>
        public static readonly HardforkSpec[] All =
        {
            FrontierSpec.Instance,
            HomesteadSpec.Instance,
            TangerineWhistleSpec.Instance,
            SpuriousDragonSpec.Instance,
            ByzantiumSpec.Instance,
            ConstantinopleSpec.Instance,
            PetersburgSpec.Instance,
            IstanbulSpec.Instance,
            BerlinSpec.Instance,
            LondonSpec.Instance,
            ParisSpec.Instance,
            ShanghaiSpec.Instance,
            CancunSpec.Instance,
            PragueSpec.Instance,
            OsakaSpec.Instance,
            OsakaBpo1Spec.Instance,
        };
    }
}
