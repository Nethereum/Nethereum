// Browser side of Nethereum.WebAuthn.Blazor: thin wrappers over navigator.credentials
// (WebAuthn Level 2, https://www.w3.org/TR/webauthn-2/) that marshal to/from base64/base64url
// strings, since Blazor JS interop cannot pass ArrayBuffer/byte[] cleanly across all render modes.

function base64UrlToBytes(base64Url) {
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64 + '=='.slice(0, (4 - (base64.length % 4)) % 4);
    const binary = atob(padded);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }
    return bytes;
}

function bytesToBase64(bytes) {
    let binary = '';
    const view = new Uint8Array(bytes);
    for (let i = 0; i < view.length; i++) {
        binary += String.fromCharCode(view[i]);
    }
    return btoa(binary);
}

function bytesToBase64Url(bytes) {
    return bytesToBase64(bytes).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function utf8BytesToString(bytes) {
    return new TextDecoder('utf-8').decode(bytes);
}

// optionsJson: { rpId, rpName, userName, userIdB64Url, challengeB64Url, requireUserVerification }
export async function createCredential(optionsJson) {
    const options = JSON.parse(optionsJson);

    const publicKey = {
        rp: { id: options.rpId, name: options.rpName },
        user: {
            id: base64UrlToBytes(options.userIdB64Url),
            name: options.userName,
            displayName: options.userName
        },
        challenge: base64UrlToBytes(options.challengeB64Url),
        pubKeyCredParams: [{ type: 'public-key', alg: -7 }], // ES256 (COSE alg -7), the only algorithm the on-chain WebAuthnValidator verifies
        authenticatorSelection: {
            userVerification: options.requireUserVerification ? 'required' : 'preferred'
        }
    };

    const credential = await navigator.credentials.create({ publicKey });
    const attResp = credential.response;

    const publicKeySpki = attResp.getPublicKey ? attResp.getPublicKey() : null;
    const authenticatorData = attResp.getAuthenticatorData ? attResp.getAuthenticatorData() : null;

    return JSON.stringify({
        rawId: bytesToBase64Url(new Uint8Array(credential.rawId)),
        publicKeySpki: publicKeySpki ? bytesToBase64(new Uint8Array(publicKeySpki)) : null,
        authenticatorData: authenticatorData ? bytesToBase64(new Uint8Array(authenticatorData)) : null,
        attestationObject: bytesToBase64(new Uint8Array(attResp.attestationObject))
    });
}

export async function getAssertion(challengeB64Url, credentialIdB64Url, rpId, userVerification) {
    const publicKey = {
        challenge: base64UrlToBytes(challengeB64Url),
        allowCredentials: [{ type: 'public-key', id: base64UrlToBytes(credentialIdB64Url) }],
        rpId: rpId,
        userVerification: userVerification ? 'required' : 'preferred'
    };

    const assertion = await navigator.credentials.get({ publicKey });
    const response = assertion.response;

    return JSON.stringify({
        authenticatorData: bytesToBase64(new Uint8Array(response.authenticatorData)),
        clientDataJSON: utf8BytesToString(new Uint8Array(response.clientDataJSON)),
        signature: bytesToBase64(new Uint8Array(response.signature)),
        userHandle: response.userHandle ? bytesToBase64(new Uint8Array(response.userHandle)) : null
    });
}
