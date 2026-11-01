# Astra — Project Master Build Plan

> **Companion Document:** Derived from [`docs/project.md`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/docs/project.md) (Master Specification), [`docs/weeklyGoal.md`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/docs/weeklyGoal.md) (Weekly Goals), and [`docs/ARCHITECTURE.md`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/docs/ARCHITECTURE.md) (Architecture Specification).

---

## 1. Project Overview & Delivery Strategy

**Astra** is an enterprise project and staffing allocation governance portal. The build execution follows a strict 7-phase milestone roadmap. No phase begins until the preceding phase's quality exit gate is verifiably satisfied with passing tests and audit compliance.

### Phase Mapping

| Phase | Milestone Name | Key Deliverables | Exit Gate Focus |
|:---:|---|---|---|
| **Week 1** | Foundation, Login & Users | Clean 4-layer architecture, Docker, SQL Server + Identity, Cookie Auth, Lockout, Role seeding, Admin User Management, Central Audit Interceptor, Base Layout | Docker runs; Seeded Admin logs in; User management CRUD & soft deactivation work; Audit entries recorded; CI green |
| **Week 2** | Clients & Projects | Client management, Project management, Status/Priority, Lead assignment, Server-side search/filter/pagination, Concurrency control | Zero lost updates (RowVersion); Duplicate prevention; Performance budget (<500ms on 1k projects) |
| **Week 3** | Team Allocation & Staff Directory | Project team assignment, Configurable project roles, Filtered unique index on active assignments, Employee profiles (current/past timeline) | No hard deletes of assignments; Point-in-time team roster queries pass |
| **Week 4** | Reassignment Workflow & Audit Center | Atomic reassignment wizard (Move, Add, Remove), Manager vs Admin authority enforcement, Task reallocation, Reassignment history, Outbox email notifications | Reassignment atomicity verified; Timeline reconstruction matches assignment history; Audit viewer operational |
| **Week 5** | Progress Tracking & Tasks | Milestones, Task tracking (ToDo/InProgress/Blocked/Done), Progress update log, Manual vs. Derived progress calculation engine | Derived progress unit tests cover edge cases (0 tasks, all cancelled); Timeline updates append-only |
| **Week 6** | Dashboard, Workload & Reporting | Role-aware dashboard, At-Risk & Overdue evaluation engine, Workload allocation view, 4 Reports with ClosedXML Excel & QuestPDF exports | Export numbers match on-screen figures; Memory streaming for large datasets; Audit logging of exports |
| **Week 7** | Hardening, Handover & Release | Full OWASP Top 10 audit, Accessibility pass (WCAG 2.1 AA / axe), Final seed dataset polish, End-to-end smoke tests, Handover documentation | 100% Definition of Done satisfied; v1.0.0 tagged; Handover report delivered |

---

## 2. Week 1 Detailed Task Breakdown

### Sub-Phase 1.1: Tooling & Solution Scaffolding
- [x] Git repository initialized, `.gitignore`, `.github/` workflows configured.
- [x] Root `.editorconfig` enforcing C# naming conventions, nullability, and analyzer warnings as errors.
- [x] Multi-project solution configuration (`Astra.sln` with `Portal.Domain`, `Portal.Application`, `Portal.Infrastructure`, `Portal.Web`).
- [x] Docker containerization: `Dockerfile` and `docker-compose.yml` (App + SQL Server).
- [x] Environment configuration template: `.env.example`.
- [x] Documentation foundation: `docs/PLAN.md`, `docs/DECISIONS.md`, `docs/SECURITY.md`.

### Sub-Phase 1.2: Database & Domain Layer
- [x] `Portal.Domain`: Core entity models (`ApplicationUser`, `AuditLog`, `Notification`, `EmailOutboxItem`).
- [x] `Portal.Infrastructure`: `ApplicationDbContext` with ASP.NET Core Identity integration.
- [x] EF Core code-first migration for baseline schema.
- [x] Seed four roles: `Admin`, `Manager`, `Employee`, `Viewer`.
- [x] Seed initial `Admin` account securely from environment variables.
- [x] Least-privilege database trigger/permissions for `audit_logs` (INSERT/SELECT only).

### Sub-Phase 1.3: Authentication & Security Engine
- [x] Cookie authentication with secure flags (`HttpOnly`, `SameSite=Lax/Strict`, sliding expiration).
- [x] Account lockout after 5 consecutive failed attempts (15-minute duration).
- [x] Secure password policy (minimum 12 characters, complex character rules).
- [x] Forced password change on initial login for seeded or newly created accounts.
- [x] Generic authentication error messages to prevent user enumeration.
- [ ] Rate limiting on `/Auth/Login` and `/Auth/ForgotPassword`.
- [x] Anti-forgery validation (`AutoValidateAntiforgeryToken`) on all mutating endpoints.
- [x] Security headers middleware (HSTS, CSP, X-Frame-Options, X-Content-Type-Options).

### Sub-Phase 1.4: Authorization Pipeline
- [x] Deny-by-default `FallbackPolicy` requiring authenticated identity for all non-public endpoints.
- [x] Role-based authorization policies (`RequireAdminRole`, `RequireManagerRole`, etc.).
- [x] Instant security stamp validation (rejects sessions immediately if a user is deactivated or role changed).

### Sub-Phase 1.5: User Management (Admin Only)
- [x] Paginated user list with real-time search and role/activity filtering.
- [x] Create user modal/page with role assignment and set-password email dispatch.
- [x] Edit user details and role mutation.
- [x] Deactivate and reactivate users (soft deactivation; block deactivating the last active Admin).
- [x] Deactivated user session termination.

### Sub-Phase 1.6: Centralized Audit Logging
- [x] EF Core `SaveChangesInterceptor` automatically recording entity creation, updates, and soft deletes into `audit_logs`.
- [x] Structured before/after state diffs (JSON with sensitive credentials redacted).
- [x] Dedicated audit logging for security events (login success, login failure, lockout, password reset).

### Sub-Phase 1.7: Testing & Exit Gate Verification
- [x] Unit tests for password validation, lockout rules, and model invariants.
- [x] Integration tests verifying login, lockout, user deactivation session rejection, and role-based route matrix.
- [x] Automated verification of CSRF protection on POST routes.
- [x] Clean compilation, analyzer validation, and Phase 1 Report generation.

---

## 3. Week 2 Detailed Task Breakdown (Clients and Projects)

### Sub-Phase 2.1: Database & Entities
- [x] Entity Framework Core migration `AddClientsAndProjects` for `clients`, `projects`, `project_clients`, and `project_roles`.
- [x] Project CHECK constraints: planned end >= start date, actual end >= start date, progress percent between 0 and 100.
- [x] Unique indexes: `projects.code` (unique), `clients.name` (case-insensitive unique), `project_roles.name` (unique).
- [x] `RowVersion` concurrency tokens (`rowversion`/timestamp) on clients and projects.
- [x] Multi-column and foreign key performance indexes: `projects(status, is_archived)`, `projects(lead_id)`, `projects(planned_end_date)`, `clients(is_active)`.
- [x] Restrictive delete behaviors (`DeleteBehavior.Restrict`) on all project and client foreign keys to prevent cascading deletions.

### Sub-Phase 2.2: Client Management
- [x] Client directory listing with real-time search (name, contact, email, phone), sorting, pagination, and active/inactive status filtering.
- [x] Create and edit client workflows with case-insensitive duplicate name detection and validation messages.
- [x] Deactivate-not-delete policy: clients linked to active or historical projects are deactivated rather than deleted to preserve audit trails.
- [x] Client details page presenting stakeholder cards, contact information, notes, and a portfolio of all linked projects.
- [x] Permissions: Admin and Manager have create/edit/toggle access; Viewer has read-only access; Employee has no client access.

### Sub-Phase 2.3: Project Management & Governance
- [x] Project creation with code auto-suggestion (`PRJ-YYYY-###`), title, scope, multi-client picker, lead assignment, milestone dates, status, and priority.
- [x] Code security: auto-suggested code is read-only for Managers; editable only by System Administrators.
- [x] Lead eligibility validation: assigned lead must be an active user in the Manager or Admin role.
- [x] Ownership governance: creating Manager is default lead/owner; Managers may edit/archive only projects they lead; Admins may edit/archive all.
- [x] Milestone validation: planned end >= start date; actual end required for Completed/Cancelled statuses; Completed status auto-prompts 100% progress.
- [x] Soft archive & restore workflows with confirmation dialogs; archived projects hidden by default from directory and workload calculations.
- [x] Status reopening rule: reopening a Completed project clears the actual end date and logs the state transition in the audit trail.
- [x] Friendly optimistic concurrency conflict resolution page (`ConcurrencyConflict.cshtml`) displaying side-by-side comparison with zero data loss.

### Sub-Phase 2.4: Project Directory & Search
- [x] High-performance server-side search across code, title, and description.
- [x] Multi-faceted filtering: status, client, lead, priority, date range, and archive state.
- [x] Column sorting and server-side pagination with complete URL query persistence for bookmarking.
- [x] Derived Overdue badge calculation: dynamically computed in memory for non-completed projects past planned end date.
- [x] Resource-based query scoping (`IProjectScopingService`): Employees see only assigned projects (returning 404 for unassigned projects to prevent IDOR).

### Sub-Phase 2.5: Admin Project Roles Lookup
- [x] Admin screen (`/ProjectRoles`) to manage project roles: add, rename, and toggle active/deactivated.
- [x] Pre-seeded baseline roles: Lead, Developer, Analyst, Designer, Tester, Support, Other.

### Sub-Phase 2.6: Testing & Quality Exit Gate
- [x] xUnit automated test project `tests/Astra.Tests` integrated into solution with 20 passing unit/integration tests.
- [x] Unit tests for date rules, completion invariants, lead eligibility, and code sequence incrementation.
- [x] Authorization matrix tests (Admin, Lead Manager, Non-Lead Manager, Employee, Viewer).
- [x] IDOR prevention tests verifying 404 responses for unauthorized resource access.
- [x] Concurrency tests proving stale submission detection.
- [x] Performance validation proving list queries across 1,000+ seeded projects execute well under the 500ms budget.
- [x] Phase 2 certification report generated.

