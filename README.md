# Astra

> **Enterprise Project Management & Team Allocation Portal**  
> Unified delivery tracking, client project linkage, and tamper-resistant staffing mobility governance.

---

## 🌟 Overview

**Astra** is an enterprise web portal designed to provide single-pane-of-glass visibility and governance across an organization's projects, client engagements, and human capital allocations:

* **Who is working on what?** Full visibility into team assignments, project roles, active workload, and historical involvement.
* **Which clients own each project?** Multi-client association per project with contact details and project health metrics.
* **How far along is each project?** Automated milestone tracking, task delivery metrics, dual-mode progress calculation (Manual and Derived), and proactive **At Risk / Overdue** early-warning indicators.
* **Staff Reassignment Mobility**: Atomic, tamper-resistant reassignment workflows with historical reconstruction, reason tracking, task handling, and non-blocking outbox notifications.
* **Tamper-Resistant Auditing**: Centralized, append-only audit trail capturing mutating actions with before/after state JSON and DB-level least-privilege immutability.

---

## 🛠️ Technology Stack

| Layer | Technology |
|---|---|
| **Framework** | ASP.NET Core MVC on .NET (C#) |
| **Database** | Microsoft SQL Server (LocalDB / Docker / Production) |
| **ORM** | Entity Framework Core (Code-First Migrations, Concurrency Tokens) |
| **Authentication** | ASP.NET Core Identity (Secure Cookies, PBKDF2, Lockout Protection) |
| **Authorization** | Policy-Based & Resource-Based Handlers (Zero-Trust Fallback) |
| **UI & Design System** | Bootstrap 5.3.3 (Sneat Admin Theme) + Progressive Vanilla JS |
| **Reporting & Export** | ClosedXML (Excel `.xlsx`) & QuestPDF (`.pdf`) |
| **Logging** | Serilog (Structured JSON Logging & Correlation IDs) |

---

## 🚀 Quick Start

### Prerequisites
* [.NET SDK](https://dotnet.microsoft.com/download) (v8.0 LTS or v10.0 Preview)
* [SQL Server](https://www.microsoft.com/sql-server) (LocalDB, SQL Express, or Docker container)

### Running Locally

1. **Clone the repository:**
   ```bash
   git clone https://github.com/eljmadila/Astra.git
   cd Astra
   ```

2. **Navigate to the web portal project:**
   ```bash
   cd portal
   ```

3. **Restore & run with hot reload:**
   ```bash
   dotnet watch
   ```

4. Open your browser and navigate to the local HTTPS URL shown in the console (e.g. `https://localhost:5001` or `http://localhost:5000`).

---

## 📖 Documentation Index

* **[Architecture & Design Specification](docs/ARCHITECTURE.md)**: Solution boundaries, clean layering, database schema (Mermaid ER), reassignment sequence workflow, security matrix, and layout hierarchy.
* **[Agent Build Specification](docs/project.md)**: Complete functional requirements, acceptance criteria, role matrix, and business rules.
* **[Weekly Build Plan](docs/weeklyGoal.md)**: 7-week milestone roadmap and quality exit gates.

---

## 🔒 Security Baseline

* **Zero-Trust**: Deny-by-default fallback policy across all non-public endpoints.
* **Anti-Forgery (CSRF)**: Automatic anti-forgery token verification on all state-changing requests.
* **Audit Immutability**: Application database login granted `INSERT` and `SELECT` permissions only on `audit_logs`, backed by database prevention triggers.
* **Protection**: Parameterized queries (SQLi prevention), Razor HTML auto-encoding (XSS mitigation), open-redirect defenses, and identity lockout after 5 failed attempts.

---

## 📄 License
This project is licensed under the MIT License.
