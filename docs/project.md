# Project Management Portal — Agent Build Specification

> **Audience:** an autonomous coding agent building this system end to end.
> **Source:** derived from the client's "Project Management Portal: Build Brief". Where the brief was vague, this document makes a firm decision (marked **DECISION**) or lists an open question (section 19). Follow this document over your own assumptions. If something is not covered, choose the simplest option that satisfies the intent, record it in `docs/DECISIONS.md`, and continue.

---

## 0. How to work (read first)

1. **Read this entire document before writing code.** Then produce a short plan in `docs/PLAN.md` mapping phases to tasks.
2. **Work phase by phase** (section 14). Do not start a phase until the previous phase's **exit gate** passes. At the end of each phase, stop and output a **Phase Report** (template in section 15).
3. **Never fake completion.** A feature is done only when it is implemented, tested, reachable from the UI, authorized on the server, and audited (where applicable). Do not stub, mock in production code, leave `TODO`s, or hardcode data to make things look finished.
4. **Run everything you build.** After each phase: build, run tests, start the app, and exercise the new features. Fix failures before reporting.
5. **Commit early and often** with Conventional Commit messages (`feat:`, `fix:`, `test:`, `docs:`, `chore:`). One logical change per commit. Never commit secrets.
6. **Ask only when blocked.** If a requirement is genuinely ambiguous and affects architecture or data, ask. Otherwise decide, document, proceed.
7. **Quality bar:** the finished product should feel like a polished internal product, not a prototype. See section 17 (Definition of Done).

---

## 1. Product summary

A web portal for an office to manage all projects in one place. It must answer, at a glance:

- Who is working on what, and in what role?
- Which client(s) does each project belong to?
- How far along is each project?
- When, why, and by whom were people moved between projects?

It must also keep a **trustworthy, tamper-resistant history** of changes (reassignments and audit log).

### Goals
- Single, always-current view of all projects.
- Team assignment with roles and dates.
- Client linkage per project.
- Progress tracking (status, % complete, milestones, tasks, update log).
- First-class **reassignment workflow** with full history.
- Dashboard, workload view, exportable reports.
- Immutable audit log.

### Non-goals (this release)
Budget/time tracking, Gantt/calendar, client-facing portal, file attachments, comments, external integrations, SSO. Design so these can be added later (see section 16), but **do not build them now**.

---

## 2. Technology (fixed)

| Concern | Choice |
|---|---|
| Framework | ASP.NET Core on the **latest LTS .NET** (C#), **Razor Pages or MVC with server-rendered views** (**DECISION:** server-rendered MVC + small amounts of vanilla JS / htmx; no SPA, keeps CSRF/RBAC simple and the footprint small) |
| Database | **SQL Server** (LocalDB or Docker for dev; connection string via config/env) |
| ORM | Entity Framework Core with **migrations** (code-first). Dapper only if a report query genuinely needs it |
| Auth | **ASP.NET Core Identity** (cookie auth), custom `ApplicationUser`, roles seeded |
| UI | Bootstrap 5 (or equivalent), responsive, accessible. No heavy JS framework |
| Validation | Server-side always (DataAnnotations/FluentValidation); client-side as enhancement |
| Logging | Serilog (console + rolling file), structured |
| Exports | Excel via **ClosedXML** (or EPPlus-free alternative); PDF via **QuestPDF** (check license fit; document choice) |
| Tests | xUnit + FluentAssertions; integration tests with `WebApplicationFactory` against a real SQL Server (Testcontainers or LocalDB); optional Playwright for a smoke E2E |
| Containerization | `Dockerfile` + `docker-compose.yml` (app + SQL Server) for one-command local run |
| CI | GitHub Actions workflow: restore, build, test, format check |
| VCS | Git from the first commit; `.gitignore`, `README.md` |

**Solution layout (DECISION):** clean, simple layering — avoid over-engineering.

```
/src
  Portal.Web            # MVC/Razor UI, controllers, view models, auth wiring
  Portal.Application    # services, use cases, DTOs, validation, authorization policies
  Portal.Domain         # entities, enums, domain rules (no EF/ASP.NET dependencies)
  Portal.Infrastructure # EF Core DbContext, migrations, repositories, exports, email
/tests
  Portal.UnitTests
  Portal.IntegrationTests
/docs
  PLAN.md  DECISIONS.md  USER_GUIDE.md  TECHNICAL_SETUP.md  SECURITY.md  PHASE_REPORTS/
/db
  schema.sql (generated)  seed/ (dev/test seed data)
```

---

## 3. Roles and permissions

Roles: **Admin**, **Manager** (Project Lead), **Employee**, **Viewer** (Executive; included, read-only).

### 3.1 Permission matrix (enforce on the SERVER via authorization policies/handlers)

| Capability | Admin | Manager | Employee | Viewer |
|---|:-:|:-:|:-:|:-:|
| Manage users, roles, activate/deactivate | ✅ | ❌ | ❌ | ❌ |
| Manage clients (create/edit) | ✅ | ✅ (create/edit) | ❌ | ❌ |
| Create project | ✅ | ✅ (becomes owner) | ❌ | ❌ |
| Edit/archive project | ✅ all | ✅ **only projects they lead** | ❌ | ❌ |
| View project list/details | ✅ all | ✅ all | ✅ **only projects they are/were assigned to** | ✅ all (read-only) |
| Assign / remove team members | ✅ all | ✅ own projects | ❌ | ❌ |
| Reassign employees | ✅ | ✅ (see 3.2) | ❌ | ❌ |
| Update project progress/status | ✅ | ✅ own projects | ❌ | ❌ |
| Create/edit milestones | ✅ | ✅ own projects | ❌ | ❌ |
| Create/edit tasks | ✅ | ✅ own projects | ❌ | ❌ |
| Update status/% of **own assigned tasks** | ✅ | ✅ | ✅ | ❌ |
| View employee profile | ✅ any | ✅ any | ✅ own only | ✅ any (read-only) |
| View workload view | ✅ | ✅ | ❌ | ✅ |
| View dashboard | ✅ full | ✅ full | ✅ personal (my projects/tasks) | ✅ full read-only |
| View/export reports | ✅ all | ✅ all | ❌ | ✅ view/export |
| View audit log | ✅ | ❌ | ❌ | ❌ |

### 3.2 Reassignment authority (**DECISION**)
A Manager may reassign an employee **out of** a project they lead and **into** a project they lead. Moving someone **from a project the manager does not lead** requires Admin. Admins may do any reassignment. The UI must only show actions the user may perform, **but the server must independently enforce every rule** (test with direct requests).

### 3.3 Enforcement rules
- Use **policy-based** authorization with **resource-based handlers** (e.g., `ProjectLeadOrAdmin`, `ProjectMemberOrStaff`).
- **Prevent IDOR:** every query that returns project/task/employee data is scoped by the caller's permissions. Guessing an ID must never leak data (return 404 or 403 consistently — **DECISION:** 404 for resources the user cannot see, 403 for visible-but-forbidden actions).
- Role changes take effect immediately (re-validate security stamp).

---

## 4. Functional requirements

Each requirement has acceptance criteria (AC). All must pass.

### 4.1 Authentication and access
- Email + password login; cookie auth; **account lockout** after 5 failed attempts (15 min); generic error messages (no user enumeration).
- Password policy: min 12 chars, check against common-password list if feasible; hashed with Identity's PBKDF2 (or Argon2 if added; document).
- **Password reset** via emailed single-use, expiring token. Email sender is an interface (`IEmailSender`); dev implementation writes to a viewable local outbox/log; production SMTP configurable.
- **Account deactivation, never deletion** for users with history. Deactivated users cannot log in, are hidden from assignment pickers by default, but remain visible in history.
- First-run: seeded Admin created from environment config (never a hardcoded password committed to the repo). Force password change on first login.
- Session: sliding expiry, secure/HttpOnly/SameSite cookies, logout invalidates session.
- **AC:** unauthenticated users are redirected to login for every non-public route; deactivated user's existing session is rejected on next request; all pages in 3.1 return correct allow/deny.

### 4.2 Project management
- Create, edit, **archive** (soft; archived projects hidden by default, restorable by Admin/lead).
- Fields: `code` (unique, auto-suggested like `PRJ-2026-001`, editable by Admin), `name`, `description`, `client(s)`, `lead`, `start_date`, `planned_end_date`, `actual_end_date`, `status`, `priority`, `progress_percent`.
- **Status:** Not Started, In Progress, On Hold, Completed, Cancelled.
- **Priority (DECISION):** Low, Medium, High, Critical.
- Validation: planned end ≥ start; actual end ≥ start; **actual end required when status = Completed/Cancelled** (default today, editable); `progress_percent` 0–100; Completed ⇒ 100% (offer to set automatically); lead must be an active Manager/Admin user.
- Project list: **search** (code/name/description), **filter** (status, client, lead, priority, date range, archived), **sort** (any column), **pagination** (default 25). Filters reflected in URL (shareable/bookmarkable).
- **Derived flags (computed, not stored):** `Overdue` = not Completed/Cancelled and planned_end < today; `AtRisk` — see 4.7 rule.
- **AC:** every field validates with clear messages; list handles 1,000+ projects within the performance budget (section 12); optimistic concurrency prevents lost updates (use `RowVersion`; show a friendly conflict page).

### 4.3 Team assignment
- Assign one or more employees to a project with `role_on_project` and `start_date`.
- **Roles on project (DECISION):** configurable lookup table seeded with Lead, Developer, Analyst, Designer, Tester, Support, Other. Admin can manage the list.
- An employee may be on multiple projects simultaneously. The same employee cannot hold **two active assignments on the same project** (enforce with a filtered unique index where `end_date IS NULL`).
- Removing someone sets `end_date` (never delete rows). Removal requires an effective date and optional reason, and creates an audit entry. (Removal is distinct from *reassignment*, 4.6.)
- Project page: current team table (name, role, since). Toggle to show past members.
- Employee profile: current projects and past projects (with roles and date ranges), open tasks, reassignment history.
- **AC:** no hard deletes of assignments anywhere in code; a point-in-time query ("who was on project X on date D?") returns the correct result.

### 4.4 Client management
- Create/edit clients: name (unique, case-insensitive), contact person, email, phone, notes.
- **Clients are deactivated/archived, not deleted,** if linked to any project.
- Link one or many clients to a project (many-to-many); client page lists all linked projects (with status and lead).
- **AC:** duplicate-name protection; email/phone format validated; client list searchable.

### 4.5 Progress tracking
- Project-level: status + `progress_percent`.
- **Progress mode (DECISION — brief left this open):** each project has a `ProgressMode` of **Manual** (default) or **Derived**.
  - *Manual:* lead enters %.
  - *Derived:* % = completed tasks ÷ total non-cancelled tasks (if no tasks, fall back to milestones; if neither, 0). Recalculated automatically on task/milestone change.
  - The mode is visible on the project page and switchable by the lead; this is flagged as an open question for the client (section 19) but the app must support both.
- **Milestones:** title, due date, completed (with completion date).
- **Tasks:** title, description (optional), assignee (must be an active member of the project), due date, status (**To Do, In Progress, Blocked, Done**). Assignee may update their own task's status; leads manage all.
- **Progress update log:** each time progress/status is updated, record note (required, ≥ 3 chars, ≤ 1000), date, resulting percent/status, and author. Displayed as a timeline on the project page. Log entries are append-only.
- **AC:** changing status or % without a note is allowed only via derived recalculation; manual updates always require a note; derived % is covered by unit tests including edge cases (0 tasks, all cancelled, mix).

### 4.6 Reassignment tracking (KEY REQUIREMENT)
This is the heart of the product. Build it with extra rigor.

- A **Reassign** action is the **only** way to move an employee between projects. No UI or API may silently edit/delete assignment rows to achieve a move.
- Supported operations (all go through the same service and produce a `reassignments` record):
  1. **Move:** employee from Project A → Project B.
  2. **Add to project** (from none → B) — "from_project" null.
  3. **Remove from project** (A → none) — "to_project" null.
- Inputs: employee, from project (nullable), to project (nullable; at least one required; **from ≠ to**), **effective date**, **reason (required, ≥ 10 chars)**, role on new project (required when to_project present), performed by (automatic from the session — never user-supplied).
- **Atomic:** one DB transaction does all of: validate → close old assignment (set `end_date` = effective date) → open new assignment (`start_date` = effective date) → insert `reassignments` row → insert audit log → queue notifications. If any step fails, **nothing** is persisted.
- **Validation rules:**
  - Employee must have an **active assignment** on `from` project at the effective date.
  - Employee must **not** already be actively assigned to `to` project.
  - Effective date cannot be before the old assignment's start date; **backdating limit (DECISION):** up to 30 days for Managers, unlimited for Admin; future-dated moves are allowed (**DECISION:** executed immediately in data with `start_date` in future; views treat "current" using `end_date IS NULL OR end_date > today` and `start_date <= today`).
  - Employee and both projects must be active/non-archived (completed/cancelled projects cannot receive new assignees unless Admin overrides with a reason).
  - Concurrency-safe: two simultaneous reassignments of the same employee must not corrupt state (use transaction isolation / row version checks; test it).
  - If the employee is the **project lead** of the source project, block the move until a new lead is chosen (return a clear message).
  - Open tasks assigned to the employee on the source project: **DECISION** — show a warning with the count and require the user to choose: *unassign tasks* or *keep assigned (flagged)*; default is unassign. Record the choice in the reassignment details.
- **Notifications (optional per action via checkbox, default ON):** in-app notification and email to the employee, the old lead, and the new lead. Implement an `INotificationService` with an outbox table so email failures don't fail the reassignment. In-app notifications shown via a bell with unread count.
- **History views:** per project (all moves in/out) and per employee (full timeline), plus a global reassignment history (Admin/Manager/Viewer) with filters (employee, project, date range, performed_by). Rows show: employee, from → to, effective date, reason, performed by, timestamp.
- **Immutability:** reassignment records are append-only. Corrections are made by creating a new compensating reassignment, not by editing.
- **AC:** a test proves that after any sequence of moves, assignment history reconstructs exactly the same timeline as the reassignment log; partial-failure tests prove atomicity; a Manager attempting an unauthorized move gets 403/404 and no data changes.

### 4.7 Dashboard and reports

**Dashboard** (role-aware):
- Project counts **by status** (chart + numbers; each tile links to a pre-filtered project list).
- **At risk / overdue** projects list.
- **Recent reassignments** (last 10).
- **Recent progress updates** (last 10).
- Employee view: "My projects", "My open tasks", "Due soon".

**At-risk rule (DECISION, document in the UI via tooltip):** a project is **At Risk** if it is In Progress and any of: (a) planned end is within 14 days and progress < 80%; (b) it has overdue milestones; (c) ≥ 30% of tasks are Blocked or overdue; (d) status is On Hold for > 14 days. **Overdue** = past planned end and not Completed/Cancelled. Thresholds live in config (`appsettings`), not magic numbers.

**Workload view:** table of employees with **number of active projects**, number of open tasks, and a simple visual bar; sortable; highlight over-allocation (threshold configurable, default > 4 active projects). Click-through to employee profile.

**Reports (each filterable, viewable on-screen, exportable to Excel AND PDF):**
1. Project status report
2. Team allocation report (project × employee × role × dates)
3. Client–project report
4. Reassignment history

- Exports must contain headers, generation timestamp, who generated it, applied filters, and be correctly formatted (dates, widths, wrapped text). Large exports stream rather than load entirely into memory.
- Exporting is itself logged in the audit log.
- **AC:** exported numbers equal on-screen numbers (test it); empty datasets export cleanly with headers only.

### 4.8 Audit log
- Automatically record **create, update, delete/archive, assign, reassign, remove, status change, login success/failure, role change, user (de)activation, export** with: user, action, entity, entity id, **before/after details (JSON, redacted of secrets)**, IP address, timestamp.
- Implemented centrally (e.g., EF `SaveChanges` interceptor + explicit domain events) so nothing can be forgotten.
- **Read-only from the UI:** no edit/delete endpoints exist at all. **DECISION:** also enforce at the DB level — the application's DB login has INSERT/SELECT only on `audit_logs` (document in `SECURITY.md` and migration/deploy notes), and add a trigger or permission denying UPDATE/DELETE.
- Admin-only viewer with filters (user, action, entity, date range), search, pagination, and CSV export.
- **AC:** every mutating endpoint in the test suite is proven to write an audit entry (use a test that enumerates mutating routes or a table-driven test).

---

## 5. Data model

Use the brief's tables as the base with the hardening below. Use `uniqueidentifier`/GUID or `int` identity keys — **DECISION: `int` identity internally; expose non-sequential public identifiers only if needed** (authorization already prevents IDOR). All tables get `created_at`, `updated_at`; mutable tables get `RowVersion` (concurrency token).

```
users (ASP.NET Identity AspNetUsers extended)
  id, name, email (unique), password_hash, role (via Identity roles), department,
  is_active, must_change_password, created_at, last_login_at

clients
  id, name (unique ci), contact_person, email, phone, notes, is_active, created_at, updated_at, row_version

projects
  id, code (unique), name, description, lead_id → users, status, priority,
  progress_mode (Manual|Derived), progress_percent (0–100 CHECK),
  start_date, planned_end_date, actual_end_date, is_archived,
  created_by, created_at, updated_at, row_version
  CHECKs: planned_end_date >= start_date; actual_end_date >= start_date

project_clients
  project_id → projects, client_id → clients   (PK composite)

project_roles                      -- lookup (Lead, Developer, ...)
  id, name (unique), is_active

project_assignments
  id, project_id, user_id, role_on_project_id, start_date, end_date NULL,
  created_by, created_at
  UNIQUE FILTERED INDEX (project_id, user_id) WHERE end_date IS NULL
  CHECK end_date IS NULL OR end_date >= start_date

reassignments                      -- append-only
  id, user_id, from_project_id NULL, to_project_id NULL, effective_date,
  reason, performed_by, tasks_handling (Unassigned|Kept), created_at
  CHECK (from_project_id IS NOT NULL OR to_project_id IS NOT NULL)
  CHECK (from_project_id IS NULL OR to_project_id IS NULL OR from_project_id <> to_project_id)

milestones
  id, project_id, title, due_date, completed, completed_on, created_at, updated_at

tasks
  id, project_id, title, description, assignee_id NULL, due_date,
  status (ToDo|InProgress|Blocked|Done), created_at, updated_at, row_version

progress_updates                   -- append-only
  id, project_id, note, progress_percent, status_at_time, created_by, created_at

audit_logs                         -- append-only
  id, user_id NULL, action, entity, entity_id, details (nvarchar(max) JSON),
  ip_address, created_at

notifications
  id, user_id, type, title, body, link, is_read, created_at

email_outbox
  id, to_address, subject, body, status (Pending|Sent|Failed), attempts, last_error, created_at, sent_at
```

**Indexes:** FKs, `projects(status, is_archived)`, `projects(lead_id)`, `projects(planned_end_date)`, `project_assignments(user_id, end_date)`, `reassignments(user_id, effective_date)`, `audit_logs(created_at)`, `audit_logs(entity, entity_id)`, `tasks(assignee_id, status)`.

**Rules:** use `DateOnly` for dates and UTC `DateTime` for timestamps; store enums as strings or small lookup for readability; no cascading deletes on history tables (`Restrict`).

---

## 6. Security requirements (non-negotiable)

- **OWASP Top 10** reviewed; document how each applies in `docs/SECURITY.md`.
- **SQL injection:** parameterized queries only (EF/Dapper parameters). No string-concatenated SQL.
- **XSS:** Razor auto-encoding; never use `Html.Raw` on user content; set a strict **Content-Security-Policy** (no inline script unless nonce'd).
- **CSRF:** anti-forgery tokens on every state-changing request (global `AutoValidateAntiforgeryToken`); all mutations are POST/PUT/DELETE — never GET.
- **AuthZ on the server** for every action (see 3.3); deny by default (`FallbackPolicy` requires authenticated user).
- **Security headers:** HSTS, X-Content-Type-Options, X-Frame-Options/`frame-ancestors`, Referrer-Policy, Permissions-Policy.
- **HTTPS only**; secure cookies; no secrets in source — use User Secrets / environment variables; `.env.example` provided.
- **Rate limiting** on login/reset endpoints.
- **Input validation** and length limits everywhere; reject oversized requests; sanitize filenames (for exports).
- **Open-redirect protection** on `returnUrl`.
- **Error handling:** friendly error pages; no stack traces or SQL errors in production responses; correlation ID shown to user and logged.
- **Dependency hygiene:** `dotnet list package --vulnerable` clean; pinned versions; Dependabot config.
- **Least privilege DB user** for the app (see 4.8).
- **PII:** passwords never logged; audit "details" redact sensitive fields.

---

## 7. UX and UI requirements

- **Responsive:** fully usable from 360px mobile to wide desktop. Tables become stacked cards or horizontally scroll gracefully on mobile; no horizontal page scroll.
- **Consistent design system:** one layout, one palette, consistent buttons/forms/tables/badges. Status and priority use consistent colored badges **plus text** (never color alone).
- **Navigation:** left sidebar or top nav showing only what the role may access; breadcrumbs on detail pages; global search box (projects, clients, employees).
- **Every list:** search, filter, sort, pagination, empty state with guidance, loading state.
- **Every form:** inline validation messages, preserved input on error, clear primary action, confirm for destructive/irreversible actions, success toast after save.
- **Accessibility:** WCAG 2.1 AA target — semantic HTML, labels, focus states, keyboard navigable, sufficient contrast, ARIA where needed, skip link. Run automated checks (axe) in the E2E smoke test.
- **Simple enough for little training:** plain-language labels, helpful hint text/tooltips (especially for At Risk, progress mode, reassignment rules), sensible defaults.
- **Reassign flow** should be a clear guided form (select employee → see their current projects → choose destination → effective date/reason/role → review summary → confirm), with a visible warning of consequences.
- **Pages (minimum):** Login, Forgot/Reset password, Change password, Dashboard, Projects list, Project detail (tabs: Overview, Team, Tasks & Milestones, Progress log, Reassignments, Activity), Project create/edit, Clients list/detail/edit, Employees list, Employee profile, Reassign wizard, Global reassignment history, Workload, Reports hub (+4 reports), Users admin, Project-role lookup admin, Audit log (Admin), Notifications, My tasks, Error pages (403/404/500).

---

## 8. Non-functional requirements

- **Performance budget:** at 500 projects, 300 users, 20k tasks, 100k audit rows: list/detail pages < 500 ms server time (p95) on typical dev hardware; dashboard < 1 s; no N+1 queries (verify with query logging in tests); all list endpoints paginated server-side.
- **Reliability:** transactions for multi-step writes; idempotent notification outbox with retries; graceful degradation if email is down.
- **Maintainability:** consistent naming, nullable reference types enabled, warnings as errors in CI, analyzers on, no dead code, meaningful names, small cohesive services. Business rules live in Domain/Application, not controllers or views.
- **Observability:** structured logs with correlation IDs; `/health` (liveness) and `/health/ready` (DB check) endpoints.
- **Configuration:** all environment-specific values via config/env; thresholds (at-risk, overload, backdating) in options classes.
- **Time:** store UTC, display in a configurable office time zone (**default: Africa/Nairobi — confirm, section 19**); date-only fields stay date-only.
- **Localization:** English only for now; keep strings out of deep logic to allow later translation.
- **Browser support:** current Chrome, Edge, Firefox, Safari.

---

## 9. Seed data (required)

Provide **dev/test seed data** (idempotent seeder + `db/seed/` SQL or JSON) so the app is demonstrable on first run:
- 1 Admin, 3 Managers, 12 Employees, 1 Viewer (all **fake** data; documented demo credentials only for the Development environment — never seeded in Production).
- 8–10 clients; 15–25 projects spanning every status/priority, including overdue and at-risk examples; project-client links (some projects with multiple clients).
- Assignments (current and historical), a realistic set of reassignments, milestones, tasks (every status), progress updates, audit entries, a few notifications.
- Seed demonstrates edge cases: an over-allocated employee, a deactivated user with history, an archived project, a project with derived progress, one with manual progress.

---

## 10. Testing requirements

- **Unit tests:** domain/application rules — progress derivation, at-risk/overdue logic, reassignment validation, date rules, backdating limits, permission handlers.
- **Integration tests (real SQL Server):** every endpoint's authorization (matrix in 3.1 as a table-driven test: role × route × expected status), CRUD flows, reassignment atomicity and concurrency, audit-log coverage, export correctness, migrations apply cleanly from scratch.
- **Security tests:** CSRF token required, IDOR attempts, unauthenticated access, role escalation attempts, XSS payload in every text field renders encoded, SQLi strings stored/displayed harmlessly, lockout works.
- **E2E smoke (Playwright, optional but encouraged):** login → create client → create project → assign → reassign → update progress → view dashboard → export report. Include axe accessibility check on key pages.
- **Coverage target:** ≥ 80% on Domain + Application layers; **100% of authorization rules and reassignment logic** exercised.
- All tests must pass in CI from a clean checkout. Flaky tests are bugs — fix them.

---

## 11. Documentation deliverables

- `README.md`: what it is, stack, prerequisites, **one-command start** (`docker compose up`), manual setup, running tests, default dev credentials (dev only), project structure, troubleshooting.
- `docs/TECHNICAL_SETUP.md`: environments, config reference (every setting), DB migration & rollback procedure, deploy steps for a test environment (IIS/Docker/Azure — document at least one), backup notes, DB permission setup for the audit table.
- `docs/USER_GUIDE.md`: short, task-oriented, with screenshots for each role (how to create a project, assign, reassign, update progress, read the dashboard, export a report). Written for non-technical staff.
- `docs/SECURITY.md`: threat summary and controls.
- `docs/DECISIONS.md`: every **DECISION** and assumption you made, with rationale.
- `docs/ARCHITECTURE.md`: layers, data model diagram (Mermaid ER), reassignment sequence diagram.
- `db/schema.sql`: generated full schema script.
- `CHANGELOG.md` and per-phase reports in `docs/PHASE_REPORTS/`.

---

## 12. Engineering standards

- C# conventions, `nullable` enabled, `async` all the way, `CancellationToken` plumbed through.
- No business logic in controllers/views; controllers are thin.
- View models separate from entities (no over-posting: use explicit binding / `[Bind]` or dedicated input models).
- Centralized exception handling & `ProblemDetails` for non-HTML endpoints.
- Use `IClock`/`TimeProvider` abstraction so time-dependent logic is testable.
- EF: no lazy loading; explicit `Include`/projection; `AsNoTracking` for read paths; migrations are small, named, and reviewed; never edit applied migrations.
- Formatting enforced via `.editorconfig` + `dotnet format` in CI.
- Public/complex methods have XML docs or clear comments explaining *why*.

---

## 13. Edge cases the build must handle (checklist)

- [ ] Reassigning the only lead of a project
- [ ] Reassigning someone with open tasks
- [ ] Reassigning to a Completed/Cancelled/archived project
- [ ] Two managers reassigning the same employee concurrently
- [ ] Effective date in the past/future; before assignment start
- [ ] Employee removed then re-added to the same project later (new assignment row, history intact)
- [ ] Deactivating a user who leads projects or has active assignments/tasks (block or require reassignment first — **DECISION: block with a clear list of what must be resolved**)
- [ ] Deleting/deactivating a client linked to projects
- [ ] Changing a project lead (old lead remains a team member only if explicitly kept)
- [ ] Project marked Completed with incomplete tasks/milestones (warn, allow with confirmation)
- [ ] Reopening a Completed project (clears `actual_end_date`, logged)
- [ ] Derived progress with zero tasks
- [ ] Duplicate project code / client name (case-insensitive)
- [ ] Very long names/notes; emoji and non-ASCII text
- [ ] Concurrent edits to the same project (conflict page)
- [ ] Session expiry mid-form (preserve nothing sensitive; redirect with return URL)
- [ ] Export with zero rows / with 10k+ rows
- [ ] User with no role / role removed mid-session

---

## 14. Build phases and exit gates

Targets follow the brief's 7-week plan but **quality gates matter more than dates**.

| Phase | Scope | Exit gate (all must be true) |
|---|---|---|
| **1** | Repo, solution layout, CI, Docker, DB + migrations, Identity, login/reset/lockout, roles, user management (Admin), audit infrastructure, base layout & design system | App runs via `docker compose up`; login/reset work; role seeding works; user deactivate works; audit interceptor writes entries; auth matrix tests pass; CI green |
| **2** | Clients and Projects CRUD, project roles lookup, lead, list search/filter/sort/pagination, archive, concurrency | All 4.2 & 4.4 AC pass; permission tests pass; list perf verified with seed + load script |
| **3** | Team assignment, project team tab, employee list & profile (current/past projects) | 4.3 AC pass; point-in-time team query tested; filtered unique index verified |
| **4** | Reassignment workflow + notifications (in-app + outbox email), reassignment history views, audit log viewer | 4.6 & 4.8 AC pass; atomicity + concurrency tests pass; reconstruction test passes |
| **5** | Milestones, tasks, progress log, manual/derived progress, My tasks | 4.5 AC pass; derivation unit tests pass; employee task-scope enforced |
| **6** | Dashboard, workload view, 4 reports with Excel + PDF export | 4.7 AC pass; export-equals-screen tests pass |
| **7** | Hardening: full security review, accessibility pass, performance pass, docs, user guide with screenshots, seed polish, handover | Section 17 Definition of Done fully satisfied; all docs present; clean-clone run verified |

---

## 15. Phase Report template (output at the end of every phase)

```
## Phase N Report
- Scope delivered: ...
- Acceptance criteria status: (list each AC → ✅/❌)
- Tests: X unit, Y integration, Z E2E — all passing? (paste summary)
- Demo steps (click-path the client can follow, per role)
- Decisions made / assumptions (also logged in DECISIONS.md)
- Known issues / deferred items (with justification)
- Risks and questions for the client
- Next phase plan
```

---

## 16. Future-proofing (design for, do not build)

Keep seams for: file attachments (entity + storage interface), comments (polymorphic comment entity), notifications for deadlines (background job via `IHostedService`/Hangfire), Gantt/calendar (milestone/task dates already modeled), budget/time tracking (project-scoped entities), SSO (Identity external providers/OIDC), client-facing read-only view (separate policy + view models), integrations (service interfaces + API layer). Do **not** add speculative tables or UI for these now.

---

## 17. Definition of Done — "10/10" checklist

The project is complete **only when every box is true**:

**Functionality**
- [ ] Every requirement and AC in section 4 is implemented and demonstrable
- [ ] Reassignment is the only path for moving people; history reconstructs exactly
- [ ] All four reports export correctly to Excel and PDF and match on-screen data
- [ ] Audit log covers all mutating actions and is not editable anywhere

**Security**
- [ ] Server-side authorization verified by automated tests for every role × route
- [ ] CSRF, XSS, SQLi, IDOR, open-redirect, lockout, rate-limit tests pass
- [ ] No secrets in the repo; vulnerable-package scan is clean; security headers present

**Quality**
- [ ] All tests pass from a clean clone in CI; coverage targets met
- [ ] Zero compiler/analyzer warnings; formatted; no `TODO`/commented-out code/dead files
- [ ] No N+1 queries; performance budget met with seed + load data
- [ ] Friendly error pages; health endpoints work

**UX**
- [ ] Fully usable on mobile (360px) and desktop; consistent design; accessible (axe clean on key pages)
- [ ] Empty states, validation messages, confirmations, and success feedback everywhere

**Delivery**
- [ ] `docker compose up` yields a working, seeded app with documented demo logins (dev only)
- [ ] README, TECHNICAL_SETUP, USER_GUIDE (with screenshots), SECURITY, DECISIONS, ARCHITECTURE, schema.sql, CHANGELOG all present and accurate
- [ ] Git history is clean and meaningful; tagged release `v1.0.0`
- [ ] Final handover summary lists what's built, what's deferred, and recommended next steps

---

## 18. Working arrangement (from the client)

- Check in with the stakeholder at least **twice a week**; raise blockers early.
- **Demo each phase** before starting the next (use the Phase Report).
- **Ask when a requirement is unclear** rather than assuming — but batch questions and propose a default for each so work is never blocked.

---

## 19. Open questions for the client (build with the stated default; make easy to change)

| # | Question | Default used in this spec |
|---|---|---|
| 1 | Progress %: derived from tasks/milestones, manual, or both? (brief: "confirm with Paul") | **Both**, per-project `ProgressMode`, default Manual |
| 2 | Should Viewer/Executive be included now? | **Yes**, read-only |
| 3 | Is single sign-on needed soon (Microsoft Entra / Google)? | Not now; design Identity to allow it later |
| 4 | Who can reassign across projects the manager doesn't lead? | Admin only (3.2) |
| 5 | Office time zone and working-week definition | Africa/Nairobi, Mon–Fri (used only for display and "due soon") |
| 6 | Hosting target for the test environment (IIS, Azure, Docker host)? | Docker + documented IIS/Azure steps |
| 7 | Email provider/SMTP details | Local outbox in dev; SMTP via config |
| 8 | Final list of project roles and priorities | Seeded defaults in 4.3 / 4.2, Admin-editable |
| 9 | Over-allocation threshold and at-risk thresholds | Config defaults in 4.7 |
| 10 | Does "Employee" need to see colleagues' profiles? | Own profile only; project team lists visible on their projects |
| 11 | Data retention for audit logs | Keep indefinitely; no purge feature |

---

## 20. Final instruction to the agent

Build this as if it will be audited and handed to non-technical staff tomorrow. Prefer correctness, security, and clarity over cleverness. Do not skip tests, do not skip docs, do not leave anything half-built, and do not claim a phase is complete until its exit gate is verifiably met. When finished, deliver the final handover summary described in section 17.