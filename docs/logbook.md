# Astra — Engineering & Development Logbook

> **Engineer:** Elyamine Madila  
> **Project:** Astra (Enterprise Project Management & Team Allocation Governance Portal)  
> **Stack:** ASP.NET Core MVC (.NET 10), Entity Framework Core, Microsoft SQL Server, Bootstrap 5.3  
> **Repository:** [https://github.com/eljmadila/Astra.git](https://github.com/eljmadila/Astra.git)

---

## Logbook Purpose
This logbook serves as a structured technical journal capturing development milestones, architecture choices, engineering hurdles, key learnings, and weekly retrospective summaries across the Astra delivery roadmap.

---

## Week 1: Solution Scaffolding, Authentication & Security Baseline
* **Period:** October 2026 — Phase 1 Milestone
* **Status:** ✅ Completed & Certified ([Phase 1 Report](PHASE_REPORTS/PHASE_1_REPORT.md))

### 1. What I Did
* **Project Architecture & Scaffolding**:
  * Established the repository structure with clean `.editorconfig` (strict C# 12 nullable checks, analyzer rules), `.env.example`, `Dockerfile`, and `docker-compose.yml` for multi-container orchestration with SQL Server.
  * Formulated foundational architecture documentation: [`docs/PLAN.md`](PLAN.md), [`docs/DECISIONS.md`](DECISIONS.md), [`docs/ARCHITECTURE.md`](ARCHITECTURE.md), and [`docs/SECURITY.md`](SECURITY.md).
* **Identity & Database Foundation**:
  * Configured ASP.NET Core Identity with custom integer primary keys (`ApplicationUser`, `ApplicationRole`).
  * Created and applied initial EF Core migration (`InitialIdentityAndAudit`) creating `users`, `roles`, `audit_logs`, `notifications`, and `email_outbox`.
  * Seeded 4 baseline enterprise roles (`Admin`, `Manager`, `Employee`, `Viewer`) and an initial System Administrator account via environment variables.
  * Deployed a database-level trigger (`TR_AuditLogs_PreventModification`) to enforce append-only immutability by aborting direct `UPDATE` and `DELETE` queries on `audit_logs`.
* **Authentication Engine & Hardening**:
  * Implemented secure cookie authentication (`HttpOnly`, `SameSite=Lax`, sliding expiration).
  * Enforced account lockout: 5 failed login attempts lock the account for 15 minutes.
  * Configured enterprise password policy (minimum 12 characters, requiring uppercase, lowercase, numbers, and symbols).
  * Built forced password change workflow on initial login for newly provisioned accounts.
  * Implemented generic authentication error messages to prevent user enumeration attacks.
* **Authorization & Access Control**:
  * Enforced a zero-trust `FallbackPolicy` requiring authentication across all non-public routes.
  * Configured `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero` for instant session revocation upon account deactivation or role mutation.
* **Admin User Management**:
  * Built user directory with server-side pagination, search, and role/status filtering.
  * Created user provisioning, editing, and soft-deactivation flows (preventing deletion and preventing deactivation of the last active System Administrator).
* **Central Audit Trail**:
  * Implemented EF Core `AuditSaveChangesInterceptor` to record before/after JSON diffs on entity modifications, with sensitive credential fields automatically redacted.
  * Built admin audit log viewer with search, filtering, JSON diff modal, and CSV export.

### 2. What I Learned
* **Instant Session Revocation**: Learned how ASP.NET Core Identity's security stamp validator works. By setting `ValidationInterval = TimeSpan.Zero`, every incoming request verifies the user's security stamp against the database, ensuring that deactivating a user or revoking an admin role terminates the active session immediately without waiting for cookie expiration.
* **Database-Tier Immutability**: Learned that application-level read-only checks are insufficient for strict compliance; adding a database trigger (`TR_AuditLogs_PreventModification`) ensures that even direct SQL executions or compromised database credentials cannot tamper with existing audit trails.
* **EF Core Interceptors vs. Domain Events**: Learned how to leverage `SaveChangesInterceptor` to capture entity state transitions (`Added`, `Modified`, `Deleted`) and generate audit log rows before changes are committed, ensuring that entity persistence and audit logging occur in the exact same transaction.
* **Defense-in-Depth**: Learned how to configure strict security response headers (HSTS, CSP, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy`) and global anti-forgery token validation (`AutoValidateAntiforgeryTokenAttribute`) to prevent CSRF, clickjacking, and MIME-sniffing.

### 3. Week 1 Summary
* **Highlights**: Successfully built a production-ready authentication and authorization core with enterprise audit logging and admin user management.
* **Challenges & Solutions**: Balancing strict password requirements and lockout rules without exposing user enumeration vulnerabilities; solved by standardizing generic login failure notifications while recording discrete security reasons in the background audit log.
* **Outcome**: Met all Week 1 exit gates certified in the Phase 1 Report.

---

## Week 2: Clients, Projects, Concurrency & Governance
* **Period:** October 28 — November 1, 2026 — Phase 2 Milestone
* **Status:** ✅ Completed & Certified ([Phase 2 Report](PHASE_REPORTS/PHASE_2_REPORT.md))

### 1. What I Did
* **Database Schema & Relational Integrity**:
  * Created and applied EF Core migration `AddClientsAndProjects` provisioning `clients`, `projects`, `project_clients` (many-to-many join), and `project_roles` (lookup catalog).
  * Added database CHECK constraints: planned end date $\ge$ start date, actual end date $\ge$ start date, and progress percent between 0 and 100.
  * Configured unique indexes on `projects.code`, case-insensitive unique index on `clients.name`, and `project_roles.name`.
  * Configured `RowVersion` concurrency tokens (`rowversion`/timestamp) on both clients and projects.
  * Applied `DeleteBehavior.Restrict` across foreign keys to prevent accidental cascading deletes.
* **Client Management Subsystem (`/Clients`)**:
  * Built client directory with search across name, contact person, email, and phone; active/inactive filters; column sorting; and URL-persisted pagination.
  * Built create and edit forms with case-insensitive duplicate name detection and regex validation on email and phone numbers.
  * Implemented the **Deactivate-Instead-of-Delete** policy: attempting to delete a client that has linked projects automatically converts the action to soft deactivation (`IsActive = false`), preserving project history.
  * Built Client Profile Details view (`/Clients/Details/{id}`) displaying stakeholder cards, notes, and a portfolio table of linked projects with health badges.
* **Project Management & Delivery Governance (`/Projects`)**:
  * Built project provisioning workflow with automated code suggestion in `PRJ-YYYY-###` format (e.g., `PRJ-2026-001`).
  * Enforced code privileges: auto-suggested code is read-only for standard Managers; editable only by System Administrators.
  * Enforced lead eligibility: project leads must be active users with either Manager or Admin roles.
  * Implemented project ownership: the creating Manager is the owner; Managers may edit and archive only projects they lead (`LeadId == CurrentUserId`); Administrators have global editing authority.
  * Enforced milestone validation: planned end cannot precede start date; actual end date is mandatory when status is Completed or Cancelled.
  * Added automated status helper rules: setting status to Completed automatically prompts or sets 100% progress; reopening a Completed project automatically clears the actual end date and logs the transition.
  * Built soft archive & restore functionality with modal confirmations; archived projects are excluded by default from active listings.
  * Developed friendly optimistic concurrency conflict resolution (`ConcurrencyConflict.cshtml`) displaying a side-by-side comparison of current database values versus submitted changes, allowing users to reload or safely overwrite without losing data.
* **Project Directory & Real-Time Filtering**:
  * Implemented high-performance server-side search across code, title, and description.
  * Implemented multi-faceted filters: status, client, lead, priority, date range, and archive state.
  * Implemented derived **Overdue** badge calculation (computed in real-time in memory for non-completed projects past their planned end date, displaying days overdue).
  * Implemented `IProjectScopingService` for IDOR defense: Employees see only assigned projects (unauthorized lookups return HTTP 404 rather than 403 to prevent IDOR enumeration).
* **Project Roles Administration (`/ProjectRoles`)**:
  * Built admin lookup screen allowing System Administrators to add, rename, and toggle active status on project roles.
  * Pre-seeded 7 standard roles: *Lead, Developer, Analyst, Designer, Tester, Support, Other*.
* **Automated Testing Suite (`tests/Astra.Tests`)**:
  * Created an xUnit test project integrated into `portal/AspnetCoreMvcFull.sln`.
  * Wrote 20 comprehensive unit and integration tests covering:
    * Date validation and status completion rules.
    * Code generation formatting and sequence incrementation.
    * Authorization matrix tests (Admin, Lead Manager, Non-Lead Manager, Employee, Viewer).
    * IDOR defense verification (404 on unassigned projects).
    * Duplicate client name case-insensitive protection.
    * Deactivate-not-delete policy for linked clients.
    * XSS HTML-encoding defenses.
    * Concurrency conflict detection via `RowVersion`.
    * High-volume performance benchmark: query across 1,000+ seeded projects executes in < 80ms (budget < 500ms).

### 2. What I Learned
* **Optimistic Concurrency Control in EF Core**: Learned how SQL Server's binary `rowversion` column works seamlessly with EF Core's `[Timestamp]` attribute. EF Core adds `WHERE [RowVersion] = @original` to `UPDATE` statements; if another user updated the row in between, 0 rows are affected and EF Core throws `DbUpdateConcurrencyException`. Instead of crashing or showing a generic error, capturing this exception enables rendering a dedicated side-by-side diff page so no user input is lost.
* **IDOR Prevention (404 vs. 403)**: Learned an essential security concept: when a user attempts to access a resource they do not have permission to view, returning HTTP 403 (Forbidden) leaks the existence of that resource (allowing attackers to enumerate project IDs). Returning HTTP 404 (Not Found) for unassigned resources provides true zero-knowledge privacy. HTTP 403 is reserved only when a resource is visibly acknowledged but a specific mutating action is forbidden (e.g., a Manager attempting to edit another Manager's project).
* **Derived vs. Stored State**: Learned why time-sensitive business flags like `IsOverdue` should be calculated dynamically in memory/ViewModels rather than stored in database columns. A stored `is_overdue` column becomes stale every night unless updated by scheduled batch jobs; computing it from `PlannedEndDate < Today` ensures 100% accuracy at zero storage cost.
* **Multi-Tier Validation**: Learned the value of enforcing business invariants at all 4 architectural layers:
  1. *Client-side*: Responsive date pickers, auto-fill helpers, and confirmation dialogs.
  2. *ViewModel / Controller*: `IValidatableObject` and `ModelState` checks.
  3. *Application Service*: Role eligibility, ownership boundaries, and query scoping.
  4. *Database*: Foreign key `Restrict` rules and SQL `CHECK` constraints.
* **Eliminating N+1 Queries**: Learned how to construct efficient projection and eager loading queries using `.Include(p => p.Lead).Include(p => p.ProjectClients).ThenInclude(pc => pc.Client)` to fetch all required relations in a single database round-trip, keeping query times under 80ms even with 1,000+ projects.

### 3. Week 2 Summary
* **Highlights**: Successfully delivered the complete Clients, Projects, and Project Roles subsystems, passing all 4.2 & 4.4 acceptance criteria with 20 passing automated tests.
* **Challenges & Solutions**: 
  * *Concurrency Handling*: Addressed potential data loss during simultaneous edits by building `ConcurrencyConflict.cshtml` to present side-by-side diffs.
  * *Relational Integrity*: Implemented the "deactivate-instead-of-delete" policy to ensure that deleting a client linked to projects never orphans historical project records.
* **Outcome**: Week 2 certified, exit gates passed, database seeded with realistic projects (including an active Overdue project for visual verification), and live `dotnet watch` environment validated.

---

## Looking Ahead: Week 3 Milestone
* **Focus**: Team Assignment and Employee Profiles
* **Upcoming Objectives**:
  * Migration for `project_assignments` with a filtered unique index on `(project_id, user_id) WHERE end_date IS NULL` to ensure no user holds duplicate active assignments on the same project.
  * Team assignment workflows (assigning employees with roles, start dates, and past membership retention).
  * Project page "Team Staffing" tab activation.
  * Employee profile views with past and current project timelines.
