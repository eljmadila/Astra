# Project Management Portal: Weekly Build Plan (Weeks 1 to 7)

> Companion to `PROJECT_PORTAL_AGENT_SPEC.md` (the master reference). If anything here conflicts with the spec, follow the spec and log the conflict in `docs/DECISIONS.md`.

## How to use this plan
1. Work one week at a time, in order. Do not start a week until the previous week's exit gate is fully ticked.
2. At the end of each week, run the demo script and output a Phase Report (template in spec section 15).
3. A task is done only when it is implemented, tested, reachable from the UI, authorized on the server and audited where applicable. No stubs, hardcoded data or TODOs.
4. Check in with the stakeholder at least twice a week; raise blockers early and ask when a requirement is unclear.

## Contents
- [Week 1: Foundation, Login and Users](#week-1-foundation-login-and-users)
- [Week 2: Clients and Projects](#week-2-clients-and-projects)
- [Week 3: Team Assignment and Employee Profiles](#week-3-team-assignment-and-employee-profiles)
- [Week 4: Reassignment Workflow and Audit Log (Key Requirement)](#week-4-reassignment-workflow-and-audit-log-key-requirement)
- [Week 5: Progress Tracking](#week-5-progress-tracking)
- [Week 6: Dashboard, Workload and Reports](#week-6-dashboard-workload-and-reports)
- [Week 7: Testing, Hardening and Handover](#week-7-testing-hardening-and-handover)

---

# Week 1: Foundation, Login and Users

> Reference: `PROJECT_PORTAL_AGENT_SPEC.md` (sections 2, 3, 4.1, 4.8, 6, 7). Follow it over any assumption.

## Goal
A running app where people can securely sign in and an Admin can manage users. This week sets the base every later week builds on, so correctness matters more than speed.

## Prerequisites
- None. This is the starting point.

## Tasks

### 1. Repository and tooling
- [ ] Initialize Git; add `.gitignore`, `.editorconfig`, `README.md`
- [ ] Create the solution layout: `Portal.Web`, `Portal.Application`, `Portal.Domain`, `Portal.Infrastructure`, `tests/Portal.UnitTests`, `tests/Portal.IntegrationTests`, plus `docs/` and `db/`
- [ ] Enable nullable reference types, analyzers and warnings-as-errors
- [ ] Add `Dockerfile` and `docker-compose.yml` (app + SQL Server) so `docker compose up` starts everything
- [ ] Add a GitHub Actions workflow: restore, build, test, format check
- [ ] Add `.env.example`; keep secrets out of the repo (User Secrets / environment variables)
- [ ] Write `docs/PLAN.md` and start `docs/DECISIONS.md`

### 2. Database
- [ ] Set up EF Core with SQL Server and code-first migrations
- [ ] Create the Identity tables, extended `ApplicationUser` (name, department, `is_active`, `must_change_password`, `created_at`, `last_login_at`)
- [ ] Create `audit_logs` (append-only) with the indexes from spec section 5
- [ ] Create `notifications` and `email_outbox` tables (used later, create now to avoid churn)
- [ ] Seed the four roles: Admin, Manager, Employee, Viewer
- [ ] Seed the first Admin from environment config (never a hardcoded password in the repo)

### 3. Authentication
- [ ] Email + password login with cookie auth (secure, HttpOnly, SameSite, sliding expiry)
- [ ] Account lockout: 5 failed attempts locks for 15 minutes
- [ ] Generic error messages (no user enumeration)
- [ ] Password policy: minimum 12 characters; hashed by Identity
- [ ] Forgot/reset password with a single-use, expiring token via `IEmailSender` (dev implementation writes to a local outbox/log)
- [ ] Forced password change on first login for seeded or newly created accounts
- [ ] Logout invalidates the session
- [ ] Rate limiting on login and reset endpoints
- [ ] Open-redirect protection on `returnUrl`

### 4. Authorization
- [ ] Deny by default: a fallback policy requires an authenticated user on every route
- [ ] Define the policies and handlers skeleton for the four roles (resource-based handlers come in later weeks)
- [ ] Role changes and deactivation take effect immediately (security stamp validation)

### 5. User management (Admin only)
- [ ] User list with search, filter by role/active, sort, pagination
- [ ] Create user (name, email, department, role); send a set-password email
- [ ] Edit user and change role
- [ ] Deactivate and reactivate users; never delete users
- [ ] A deactivated user cannot log in and an existing session is rejected on the next request
- [ ] Block deactivating the last active Admin

### 6. Audit infrastructure
- [ ] Central EF `SaveChanges` interceptor plus an explicit audit service for non-entity events
- [ ] Log: login success/failure, user create/update, role change, activate/deactivate
- [ ] Record user, action, entity, entity id, before/after details (secrets redacted), IP, timestamp
- [ ] No edit or delete endpoints for audit logs exist anywhere
- [ ] Document the least-privilege DB permission setup (INSERT/SELECT only on `audit_logs`) in `docs/SECURITY.md`

### 7. Base UI and design system
- [ ] Shared layout with role-aware navigation and breadcrumbs
- [ ] Bootstrap 5 theme, consistent buttons, forms, tables and badges
- [ ] Responsive from 360px to wide desktop, no horizontal page scroll
- [ ] Accessibility basics: labels, focus states, keyboard navigation, skip link, contrast
- [ ] Friendly 403, 404 and 500 pages with a correlation ID
- [ ] Toast messages for success and error feedback

### 8. Security baseline
- [ ] Anti-forgery tokens on every state-changing request
- [ ] Security headers: HSTS, CSP, X-Content-Type-Options, frame-ancestors, Referrer-Policy, Permissions-Policy
- [ ] HTTPS only; no stack traces or SQL errors shown in production
- [ ] Serilog structured logging with correlation IDs
- [ ] `/health` and `/health/ready` endpoints

## Tests required
- [ ] Unit tests for password and lockout rules where custom logic exists
- [ ] Integration tests (real SQL Server): login success and failure, lockout, reset token single-use and expiry, deactivated user rejected
- [ ] Table-driven authorization test: role x route x expected status for every route that exists this week
- [ ] CSRF token required on all POST routes
- [ ] Audit entry written for every mutating user-management action
- [ ] Migration applies cleanly on an empty database
- [ ] CI green from a clean checkout

## Deliverables
- Working repo with CI, Docker and migrations
- `README.md` with one-command start
- `docs/PLAN.md`, `docs/DECISIONS.md`, `docs/SECURITY.md` (initial)
- Phase 1 report (template in spec section 15)

## Exit gate (all must be true)
- [ ] `docker compose up` starts a working app
- [ ] Login, logout and password reset work end to end
- [ ] An Admin can create, edit, deactivate and reactivate users and change roles
- [ ] Roles are seeded and enforced on the server
- [ ] Audit entries are written for user and login events
- [ ] Authorization matrix tests pass; CI is green

## Demo script for the stakeholder
1. Start the app with one command; log in as the seeded Admin and change the password.
2. Create a Manager and an Employee; sign in as each and show what they can and cannot open.
3. Deactivate the Employee and show they are locked out.
4. Trigger a lockout with wrong passwords; reset a password by email.
5. Show the audit entries these actions created.

---

# Week 2: Clients and Projects

> Reference: `PROJECT_PORTAL_AGENT_SPEC.md` (sections 3, 4.2, 4.4, 5, 7). Follow it over any assumption.

## Goal
The office's projects and clients live in one place, with fast search, filters and correct permissions.

## Prerequisites
- Week 1 exit gate passed (auth, roles, user management, audit infrastructure).

## Tasks

### 1. Database
- [ ] Migration for `clients`, `projects`, `project_clients`, `project_roles` (seed: Lead, Developer, Analyst, Designer, Tester, Support, Other)
- [ ] Project CHECK constraints: planned end >= start, actual end >= start, `progress_percent` between 0 and 100
- [ ] Unique indexes: project `code`, client `name` (case-insensitive)
- [ ] `RowVersion` concurrency tokens on clients and projects
- [ ] Indexes: `projects(status, is_archived)`, `projects(lead_id)`, `projects(planned_end_date)`
- [ ] No cascading deletes; use Restrict on history-bearing tables

### 2. Client management
- [ ] Client list with search, sort, pagination, and an active/inactive filter
- [ ] Create and edit client: name, contact person, email, phone, notes (validate email and phone formats)
- [ ] Duplicate-name protection (case-insensitive) with a clear message
- [ ] Deactivate instead of delete when a client is linked to any project
- [ ] Client detail page listing all linked projects with status and lead
- [ ] Permissions: Admin and Manager can create and edit; Employee has no access; Viewer is read-only

### 3. Project management
- [ ] Create project with: code, name, description, client(s), lead, start date, planned end, actual end, status, priority
- [ ] Auto-suggest the code (for example `PRJ-2026-001`); only Admin may edit it
- [ ] Statuses: Not Started, In Progress, On Hold, Completed, Cancelled
- [ ] Priorities: Low, Medium, High, Critical
- [ ] Lead must be an active Manager or Admin
- [ ] Creator Manager becomes the owner; a Manager may edit and archive only projects they lead; Admin can edit all
- [ ] Validation: end dates not before start; actual end required when Completed or Cancelled (default today); Completed offers to set 100%
- [ ] Archive (soft) and restore; archived projects hidden by default
- [ ] Reopening a Completed project clears the actual end date and is logged
- [ ] Concurrency: a friendly conflict page when two people edit the same project

### 4. Project list
- [ ] Search across code, name and description
- [ ] Filters: status, client, lead, priority, date range, archived
- [ ] Sort on any column; server-side pagination (default 25)
- [ ] Filters and sort kept in the URL so views can be bookmarked and shared
- [ ] Derived badges: Overdue (computed, not stored); At Risk is added in Week 6
- [ ] Employees see only projects they are or were assigned to (the assignment table arrives in Week 3, so build the scoping query now with a clear extension point and a test)
- [ ] Return 404 for projects a user may not see, 403 for visible but forbidden actions

### 5. Admin lookup
- [ ] Admin screen to manage the project-role list (add, rename, deactivate)

### 6. Audit
- [ ] Log create, update, archive, restore, status change and client link changes with before/after details

### 7. UI
- [ ] Project detail page shell with tabs (Overview now; Team, Tasks, Progress, Reassignments added in later weeks)
- [ ] Forms with inline validation, preserved input on error, success toasts, confirmations for archive
- [ ] Empty states with guidance; mobile-friendly tables

## Tests required
- [ ] Unit tests: date rules, status rules, code generation, lead eligibility
- [ ] Integration tests: full CRUD for clients and projects; duplicate protection; deactivate-not-delete for linked clients
- [ ] Authorization matrix tests for every new route (Admin, Manager lead, Manager non-lead, Employee, Viewer, anonymous)
- [ ] IDOR test: guessing another project's ID returns 404 for an unauthorized user
- [ ] Concurrency test: second save of a stale form shows the conflict page and loses no data
- [ ] XSS payload in name, description and notes renders encoded
- [ ] List performance check with a seeded set of 1,000+ projects (page under 500 ms p95)
- [ ] No N+1 queries on the list page

## Deliverables
- Clients and projects fully working with permissions
- Updated seed data (clients and a spread of projects across statuses and priorities)
- Updated docs and decisions log
- Phase 2 report

## Exit gate (all must be true)
- [ ] All spec 4.2 and 4.4 acceptance criteria pass
- [ ] A Manager can create a project, link clients, and find it again with filters
- [ ] An Employee cannot edit projects; a non-lead Manager cannot edit another Manager's project
- [ ] Performance target met on the large seed set
- [ ] Permission and concurrency tests pass; CI is green

## Demo script for the stakeholder
1. As a Manager, create two clients and a project linked to both.
2. Filter and sort the list by status, client and lead; bookmark a filtered view.
3. Try to edit that project as another Manager and as an Employee (denied).
4. Mark the project Completed and show the end-date prompt; archive and restore it.
5. Open the same project in two browser tabs, save in both, and show the conflict page.

---

# Week 3: Team Assignment and Employee Profiles

> Reference: `PROJECT_PORTAL_AGENT_SPEC.md` (sections 3, 4.3, 5, 13). Follow it over any assumption.

## Goal
It is always clear who is on which project, in what role, and since when, with past membership preserved forever.

## Prerequisites
- Week 2 exit gate passed (clients, projects, project roles, permissions).

## Tasks

### 1. Database
- [ ] Migration for `project_assignments`: project, user, role on project, start date, end date (null while active), created by, created at
- [ ] Filtered unique index on (`project_id`, `user_id`) WHERE `end_date IS NULL` so nobody holds two active assignments on one project
- [ ] CHECK: `end_date IS NULL OR end_date >= start_date`
- [ ] Indexes: `project_assignments(user_id, end_date)`
- [ ] Restrict deletes; the code must never hard-delete an assignment

### 2. Team assignment on a project
- [ ] "Team" tab on the project page: current team table with name, role, start date
- [ ] Toggle to show past members with their start and end dates
- [ ] Assign one or more employees at once, each with a role (from the lookup) and a start date
- [ ] Only active users can be assigned; deactivated users are hidden from pickers by default
- [ ] An employee can be on several projects at the same time
- [ ] Removing a member sets an end date (effective date required, optional reason) and writes an audit entry
- [ ] Re-adding someone who was removed creates a new assignment row, leaving history intact
- [ ] Block assigning to Completed, Cancelled or archived projects (Admin override with a reason)
- [ ] Permissions: Admin on all projects; Manager on projects they lead; Employee and Viewer read-only
- [ ] Note: moving someone between projects is Week 4's reassignment workflow. This week handles add and remove on a single project only, and must not offer a "move" shortcut.

### 3. Project lead handling
- [ ] The project lead appears on the team as a member with the Lead role
- [ ] Changing the project lead is logged; the old lead stays on the team only if explicitly kept
- [ ] Block removing the current lead from the team until a new lead is chosen

### 4. Employee list and profiles
- [ ] Employee list with search, filter by department and active status, sort, pagination
- [ ] Profile page: name, email, department, role, active state
- [ ] Current projects (project, role on project, since) and past projects (role, date range)
- [ ] Open tasks section (empty until Week 5; render an empty state)
- [ ] Permissions: Admin and Manager see any profile; Viewer read-only; Employee sees their own profile only; return 404 for others
- [ ] A deactivated user remains visible in history and profiles, marked as inactive

### 5. Point-in-time query
- [ ] Application service `GetTeamOn(projectId, date)` returning who was on the team on any date
- [ ] Use the same rule everywhere: active on date D means `start_date <= D AND (end_date IS NULL OR end_date > D)`
- [ ] Unit tests cover boundary dates (start day, end day, same-day start and end)

### 6. Deactivation rule update
- [ ] Block deactivating a user who has active assignments or leads projects; show a clear list of what must be resolved first (the full unassign flow lands in Week 4)

### 7. Audit and UI
- [ ] Audit: assign, remove, role-on-project change, lead change
- [ ] Forms with inline validation, confirmations for removal, success toasts
- [ ] Mobile-friendly team tables

## Tests required
- [ ] Unit tests: point-in-time logic, overlap and duplicate rules, boundary dates
- [ ] Integration tests: assign, remove, re-add, toggle past members; filtered unique index rejects a duplicate active assignment even under concurrent requests
- [ ] A code or schema test proving no hard-delete path exists for assignments
- [ ] Authorization matrix for every new route; Employee cannot open another employee's profile
- [ ] IDOR tests on profile and team endpoints
- [ ] Audit entry exists for every assign and remove

## Deliverables
- Team management and employee profiles
- Seed data with current and historical assignments
- Updated docs
- Phase 3 report

## Exit gate (all must be true)
- [ ] All spec 4.3 acceptance criteria pass
- [ ] `GetTeamOn(project, date)` returns correct results, including boundaries
- [ ] No duplicate active assignment is possible, verified at the database level
- [ ] No hard deletes of assignments anywhere in the code
- [ ] Permission tests pass; CI is green

## Demo script for the stakeholder
1. As a Manager, add three people to a project with different roles and start dates.
2. Remove one person, then show them under "past members".
3. Re-add the same person and show both periods in their profile.
4. Ask "who was on this project last month?" using the date view.
5. Sign in as an Employee and show they see only their own profile and their own projects.

---

# Week 4: Reassignment Workflow and Audit Log (Key Requirement)

> Reference: `PROJECT_PORTAL_AGENT_SPEC.md` (sections 3.2, 4.6, 4.8, 5, 13). This is the heart of the product. Build it with extra rigor and extra tests.

## Goal
Every move of a person between projects goes through one controlled action and is recorded with who, from where, to where, when, by whom and why. The audit log is complete and cannot be edited.

## Prerequisites
- Week 3 exit gate passed (assignments, profiles, point-in-time team query).

## Tasks

### 1. Database
- [ ] Migration for `reassignments` (append-only): user, from project (nullable), to project (nullable), effective date, reason, performed by, tasks handling (Unassigned or Kept), created at
- [ ] CHECK: at least one of from/to is present; if both, from and to differ
- [ ] Index: `reassignments(user_id, effective_date)`
- [ ] No UPDATE or DELETE path for `reassignments` in the code
- [ ] Tighten `audit_logs` at the DB level: the app login has INSERT and SELECT only (trigger or permission denies UPDATE and DELETE); document in `docs/SECURITY.md` and `docs/TECHNICAL_SETUP.md`

### 2. Reassignment service (the only way to move people)
- [ ] One application service handles all three operations: **Move** (A to B), **Add** (none to B), **Remove** (A to none)
- [ ] Inputs: employee, from project, to project, effective date, reason (required, at least 10 characters), role on the new project (required when there is a destination)
- [ ] "Performed by" always comes from the session, never from user input
- [ ] **One database transaction** does all of: validate, close the old assignment (end date = effective date), open the new one (start date = effective date), insert the reassignment record, write the audit entry, queue notifications. If any step fails, nothing is saved
- [ ] Remove all direct "edit assignment" paths that could move someone silently (Week 3's add/remove stays for single-project actions but must also write through consistent rules)

### 3. Validation rules
- [ ] Employee must have an active assignment on the source project at the effective date
- [ ] Employee must not already be actively assigned to the destination project
- [ ] Source and destination must differ
- [ ] Effective date cannot be before the old assignment's start date
- [ ] Backdating: up to 30 days for Managers, unlimited for Admin (configurable)
- [ ] Future-dated moves allowed; views treat "current" as `start_date <= today AND (end_date IS NULL OR end_date > today)`
- [ ] Employee and both projects must be active and not archived; Completed or Cancelled destinations need an Admin override with a reason
- [ ] If the employee is the source project's lead, block until a new lead is chosen, with a clear message
- [ ] Open tasks on the source project: show the count and require a choice (unassign them, or keep them assigned and flagged); default is unassign; record the choice. Tasks arrive in Week 5, so build the hook now and test it with a stub task source
- [ ] Concurrency-safe: two simultaneous reassignments of the same employee cannot corrupt data (isolation level and row version checks)

### 4. Permissions (server-enforced)
- [ ] Admin: any reassignment
- [ ] Manager: only moves out of and into projects they lead
- [ ] Moving someone out of a project the Manager does not lead requires Admin
- [ ] Employee and Viewer cannot reassign (Viewer can see history)
- [ ] Unauthorized attempts return 403 or 404 and change no data

### 5. Reassignment UI (guided flow)
- [ ] Wizard: select employee, see their current projects, choose operation and destination, effective date, reason, role, review summary, confirm
- [ ] Warnings shown before confirm (lead block, open tasks, backdating limit)
- [ ] Optional notify checkbox, default on
- [ ] Only show actions the user is allowed to perform
- [ ] Entry points: employee profile, project Team tab, and a global Reassign page

### 6. Notifications
- [ ] `INotificationService` with an outbox so email failure never fails the reassignment
- [ ] In-app notification (bell with unread count, list page, mark as read)
- [ ] Recipients: the employee, the old lead and the new lead
- [ ] Email sender via `IEmailSender`, with retry and failure tracking in `email_outbox`; dev mode shows messages in a local outbox view

### 7. History views
- [ ] Per project: all moves in and out (Reassignments tab)
- [ ] Per employee: full timeline of moves
- [ ] Global history for Admin, Manager and Viewer with filters (employee, project, date range, performed by), sort and pagination
- [ ] Each row shows employee, from to to, effective date, reason, performed by, timestamp
- [ ] Corrections are new compensating reassignments, never edits

### 8. Audit log completion
- [ ] Ensure every mutating action in the app so far writes an audit entry (create, update, delete/archive, assign, reassign, remove, status change, role change, activate/deactivate, login success/failure, export hook for later)
- [ ] Admin-only viewer with filters (user, action, entity, date range), search, pagination and CSV export
- [ ] Details show before/after JSON, readable; secrets redacted
- [ ] No edit or delete endpoints exist for audit logs
- [ ] Deactivation flow completed: an Admin can resolve blockers by reassigning, then deactivate

## Tests required
- [ ] Unit tests for every validation rule, including boundary dates and role limits
- [ ] **Reconstruction test:** after any random sequence of moves, the assignment history rebuilds exactly the same timeline as the reassignment log
- [ ] **Atomicity tests:** force a failure at each step (old assignment closed, new opened, record inserted, audit written) and prove nothing is persisted
- [ ] **Concurrency test:** two parallel moves of the same employee produce one success and one clean failure
- [ ] Authorization matrix: Admin, lead Manager, non-lead Manager, Employee, Viewer, anonymous on every route
- [ ] A Manager cannot move someone out of a project they do not lead (no data changes)
- [ ] Notification outbox: failed email retries and does not roll back the reassignment
- [ ] Table-driven audit coverage: every mutating route writes an audit entry
- [ ] DB permission test: UPDATE or DELETE on `audit_logs` fails for the app login
- [ ] XSS payload in reason renders encoded

## Deliverables
- Reassignment workflow, notifications and history views
- Audit log viewer
- `docs/ARCHITECTURE.md` with the reassignment sequence diagram (Mermaid)
- Updated seed data with realistic reassignments
- Phase 4 report

## Exit gate (all must be true)
- [ ] All spec 4.6 and 4.8 acceptance criteria pass
- [ ] Atomicity, concurrency and reconstruction tests pass
- [ ] Managers cannot move people from projects they do not lead
- [ ] No code path moves a person without creating a reassignment record
- [ ] Audit log is complete, Admin-only and not editable anywhere
- [ ] CI is green

## Demo script for the stakeholder
1. As a Manager, move an employee from Project A to Project B with a reason; show the new team on both projects.
2. Show the history on both projects and on the employee's profile.
3. Try to move someone from a project the Manager does not lead (denied); do it as Admin (allowed).
4. Try to move a project lead (blocked with a clear message).
5. Show the in-app notifications and the dev email outbox.
6. As Admin, open the audit log, filter to the reassignment, and show before/after details.

---

# Week 5: Progress Tracking

> Reference: `PROJECT_PORTAL_AGENT_SPEC.md` (sections 3, 4.5, 5, 13). Follow it over any assumption.

## Goal
Anyone with access can see how far along each project is, what remains, and what changed and why.

## Prerequisites
- Week 4 exit gate passed (reassignment, notifications, full audit log).

## Tasks

### 1. Database
- [ ] Migration for `milestones`: project, title, due date, completed, completed on, timestamps
- [ ] Migration for `tasks`: project, title, description, assignee (nullable), due date, status (To Do, In Progress, Blocked, Done), timestamps, `RowVersion`
- [ ] Migration for `progress_updates` (append-only): project, note, progress percent, status at time, created by, created at
- [ ] Add `progress_mode` (Manual or Derived) to `projects`, default Manual
- [ ] Index: `tasks(assignee_id, status)`, `tasks(project_id)`, `milestones(project_id)`

### 2. Milestones
- [ ] Create, edit, delete or reorder milestones on a project (Admin and the project's lead)
- [ ] Mark done or not done; record the completion date
- [ ] Show overdue milestones clearly
- [ ] Audit every change

### 3. Tasks
- [ ] Create, edit and cancel tasks with title, description, assignee, due date and status (Admin and the project's lead)
- [ ] The assignee must be an active member of the project
- [ ] Employees can update the status of **only their own assigned tasks**; they cannot edit other fields or other people's tasks
- [ ] Viewer is read-only
- [ ] Task list on the project page with filters (status, assignee, overdue) and sorting
- [ ] "My tasks" page for any user: their open tasks across projects, grouped by due date, with quick status update
- [ ] Complete the Week 4 hook: when someone is reassigned off a project, their open tasks are unassigned or kept and flagged according to the chosen option
- [ ] Fill the empty "open tasks" section on the employee profile

### 4. Progress modes
- [ ] **Manual:** the lead enters the percentage
- [ ] **Derived:** percentage = completed tasks divided by total non-cancelled tasks; if there are no tasks, fall back to milestones (completed divided by total); if neither exist, 0
- [ ] Recalculate automatically whenever a task or milestone changes
- [ ] Show the mode on the project page, with a tooltip explaining it; the lead can switch modes
- [ ] Record the mode choice as a documented open question for the client in `docs/DECISIONS.md`

### 5. Progress updates and log
- [ ] "Update progress" form: new status and/or percentage plus a required note (3 to 1000 characters) and date
- [ ] Manual updates always require a note; derived recalculations write an automatic log entry
- [ ] Append-only timeline on the project page showing date, author, note, percent and status
- [ ] No edit or delete of log entries; corrections are new entries
- [ ] Rules from Week 2 remain: Completed implies 100% (offer to set it); warn if marking Completed with incomplete tasks or milestones and require confirmation

### 6. Permissions and edge cases
- [ ] Admin: everything; Manager: only projects they lead; Employee: own tasks only; Viewer: read-only
- [ ] Task and milestone IDs must be scoped to a project the user can see (IDOR protection)
- [ ] Edge cases: zero tasks, all tasks cancelled, task reassigned between projects not allowed (create a new task instead), deleting a milestone that affects derived percent, concurrent task edits (conflict page)

### 7. UI
- [ ] Tasks and Milestones tab, Progress log tab on the project page
- [ ] Progress bar with percentage and status badge (text plus color)
- [ ] Inline validation, confirmations, success toasts, empty states, mobile-friendly lists

## Tests required
- [ ] Unit tests for derived percentage: no tasks, all done, none done, all cancelled, mix, milestone fallback, rounding
- [ ] Integration tests: milestone and task CRUD, status updates, progress log append-only, mode switch
- [ ] Employee can update own task status and gets 403 or 404 on someone else's task or on editing other fields
- [ ] Authorization matrix for every new route
- [ ] Reassignment hook test with real tasks (unassign and keep-flagged paths)
- [ ] Audit entries for task, milestone and progress changes
- [ ] XSS payload in task title, description and notes renders encoded

## Deliverables
- Milestones, tasks, progress modes and the progress log
- "My tasks" page
- Updated seed data (tasks in every status, milestones, updates, one Derived and one Manual project)
- Phase 5 report

## Exit gate (all must be true)
- [ ] All spec 4.5 acceptance criteria pass
- [ ] Derived percentage is correct in all edge cases and covered by tests
- [ ] Employees can change only their own tasks
- [ ] The progress log is append-only and shows on the project page
- [ ] CI is green

## Demo script for the stakeholder
1. As a lead, add milestones and tasks to a project and assign them to team members.
2. As an Employee, update the status of your own task, then try someone else's (denied).
3. Show the derived percentage change as tasks are completed; switch a project to Manual and update it with a note.
4. Show the progress timeline and the warning when completing a project with open tasks.
5. Reassign an employee with open tasks and show both handling options.

---

# Week 6: Dashboard, Workload and Reports

> Reference: `PROJECT_PORTAL_AGENT_SPEC.md` (sections 3, 4.7, 8, 10). Follow it over any assumption.

## Goal
Management can see project health, workload and reports, and export them, without asking around.

## Prerequisites
- Week 5 exit gate passed (tasks, milestones, progress, full data to report on).

## Tasks

### 1. Configuration
- [ ] Options class for thresholds in `appsettings` (no magic numbers): at-risk window (14 days), at-risk progress (80%), blocked or overdue task share (30%), on-hold days (14), over-allocation limit (more than 4 active projects)
- [ ] Display time zone setting (default Africa/Nairobi, flagged for client confirmation)

### 2. Risk logic
- [ ] **Overdue:** planned end is in the past and status is not Completed or Cancelled
- [ ] **At Risk:** an In Progress project is at risk if any of these apply: planned end within 14 days with progress below 80%; overdue milestones; at least 30% of tasks Blocked or overdue. An On Hold project is also at risk once it has been On Hold for more than 14 days
- [ ] Computed in one domain service, reused by the dashboard, project list badges and reports
- [ ] Tooltip in the UI explaining the rule in plain language

### 3. Dashboard (role-aware)
- [ ] Projects by status: chart and numbers; each tile links to a pre-filtered project list
- [ ] At-risk and overdue projects list
- [ ] Recent reassignments (last 10) and recent progress updates (last 10)
- [ ] Admin, Manager and Viewer: full dashboard (Viewer read-only)
- [ ] Employee: personal dashboard (my projects, my open tasks, due soon)
- [ ] Efficient queries (projections, no N+1); target under 1 second

### 4. Workload view
- [ ] Table of employees with number of active projects, open tasks, and a simple bar
- [ ] Sortable and filterable by department; highlight over-allocation using the configured limit
- [ ] Click through to the employee profile
- [ ] Access: Admin, Manager and Viewer only

### 5. Reports hub
Four reports, each with on-screen view, filters, and Excel and PDF export:
- [ ] **Project status report:** code, name, client(s), lead, status, priority, progress, dates, overdue and at-risk flags
- [ ] **Team allocation report:** project, employee, role, start and end date; filter by project, employee, date range
- [ ] **Client-project report:** each client with its projects, statuses and leads
- [ ] **Reassignment history report:** employee, from to to, effective date, reason, performed by

### 6. Exports
- [ ] Excel via ClosedXML (or equivalent) and PDF via QuestPDF (check license fit and note it in `docs/DECISIONS.md`)
- [ ] Each export includes headers, generation timestamp, who generated it, and the applied filters
- [ ] Proper formatting: date formats, column widths, wrapped text, frozen header row
- [ ] Stream large exports instead of loading everything into memory
- [ ] Empty results export cleanly with headers only
- [ ] Sanitized file names; protect against spreadsheet formula injection (prefix cells starting with `=`, `+`, `-`, `@`)
- [ ] Every export is written to the audit log

### 7. Permissions
- [ ] Reports and exports: Admin, Manager and Viewer; Employee has no access
- [ ] Reports respect visibility rules; return 404 or 403 consistently

### 8. UI
- [ ] Charts that are accessible (text alternative and data table fallback)
- [ ] Responsive layout; export buttons clearly labeled
- [ ] Loading and empty states

## Tests required
- [ ] Unit tests for at-risk and overdue rules, including every threshold boundary
- [ ] Integration tests: dashboard numbers equal database truth for each role
- [ ] **Export equals screen:** totals and rows in Excel and PDF match the on-screen report for the same filters
- [ ] Empty dataset, 10,000+ row export, and special characters (formula injection, non-ASCII) handled
- [ ] Authorization matrix for the dashboard, workload view and every report and export route
- [ ] Audit entry written for each export
- [ ] Performance: dashboard under 1 second and reports under budget with the large seed set
- [ ] No N+1 queries on dashboard and reports

## Deliverables
- Dashboard, workload view, four reports with Excel and PDF export
- Large seed set or load script for performance checks
- Phase 6 report

## Exit gate (all must be true)
- [ ] All spec 4.7 acceptance criteria pass
- [ ] Exported numbers equal on-screen numbers
- [ ] Each role sees the correct dashboard and report access
- [ ] Performance targets met; CI is green

## Demo script for the stakeholder
1. Open the dashboard as Admin; click a status tile to land on the filtered project list.
2. Show an at-risk and an overdue project and hover the explanation tooltip.
3. Open the workload view and point out the over-allocated employee.
4. Run each report with a filter and export to Excel and PDF; compare to the screen.
5. Sign in as an Employee and show the personal dashboard and the blocked report access.

---

# Week 7: Testing, Hardening and Handover

> Reference: `PROJECT_PORTAL_AGENT_SPEC.md` (sections 6 to 13, 17). This week proves the product is finished. No new features.

## Goal
A polished, secure, documented product that runs from a clean checkout, deployed to a test environment and ready for handover.

## Prerequisites
- Week 6 exit gate passed. All features from the brief's "Must have" list exist.

## Tasks

### 1. Feature completeness audit
- [ ] Walk through spec section 4 line by line; mark every acceptance criterion pass or fail with evidence
- [ ] Walk through the edge-case checklist in section 13 and test each item
- [ ] Fix every failure; do not defer a Must-have item
- [ ] Confirm nothing from the "Nice to have" list was built; extension points exist only as interfaces

### 2. Security review
- [ ] Review against the OWASP Top 10 and record results in `docs/SECURITY.md`
- [ ] Verify: parameterized queries only, no `Html.Raw` on user content, CSP active, anti-forgery on all mutations, no state changes on GET
- [ ] Re-run role x route matrix across the whole app; confirm deny by default
- [ ] IDOR sweep: try every ID-based route with another user's IDs
- [ ] Verify lockout, rate limiting, open-redirect protection, secure cookies, security headers
- [ ] No secrets in the repo or git history; `.env.example` complete
- [ ] `dotnet list package --vulnerable` clean; Dependabot configured
- [ ] Production error pages hide stack traces and SQL details; correlation IDs work
- [ ] Verify the least-privilege database login and the audit table permissions

### 3. Performance pass
- [ ] Load the seed set at the budget size: 500 projects, 300 users, 20k tasks, 100k audit rows
- [ ] Confirm list and detail pages under 500 ms p95 and the dashboard under 1 second
- [ ] Review query logs for N+1 queries; add or fix indexes
- [ ] Confirm all list endpoints paginate on the server

### 4. Accessibility and usability pass
- [ ] Run automated axe checks on key pages; fix issues
- [ ] Keyboard-only walkthrough of the main flows; visible focus everywhere
- [ ] Color contrast and "never color alone" for status and priority badges
- [ ] Test on 360px mobile and wide desktop; no horizontal page scroll
- [ ] Review wording: plain language, helpful hints and tooltips, consistent labels

### 5. Test suite
- [ ] Coverage at least 80% on Domain and Application; 100% of authorization and reassignment logic exercised
- [ ] Add or confirm the E2E smoke test (Playwright): login, create client, create project, assign, reassign, update progress, view dashboard, export a report
- [ ] Remove or fix any flaky tests
- [ ] All tests pass in CI from a clean checkout

### 6. Code quality cleanup
- [ ] Zero compiler or analyzer warnings; formatting enforced
- [ ] Remove dead code, commented-out code, unused files and every `TODO`
- [ ] Thin controllers; business rules in Domain and Application
- [ ] Review migrations: small, named, none edited after being applied

### 7. Seed data polish
- [ ] Final dev and test seed: 1 Admin, 3 Managers, 12 Employees, 1 Viewer; 8 to 10 clients; 15 to 25 projects across all statuses
- [ ] Includes edge cases: over-allocated employee, deactivated user with history, archived project, Derived and Manual progress projects, overdue and at-risk examples
- [ ] Demo credentials only in Development; never seeded in Production

### 8. Documentation
- [ ] `README.md`: purpose, stack, prerequisites, one-command start, manual setup, tests, structure, troubleshooting
- [ ] `docs/TECHNICAL_SETUP.md`: environments, every config setting, migration and rollback steps, test-environment deployment (at least one of Docker, IIS or Azure), backups, audit-table permissions
- [ ] `docs/USER_GUIDE.md`: short, task-based, with screenshots per role (create a project, assign, reassign, update progress, read the dashboard, export a report)
- [ ] `docs/SECURITY.md`, `docs/DECISIONS.md` (every decision and assumption), `docs/ARCHITECTURE.md` (layers, ER diagram, reassignment sequence)
- [ ] `db/schema.sql` generated; `CHANGELOG.md` written
- [ ] All seven phase reports in `docs/PHASE_REPORTS/`

### 9. Deployment and release
- [ ] Deploy to a test environment and run the smoke test there
- [ ] Verify `docker compose up` from a fresh clone
- [ ] Tidy Git history; tag `v1.0.0`
- [ ] Write the final handover summary: what was built, what is deferred (Nice-to-have list), known issues, recommended next steps, and the unresolved client questions from spec section 19

## Final Definition of Done (every box must be true)

**Functionality**
- [ ] Every requirement and acceptance criterion in spec section 4 is implemented and demonstrable
- [ ] Reassignment is the only path for moving people; history reconstructs exactly
- [ ] All four reports export correctly to Excel and PDF and match on-screen data
- [ ] Audit log covers all mutating actions and is not editable anywhere

**Security**
- [ ] Server-side authorization verified by automated tests for every role x route
- [ ] CSRF, XSS, SQL injection, IDOR, open-redirect, lockout and rate-limit tests pass
- [ ] No secrets in the repo; vulnerability scan clean; security headers present

**Quality**
- [ ] All tests pass from a clean clone in CI; coverage targets met
- [ ] Zero warnings; formatted; no dead code or TODOs
- [ ] No N+1 queries; performance budget met
- [ ] Friendly error pages; health endpoints work

**UX**
- [ ] Fully usable on mobile and desktop; consistent design; accessible
- [ ] Empty states, validation messages, confirmations and success feedback everywhere

**Delivery**
- [ ] `docker compose up` gives a working, seeded app
- [ ] All documents present and accurate
- [ ] Clean Git history; `v1.0.0` tagged
- [ ] Final handover summary delivered

## Demo script for the stakeholder
1. Fresh clone, one command, working app.
2. Full tour as each role: Admin, Manager, Employee, Viewer.
3. Walk the key flow end to end: create, assign, reassign, update progress, dashboard, export.
4. Show the audit log and the security and test results.
5. Hand over the repo, documents and the list of open client questions.