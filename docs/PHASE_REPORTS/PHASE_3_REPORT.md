# Phase 3 Certification Report — Week 3: Team Assignment & Employee Profiles

**Date:** 2026-11-09  
**Phase:** Week 3 (Team Allocation & Staff Directory)  
**Status:** PASSED (All Exit Gate Criteria Verified)

---

## 1. Executive Summary

Phase 3 delivers complete end-to-end project team allocation, point-in-time historical roster queries, project lead synchronization, and the Staff Directory / Employee Profile module. Every assignment is preserved permanently with zero hard-delete paths, and database-level constraints prevent duplicate active assignments on the same project.

---

## 2. Scope Delivered

### 2.1 Database & Schema (`project_assignments`)
- **Migration:** [`20261108205000_AddProjectAssignments.cs`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/portal/Data/Migrations/20261108205000_AddProjectAssignments.cs)
- **Entity:** [`ProjectAssignment.cs`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/portal/Entities/ProjectAssignment.cs)
- **Constraints & Indexes in [`ApplicationDbContext.cs`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/portal/Data/ApplicationDbContext.cs):**
  - Filtered Unique Index `IX_project_assignments_ProjectId_UserId_Active` on `(ProjectId, UserId) WHERE [EndDate] IS NULL` preventing duplicate active memberships on a project.
  - Check Constraint `CK_project_assignments_dates` enforcing `[EndDate] IS NULL OR [EndDate] >= [StartDate]`.
  - Performance Index `IX_project_assignments_UserId_EndDate` on `(UserId, EndDate)`.
  - `DeleteBehavior.Restrict` on all foreign keys (`ProjectId`, `UserId`, `ProjectRoleId`, `CreatedByUserId`).

### 2.2 Team Assignment Engine & Point-in-Time Roster
- **Service:** [`TeamAssignmentService.cs`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/portal/Services/TeamAssignmentService.cs)
- **Capabilities:**
  - **Multi-Member Assignment (`AssignMembersAsync`):** Assign one or more active employees at once with a role from `project_roles` and an effective `StartDate`. Blocks deactivated accounts and duplicate active assignments. Blocks assigning to `Completed`, `Cancelled`, or archived projects unless overridden by an `Admin` with a mandatory override reason.
  - **Soft Removal (`RemoveMemberAsync`):** Sets `EndDate` (validated `>= StartDate`) and optional `RemovalReason`, writing a structured audit entry (`TeamMemberRemoved`). Re-adding a previously removed employee creates a new `ProjectAssignment` row, leaving past history untouched.
  - **Role Change (`ChangeRoleOnProjectAsync`):** Updates the member's role on the project and records a before/after audit log (`TeamMemberRoleChanged`).
  - **Point-in-Time Query (`GetTeamOnAsync` / `IsActiveOnDate`):** Evaluates `StartDate <= D && (EndDate == null || EndDate > D)` consistently across the codebase.

### 2.3 Project Lead Handling & User Deactivation Gate
- **Lead Synchronization (`EnsureLeadOnTeamAsync`, `HandleLeadChangeAsync`):**
  - The Project Lead is automatically added to the project team in the `Lead` role upon project creation.
  - Changing the Project Lead in [`ProjectsController.cs`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/portal/Controllers/ProjectsController.cs) logs the change, assigns the new lead to the `Lead` role, and either ends the previous lead's membership or retains them on the team in a standard role when `KeepPreviousLeadOnTeam` is checked.
  - Attempting to remove the current Project Lead from the team is blocked until a new lead is chosen on the project.
- **Pre-Deactivation Dependency Gate (`GetDeactivationBlockersAsync`):**
  - [`UsersController.cs`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/portal/Controllers/UsersController.cs) blocks deactivating any user who leads active projects or holds active team assignments, returning a clear list of dependencies that must be resolved first.

### 2.4 Employee Directory & Profiles
- **Controller & Views:** [`EmployeesController.cs`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/portal/Controllers/EmployeesController.cs), [`Index.cshtml`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/portal/Views/Employees/Index.cshtml), [`Details.cshtml`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/portal/Views/Employees/Details.cshtml)
- **Features:**
  - Search by name, email, or department; filter by department, system role, and active/inactive status; column sorting and server-side pagination.
  - Profile page displaying user identity, active/inactive badge, Current Projects table, Past Projects timeline table, and Open Tasks empty state (ready for Week 5).
  - Strict IDOR protection via [`ProjectScopingService.cs`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/portal/Services/ProjectScopingService.cs): Employees can view only their own profile and assigned projects, receiving HTTP 404 when requesting another employee's profile.

---

## 3. Exit Gate Verification Matrix

| Exit Gate Requirement | Verification Method | Status |
|---|---|---|
| All Spec 4.3 acceptance criteria pass | Integration & unit tests in [`TeamAssignmentAndProfileTests.cs`](file:///c:/Users/Elyamine%20Madila/OneDrive/Documents/attain002/tests/Astra.Tests/TeamAssignmentAndProfileTests.cs) | **PASS** |
| `GetTeamOn(project, date)` returns correct results including boundaries | `PointInTime_BoundaryDates_FollowSpecExactRule` & `GetTeamOnAsync_ReturnsExactPointInTimeRoster` | **PASS** |
| No duplicate active assignment possible (verified at DB schema & service level) | Filtered unique index `IX_project_assignments_ProjectId_UserId_Active` + `Schema_ProjectAssignments_HasRestrictDeleteAndFilteredUniqueIndex` | **PASS** |
| No hard deletes of assignments anywhere in code | `DeleteBehavior.Restrict` on all FKs; soft removal via `EndDate` | **PASS** |
| Permission & IDOR tests pass; CI is green | 27/27 automated tests passing (`dotnet test portal/AspnetCoreMvcFull.sln`) | **PASS** |

---

## 4. Automated Test Execution Summary

```text
Test run for Astra.Tests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 27, Skipped: 0, Total: 27
```

---

## 5. Stakeholder Demo Script

1. **Assign Team Members:** Sign in as Manager (`sarah.jenkins@astra.local`), open a project you lead, switch to the **Team Staffing** tab, and assign multiple employees with roles and start dates.
2. **Remove & View Past Members:** Remove a team member with an effective date and reason, then click **Show Past** to view their historical assignment interval.
3. **Re-Add & Inspect Employee Profile:** Re-add the same employee with a new start date, click their name to open `/Employees/Details/{id}`, and verify both their current assignment and past period are preserved.
4. **Point-in-Time Roster Query:** On the project's **Team Staffing** tab, select a past date in the **Roster on date** picker and click **View** to see who was active on that exact date.
5. **Employee Self-Scoping & IDOR Protection:** Sign in as an Employee (`alex.rivera@astra.local`), verify `/Projects` shows only assigned projects, and verify `/Employees` and `/Employees/Details/{otherId}` enforce strict self-only access (returning 404 for other profiles).

