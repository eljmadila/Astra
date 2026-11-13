# DAYSTAR UNIVERSITY — STUDENT'S ATTACHMENT LOG-BOOK (ACS 300)

**SCHOOL OF SCIENCE, ENGINEERING AND HEALTH**  
**DEPARTMENT OF COMPUTER SCIENCE**  
**COURSE:** BSc. Applied Computer Science (ACS 300)  
**DURATION:** 12 Weeks  

---

## STUDENT'S PERSONAL INFORMATION (Page 3–4)

* **Name of Student (Surname first):** Madila, Elyamine
* **Registration No. of the Student:** *[Enter Registration Number]*
* **Faculty:** School of Science, Engineering and Health
* **Course of Study:** BSc. Applied Computer Science (ACS 300)
* **Stage / Year of Study:** Year 3 / Year 4 (Industrial Attachment — ACS 300)
* **Name and Address of Company / Establishment Attached:**  
  Attain Enterprise Solutions (Software Engineering & Digital Transformation Division)  
  P.O. Box 44400-00100, Nairobi, Kenya
* **Name of Industry-Based Supervisor:** Paul *(Project Stakeholder & Lead Engineering Supervisor)*
* **Designation:** Senior Software Engineering Manager / Product Owner
* **Telephone Contact / Mobile:** *[Enter Phone Number]*
* **Duration:**
  * **From:** October 19, 2026
  * **To:** January 8, 2027

---

## STUDENT'S WEEKLY PROGRESS CHART (Page 4)

| Week | Period | Department / Section | Core Milestone / Module Covered | Status |
|:---:|---|---|---|:---:|
| **Week 1** | Oct 19 – Oct 23, 2026 | Software Engineering & Security | Foundation, Authentication, RBAC, User Management & Immutable Audit Log | ✅ Completed |
| **Week 2** | Oct 26 – Oct 30, 2026 | Enterprise Portfolio Engineering | Clients, Projects, Project Roles, Optimistic Concurrency & Query Scoping | ✅ Completed |
| **Week 3** | Nov 2 – Nov 6, 2026 | Resource & Staffing Systems | Project Team Assignments, Filtered Unique Index, Point-in-Time Roster & Employee Profiles | ✅ Completed |

---

# WEEK ONE: Foundation, Login and Users (Pages 5–7)

| DAY | DESCRIPTION OF WORK DONE | NEW SKILLS LEARNT |
|---|---|---|
| **Mon.** | Attended attachment induction and project briefing for the **Astra Project Management & Team Allocation Governance Portal**. Analyzed the master specification (`docs/project.md`), established the 7-week build plan (`docs/PLAN.md`), and initialized the Git repository with `.gitignore`, `.editorconfig` (strict C# nullable reference types and analyzer rules), `Dockerfile`, and `docker-compose.yml` (ASP.NET Core + SQL Server). | Learned how to translate business briefs into technical architecture specifications (`ARCHITECTURE.md`), decision logs (`DECISIONS.md`), and multi-container Docker Compose configurations. |
| **Tue.** | Configured Entity Framework Core with SQL Server (`ApplicationDbContext`) and customized ASP.NET Core Identity with integer primary keys (`ApplicationUser`, `ApplicationRole`). Created and applied the initial code-first migration (`InitialIdentityAndAudit`) provisioning `users`, `roles`, `audit_logs`, `notifications`, and `email_outbox`. Deployed a SQL Server trigger (`TR_AuditLogs_PreventModification`) to block `UPDATE` and `DELETE` on `audit_logs`. | Learned EF Core Fluent API modeling, Identity schema customization, and enforcing database-tier append-only immutability using SQL Server triggers. |
| **Wed.** | Implemented the authentication engine in `AuthController.cs`: email/password login with secure cookies (`HttpOnly`, `SameSite=Lax`, sliding expiry), account lockout after 5 failed attempts for 15 minutes, 12-character password policy, forced password change on first login (`MustChangePassword`), single-use password reset tokens via `IEmailSender`, and open-redirect protection on `returnUrl`. | Learned secure session management in ASP.NET Core Identity, brute-force lockout rules, preventing user enumeration, and mitigating Open Redirect vulnerabilities. |
| **Thur.** | Configured the zero-trust authorization pipeline in `Program.cs` with a deny-by-default `FallbackPolicy` and role policies (`Admin`, `Manager`, `Employee`, `Viewer`). Configured `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero` for instant session revocation upon role change or deactivation. Built Admin User Management (`UsersController.cs`) with search, filter, create, role edit, and soft deactivation (`IsActive = false`, blocking deactivation of the last active Admin). | Learned how ASP.NET Core's Security Stamp Validator revokes active sessions immediately on account deactivation and why soft deactivation is used instead of hard deletes to preserve audit history. |
| **Fri.** | Implemented `AuditSaveChangesInterceptor` and `AuditService` to capture before/after JSON diffs (with secrets redacted) for all user and authentication events. Built the Admin Audit Log viewer (`/AuditLogs`) with filters and CSV export, integrated the Sneat Bootstrap 5 role-aware layout (`_VerticalMenu.cshtml`), and certified Phase 1 (`PHASE_1_REPORT.md`). | Mastered EF Core `SaveChangesInterceptor` to record entity mutations atomically within the same database transaction and configured security headers (HSTS, CSP, `X-Frame-Options: DENY`). |

### TRAINEE'S WEEKLY REPORT — WEEK ONE (Page 6)
During Week 1 of my attachment, I built the architectural foundation, authentication engine, role-based access control, user management, and immutable audit logging for the **Astra** portal. I configured Docker Compose with SQL Server, created the initial EF Core migration (`InitialIdentityAndAudit`), and enforced database-level audit immutability via the `TR_AuditLogs_PreventModification` SQL trigger. I implemented cookie authentication with a 12-character password policy, 5-attempt lockout protection, forced first-login password rotation, and zero-interval security stamp validation so deactivating a user or changing their role takes effect on the very next request. Finally, I delivered the Admin User Management module and the `/AuditLogs` viewer with automatic before/after JSON diff capture.

* **DATE:** October 23, 2026  
* **SIGNATURE:** *Elyamine Madila*

### FOR THE USE BY THE INDUSTRIAL SUPERVISOR ONLY (Page 7)
* **General comments on the student's progress:**  
  *____________________*
* **NAME OF THE SUPERVISOR:** Paul
* **DEPARTMENT / UNIT:** Software Engineering & Project Delivery
* **DATE:** October 23, 2026  
* **SIGNATURE:** *____________________*

---

# WEEK TWO: Clients and Projects (Pages 8–10)

| DAY | DESCRIPTION OF WORK DONE | NEW SKILLS LEARNT |
|---|---|---|
| **Mon.** | Designed and applied EF Core migration `AddClientsAndProjects` creating `clients`, `projects`, `project_clients` (many-to-many join), and `project_roles` (lookup table seeded with *Lead, Developer, Analyst, Designer, Tester, Support, Other*). Added SQL `CHECK` constraints (`PlannedEndDate >= StartDate`, `ActualEndDate >= StartDate`, `ProgressPercent BETWEEN 0 AND 100`), unique indexes, `RowVersion` concurrency tokens, and `DeleteBehavior.Restrict`. | Learned how to enforce relational integrity at the database level using SQL `CHECK` constraints, unique indexes, `DeleteBehavior.Restrict`, and SQL Server `rowversion` concurrency tokens. |
| **Tue.** | Built the Client Management module (`ClientsController.cs`) and Admin Project Roles lookup (`ProjectRolesController.cs`). Implemented client search, active/inactive filtering, pagination, case-insensitive duplicate name protection, client detail portfolio pages, and the **Deactivate-Instead-of-Delete** policy when a client is linked to any project. | Learned how to prevent orphaned project records by automatically converting delete requests into soft deactivations (`IsActive = false`) when foreign-key associations exist. |
| **Wed.** | Built Project Management (`ProjectsController.cs`) and `ProjectCodeGenerator` (`PRJ-YYYY-###` auto-suggested sequential codes, editable only by Admin). Implemented multi-client linking, lead eligibility checks (active Manager or Admin), project ownership rules (Managers can edit/archive only projects they lead), completion rules (setting 100% progress and requiring `ActualEndDate`), and clearing `ActualEndDate` with audit logging when reopening a Completed project. | Learned how to implement resource-based ownership rules (`LeadId == CurrentUserId`) and state-transition validations acrossViewModels, services, and database constraints. |
| **Thur.** | Built optimistic concurrency conflict resolution (`ConcurrencyConflict.cshtml`) catching `DbUpdateConcurrencyException` and displaying a side-by-side comparison of current database values vs. submitted form values. Implemented `ProjectScopingService` for IDOR defense (returning HTTP 404 for unassigned projects to Employees and HTTP 403 for forbidden edits) and derived `IsOverdue` calculation. | Learned how EF Core `[Timestamp]` / `rowversion` prevents lost updates and why returning HTTP 404 instead of 403 for unauthorized resources prevents IDOR enumeration attacks. |
| **Fri.** | Created the `tests/Astra.Tests` xUnit test project with 20 automated unit, authorization, IDOR, concurrency, XSS, and performance tests. Verified that querying 1,000+ seeded projects with eager loading (`.Include`) executes in under 80ms (budget < 500ms) with zero N+1 queries. Produced `PHASE_2_REPORT.md`. | Learned how to write xUnit tests for authorization matrices, optimistic concurrency collisions, HTML encoding against XSS, and 1,000+ row query performance benchmarks. |

### TRAINEE'S WEEKLY REPORT — WEEK TWO (Page 10)
During Week 2, I delivered the Clients, Projects, and Project Roles management subsystems (Phase 2 Milestone). I created the `AddClientsAndProjects` migration with SQL `CHECK` constraints, unique indexes, `DeleteBehavior.Restrict`, and `RowVersion` concurrency tokens. For Client Management, I built search, filtering, case-insensitive duplicate detection, and the deactivate-instead-of-delete safeguard for linked clients. For Project Management, I implemented `PRJ-YYYY-###` sequential code generation, lead eligibility validation, derived Overdue badges, soft archiving, URL-persisted multi-filter search, and a side-by-side optimistic concurrency conflict page (`ConcurrencyConflict.cshtml`). All 20 automated xUnit tests—including IDOR 404 verification and a <80ms performance benchmark on 1,000+ projects—passed.

* **DATE:** October 30, 2026  
* **SIGNATURE:** *Elyamine Madila*

### FOR THE USE BY THE INDUSTRIAL SUPERVISOR ONLY (Page 10)
* **General comments on the student's progress:**  
  *____________________*
* **NAME OF THE SUPERVISOR:** Paul
* **DEPARTMENT / UNIT:** Software Engineering & Project Delivery
* **DATE:** October 30, 2026  
* **SIGNATURE:** *____________________*

---

# WEEK THREE: Team Assignment and Employee Profiles (Pages 11–14)

| DAY | DESCRIPTION OF WORK DONE | NEW SKILLS LEARNT |
|---|---|---|
| **Mon.** | Modeled the `ProjectAssignment` entity (`project_assignments` table) and created EF Core migration `AddProjectAssignments`. Configured a SQL Server filtered unique index on `(ProjectId, UserId) WHERE [EndDate] IS NULL` (`IX_project_assignments_ProjectId_UserId_Active`), a `CHECK` constraint (`[EndDate] IS NULL OR [EndDate] >= [StartDate]`), a covering index on `(UserId, EndDate)`, and `DeleteBehavior.Restrict` on all foreign keys so assignments can never be hard-deleted. | Learned how SQL Server filtered unique indexes (`WHERE [EndDate] IS NULL`) enforce at most one active assignment per employee per project while allowing multiple historical closed rows when an employee is removed and later re-added. |
| **Tue.** | Built `TeamAssignmentService` (`ITeamAssignmentService`) implementing point-in-time roster queries `GetTeamOnAsync(projectId, date)` using the rule `StartDate <= D && (EndDate == null || EndDate > D)`. Implemented batch assignment of one or more active employees with roles and start dates, soft removal (setting `EndDate` and optional `RemovalReason` with audit logging), and role-on-project updates. | Learned temporal database query design for point-in-time roster reconstruction and how to test boundary dates (start day, end day, and same-day start and end). |
| **Wed.** | Implemented project lead team synchronization and governance rules: automatically adding the Project Lead to the team with the `Lead` role upon project creation; logging lead changes (`LeadChanged`) with an explicit option (`KeepPreviousLeadOnTeam`) to retain or close the previous lead's membership; blocking removal of the current Project Lead from the team until a new lead is chosen; and blocking assignments to Completed, Cancelled, or Archived projects (unless overridden by an Admin with a documented reason). | Learned how to enforce cross-entity domain invariants between Project leadership and Team Assignments and how to implement auditable administrative overrides. |
| **Thur.** | Activated the "Team Staffing" tab on `Views/Projects/Details.cshtml` with the current team table, a toggle to view past members (`ShowPastMembers`), a point-in-time date selector ("Roster on Date"), a multi-employee assignment modal, and a removal confirmation modal. Built `EmployeesController` and `Views/Employees/` (`Index.cshtml` and `Details.cshtml`) for the Staff Directory and Employee Profiles (displaying current projects, past projects, inactive status badges, and an Open Tasks empty state). | Learned how to build multi-row dynamic assignment forms in Razor/Bootstrap 5 and enforce strict IDOR scoping (`CanUserViewEmployeeProfile`) so Employees can view only their own profile (returning HTTP 404 for others). |
| **Fri.** | Updated `UsersController.ToggleStatus` to block deactivating a user who leads active projects or holds active team assignments, displaying a clear list of projects that must be resolved first. Expanded `DbInitializer.cs` with current and historical team assignments and a deactivated employee with preserved history. Added comprehensive Week 3 xUnit tests (`TeamAssignmentAndProfileTests.cs`) covering point-in-time boundaries, duplicate rejection, soft removal/re-addition, IDOR rules, and deactivation blockers, and generated `PHASE_3_REPORT.md`. | Learned how to implement pre-deactivation dependency checks across modules and verify via automated tests that no hard-delete paths exist for historical assignment records. |

### TRAINEE'S WEEKLY REPORT — WEEK THREE (Page 13–14)
During Week 3, I implemented the Team Assignment and Employee Profiles module (Phase 3 Milestone). On the database layer, I created the `project_assignments` table with a filtered unique index (`WHERE [EndDate] IS NULL`) and a date `CHECK` constraint (`EndDate >= StartDate`), ensuring no employee can hold two active assignments on the same project while preserving historical memberships forever when an employee is removed and later re-added. I built `TeamAssignmentService` to support assigning one or more active employees at once, soft-removing members with an effective end date and audit entry, synchronizing the Project Lead as a team member with the `Lead` role, blocking removal of the current lead, and executing point-in-time roster queries (`GetTeamOnAsync`). On the UI, I activated the Project "Team Staffing" tab, built the Staff Directory and Employee Profile views (`/Employees`) with strict IDOR protection (Employees see only their own profile and assigned projects, receiving 404 for others), updated user deactivation to block deactivating users with active assignments or led projects, and verified all requirements with automated xUnit tests.

* **DATE:** November 6, 2026  
* **SIGNATURE:** *Elyamine Madila*

### FOR THE USE BY THE INDUSTRIAL SUPERVISOR ONLY (Page 14)
* **General comments on the student's progress:**  
  *____________________*
* **NAME OF THE SUPERVISOR:** Paul
* **DEPARTMENT / UNIT:** Software Engineering & Project Delivery
* **DATE:** November 6, 2026  
* **SIGNATURE:** *____________________*
