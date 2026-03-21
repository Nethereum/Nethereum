import { execSync } from 'child_process';
import { writeFileSync, readFileSync, mkdirSync, rmSync } from 'fs';
import { join } from 'path';
import { tmpdir } from 'os';
import { randomBytes } from 'crypto';
import { fileURLToPath } from 'url';

export async function fullProve(inputSignals, wasmPath, zkeyPath) {
  const tempDir = join(tmpdir(), `snarkjs_${randomBytes(4).toString('hex')}`);
  mkdirSync(tempDir, { recursive: true });

  const inputPath = join(tempDir, 'input.json');
  const proofPath = join(tempDir, 'proof.json');
  const publicPath = join(tempDir, 'public.json');

  try {
    writeFileSync(inputPath, JSON.stringify(inputSignals));

    const scriptsDir = fileURLToPath(new URL('.', import.meta.url));
    const snarkjsCli = join(scriptsDir, 'node_modules', 'snarkjs', 'cli.js');

    execSync(
      `node "${snarkjsCli}" groth16 fullprove "${inputPath}" "${wasmPath}" "${zkeyPath}" "${proofPath}" "${publicPath}"`,
      { timeout: 120_000, stdio: ['pipe', 'pipe', 'pipe'] }
    );

    const proof = JSON.parse(readFileSync(proofPath, 'utf8'));
    const publicSignals = JSON.parse(readFileSync(publicPath, 'utf8'));
    return { proof, publicSignals };
  } finally {
    try { rmSync(tempDir, { recursive: true }); } catch {}
  }
}
