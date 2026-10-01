# CreditWorks Vehicle Register

An ASP.NET Core Razor Pages application for registering vehicles and classifying them using configurable weight ranges. Built for the CreditWorks Software Engineer programming assignment.

Users can add vehicles, browse and sort the register, and maintain a complete set of categories. Classification is calculated when the list is loaded, so saved category changes apply to existing vehicles without updating their records.

## Documentation

| Document | Contents |
| --- | --- |
| [System Design](docs/SYSTEM_DESIGN.md) | Architecture, schema, interfaces, validation, security, decisions and limitations |
| [Data Flow and Business Processes](docs/DATA_FLOW.md) | Context and process diagrams, registration, classification, category updates and failure paths |
| [Acceptance Report](docs/ACCEPTANCE_REPORT.md) | Fresh-source build, clean database initialization, automated tests and HTTP/browser acceptance results |

These documents describe the current implementation. Mermaid diagrams render in GitHub's Markdown viewer.

## Features

- Register an owner's name, manufacturer, manufacture year and weight in kilograms.
- Display manufacturer, year, weight, category name and category icon for each vehicle.
- Sort by owner, manufacturer, year or weight in either direction, with an active-sort indicator.
- Browse pages of 5, 20 or 50 vehicles. Sorting executes in SQL Server before pagination.
- Add, edit and remove categories and select from three bundled SVG icons.
- Validate and save the complete category configuration in one transaction.
- Reject gaps, overlaps and invalid boundaries; resolve each valid weight to exactly one category.
- Preserve submitted form values after expected database errors and explain the next action.
- Manage manufacturers through a JSON API with development-only Swagger documentation.

## Technology and repository structure

| Component | Implementation |
| --- | --- |
| Runtime | .NET 10 / C# |
| Web UI | ASP.NET Core Razor Pages, Bootstrap and JavaScript |
| Persistence | EF Core 10.0.12 and Microsoft SQL Server |
| API documentation | Swashbuckle.AspNetCore 10.2.3 |
| Automated tests | xUnit; SQL Server integration tests |

```text
CreditWorks.slnx
dotnet-tools.json                    Local EF Core tool manifest
src/CreditWorks.Web/
  Pages/                            Razor views and request handlers
  Endpoints/                        Manufacturer JSON API
  Services/                         Category rules, saves and error messages
  Models/                           Vehicle, Manufacturer and VehicleCategory
  Data/                             EF Core context and seed data
  Migrations/                       Versioned SQL Server schema
  wwwroot/                          CSS, JavaScript and category icons
tests/CreditWorks.Tests/
  Integration/                      Isolated SQL Server tests and fixture
docs/                               Design and data-flow documentation
```

## Prerequisites

- .NET SDK 10.
- Microsoft SQL Server 2019 or later. The container example uses SQL Server 2022.
- Docker with a running daemon if using the container example; an existing SQL Server instance can also be used.
- Git.

The setup commands below use **Bash**. On macOS, run `bash` first if your terminal uses zsh. On Windows, use a Bash environment such as WSL, or adapt environment-variable and password-entry commands to PowerShell. The `dotnet` commands themselves are the same.

## Build and run locally

### 1. Clone and restore

Clone the repository using its submission URL and change into the repository root. Then run:

```bash
dotnet restore
dotnet tool restore
dotnet build --no-restore
```

The repository-root `dotnet-tools.json` pins the local EF Core tool version. Building and running unit tests do not require SQL Server.

### 2. Start SQL Server

For a new local container, enter a strong SQL Server password when prompted. Keep the same shell open for the connection-string step:

```bash
read -r -s -p 'SQL Server password: ' MSSQL_SA_PASSWORD
printf '\n'

docker run --name creditworks-sql \
  --platform linux/amd64 \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD="$MSSQL_SA_PASSWORD" \
  -p 127.0.0.1:1433:1433 \
  -v creditworks-sql-data:/var/opt/mssql \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

The `linux/amd64` image runs through emulation on Apple Silicon. The named volume preserves database files when the container stops. The port is bound to the local machine.

If the container already exists, run `docker start creditworks-sql` instead. When configuring secrets in a new shell, use the password prompt above to enter the existing container password.

Check readiness with:

```bash
docker logs creditworks-sql
```

Wait for the message indicating that SQL Server is ready for client connections. If using an existing SQL Server instance, skip the container commands and use its server and credentials below.

### 3. Configure the connection string

Store local credentials in .NET User Secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=127.0.0.1,1433;Database=CreditWorks;User Id=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True" \
  --project src/CreditWorks.Web/CreditWorks.Web.csproj

unset MSSQL_SA_PASSWORD
export ASPNETCORE_ENVIRONMENT=Development
```

The example assumes the password contains no connection-string delimiters such as semicolons. If it does, quote/escape the password according to SQL Server connection-string syntax. User Secrets are local development configuration, not an encrypted production secret store. Do not commit credentials.

For another environment, supply `ConnectionStrings__DefaultConnection` through the host's environment or secret configuration. The local example uses `sa` and trusts the server certificate for development; deployment should use a dedicated identity and validated certificate.

### 4. Apply migrations and seed data

```bash
dotnet ef database update \
  --project src/CreditWorks.Web/CreditWorks.Web.csproj \
  --startup-project src/CreditWorks.Web/CreditWorks.Web.csproj \
  --context AppDbContext
```

This creates the schema and invokes the configured seeder. Empty manufacturer and category tables receive their initial values. Seeding does not reset tables that already contain data. The web application does not apply migrations automatically on startup.

### 5. Start the application

```bash
dotnet run --project src/CreditWorks.Web --no-launch-profile \
  --urls http://localhost:5050
```

Open [the vehicle register](http://localhost:5050/Vehicles) or [category administration](http://localhost:5050/Categories).

The explicit URL and `--no-launch-profile` make this walkthrough independent of `launchSettings.json`. The HTTP-only walkthrough may log an HTTPS-redirection warning because no HTTPS port is configured. To use the HTTPS profile instead, run `dotnet dev-certs https --trust`, then `dotnet run --project src/CreditWorks.Web --launch-profile https`, and open [the HTTPS register](https://localhost:7059/Vehicles).

Press Ctrl+C to stop the application. Use `docker stop creditworks-sql` to stop the database without removing its volume. Restart the application after changing compiled code when using `dotnet run`.

## Using the application

1. Open **Vehicles**, select **Add vehicle**, complete all four fields and save.
2. Select a column heading to sort; select it again to reverse direction. Use pagination to browse the full register.
3. Open **Categories** to edit names, icons and boundaries. Add or remove rows, then select **Save all categories**.
4. Adjust adjacent ranges together. Removing a middle range without extending a neighbour leaves a gap and is rejected.
5. Return to the list to see updated classifications. Other already-open browser tabs update on their next list request or refresh.

### Initial data and boundary rules

Manufacturers: **Mazda, Mercedes, Honda, Ferrari and Toyota**. They are stored in a separate table; vehicles reference their IDs.

| Category | Weight range | Icon |
| --- | --- | --- |
| Light | `0 <= weight < 500` kg | `light.svg` |
| Medium | `500 <= weight < 2500` kg | `medium.svg` |
| Heavy | `weight >= 2500` kg | `heavy.svg` |

Ranges include their minimum and exclude their maximum. Exactly **500.00 kg** belongs to Medium; exactly **2500.00 kg** belongs to Heavy. A blank final maximum means no category upper bound. Vehicle input is still limited to the database's representable weight range.

- Owner: required, up to 150 characters; saved with surrounding whitespace trimmed.
- Manufacturer: required and must exist.
- Year: required, from 1886 through the current UTC year, inclusive.
- Weight: required, positive, at most two decimal places, and no greater than `9999999999999999.99` kg (`decimal(18,2)`).
- Categories: at least one; unique trimmed names ignoring case; an allowed icon; valid adjacent ranges beginning at zero and ending with an unbounded final range.

If Medium ends at 2000 kg and Heavy starts at 2000 kg, an existing 2200 kg vehicle displays as Heavy on the next list load. No category ID is stored on the vehicle.

## API documentation

In Development, open [Swagger UI](http://localhost:5050/swagger) or [the OpenAPI document](http://localhost:5050/swagger/v1/swagger.json).

Swagger documents `/api/manufacturers`. Vehicle and category workflows use Razor Pages and HTML forms. See [interface details](docs/SYSTEM_DESIGN.md#4-interfaces) for routes, parameters, example responses and errors.

## Automated tests

Run the default suite without a database:

```bash
dotnet test
```

Unit tests run; SQL Server integration tests are explicitly reported as skipped unless enabled. To run all tests, start SQL Server, configure the web project's User Secrets as above, and run:

```bash
dotnet test --environment CREDITWORKS_RUN_SQL_TESTS=1
```

To run only integration tests:

```bash
dotnet test --environment CREDITWORKS_RUN_SQL_TESTS=1 --filter 'Category=Integration'
```

`CREDITWORKS_TEST_CONNECTION_STRING` can supply an alternative test-server connection string and takes precedence over User Secrets. The login needs permission to create and drop databases. Use a local or dedicated test server.

The fixture replaces the configured database name with `CreditWorksTests_<guid>`, applies actual migrations, resets seed data between tests and drops the temporary database at the end. It does not use application data. Enabled integration tests fail if the server is unavailable. A forcibly terminated run may leave its temporary database behind for manual cleanup.

The latest verification on **2 October 2026** passed **93 tests**, with no failures or skips when integration tests were enabled. This is a verification snapshot, not a fixed required test count.

Coverage includes boundary values, category drafts, vehicle validation and weight limits, SQL sorting and pagination, category persistence, rollback, reclassification, retained input after failures, and timeouts after a vehicle has already been saved. Tests exercise data annotations, page handlers and services; HTTP model binding, antiforgery, browser JavaScript and load testing are not covered.

## Design decisions and known limitations

- One web application keeps deployment and maintenance appropriate to the assignment. Presentation, business rules and persistence are separated within the project.
- SQL Server is the source of truth. Category membership is derived on read; configuration saves use a serializable transaction.
- Server-side validation is authoritative. HTML constraints provide earlier feedback.
- JavaScript enhances navigation and category editing. Vehicle sorting runs on the server, even when navigation replaces only the page's main content.
- Forms retain input after expected database failures. An unconfirmed save asks the user to check current data before submitting again. Saves are not automatically retried. Unexpected errors use a generic page and server-side logs.
- Authentication and authorization are intentionally omitted for the assignment. Page and API operations are accessible without signing in. Access control would be required before shared production deployment.
- Category saves have no configuration version or optimistic concurrency token. A stale full-form submission can overwrite newer data, including removing rows added by someone else. Serializable transactions protect save atomicity but do not detect all stale edits.
- Cross-category coverage and category-name uniqueness are application rules, not database constraints. Direct SQL writes can violate them.
- Icons are selected from three bundled files. Uploads, vehicle editing/deletion, VIN/plate search, audit history and automatic browser updates are outside the current scope.

Further decisions and proposed improvements are in [System Design](docs/SYSTEM_DESIGN.md#8-assumptions-limitations-and-future-work).

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Connection/login failure | SQL Server readiness, server/port, existing container password and web-project User Secrets |
| Missing tables or empty dropdowns | Set `ASPNETCORE_ENVIRONMENT=Development` and apply migrations |
| `dotnet ef` unavailable | Run `dotnet tool restore` from the repository root |
| A different port opens | Use the documented `--no-launch-profile --urls` command or HTTPS-profile URL |
| Integration tests skipped | Enable `CREDITWORKS_RUN_SQL_TESTS=1` with the documented command |
| Save outcome uncertain | Check the vehicle list or current categories before resubmitting; inspect server logs |
| Unexpected error page | Use its request ID and server logs to investigate; technical details are not displayed to users |
