# Astra — Architectural & Technical Decisions Log

This document records all architectural, architectural-compromise, and requirement decisions made for **Astra**, along with rationale and implications.

---

### DECISION-001: Server-Rendered ASP.NET Core MVC with Progressive Enhancement
* **Date:** 2026-10-27
* **Status:** Accepted
* **Context:** The build brief required choosing between an SPA (React/Vue/Angular) or Server-Rendered views.
* **Decision:** Use server-rendered ASP.NET Core MVC with Razor views and minimal vanilla JS / htmx for micro-interactions.
* **Rationale:** Maximizes native security (built-in CSRF anti-forgery token validation, server-side RBAC and view trimming), minimizes frontend build complexity, eliminates client-side bundle bloat, and provides fast initial page loads.

---

### DECISION-002: Four-Layer Clean Architecture Layout
* **Date:** 2026-10-27
* **Status:** Accepted
* **Context:** Need a maintainable, testable solution structure that avoids over-engineering while enforcing clear separation of concerns.
* **Decision:** Organize code into four projects:
  * `Portal.Domain`: Core POCO entities, enums, pure domain logic (zero external dependencies).
  * `Portal.Application`: Services, use cases, DTOs, validation, authorization requirements/handlers.
  * `Portal.Infrastructure`: EF Core `ApplicationDbContext`, SQL Server migrations, interceptors, email outbox, export generators.
  * `Portal.Web`: MVC controllers, ViewModels, Razor layouts, auth & middleware pipeline.
* **Rationale:** Keeps domain business rules completely independent of frameworks, UI, and database implementations; makes testing frictionless.

---

### DECISION-003: Project Brand Identity — "Astra"
* **Date:** 2026-10-27
* **Status:** Accepted
* **Context:** The project needed a distinct, authoritative enterprise product identity.
* **Decision:** Named the portal **Astra** (*Latin for Stars*, from *Per aspera ad astra*), with the tagline *Project Management & Team Allocation*.
* **Rationale:** Evokes precision celestial navigation and constellation networks, mirroring the portal's core purpose of navigating complex networks of projects, clients, and talent mobility.

---

### DECISION-004: Sneat Bootstrap 5 Admin Theme for UI Scaffolding
* **Date:** 2026-10-27
* **Status:** Accepted
* **Context:** Need an accessible, mobile-responsive, enterprise-grade design system without building custom CSS from scratch.
* **Decision:** Integrate the Sneat Bootstrap 5 Admin template into `Portal.Web`, stripping third-party promotional marketing links, cleaning demo views, and adapting layouts to be role-aware.
* **Rationale:** Provides out-of-the-box responsive navigation, rich tables, status badges, timelines, and dashboard cards, accelerating delivery while achieving a professional visual appearance.

---

### DECISION-005: Dual Progress Evaluation Modes (Manual vs. Derived)
* **Date:** 2026-10-27
* **Status:** Accepted
* **Context:** The client brief left open whether project progress percentage should be manually entered by leads or computed automatically.
* **Decision:** Support both modes on a per-project basis via `ProgressMode (Manual | Derived)`.
  * In **Manual** mode: Lead supplies percentage accompanied by a mandatory update note.
  * In **Derived** mode: Progress is automatically computed based on completed tasks / total non-cancelled tasks (falling back to milestones if no tasks exist).
* **Rationale:** Gives project leads flexibility for projects managed via lightweight milestones vs. granular task lists.

---

### DECISION-006: Reassignment Authority & Scope Enforcement
* **Date:** 2026-10-27
* **Status:** Accepted
* **Context:** Determining who has authority to reassign staff between projects.
* **Decision:**
  * **Admins** have unrestricted reassignment authority across all projects.
  * **Managers** may reassign staff *only* if they lead both the source project (for removals/transfers) and destination project (for additions/transfers). Moving someone from a project led by another manager strictly requires Admin intervention.
* **Rationale:** Prevents project leads from poaching staff or modifying staffing on projects outside their administrative purview.

---

### DECISION-007: Append-Only Immutable Audit Log with Database Enforced Immutability
* **Date:** 2026-10-27
* **Status:** Accepted
* **Context:** Compliance and client trust require tamper-resistant audit logs.
* **Decision:** `audit_logs` is append-only. No update/delete endpoints exist in application code. Furthermore, database-level least-privilege permissions (INSERT/SELECT only) and a SQL Server trigger reject UPDATE or DELETE operations.
* **Rationale:** Guarantees that historical audit entries cannot be altered, even by compromised admin accounts or developer error.

---

### DECISION-008: Soft User Deactivation (No Hard Deletes)
* **Date:** 2026-10-27
* **Status:** Accepted
* **Context:** User account lifecycle handling.
* **Decision:** Users with historical audit entries, assignments, or task history cannot be deleted. Accounts are soft-deactivated (`IsActive = false`), hiding them from selection pickers while retaining historical integrity. Active sessions of deactivated users are terminated on the subsequent request via security stamp validation.
* **Rationale:** Maintains relational integrity and audit reconstruction guarantees.

---

### DECISION-009: Transactional Outbox Pattern for Notifications
* **Date:** 2026-10-27
* **Status:** Accepted
* **Context:** Notifications (email and in-app) triggered during reassignment and critical project actions.
* **Decision:** Enqueue notifications into database outbox tables (`notifications` and `email_outbox`) inside the same atomic database transaction as the business operation. A background worker dispatches emails asynchronously with retry backoff.
* **Rationale:** Prevents external SMTP latency or failure from delaying or rolling back core business operations.

---

### DECISION-010: Client Deactivate-Instead-of-Delete for Historical Projects
* **Date:** 2026-11-01
* **Status:** Accepted
* **Context:** Preventing relational data loss when clients are associated with historical or active project engagements.
* **Decision:** When a deletion request is made on a client linked to one or more projects, the operation automatically converts to a soft deactivation (`IsActive = false`). Hard delete is only permitted for clients with zero project linkages. Foreign keys between `project_clients` and `clients` specify `DeleteBehavior.Restrict`.
* **Rationale:** Preserves historical reporting, audit trails, and financial linkages across project lifecycles.

---

### DECISION-011: Optimistic Concurrency Control via RowVersion and Side-by-Side Conflict Resolution
* **Date:** 2026-11-01
* **Status:** Accepted
* **Context:** Multi-user concurrent edits on project and client metadata.
* **Decision:** Implement native SQL Server `rowversion` / EF Core `[Timestamp]` concurrency tokens on `clients` and `projects`. If a `DbUpdateConcurrencyException` occurs, the controller captures the in-flight form data and the latest database record, rendering a dedicated `ConcurrencyConflict.cshtml` view presenting a side-by-side comparison, offering users the choice to discard & reload or safely overwrite without losing data.
* **Rationale:** Prevents the "last write wins" anomaly while eliminating silent data corruption.

---

### DECISION-012: Sequential Project Code Generator Scheme (PRJ-YYYY-###)
* **Date:** 2026-11-01
* **Status:** Accepted
* **Context:** Standardizing unique project identifiers across portfolios.
* **Decision:** Project codes follow the pattern `PRJ-{YEAR}-{SEQUENCE:D3}` (e.g. `PRJ-2026-001`). Suffixes increment based on the maximum existing code within the calendar year. The code is auto-suggested and read-only for standard Managers; only System Administrators may manually customize project codes.
* **Rationale:** Guarantees human-readable, consistent, and unique project identifiers while preventing unauthorized modification.

---

### DECISION-013: Resource-Based Query Scoping and IDOR Defense
* **Date:** 2026-11-01
* **Status:** Accepted
* **Context:** Securing project portfolio data from unauthorized access or IDOR enumeration.
* **Decision:** Implemented `IProjectScopingService` to scope queries before execution. Administrators, Viewers, and Managers have portfolio visibility; Employees only view projects they lead or are assigned to. If a user attempts to access an unauthorized project by ID, the application returns HTTP 404 (Not Found) rather than 403 to avoid confirming the resource's existence. Actions on visible but forbidden resources (e.g. non-lead Manager attempting to edit another's project) return HTTP 403 (Forbidden).
* **Rationale:** Zero-trust architecture strictly preventing information disclosure and privilege escalation.
