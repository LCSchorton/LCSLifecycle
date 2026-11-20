# LCS Lifecycle Manager

Internal student identity and iPad lifecycle application for Lanham Christian School. This foundation contains an IIS-hostable staff portal and an initial SQL Server schema. The portal currently shows setup status. Application data access and vendor integrations are upcoming milestones.

## Architecture

Use one ASP.NET Core 10 Razor Pages application and one SQL Server 2022 Express instance on the dedicated test VM. Keep Razor Pages responsible for staff interaction. Add feature services for roster reconciliation, student identities, iPad inventory, and case workflows. Give each external integration its own adapter and credential. Use versioned SQL scripts deployed by an operator and a lower-privilege application database identity.

FACTS owns student identifiers and enrollment status. Entra object IDs link to students after verified matching. Apple and Securly supply timestamped observations keyed to the device serial. The application records physical custody and ownership as staff-confirmed history. Store departures as explicit evidence with an effective date and provenance. Missing students from any roster lead to a review item only.

When external mutations are introduced, persist action intents and idempotency keys in SQL before calling providers. Use a scheduled worker with atomic job claiming, bounded retries and state verification after timeouts. Keep that worker independent of IIS app-pool uptime. Keep human workflow steps in the case record, and append staff or service actions to the audit trail. Grant the runtime audit table insert/read access without update or delete.

## Test VM setup

1. Verify the Hyper-V host's core licensing, guest activation method, and applicable CAL coverage under the school's agreement. Use the proposed 2 vCPU, 4 GB RAM and 80 GB starting allocation and measure headroom under IIS, SQL, updates and backup load. Set a SQL memory cap after measuring OS requirements.
2. Install IIS and the supported .NET 10 Hosting Bundle. Configure an HTTPS binding on an internal DNS name accessible on the school LAN and VPN. Use a dedicated app pool identity and keep SQL traffic local. The browser and VM need outbound access to Entra.
3. Register a single-tenant Entra web app with HTTPS redirect URI ending in `/signin-oidc`. Define user app roles `Lcs.Registrar`, `Lcs.DeviceStaff`, and `Lcs.Administrator`. Set the enterprise application to require assignment and assign dedicated test staff. The web app requires no Graph application permissions.
4. Configure `Entra__TenantId`, `Entra__ClientId`, and `Entra__ClientSecret` outside source control and publish output. Limit access to the deployment operator and app pool identity. Local development can use .NET user secrets. Before production, settle certificate-backed credentials and rotation and configure persistent, protected ASP.NET Core Data Protection keys.
5. Build with `dotnet build src/Lcs.Web -c Release` and publish with `dotnet publish src/Lcs.Web -c Release`. Serve the publish directory from a dedicated IIS site using the default in-process hosting model. Keep test student and device data synthetic.
6. Run `database/001-foundation.sql` against a dedicated empty test database with `sqlcmd -b` or an equivalent deployment runner. The script is transactional and intended for one application. The portal does not yet connect to the schema.
7. Verify HTTPS and a 200 on `/health/live`, successful staff role access, and denial for an unassigned identity. The probe checks process liveness. Add SQL readiness when the application consumes the database. Use ASP.NET Core operational logging without request bodies, tokens, or roster contents.

## Review milestones

| Milestone | Review acceptance |
| --- | --- |
| 1. Foundation | Private repository review, clean build, IIS deployment on the dedicated VM, verified staff and denied test sign-in, SQL schema successfully applied to the test instance. |
| 2. Read-only reconciliation | FACTS and an approved CSV template tested with synthetic records. Graph student inventory scoped in the app. Apple and Securly device records matched by serial. Stale feeds, incomplete rosters, and ambiguous matches surfaced for manual review. |
| 3. iPad workflows | Assign, return, replace, and record damage, loss, or theft. Walk staff through each manual management step. Verify simultaneous attempts cannot claim the same device and that cases resume after interruption. |
| 4. Identity automation | Demonstrate onboarding, resumable license and group assignment, and confirmed-departure disabling and session revocation using named test identities. Load-test batch limits, alerting, and action retry. Add separately authorized deletion only after retention terms are approved. |
| 5. Production readiness | Review credentials and permissions, backups and a successful restore, privacy and retention, monitoring, production VM decisions, and rollback. Review each deployment through a PR. |

## Decisions before integration or live actions

- **FACTS** — The vendor documents a OneRoster integration configured through SIS Integrations. Confirm LCS entitlement, pricing, endpoint and access scope with FACTS. Validate term boundaries, stable student IDs, departure status and effective-date semantics against actual exports or responses. If OneRoster only offers active records, use explicit FACTS evidence verified by a registrar. A controlled CSV import needs staging, duplicate and scope checks, counts, preview, approval, and retained batch metadata.
- **Microsoft and Google** — Confirm the existing SKU, usage location, disabled Exchange service plans, group object ID, current provisioning and federation path, username normalization and collision policy, and password delivery. Separate staff sign-in, Graph read and mutating credentials. Treat application privileges as potentially tenant-wide, and constrain writes in the app to verified object IDs and designated test identities. Confirm Google deprovisioning and session behavior separately.
- **Devices** — Check Apple School Manager API role availability and read operations in the school tenant. Confirm the Securly MDM product and plan, supported inventory API or export, serial field, last-check-in meaning, and cost. The school must confirm ownership, repair and loaner rules, handoff authorization, activation lock handling, and when a device may be permanently released. Record MDM removal, ASM unassignment, and ASM release as distinct actions.
- **Offboarding controls** — Agree on effective dates, authorized roles, retention and legal holds, alert recipients, and per-run and rolling action limits before enabling writes. Reject stale or unvalidated batches, pause unusual volume, and require explicit approval for deletion. Verify authorized test scope before any automation calls.
- **Operational ownership** — Confirm internal DNS and certificate owner, staff role assignments, retention schedule, encrypted off-VM backups, and recovery responsibilities. Follow LCS privacy rules for synthetic tests, minimal personal data, and access reviews.

## Vendor references

- [Microsoft .NET on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
- [SQL Server 2022 operating system requirements](https://learn.microsoft.com/en-us/sql/sql-server/install/hardware-and-software-requirements-for-installing-sql-server-2022)
- [Windows Server virtualization licensing guidance](https://www.microsoft.com/licensing/guidance/Windows-Server-Virtualization-Technologies)
- [Microsoft Entra app roles](https://learn.microsoft.com/en-us/entra/identity-platform/howto-add-app-roles-in-apps)
- [Apple School Manager API](https://developer.apple.com/documentation/appleschoolmanagerapi)
