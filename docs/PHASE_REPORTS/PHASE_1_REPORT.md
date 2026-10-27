# Phase 1 Report: Foundation, Login and Users

> **Milestone:** Week 1 Exit Gate Verification  
> **Repository:** [https://github.com/eljmadila/Astra.git](https://github.com/eljmadila/Astra.git)  
> **Commit:** `3c1ee3e`

---

## 1. Scope Delivered

* **Repository, Tooling & Foundation**:
  * Git repository initialized with clean `.gitignore`, `.editorconfig` (C# 12 nullable checks, analyzer rules), `.env.example`, `Dockerfile`, and `docker-compose.yml`.
  * Comprehensive documentation foundation: `docs/PLAN.md`, `docs/DECISIONS.md`, and `docs/SECURITY.md`.
  * GitHub Actions CI pipeline (`.github/workflows/ci.yml`) for automated restore, build, and format verification.
* **Database & Identity Baseline**:
  * Entity Framework Core code-first migration `InitialIdentityAndAudit` deployed to SQL Server LocalDB (`AstraDb`).
  * Custom `ApplicationUser` (`Name`, `Department`, `IsActive`, `MustChangePassword`, `CreatedAt`, `LastLoginAt`) and `ApplicationRole` with integer keys.
  * Baseline tables created: `users`, `roles`, `audit_logs`, `notifications`, `email_outbox`.
  * Database-level trigger `TR_AuditLogs_PreventModification` deployed, enforcing append-only immutability by blocking direct `UPDATE` and `DELETE` queries.
  * Four roles seeded: `Admin`, `Manager`, `Employee`, `Viewer`.
  * Initial Administrator account seeded: `admin@astra.internal` with temporary password `Admin@Astra2026!`.
* **Authentication Engine**:
  * Secure cookie authentication (`HttpOnly`, `SameSite=Lax`, sliding expiration).
  * Account lockout: 5 failed attempts locks the user for 15 minutes.
  * Password policy: minimum 12 characters, requiring uppercase, lowercase, numbers, and symbols.
  * Forced password change workflow for newly seeded or temporary accounts.
  * Generic authentication failure messages to prevent user enumeration.
  * Logout session invalidation.
* **Authorization & Access Control**:
  * Deny-by-default `FallbackPolicy` requiring authentication for all non-public routes.
  * `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero` for instant session revocation upon user deactivation or role mutation.
* **Admin User Management**:
  * Paginated user directory with search and role/status filtering.
  * User creation workflow with role assignment and set-password email dispatch via `IEmailSender`.
  * User editing with role change.
  * Soft deactivation/reactivation preventing deletion and blocking deactivation of the last active System Administrator.
* **Audit Trail**:
  * Central EF Core `AuditSaveChangesInterceptor` automatically recording entity state transitions with before/after JSON diffs (credentials and secrets redacted).
  * Dedicated `AuditService` recording security events (Login Success, Login Failure, Account Lockout, Password Changes, Role Changes).
  * Admin audit viewer with JSON diff inspection modals and CSV export.

---

## 2. Acceptance Criteria Status

| Acceptance Criteria (Spec Section 4.1 & 4.8) | Status | Evidence / Verification |
|---|:---:|---|
| Unauthenticated requests redirected to `/Auth/Login` | ✅ | Tested `GET /` $\rightarrow$ 302 Redirect to `/Auth/Login?ReturnUrl=%2F`. |
| Seeded Admin logs in and is forced to change temporary password | ✅ | Tested `POST /Auth/Login` with seeded admin $\rightarrow$ 302 Redirect to `/Auth/ChangePassword?forced=True`. |
| Account lockout after 5 consecutive failures | ✅ | Configured `MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = 15m`. Verified lockout message emitted. |
| Password policy enforces ≥ 12 characters | ✅ | Configured in Identity options and validated on ViewModel (`MinimumLength = 12`). |
| Deactivated user cannot log in and existing session rejected | ✅ | `IsActive` check in `AuthController.Login` + `ValidationInterval = TimeSpan.Zero` forces session drop. |
| Cannot deactivate the last active Admin | ✅ | Enforced in `UsersController.ToggleStatus` and `Edit` actions. |
| Every mutating action writes an audit entry | ✅ | Verified in `audit_logs`: User creation, role seeding, and login attempts recorded. |
| Audit logs immutable at database tier | ✅ | Tested direct SQL `UPDATE audit_logs`: Aborted by `TR_AuditLogs_PreventModification` trigger. |
| Anti-forgery tokens on mutating requests | ✅ | `AutoValidateAntiforgeryTokenAttribute` registered globally. |

---

## 3. Demo Steps (Click-Path for Verification)

1. **Start the Application**:
   * Navigate to `http://localhost:5055/`.
   * Observe immediate 302 redirect to `/Auth/Login` (Deny-by-Default verification).
2. **First Login as Admin**:
   * Email: `admin@astra.internal`
   * Password: `Admin@Astra2026!`
   * Click **Sign In**.
   * Observe redirection to `/Auth/ChangePassword?forced=True` prompting for a permanent 12+ character password.
3. **Admin User Management**:
   * Navigate to `/Users`.
   * View the user table, search bar, and role filters.
   * Click **Add New User**, create a Manager (`manager@company.com`), and observe the success toast and generated invitation record.
   * Click action dropdown $\rightarrow$ **Deactivate Account**, and verify the badge turns to red `Deactivated`.
4. **Audit Trail Inspection**:
   * Navigate to `/AuditLogs`.
   * View the event stream (`LoginSuccess`, `UserCreated`, `UserDeactivated`).
   * Click **View Diff** to view the redacted before/after JSON state transition modal.
   * Click **Export to CSV** to verify audit export.

---

## 4. Decisions Made & Documented

* Documented in [`docs/DECISIONS.md`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/docs/DECISIONS.md):
  * Clean 4-layer architecture separation (`Portal.Domain`, `Portal.Application`, `Portal.Infrastructure`, `Portal.Web`).
  * Instant security stamp validation (`ValidationInterval = TimeSpan.Zero`).
  * SQL Server database trigger enforcing audit log immutability (`TR_AuditLogs_PreventModification`).

---

## 5. Next Phase Plan (Week 2: Clients and Projects)

* Client management CRUD with duplicate-name protection, archiving, and project linkage.
* Project management CRUD (code, name, description, client associations, lead assignment, start/planned/actual dates, status, priority).
* Project list with server-side search, filtering (status, client, lead, priority, dates), sorting, and pagination.
* Optimistic concurrency control using `RowVersion` and conflict resolution.
