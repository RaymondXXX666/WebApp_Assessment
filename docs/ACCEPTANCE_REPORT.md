# CreditWorks Vehicle Register — Acceptance Report

**Date:** 2 October 2026

**Result:** Passed for the scope below

**Final reviewer-path check:** Anonymous cloning of the public repository, fresh-package restore, isolated-database initialization, 93 automated tests and 62 additional HTTP checks passed. See the final section for the follow-up review and its scope.

**Related documents:** [README](../README.md), [System Design](SYSTEM_DESIGN.md), [Data Flow](DATA_FLOW.md)

## Environment and isolation

Acceptance used an independent source export containing tracked files and the pending submission files. Existing `bin`, `obj` and Git metadata were excluded. Dependencies were restored into a new, empty package directory with HTTP package-cache reuse disabled for the initial restore.

The environment was the existing macOS host with .NET SDK **10.0.401** and the local SQL Server container image `mcr.microsoft.com/mssql/server:2022-latest`. A uniquely named `CreditWorksAcceptance_<guid>` database was created for HTTP checks. Integration tests created their own `CreditWorksTests_<guid>` database. The application used a temporary loopback port and a connection-string override supplied through the process environment.

This verifies fresh source/build state and fresh databases on an existing development machine. It is not a fresh operating-system installation or a cross-platform certification. The existing application database and running application were not used for acceptance writes.

## Build, initialization and tests

| Check | Result |
| --- | --- |
| NuGet restore into an empty package directory | Passed |
| Local EF tool restore from the repository manifest | Passed |
| Solution build | Passed; **0 warnings, 0 errors** |
| Initial EF migration against a new database | Passed |
| Actual application seeding through the migration command | Five manufacturers, three categories, zero vehicles |
| Repeat migration | Passed; no duplicate seed data |
| Default test command | **50 passed**; opt-in SQL test methods explicitly skipped |
| Full suite with SQL tests enabled | **93 passed, 0 failed, 0 skipped** |

The default run reported 16 skipped test methods. Skipped theory methods are not expanded into their individual data cases in that report. The full run expanded and executed all cases.

The documented build/migration/test workflow was used, with an isolated database and temporary port replacing the normal development targets:

```bash
dotnet restore --packages <empty-package-directory> --no-http-cache
dotnet tool restore
dotnet build --no-restore
dotnet ef database update --no-build \
  --project src/CreditWorks.Web \
  --startup-project src/CreditWorks.Web \
  --context AppDbContext
dotnet test --no-restore
dotnet test --no-restore --environment CREDITWORKS_RUN_SQL_TESTS=1
```

The package-directory placeholder is descriptive. The actual run used a unique temporary directory. Credentials were loaded from local configuration, overridden for the temporary database and never added to the repository.

## HTTP acceptance

An external acceptance harness exercised the running application using real HTTP requests, cookies and generated antiforgery tokens. It was run from temporary storage rather than added to the maintained test suite.

| Area | Verified behaviour |
| --- | --- |
| Startup and assets | Home, vehicle list, registration, categories, OpenAPI, stylesheet and category icon return successfully |
| Antiforgery | A vehicle POST without its token returns HTTP 400 |
| Required-field validation | Empty vehicle fields redisplay errors without inserting data |
| Storage-limit validation | An oversized weight redisplays its validation error without insertion |
| Registration | Valid submission persists a vehicle and redirects to the register |
| Initial classification | A newly registered 2200 kg vehicle displays as Medium |
| Invalid category changes | A gap is rejected and stored ranges remain unchanged |
| Reclassification | Moving the Medium/Heavy boundary to 2000 kg changes that vehicle's display to Heavy |
| Category creation/deletion | A valid additional range saves; removing it with repaired coverage also saves |
| Sorting and pagination | Weight sorting applies before paging; page two contains the remaining two of seven records |
| Manufacturer API | List, create, update and delete succeed; creation includes a Location header |
| API errors | Blank name: 400; duplicate name: 409; referenced deletion: 409; deleted record: 404 |
| Unexpected exception | Deliberately invalid rules in the disposable database produce a safe HTTP 500 page with request ID and no exception type/stack trace |
| Persistence | Vehicle data remains after stopping and restarting the isolated application |

All 43 harness checkpoints passed, including setup/test commands and fixture-creation steps. That number is separate from the 93 maintained automated tests.

## Browser acceptance

The isolated application was also opened in the in-app browser. The following interactions were confirmed:

- The vehicle list rendered the persisted records and category icons.
- Selecting five rows per page updated the list; Next showed records 6–7 of 7.
- Navigating from Vehicles to Categories initialized the category editor correctly.
- Add category inserted a draft row, and Remove removed that unsaved row.
- The category page was visually inspected for readable fields, visible icons and save controls.

This was a focused desktop-browser smoke check. It does not replace a maintained end-to-end browser suite or establish coverage across devices and browsers.

## Cleanup and submission review

- The temporary application was stopped and the acceptance browser tab closed.
- The acceptance database was deleted by the guarded cleanup routine.
- The integration fixture completed its own temporary-database cleanup.
- Pending UI whitespace issues were normalized before submission.
- Source/configuration and staged-file review excluded credentials, package caches, compiled output and temporary acceptance artifacts from the submission.

The known limitations in the design document remain applicable, including absent authentication, stale category-form concurrency, and the lack of performance testing. Acceptance does not claim production readiness or a measured performance target.

## Final public-repository review

A follow-up review on 2 October 2026 checked the submission against the supplied CreditWorks Software Engineer assignment, including the requirement that another developer can clone, configure, migrate, build, run and test it without undocumented steps.

Anonymous access to the public repository was verified with Git's credential helper and askpass disabled. The canonical submission URL is `https://github.com/RaymondXXX666/WebApp_Assessment`. Final submission cleanup changes were applied to an independent clone before verification. NuGet dependencies were restored into a new, empty package directory with HTTP cache reuse disabled. The same .NET SDK and local SQL Server instance described above were used, with a new `CreditWorksReview_<guid>` database and a separate loopback port. The application's existing database was not modified.

| Check | Result |
| --- | --- |
| Public repository access without credentials | Passed |
| Fresh-package restore and local EF tool restore | Passed |
| Build after final source cleanup | 0 warnings, 0 errors |
| Initial and repeated EF migration | Passed; five manufacturers, three categories, zero initial vehicles |
| Default test suite | 50 passed, 16 opt-in methods skipped |
| Full SQL-enabled suite | 93 passed, 0 failed, 0 skipped |
| Additional HTTP acceptance | 62 checks passed |
| Browser registration and required-field feedback | Passed from the vehicle-list navigation |
| Browser sorting, pagination and category editing | Passed |
| Existing 2200 kg vehicle after boundary change to 2000 kg | Displayed as Heavy after returning to the list |
| Documentation links | Local targets resolved |
| Cleanup | Temporary application stopped and review database removed |

The 62 HTTP checks covered application routes and assets; initial seed data; antiforgery; required fields; whitespace and overlong owner names; missing manufacturers; invalid years; zero, negative, over-precision and overflowing weights; exact 499.99/500.00/2500.00 boundaries; the maximum storable weight; all four sorting fields in both directions; sorting before pagination; gap/overlap rejection without stored changes; category creation/deletion and icon changes; reclassification; manufacturer CRUD and error responses; and HTML encoding of an owner name containing markup. These checks were performed by a temporary external harness, separate from the 93 maintained tests.

The browser follow-up also confirmed that adding and removing an unsaved category row still works after navigating from the vehicle list. The saved category editor was visually checked for readable fields, icon selectors and save controls.

### Submission cleanup

- Added the exact public clone command and a short review walkthrough to the README.
- Added a complete native PowerShell/SQL Server Express setup guide, including its environment and permission assumptions.
- Removed the unused client-side sorting script and its obsolete loader; sorting remains on SQL Server before pagination.
- Used full navigation for the registration link so its validation scripts load consistently.
- Removed the unused default Privacy template page and aligned saved/new category-row layouts.
- Kept credentials, database contents, build output, package caches and temporary acceptance tooling outside the submission.

The execution evidence is for macOS with SQL Server 2022 in a local container. Native Windows/SQL Server Express execution was not performed; that limitation is stated in the [PowerShell guide](WINDOWS_SETUP.md). Reviewers need .NET 10 and a reachable SQL Server 2019+ instance. The repository is source code, not a hosted live application; following the setup guide runs it locally.
