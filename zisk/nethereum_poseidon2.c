/* Thin C wrapper for Zisk poseidon2 precompile.
   Calls the CSR directly — avoids the DllImport thunk that triggers DMA memcpy. */

extern void zkvm_poseidon2(unsigned long *state);

void poseidon2_c(unsigned long *state) {
    zkvm_poseidon2(state);
}
