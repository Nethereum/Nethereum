.option norelax

.macro ZKVM_CSR_VOID name, csr
    .section .text, "ax", @progbits
    .balign 4
    .global \name
    .type \name, @function
\name:
    .cfi_startproc
    csrs    \csr, a0
    ret
    .cfi_endproc
    .size \name, .-\name
.endm

/* Nethereum precompile syscalls — excludes DMA (0x813, 0x814)
   which conflict with the Zisk transpiler's instruction sequence rules. */
ZKVM_CSR_VOID     zkvm_poseidon2,             0x812
