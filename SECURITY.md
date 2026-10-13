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

## A Dependency of This Library Is Blocking Your Build

A vulnerability in something SharpConsoleUI depends on is not a vulnerability in
SharpConsoleUI, and it needs no private report. If a transitive dependency is failing your
restore or your audit, **open a public issue here**. Say which advisory, which version of
SharpConsoleUI you are on, and whether you are blocked or have worked around it.

This is worth doing even when you have already suppressed the advisory locally and are waiting
for a release. The maintainer does not see issues opened in your repository, and a fix may
already exist, or may be waiting on nothing more than knowing that somebody needs it.

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
