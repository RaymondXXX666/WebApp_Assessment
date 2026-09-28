# CreditWorks Vehicle Register

An ASP.NET Core Razor Pages application for recording vehicles and classifying them by weight. Vehicle categories can be edited without changing existing vehicle records.

## Requirements

- .NET SDK 10
- Docker with a running daemon
- Git

The database uses SQL Server 2022. On an Apple Silicon Mac, the Docker command below runs the `linux/amd64` image through emulation.

## Run locally

From the repository root, start SQL Server. Choose a strong password when prompted:

```bash
read -rs 'MSSQL_SA_PASSWORD?SQL Server password: '
printf '\n'

docker run --name creditworks-sql \
  --platform linux/amd64 \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD="$MSSQL_SA_PASSWORD" \
  -p 127.0.0.1:1433:1433 \
  -v creditworks-sql-data:/var/opt/mssql \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

If the container already exists, use `docker start creditworks-sql` instead of `docker run`. Wait until `docker logs creditworks-sql` reports that SQL Server is ready for client connections.

Store the connection string outside the repository:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=127.0.0.1,1433;Database=CreditWorks;User Id=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True" \
  --project src/CreditWorks.Web/CreditWorks.Web.csproj

unset MSSQL_SA_PASSWORD
```

Restore the local EF Core tool, create the database, and run the app:

```bash
dotnet tool restore
export ASPNETCORE_ENVIRONMENT=Development

dotnet ef database update \
  --project src/CreditWorks.Web/CreditWorks.Web.csproj \
  --startup-project src/CreditWorks.Web/CreditWorks.Web.csproj \
  --context AppDbContext

dotnet run --project src/CreditWorks.Web --no-launch-profile \
  --urls http://localhost:5050
```

Open `http://localhost:5050/Vehicles`. The database migration also seeds five manufacturers and three initial weight categories. To stop the app, press Ctrl+C. To stop SQL Server, run `docker stop creditworks-sql`.

## Features

- Add a vehicle with owner, manufacturer, year of manufacture, and weight.
- View vehicles with manufacturer and weight category; sort the list by owner, manufacturer, year, or weight.
- Add, edit, and remove categories and choose their icons.
- Save a complete category configuration in one database transaction. Validation rejects gaps, overlaps, duplicate names, and ranges without a final upper-unbounded category.
- Calculate each vehicle's category from its weight and the current category configuration when the list is loaded. Changing a category boundary therefore updates how existing vehicles are displayed.

Ranges include their minimum and exclude their maximum. The initial categories are Light `[0, 500)`, Medium `[500, 2500)`, and Heavy `[2500, infinity)` kg. Vehicle weights must be positive and use at most two decimal places.

## Tests

```bash
dotnet test
```

The tests cover category boundaries, invalid gaps and overlaps, and the effect of changing category rules.

## Implementation notes

The application uses Razor Pages, EF Core 10, and SQL Server. EF Core migrations define the schema. The SQL Server password is stored in .NET User Secrets for local development and is not committed. Client-side sorting and page navigation provide immediate interaction while Razor Pages remain directly accessible by URL.
