let snarkjs = null;

export async function initialize(snarkjsUrl) {
    if (!snarkjsUrl) {
        throw new Error(
            'snarkjsUrl is required. Provide a local path (e.g. "./js/snarkjs.min.js") ' +
            'or a CDN URL (e.g. "https://cdn.jsdelivr.net/npm/snarkjs@latest/build/snarkjs.min.js"). ' +
            'To self-host: npm install snarkjs, then copy build/snarkjs.min.js to your wwwroot.');
    }

    if (window.snarkjs) {
        snarkjs = window.snarkjs;
        return;
    }

    await new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = snarkjsUrl;
        script.onload = () => {
            snarkjs = window.snarkjs;
            if (!snarkjs) {
                reject(new Error('snarkjs loaded but window.snarkjs is not defined'));
            } else {
                resolve();
            }
        };
        script.onerror = () => reject(new Error('Failed to load snarkjs from: ' + snarkjsUrl));
        document.head.appendChild(script);
    });
}

export async function fullProve(inputJson, wasmBase64, zkeyBase64) {
    if (!snarkjs) {
        throw new Error('snarkjs not initialized. Call initialize() first.');
    }

    const wasm = Uint8Array.from(atob(wasmBase64), c => c.charCodeAt(0));
    const zkey = Uint8Array.from(atob(zkeyBase64), c => c.charCodeAt(0));

    const { proof, publicSignals } = await snarkjs.groth16.fullProve(
        JSON.parse(inputJson), wasm, zkey);

    return JSON.stringify({ proof: JSON.stringify(proof), publicSignals: JSON.stringify(publicSignals) });
}
