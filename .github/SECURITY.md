# Security Policy

## Supported Versions

We provide security updates for the following versions of Nethereum:

| Version | Supported          |
| ------- | ------------------ |
| 5.8.x   | :white_check_mark: |
| < 5.8   | :x:                |

## Reporting a Vulnerability

If you've discovered a security vulnerability in Nethereum, we appreciate your help in disclosing it to us in a responsible manner.

**Please do not open a public issue for security vulnerabilities.**

Instead, please report vulnerabilities by:
- Opening a draft security advisory on GitHub under the "Security" tab.
- Or by emailing the core maintainer at [juanfranblanco@hotmail.com](mailto:juanfranblanco@hotmail.com).

Please include the following information in your report:
- A description of the vulnerability.
- Instructions on how to reproduce the issue.
- Potential impact of the vulnerability.

We will acknowledge your report within 48 hours and work with you to analyze and address the issue.

## Security Best Practices for Developers

When using Nethereum to handle sensitive key material:
1. **Memory Management**: Use `using` blocks or manually call `Dispose()` on `EthECKey` instances (available from version 5.8.x) to ensure that temporary byte buffers are cleared from memory.
2. **Buffer Safety**: When passing private keys as `byte[]`, ensure you clear the source buffers using `Array.Clear()` or `CryptographicOperations.ZeroMemory()` after the operations are complete.
3. **Key Storage**: Use hardware wallets (Ledger/Trezor) or secure enclaves (AWS KMS/Azure Key Vault) for high-value production environments rather than storing raw private keys in application memory.
