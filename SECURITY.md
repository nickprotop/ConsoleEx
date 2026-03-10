# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| Latest  | Yes       |

## Reporting a Vulnerability

If you discover a security vulnerability in SharpConsoleUI, please report it responsibly.

**Do NOT open a public GitHub issue for security vulnerabilities.**

Instead, please email: **nikolaos.protopapas@gmail.com**

Include:
- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested fix (if any)

## Response Timeline

- **Acknowledgment**: Within 48 hours
- **Initial assessment**: Within 1 week
- **Fix or mitigation**: Depends on severity, but we aim for prompt resolution

## Scope

SharpConsoleUI is a console UI rendering library. Security concerns may include:
- Input handling vulnerabilities (escape sequence injection)
- Resource exhaustion through malformed input
- Unsafe file operations in controls that handle file paths

## Disclosure

We follow coordinated disclosure. Once a fix is available, we will:
1. Release a patched version
2. Credit the reporter (unless anonymity is requested)
3. Publish a brief advisory if the issue is significant
