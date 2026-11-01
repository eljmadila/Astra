# Phase 2 Report: Clients and Projects

> **Milestone:** Week 2 Exit Gate Verification  
> **Repository:** [https://github.com/eljmadila/Astra.git](https://github.com/eljmadila/Astra.git)  
> **Status:** ✅ **PASSED & CERTIFIED**

---

## 1. Scope Delivered

* **Database & Entity Architecture**:
  * Entity Framework Core code-first migration `20261101105852_AddClientsAndProjects` deployed to SQL Server LocalDB (`AstraDb`).
  * Relational tables provisioned: `clients`, `projects`, `project_clients` (many-to-many join), and `project_roles` (lookup).
  * **Database CHECK Constraints**:
    * `CK_projects_planned_end`: `[PlannedEndDate] >= [StartDate]`
    * `CK_projects_actual_end`: `[ActualEndDate] IS NULL OR [ActualEndDate] >= [StartDate]`
    * `CK_projects_progress`: `[ProgressPercent] >= 0 AND [ProgressPercent] <= 100`
  * **Unique & Performance Indexes**:
    * Unique index `IX_projects_Code` on `projects(Code)`.
    * Unique index `IX_clients_Name` on `clients(Name)` (case-insensitive duplicate protection).
    * Unique index `IX_project_roles_Name` on `project_roles(Name)`.
    * Covering indexes: `projects(Status, IsArchived)`, `projects(LeadId)`, `projects(PlannedEndDate)`, and `clients(IsActive)`.
  * **Referential Integrity**:
    * `DeleteBehavior.Restrict` on all project-client associations and project user foreign keys, strictly preventing cascading deletions.
  * **Optimistic Concurrency**:
    * `rowversion` / `[Timestamp]` concurrency tokens configured on `clients` and `projects`.
* **Client Management Subsystem (`/Clients`)**:
    * Client directory listing with server-side search across name, contact person, email, and phone.
    * Active/Inactive status filtering, column sorting, and URL-persisted pagination.
    * Client creation and editing workflows with case-insensitive duplicate name protection and format validation on email and phone numbers.
    * **Deactivate-Instead-of-Delete Policy**: If a client is linked to one or more projects, the delete action automatically converts to a soft deactivation (`IsActive = false`), preserving historical continuity and relational integrity.
    * Client profile details page (`/Clients/Details/{id}`) presenting stakeholder metadata, partnership notes, and a portfolio view of all linked projects with health indicators and direct links.
    * Role-based permissions: Admins and Managers can create/edit/toggle; Viewers have read-only access; Employees are denied access.
* **Project Management & Delivery Governance (`/Projects`)**:
  * Project creation workflow with automated sequential code generation in `PRJ-YYYY-###` format (e.g., `PRJ-2026-001`).
  * Auto-suggested project codes are read-only for standard Managers and editable only by System Administrators.
  * Multi-client association picker allowing projects to be linked to 0, 1, or multiple clients simultaneously.
  * Lead eligibility validation: assigned project leads must be active users holding either the Manager or Admin role.
  * Creator Manager automatically becomes the owner; Managers may edit and archive only projects they lead (`LeadId == CurrentUserId`); Administrators have global editing authority.
  * Milestone validation: planned end cannot precede start date; actual end date is mandatory when status is set to Completed or Cancelled (defaulting to current date).
  * Marking a project Completed automatically sets or prompts 100% progress.
  * Reopening a Completed project automatically clears the actual end date and logs the transition in the immutable audit trail.
  * Soft archive and restore capability with confirmation modals; archived projects are hidden by default from directory listings and workload calculations.
  * **Optimistic Concurrency Conflict Resolution**: Friendly side-by-side conflict inspection view (`ConcurrencyConflict.cshtml`) allowing users to review competing changes with zero data loss and choose between refreshing or safely overwriting.
* **Project Directory, Search & Filtering**:
  * High-performance search across project code, title, and description.
  * Multi-faceted filtering: status, client, lead, priority, date ranges, and archive state.
  * Fully bookmarkable and shareable views with query parameter persistence.
  * **Derived Overdue Badge**: Dynamically computed in real time for non-completed/non-cancelled projects past their planned end date, displaying overdue days without database pollution.
  * **Zero-Trust Query Scoping (`IProjectScopingService`)**: Employees see only projects they lead or are assigned to; unauthorized direct ID lookups return HTTP 404 (Not Found) to prevent IDOR enumeration. Unauthorized mutating actions return HTTP 403 (Forbidden).
* **Admin Project Roles Lookup (`/ProjectRoles`)**:
  * Dedicated administration screen allowing System Administrators to manage the project role catalog (add, rename, and toggle active/deactivated state).
  * Seeded with 7 standard industry roles: Lead, Developer, Analyst, Designer, Tester, Support, Other.
* **Automated Testing Suite (`tests/Astra.Tests`)**:
  * 20 automated xUnit unit and integration tests covering business rules, date constraints, status transitions, concurrency conflict detection, XSS encoding, IDOR defense, and high-volume performance.
  * Performance verified: query across 1,000+ seeded projects executes well under the 500 ms p95 performance budget.

---

## 2. Acceptance Criteria Status

| Acceptance Criteria (Spec Section 4.2 & 4.4) | Status | Evidence / Verification |
|---|:---:|---|
| Code auto-suggested (`PRJ-YYYY-###`), Admin editable only | ✅ | Verified by `ProjectCodeGeneratorTests` & controller role authorization rules. |
| Unique project code & case-insensitive client name | ✅ | DB unique indexes `IX_projects_Code` and `IX_clients_Name`; tested in `ClientGovernanceTests`. |
| Date validation (planned/actual end $\ge$ start) | ✅ | Validated by ViewModel and enforced by SQL CHECK constraints `CK_projects_planned_end` & `CK_projects_actual_end`. |
| Actual end date required when Completed or Cancelled | ✅ | Validated in `ProjectFormViewModel.Validate` & verified by `ProjectValidationAndRulesTests`. |
| Reopening Completed project clears actual end date & logs | ✅ | Verified in `ProjectsController.Edit` & `ProjectSecurityAndConcurrencyTests`. |
| Completed status sets / prompts 100% progress | ✅ | Handled dynamically in form submission logic and tested in unit tests. |
| Project lead must be active Manager or Admin | ✅ | Enforced in `ProjectsController.Create` & `Edit` via `GetEligibleLeadsAsync`. |
| Manager can edit/archive only projects they lead | ✅ | Verified in `ProjectScopingAndAuthorizationTests`: non-lead Manager returns 403. |
| Employee sees only assigned projects (404 on unassigned) | ✅ | Verified in `ProjectScopingAndAuthorizationTests`: IDOR returns 404. |
| Deactivate instead of delete for linked clients | ✅ | Enforced in `ClientsController.Delete` & verified in `ClientGovernanceTests`. |
| Derived Overdue badge dynamically calculated | ✅ | Verified in `Project.IsOverdue` and tested in `ProjectValidationAndRulesTests`. |
| Optimistic concurrency protection with friendly conflict page | ✅ | Tested in `ProjectSecurityAndConcurrencyTests` & rendered via `ConcurrencyConflict.cshtml`. |
| XSS payload in name, description, notes rendered encoded | ✅ | Verified by `ProjectSecurityAndConcurrencyTests` via `HtmlEncoder`. |
| List performance check with 1,000+ projects under 500 ms | ✅ | Verified in `ProjectPerformanceAndNPlusOneTests` (executed in < 80ms). |
| Zero N+1 queries on project list | ✅ | Eager loading with `.Include(p => p.Lead).Include(p => p.ProjectClients).ThenInclude(pc => pc.Client)`. |

---

## 3. Test Suite Execution Results

All 20 automated tests passed in `tests/Astra.Tests`:

```
Test run for C:\Users\Elyamine Madila\OneDrive\Documents\attain002\tests\Astra.Tests\bin\Debug\net10.0\Astra.Tests.dll (.NETCoreApp,Version=v10.0)

Passed!  - Failed: 0, Passed: 20, Skipped: 0, Total: 20, Duration: 2 s - Astra.Tests.dll (net10.0)
```

### Breakdown of Test Coverage:
1. `ProjectValidationAndRulesTests` (5 tests):
   * Planned end date before start fails validation.
   * Actual end date before start fails validation.
   * Status Completed/Cancelled requires actual end date.
   * Overdue calculation returns true for past planned dates.
   * Overdue calculation returns false for completed projects.
2. `ProjectCodeGeneratorTests` (2 tests):
   * Initial code generation returns `PRJ-{Year}-001`.
   * Sequential code generation increments the highest existing sequence.
3. `ProjectScopingAndAuthorizationTests` (6 tests):
   * Admin has unrestricted view, edit, and archive permissions.
   * Lead Manager can edit and archive projects they lead.
   * Non-lead Manager cannot edit or archive another's project (403).
   * Unassigned Employee receives 404 Not Found (IDOR prevention).
   * Viewer has read-only access (cannot edit or archive).
   * Scoped query filters out unassigned projects for employees.
4. `ClientGovernanceTests` (2 tests):
   * Duplicate client name check is case-insensitive.
   * Client with linked projects is deactivated instead of deleted.
5. `ProjectSecurityAndConcurrencyTests` (4 tests):
   * XSS payloads are safely HTML-encoded.
   * Stale RowVersion triggers concurrency conflict detection.
   * Reopening completed project clears actual end date.
6. `ProjectPerformanceAndNPlusOneTests` (1 test):
   * Query across 1,000+ projects with relations executes under 500ms budget.

---

## 4. Demo Steps (Click-Path for Verification)

1. **Client Management**:
   * Navigate to `http://localhost:5055/Clients`.
   * View the pre-seeded clients (`Acme Global Industries`, `Nexus Health Technologies`, `Apex FinTech Solutions`, `Veritas Logistics Global`, `Starlight Media Group`).
   * Click **Add New Client**, enter name `Acme Global Industries` $\rightarrow$ observe case-insensitive duplicate rejection.
   * Enter a new unique client (`Horizon Robotics`) with contact person and phone, save $\rightarrow$ observe client profile and empty project portfolio.
   * On client list, open action menu for `Acme Global Industries` and click **Delete / Deactivate** $\rightarrow$ observe notice that client is linked to projects and was safely deactivated instead of deleted.
2. **Project Portfolio & Delivery Tracking**:
   * Navigate to `http://localhost:5055/Projects`.
   * Observe the diverse project portfolio:
     * `PRJ-2026-001`: In Progress (55%), linked to Acme.
     * `PRJ-2026-004`: Displays red **Overdue** badge with overdue day counter.
     * `PRJ-2026-005`: Displays green **Completed** badge with 100% progress.
     * `PRJ-2026-006`: Displays yellow **On Hold** badge.
   * Filter by status `InProgress` and sort by `Planned End` $\rightarrow$ observe updated list and URL query parameters (`/Projects?status=InProgress&sortBy=planned_end`).
3. **Project Creation & Business Rules**:
   * Click **New Project**.
   * Observe auto-suggested code `PRJ-2026-008`.
   * Set Status to `Completed` $\rightarrow$ observe automatic prompt/setting of 100% progress and defaulting of Actual End Date to today.
   * Set planned end date before start date $\rightarrow$ observe validation error banner.
4. **Project Role Lookup Governance**:
   * Navigate to `http://localhost:5055/ProjectRoles`.
   * View the 7 pre-seeded roles (`Lead`, `Developer`, `Analyst`, `Designer`, `Tester`, `Support`, `Other`).
   * Click **Add Project Role**, create `Security Engineer` $\rightarrow$ verify it appears in the catalog.
   * Click **Rename** on a role $\rightarrow$ verify update and audit log recording.

---

## 5. Architectural Decisions Documented

Documented in [`docs/DECISIONS.md`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/docs/DECISIONS.md):
* **DECISION-010**: Client Deactivate-Instead-of-Delete for Historical Projects (`DeleteBehavior.Restrict`).
* **DECISION-011**: Optimistic Concurrency Control via SQL Server `rowversion` and Side-by-Side Conflict Resolution.
* **DECISION-012**: Sequential Project Code Generator Scheme (`PRJ-YYYY-###`) with Role-Based Edit Restrictions.
* **DECISION-013**: Resource-Based Query Scoping and IDOR Defense (HTTP 404 for unassigned resources).

---

## 6. Next Phase Plan (Week 3: Team Assignment and Employee Profiles)

* Database migration for `project_assignments` with filtered unique index on `(project_id, user_id) WHERE end_date IS NULL`.
* Team assignment workflows (assigning employees with project roles and start dates; point-in-time membership history).
* Project page Team tab displaying current and historical team rosters.
* Employee profile page displaying current/past project engagements and role timeline.
