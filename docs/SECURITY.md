# Astra — Security Architecture & Threat Model

> **Status:** Active Baseline  
> **Audience:** Engineering team, security auditors, deployment administrators.

---

## 1. Security Architecture Principles

Astra enforces a **Zero-Trust, Defense-in-Depth** security model:

1. **Deny-by-Default Authorization**: Every endpoint requires authentication unless explicitly decorated with `[AllowAnonymous]`.
2. **Server-Side Enforcement**: Client-side UI visibility is a convenience; the server verifies permissions and resource ownership on every incoming request.
3. **Data Immutability & Audit Integrity**: Append-only audit logs protected at the database tier against alteration or truncation.
4. **Credential Isolation**: Zero hardcoded secrets; configuration loaded exclusively via environment variables and User Secrets.

---

## 2. OWASP Top 10 Mitigation Matrix

| Vulnerability | Astra Defense / Control Mechanism | Implementation |
|---|---|---|
| **A01: Broken Access Control** | Policy- and resource-based authorization handlers verify role and project ownership. IDOR mitigated by returning 404 for unassigned projects and 403 for unauthorized actions. Fallback policy denies anonymous access by default. | `Program.cs`, `Portal.Application/Authorization/` |
| **A02: Cryptographic Failures** | Strict HTTPS enforcement with HSTS (`max-age=31536000`). ASP.NET Core Identity PBKDF2 with SHA-256 password hashing. Secure, HttpOnly, SameSite cookies. | Middleware pipeline, Identity configuration |
| **A03: Injection (SQLi)** | Exclusively parameterized queries executed via Entity Framework Core and Dapper parameters. Raw SQL string concatenation is forbidden. | `Portal.Infrastructure/Data/` |
| **A04: Insecure Design** | Atomic reassignment transactions prevent split states. Dual confirmation on destructive actions. Thread-safe concurrency checks via `RowVersion`. | Domain service invariants |
| **A05: Security Misconfiguration** | Custom error pages (403, 404, 500) hide stack traces and database exceptions in production. Comprehensive security response headers. | `Program.cs` / `portal/Views/Shared/Error.cshtml` |
| **A06: Vulnerable & Outdated Components** | Pinned package versions with zero high/critical vulnerabilities. Checked via `dotnet list package --vulnerable` in CI. | `.github/workflows/ci.yml` |
| **A07: Identification & Auth Failures** | Account lockout: 5 failed attempts locks the account for 15 minutes. Minimum 12-character password policy. Single-use, time-expiring reset tokens. Generic login error messages prevent username enumeration. | ASP.NET Core Identity options |
| **A08: Software & Data Integrity Failures** | `audit_logs` protected by SQL Server trigger and role permissions. State-changing requests mandate valid AntiForgery tokens. | Interceptor & Database permissions |
| **A09: Security Logging & Monitoring** | Serilog structured JSON logging with correlation IDs. Every mutating action (create, update, reassign, status change, login failure) logged with user, IP, timestamp, and redacted payload diffs. | `Portal.Infrastructure/Logging/` |
| **A10: Server-Side Request Forgery (SSRF)** | No arbitrary user-supplied outbound HTTP requests. Outbound notifications strictly routed to preconfigured internal SMTP/mail relay endpoints. | Notification Outbox Processor |

---

## 3. Database Least-Privilege & Audit Immutability

### Application Database Account Permissions
The web application connects using a dedicated database user granted standard read/write permissions on business tables, with strict restrictions on `audit_logs`:

```sql
-- Grant read/write on normal business tables
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO AstraAppUser;

-- Restrict audit_logs: Allow only INSERT and SELECT
DENY UPDATE, DELETE ON dbo.audit_logs TO AstraAppUser;
```

### Tamper-Prevention Trigger
To safeguard audit logs against accidental modifications or administrative error, an immutable trigger is deployed:

```sql
CREATE TRIGGER TR_AuditLogs_PreventModification
ON audit_logs
FOR UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    RAISERROR ('Security Violation: Audit logs are append-only and cannot be updated or deleted.', 16, 1);
    ROLLBACK TRANSACTION;
END;
```

---

## 4. HTTP Security Headers Baseline

The web application emits the following headers on all responses:

* **Strict-Transport-Security (HSTS)**: `max-age=31536000; includeSubDomains; preload`
* **X-Content-Type-Options**: `nosniff`
* **X-Frame-Options**: `DENY`
* **Referrer-Policy**: `strict-origin-when-cross-origin`
* **Permissions-Policy**: `camera=(), microphone=(), geolocation=()`
* **Content-Security-Policy (CSP)**:
  ```text
  default-src 'self';
  script-src 'self';
  style-src 'self' 'unsafe-inline';
  img-src 'self' data:;
  font-src 'self';
  frame-ancestors 'none';
  ```

---

## 5. Secret Management
* **Development**: Managed via .NET User Secrets (`dotnet user-secrets`).
* **Docker / CI / Production**: Injected via environment variables (see [`.env.example`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/.env.example)).
* Secrets must **never** be committed to version control.
