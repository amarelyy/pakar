# PAKAR (Parkir Akurat Real-Time): Smart Parking System with .NET 10 & Azure
This is a Senior Project of Group 2. PAKAR is a program that will gives the user information on how many slots is left on certain parking spots in the Faculty of Engineering, Universitas Gadjah Mada. You do not have to waste your time finding a spot!

Team Members:
1. Ursula Maurentti Amarely - 24/533008/TK/59050 
2. Deva Zukannada - 24/546873/TK/60780 
3. Nabil Abrian Aryo Prabowo - 24/546496/TK/60770

UI/UX Design Prototype:
https://www.figma.com/design/Auql4Y3M0MsSFPgexF73C1/PAKAR?node-id=2-2&t=FBpHGCQJNRwiXnIt-1


## Configuration

Services refuse to start until their secrets are configured. Secrets are never committed;
`appsettings.json` only holds empty placeholders. Deployed environments use environment
variables with `__` as the separator (for example `Jwt__Key`), sourced from Azure Key Vault.

### Pakar.Api

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Azure SQL Database connection string (SQL Server engine). |
| `Database:ApplyMigrationsOnStartup` | Defaults to `true` in Development and `false` elsewhere. |
| `Jwt:Key` | JWT signing key, at least 32 bytes. |
| `Ingest:ApiKey` | Shared key edge devices send in the `X-Api-Key` header, at least 32 characters. |
| `Inference:BaseUrl`, `Inference:ApiKey` | Optional. Pakar.AiInference address and key; live scanning is disabled when empty. |
| `Admin:Email`, `Admin:Password` | Optional. Creates the first Admin account on startup if it does not exist. |

Production (Azure SQL, passwordless through the Container App's managed identity):

```
Server=tcp:<server>.database.windows.net,1433;Database=PakarDb;Authentication=Active Directory Default;Encrypt=True;
```

Local testing uses the same SQL Server engine, either LocalDB (Windows) or the container in
`infra/docker-compose.yml`. Secrets are stored outside the repo via user-secrets:

```bash
cd src/Pakar.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=PakarDb;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
dotnet user-secrets set "Ingest:ApiKey" "$(openssl rand -hex 32)"
dotnet user-secrets set "Admin:Email" "admin@example.com"
dotnet user-secrets set "Admin:Password" "<at least 12 characters>"
```

Health endpoints for Container Apps probes: `/health/live` (process) and `/health/ready` (database).

### Pakar.AiInference

| Key | Purpose |
| --- | --- |
| `Inference:ApiKey` | Required. Must equal `Inference:ApiKey` in Pakar.Api; at least 32 characters. |

### Pakar.Web

| Key | Purpose |
| --- | --- |
| `Api:BaseUrl` | Pakar.Api address. Defaults to `http://localhost:5249/` in Development. |

## Database Migrations

Migrations live in `src/Pakar.Migrations.SqlServer`. After changing an entity or a file in
`Persistence/Configurations`, add a migration:

```bash
dotnet ef migrations add <Name> --project src/Pakar.Migrations.SqlServer --startup-project src/Pakar.Migrations.SqlServer
```

Production applies migrations as a deployment step with a migration bundle:

```bash
dotnet ef migrations bundle --project src/Pakar.Migrations.SqlServer --startup-project src/Pakar.Migrations.SqlServer -o efbundle --self-contained
./efbundle --connection "<Azure SQL connection string>"
```

Zones and cameras are soft-deleted (`DeletedAt`) so detection history is kept; queries hide them
automatically, and `IgnoreQueryFilters()` reads them for history.
