# Security Policy

## Supported Versions

Security updates are provided for the latest minor release series only.

| Version | Supported |
|---------|-----------|
| 0.3.x   | ✓         |
| < 0.3   | ✗         |

## Threat Model

bimwright runs on `127.0.0.1` only. The attack surface is:

- Local processes that can read the discovery file (`%LOCALAPPDATA%\Bimwright\portR22.txt` / `pipeR27.txt`, etc.)
- Local processes that can connect to the TCP port or Named Pipe
- Code executed via `send_code_to_revit` or materialized by the ToolBaker engine

## Mitigations in place

### Per-session token authentication
- Each Revit session generates a 32-byte cryptographic random token.
- Token is persisted alongside port/pipe info in the discovery file.
- Every request must include the valid token — otherwise rejected.
- Constant-time string comparison prevents timing attacks.

### Discovery file ACL
- Discovery files are ACL-restricted to the current Windows user (best-effort).
- Disables inheritance, grants `FullControl` only to the current SID.
- Falls back to token-only defense if ACL fails (logged via Debug output).

### Input validation
- `--http` port validated: 1–65535, numeric only.
- `--target` validated: one of `R22`, `R23`, `R24`, `R25`, `R26`, `R27`.
- Handler parameters validated via `SchemaValidator` before dispatch.
- TCP line size limit: 1 MiB per message.
- Rate limiting: 20 requests per 10 seconds on socket.

### Secret masking
- `SecretMasker` redacts API keys, Bearer tokens, passwords in log output.
- Patterns: `sk-*`, `Bearer *`, `authorization:`, `api_key=`, `password=`.
- `ErrorSanitizer` strips Windows/UNC absolute paths from errors sent to the model — filenames preserved.

### Network binding
- TCP listener: `127.0.0.1` only (not `0.0.0.0`).
- Named Pipe: local machine scope.
- HTTP SSE path: `127.0.0.1` only, middleware rejects non-localhost `Host` headers.
- Any non-localhost plugin bind requires explicit `BIMWRIGHT_ALLOW_LAN_BIND=1` opt-in.

### Dynamic code paths (`send_code_to_revit`, ToolBaker)
- `send_code_to_revit` is Release-capable only after explicit adaptive-bake opt-in visible to the Revit plugin: set `BIMWRIGHT_ENABLE_ADAPTIVE_BAKE=1` in the plugin process environment, or set `enableAdaptiveBake` to `true` in `%LOCALAPPDATA%\Bimwright\bimwright.config.json`.
- Each `send_code_to_revit` invocation still requires the existing per-call Revit TaskDialog confirmation before code executes.
- ToolBaker bakes require user approval per tool + operate under the host Revit process trust boundary. Production hardening, including signed-bake verification, remains tracked as v1.0 hardening work.

## Reporting a vulnerability

**Please do not open a public GitHub issue for security-sensitive reports.**

Use one of these private channels:

1. **GitHub private vulnerability report** — go to [the Security tab](https://github.com/bimwright/rvt-mcp/security/advisories/new) and submit a new advisory draft. This is the preferred path.
2. **Email the maintainer** — contact via the address on the commit history.

Include:
- Version (server + plugin) and Revit year.
- Reproduction steps.
- Impact assessment (local vs remote, auth required, user interaction).

Do not publish proof-of-concept exploits in public channels until a fix has shipped.

## Disclosure timeline

- Acknowledgement within 72 hours of report.
- Assessment + fix target within 14 days for high-severity issues (auth bypass, RCE).
- Coordinated disclosure via GitHub Security Advisory with CVE assignment where applicable.

Solo-maintained project — timelines are best-effort, not contractual.
