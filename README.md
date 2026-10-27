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
| `Database:Provider` | `SqlServer` (Azure SQL, default) or `Sqlite` (local development). |
| `ConnectionStrings:DefaultConnection` | Connection string for the selected provider. |
| `Database:ApplyMigrationsOnStartup` | Defaults to `true` in Development and `false` elsewhere. |
| `Jwt:Key` | JWT signing key, at least 32 bytes. |
| `Ingest:ApiKey` | Shared key edge devices send in the `X-Api-Key` header, at least 32 characters. |
| `Inference:BaseUrl`, `Inference:ApiKey` | Optional. Pakar.AiInference address and key; live scanning is disabled when empty. |
| `Admin:Email`, `Admin:Password` | Optional. Creates the first Admin account on startup if it does not exist. |

Local development with SQLite (stored outside the repo via user-secrets):

```bash
cd src/Pakar.Api
dotnet user-secrets set "Database:Provider" "Sqlite"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=PakarDev.db"
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
dotnet user-secrets set "Ingest:ApiKey" "$(openssl rand -hex 32)"
dotnet user-secrets set "Admin:Email" "admin@example.com"
dotnet user-secrets set "Admin:Password" "<at least 12 characters>"
```

Azure SQL connection string (passwordless, using the Container App's managed identity):

```
Server=tcp:<server>.database.windows.net,1433;Database=PakarDb;Authentication=Active Directory Default;Encrypt=True;
```

### Pakar.AiInference

| Key | Purpose |
| --- | --- |
| `Inference:ApiKey` | Required. Must equal `Inference:ApiKey` in Pakar.Api; at least 32 characters. |

### Pakar.Web

| Key | Purpose |
| --- | --- |
| `Api:BaseUrl` | Pakar.Api address. Defaults to `http://localhost:5249/` in Development. |

## Database Migrations

Each provider keeps its own migration history, because column types differ between engines.
Change the model once, then add a migration to both projects:

```bash
dotnet ef migrations add <Name> --project src/Pakar.Migrations.SqlServer --startup-project src/Pakar.Migrations.SqlServer
dotnet ef migrations add <Name> --project src/Pakar.Migrations.Sqlite --startup-project src/Pakar.Migrations.Sqlite
```

Production applies migrations as a deployment step:

```bash
dotnet ef migrations bundle --project src/Pakar.Migrations.SqlServer --startup-project src/Pakar.Migrations.SqlServer -o efbundle --self-contained
./efbundle --connection "<Azure SQL connection string>"
```
