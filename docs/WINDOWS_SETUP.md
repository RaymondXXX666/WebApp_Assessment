# Windows PowerShell setup

This is the native Windows alternative to the Bash/Docker walkthrough in the [README](../README.md). It uses a local SQL Server Express instance with Windows authentication.

## Prerequisites

- Git and .NET SDK 10 installed and available in PowerShell.
- SQL Server 2019 or later, including Express. The commands below assume the Database Engine is installed as `SQLEXPRESS` on this computer and its Windows service is running.
- Your Windows login has permission to connect and create databases. Integration tests also need permission to drop their own temporary database.

If your instance has another name, replace `localhost\SQLEXPRESS` in the connection string. A default local instance can use `localhost`. SQL Server Management Studio is optional; migrations create the schema.

## 1. Clone, restore and build

Run in PowerShell:

```powershell
git clone https://github.com/RaymondXXX666/CreditWorks.git
Set-Location CreditWorks
dotnet restore
dotnet tool restore
dotnet build --no-restore
```

## 2. Configure and initialize the database

Continue in the same PowerShell window, from the repository root:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" 'Server=localhost\SQLEXPRESS;Database=CreditWorks;Integrated Security=True;Encrypt=True;TrustServerCertificate=True' --project src/CreditWorks.Web/CreditWorks.Web.csproj
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef database update --project src/CreditWorks.Web/CreditWorks.Web.csproj --startup-project src/CreditWorks.Web/CreditWorks.Web.csproj --context AppDbContext
```

Windows authentication uses your current Windows identity, so no SQL password is stored. `TrustServerCertificate=True` is for this local development connection. Use a validated certificate for shared deployment.

A new database contains five manufacturers, three weight categories and no vehicles. Repeating the migration command does not duplicate seed data. Existing nonempty reference tables are preserved.

## 3. Run

```powershell
dotnet run --project src/CreditWorks.Web --no-launch-profile --urls http://localhost:5050
```

Open [Vehicles](http://localhost:5050/Vehicles), [Categories](http://localhost:5050/Categories) or [Swagger](http://localhost:5050/swagger). The application is running locally; the GitHub repository itself is not a hosted application.

Follow the [review walkthrough](../README.md#suggested-review-walkthrough) to exercise the main requirements. Press Ctrl+C to stop the application. If starting it in a new PowerShell window, set `$env:ASPNETCORE_ENVIRONMENT = 'Development'` again before running it.

## 4. Test

After stopping the application, run:

```powershell
dotnet test
dotnet test --environment CREDITWORKS_RUN_SQL_TESTS=1
```

The first command runs unit tests and explicitly skips SQL integration tests. The second runs the full suite using the configured User Secrets. Tests create and remove their own `CreditWorksTests_<guid>` database; they do not reset the application's database.

If a different test server is needed, set `$env:CREDITWORKS_TEST_CONNECTION_STRING` to its connection string before the second command. Remove that override afterward with `Remove-Item Env:CREDITWORKS_TEST_CONNECTION_STRING`.

## Troubleshooting and verification scope

- **Cannot find the server:** verify the SQL Server service and instance name. Installing only Management Studio does not install the Database Engine.
- **Login or CREATE DATABASE denied:** connect with a Windows login that has the required local SQL Server permissions.
- **Swagger unavailable:** set the Development environment in the terminal that starts the application.
- **Port 5050 already used:** choose another free port in `--urls` and open that port in the browser.

This guide uses the same application, EF migrations and test commands as the verified macOS/SQL Server container workflow. A native Windows execution was not performed during the recorded acceptance; see the [acceptance report](ACCEPTANCE_REPORT.md) for the tested environment.
