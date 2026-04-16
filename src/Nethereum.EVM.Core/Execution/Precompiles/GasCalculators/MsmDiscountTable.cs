namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    /// <summary>
    /// EIP-2537 multi-scalar-multiplication discount table, indexed by
    /// <c>k - 1</c>, in permille (values divided by 1000 when applied).
    /// Used by both <c>BLS12_G1MSM</c> (0x0c) and <c>BLS12_G2MSM</c> (0x0e)
    /// via <see cref="Bls12MsmGasCalculator"/>. Taken verbatim from
    /// EIP-2537 and matches the table previously duplicated inside the
    /// legacy <c>EvmPreCompiledContractsExecution</c> and the Prague
    /// precompile gas schedule.
    ///
    /// For <c>k &gt; Length</c>, callers must clamp to
    /// <c>Discount[Length - 1]</c> (the last entry, 525).
    /// </summary>
    public static class MsmDiscountTable
    {
        public static readonly int[] Discount = new int[]
        {
            1000, 1000, 923, 884, 855, 832, 812, 796, 782, 770,
            759, 749, 740, 732, 724, 717, 711, 704, 699, 693,
            688, 683, 679, 674, 670, 666, 663, 659, 655, 652,
            649, 646, 643, 640, 637, 634, 632, 629, 627, 624,
            622, 620, 618, 615, 613, 611, 609, 607, 606, 604,
            602, 600, 598, 597, 595, 593, 592, 590, 589, 587,
            586, 584, 583, 582, 580, 579, 578, 576, 575, 574,
            573, 571, 570, 569, 568, 567, 566, 565, 564, 563,
            562, 561, 560, 559, 558, 557, 556, 555, 554, 553,
            552, 551, 550, 549, 548, 547, 547, 546, 545, 544,
            543, 543, 542, 541, 540, 540, 539, 538, 537, 537,
            536, 535, 535, 534, 533, 533, 532, 531, 531, 530,
            530, 529, 528, 528, 527, 527, 526, 526, 525, 525
        };
    }
}
