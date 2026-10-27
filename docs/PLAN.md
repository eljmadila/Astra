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
- [ ] Root `.editorconfig` enforcing C# naming conventions, nullability, and analyzer warnings as errors.
- [ ] Multi-project solution configuration (`Astra.sln` with `Portal.Domain`, `Portal.Application`, `Portal.Infrastructure`, `Portal.Web`).
- [ ] Docker containerization: `Dockerfile` and `docker-compose.yml` (App + SQL Server).
- [ ] Environment configuration template: `.env.example`.
- [ ] Documentation foundation: `docs/PLAN.md`, `docs/DECISIONS.md`, `docs/SECURITY.md`.

### Sub-Phase 1.2: Database & Domain Layer
- [ ] `Portal.Domain`: Core entity models (`ApplicationUser`, `AuditLog`, `Notification`, `EmailOutboxItem`).
- [ ] `Portal.Infrastructure`: `ApplicationDbContext` with ASP.NET Core Identity integration.
- [ ] EF Core code-first migration for baseline schema.
- [ ] Seed four roles: `Admin`, `Manager`, `Employee`, `Viewer`.
- [ ] Seed initial `Admin` account securely from environment variables.
- [ ] Least-privilege database trigger/permissions for `audit_logs` (INSERT/SELECT only).

### Sub-Phase 1.3: Authentication & Security Engine
- [ ] Cookie authentication with secure flags (`HttpOnly`, `SameSite=Lax/Strict`, sliding expiration).
- [ ] Account lockout after 5 consecutive failed attempts (15-minute duration).
- [ ] Secure password policy (minimum 12 characters, complex character rules).
- [ ] Forced password change on initial login for seeded or newly created users.
- [ ] Generic authentication error messages to prevent user enumeration.
- [ ] Rate limiting on `/Auth/Login` and `/Auth/ForgotPassword`.
- [ ] Anti-forgery validation (`AutoValidateAntiforgeryToken`) on all mutating endpoints.
- [ ] Security headers middleware (HSTS, CSP, X-Frame-Options, X-Content-Type-Options).

### Sub-Phase 1.4: Authorization Pipeline
- [ ] Deny-by-default `FallbackPolicy` requiring authenticated identity for all non-public endpoints.
- [ ] Role-based authorization policies (`RequireAdminRole`, `RequireManagerRole`, etc.).
- [ ] Instant security stamp validation (rejects sessions immediately if a user is deactivated or role changed).

### Sub-Phase 1.5: User Management (Admin Only)
- [ ] Paginated user list with real-time search and role/activity filtering.
- [ ] Create user modal/page with role assignment and set-password email dispatch.
- [ ] Edit user details and role mutation.
- [ ] Deactivate and reactivate users (soft deactivation; block deactivating the last active Admin).
- [ ] Deactivated user session termination.

### Sub-Phase 1.6: Centralized Audit Logging
- [ ] EF Core `SaveChangesInterceptor` automatically recording entity creation, updates, and soft deletes into `audit_logs`.
- [ ] Structured before/after state diffs (JSON with sensitive credentials redacted).
- [ ] Dedicated audit logging for security events (login success, login failure, lockout, password reset).

### Sub-Phase 1.7: Testing & Exit Gate Verification
- [ ] Unit tests for password validation, lockout rules, and model invariants.
- [ ] Integration tests verifying login, lockout, user deactivation session rejection, and role-based route matrix.
- [ ] Automated verification of CSRF protection on POST routes.
- [ ] Clean compilation, analyzer validation, and Phase 1 Report generation.
