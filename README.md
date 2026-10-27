# PAKAR (Parkir Akurat Real-Time): Smart Parking System with .NET 10 & Azure
This is a Senior Project of Group 2. PAKAR is a program that will gives the user information on how many slots is left on certain parking spots in the Faculty of Engineering, Universitas Gadjah Mada. You do not have to waste your time finding a spot!

Team Members:
1. Ursula Maurentti Amarely - 24/533008/TK/59050 
2. Deva Zukannada - 24/546873/TK/60780 
3. Nabil Abrian Aryo Prabowo - 24/546496/TK/60770

UI/UX Design Prototype:
https://www.figma.com/design/Auql4Y3M0MsSFPgexF73C1/PAKAR?node-id=2-2&t=FBpHGCQJNRwiXnIt-1


## API Configuration

`Pakar.Api` refuses to start until its secrets are configured. Secrets are never committed;
`appsettings.json` only holds empty placeholders.

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Database. A path ending in `.db` uses SQLite, otherwise SQL Server. |
| `Jwt:Key` | JWT signing key, at least 32 bytes. |
| `Ingest:ApiKey` | Shared key edge devices send in the `X-Api-Key` header, at least 32 characters. |
| `Admin:Email`, `Admin:Password` | Optional. Creates the first Admin account on startup if it does not exist. |

Local development (stored outside the repo via user-secrets):

```bash
cd src/Pakar.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=PakarDev.db"
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
dotnet user-secrets set "Ingest:ApiKey" "$(openssl rand -hex 32)"
dotnet user-secrets set "Admin:Email" "admin@example.com"
dotnet user-secrets set "Admin:Password" "<at least 12 characters>"
```

Deployed environments use environment variables with `__` as the separator
(for example `Jwt__Key`), sourced from Azure Key Vault.
