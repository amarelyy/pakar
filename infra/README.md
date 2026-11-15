# PAKAR infrastructure (Azure)

Bicep templates for issue #28 and the secret handling for issue #29.
The database is **Neon** (outside Azure), so there is no SQL resource.

**Status:** the templates compile and pass the Bicep linter (Bicep CLI 0.49.2). They have **not been deployed or run through
`what-if`** because no Azure subscription was available when they were written. Run `what-if` first (step 3); the first real
deployment may still show problems that only Azure can report. The apps also cannot start until CI (issue #21) has built and
pushed the two images (Api and Web).

**Where the AI runs:** not in Azure. The video worker (`Pakar.AiInference`, with the ONNX model and the video file) runs on a demo
laptop with `dotnet run` and sends its results to the API over HTTPS. Azure only runs the API and the web app.

## What is deployed

| Resource | Name | Purpose |
| --- | --- | --- |
| Resource group | `pakar-rg` | Holds everything below; delete it to remove it all |
| Log Analytics workspace | `pakar-logs` | Logs of the apps (30 days) |
| Container Apps environment | `pakar-env` | Shared network and logging |
| User-assigned identity | `pakar-id` | Lets the apps read Key Vault |
| Key Vault | `pakar-kv-<hash>` | The secrets (table below); access by role, soft delete 7 days |
| Container App `pakar-api` | `pakar-api` | The API. **Exactly one replica** (the realtime state is in memory). Probes `/health/live`, `/health/ready` |
| Container App `pakar-web` | `pakar-web` | The Blazor web app, public address, scales to zero by default |

Every image must listen on **port 8080** (the .NET default in containers). The API must keep a public address
(`apiExternalIngress=true`, the default) because the worker on the laptop calls it from outside Azure.

## Secrets

Secrets are passed to the deployment as secure parameters, stored in Key Vault, and read by the apps through Key Vault references.
They are never in git, in the template, or in an environment file in the repository.

| Key Vault name | App setting | Used by | Where the value comes from |
| --- | --- | --- | --- |
| `Jwt--Key` | `Jwt__Key` | Api | `openssl rand -base64 48` |
| `Ingest--ApiKey` | `Ingest__ApiKey` | Api (checks it); the worker on the laptop holds the same value in its user-secrets | `openssl rand -hex 32` |
| `Admin--Password` | `Admin__Password` | Api (first admin account) | a new random password, at least 12 characters |
| `DATABASE-URL` | `DATABASE_URL` | Api | Neon pooled URL (`neon env pull`) |
| `DATABASE-URL-UNPOOLED` | (none) | the migration step of CI | Neon direct URL (`neon env pull`) |

`Inference--ApiKey` is no longer needed: the API stops calling the inference service (see TODO.md section 0).
Use a **new** value for each secret in production; never reuse a value that was ever in git or on a developer machine.

## Deploy

Needs the Azure CLI (`az`) and an account that is **Owner** of the subscription (the template creates a role assignment, which
Contributor alone cannot do).

1. Sign in and pick the subscription:
   ```bash
   az login
   az account set --subscription "<subscription id>"
   ```
2. Prepare the secrets in the shell (they stay out of the command history because the shell expands them):
   ```bash
   cd <repository root>
   set -a; . ./.env.local; set +a            # DATABASE_URL and DATABASE_URL_UNPOOLED from `neon env pull`
   export JWT_KEY="$(openssl rand -base64 48)"
   export INGEST_KEY="$(openssl rand -hex 32)"
   export ADMIN_PASSWORD="$(openssl rand -base64 24)"   # keep it somewhere safe, you need it to log in
   ```
3. Preview, then deploy (same parameters for both):
   ```bash
   az deployment sub what-if --location eastus2 --template-file infra/bicep/main.bicep \
     --parameters @infra/main.parameters.example.json \
     --parameters jwtKey="$JWT_KEY" ingestApiKey="$INGEST_KEY" adminPassword="$ADMIN_PASSWORD" \
                  databaseUrl="$DATABASE_URL" databaseUrlUnpooled="$DATABASE_URL_UNPOOLED"
   az deployment sub create --location eastus2 --template-file infra/bicep/main.bicep \
     --parameters @infra/main.parameters.example.json \
     --parameters jwtKey="$JWT_KEY" ingestApiKey="$INGEST_KEY" adminPassword="$ADMIN_PASSWORD" \
                  databaseUrl="$DATABASE_URL" databaseUrlUnpooled="$DATABASE_URL_UNPOOLED"
   ```
   If the images are in a private registry add `registryUsername=... registryPassword=...`.
   The output shows `webUrl`, `apiUrl` and `keyVaultName`.
4. Create the schema **before** the API first starts (the API does not migrate in production). With the direct Neon URL converted to a
   connection string (`Host=<unpooled host>;Database=neondb;Username=<user>;Password=<password>;SSL Mode=Require`):
   ```bash
   dotnet ef migrations bundle --project src/Pakar.Migrations.Postgres --startup-project src/Pakar.Api -o efbundle --self-contained
   ./efbundle --connection "<connection string>"
   ```
   (Or run `dotnet ef database update` with `DATABASE_URL_UNPOOLED` set, as described in TODO.md section 0.7.)
5. Smoke test:
   ```bash
   curl https://<apiUrl>/health/ready      # Healthy
   ```
   Then open `<webUrl>`, register, log in, and open the map.

## Everyday tasks

- **Change a secret:** `az keyvault secret set --vault-name <keyVaultName> --name Jwt--Key --value "$(openssl rand -base64 48)"`.
  Apps pick up a new value within about 30 minutes, or at once when you restart the revision
  (`az containerapp revision list -g pakar-rg -n pakar-api`, then `az containerapp revision restart -g pakar-rg -n pakar-api --revision <name>`).
  A new `Jwt--Key` signs everyone out. A new `Ingest--ApiKey` must also be set in the worker's user-secrets on the laptop.
  The first admin password only applies when the admin account does not exist yet; to change an existing admin password update the `Users` row.
- **Let a teammate manage secrets:** `az role assignment create --role "Key Vault Secrets Officer" --assignee <email> --scope <Key Vault id>`.
- **Delete everything:** `az group delete -n pakar-rg --yes`. The Key Vault stays in soft delete for 7 days and blocks a redeploy with the same name;
  remove it with `az keyvault purge --name <keyVaultName>`.

## Running the AI worker on a laptop

The worker is a normal .NET program; it needs the video file and the ONNX model on the laptop (neither goes to Azure).

```bash
cd src/Pakar.AiInference
dotnet user-secrets set "Api:BaseUrl" "https://<apiUrl>/"       # the apiUrl printed by the deployment
dotnet user-secrets set "Ingest:ApiKey" "<the Ingest--ApiKey value>"
dotnet user-secrets set "Video:Root" "/path/to/the/folder/with/the/videos"
dotnet run
```
The worker asks the API which video and polygons to use (`GET /api/internal/video-sources`), plays the video, and posts results.
While it runs, the video source shows as active in the admin pages; when it stops, the source turns inactive after 2 minutes.
These settings and the worker itself are built in TODO.md tasks 2b and P5; until then this section describes the target.

## Choices worth knowing

- **One shared identity instead of one per app.** A system-assigned identity only exists after its app is created, but the app needs
  Key Vault access to be created (secret references). One user-assigned identity with the "Key Vault Secrets User" role avoids that loop.
- **Region `eastus2`.** The Neon project is in AWS `us-east-2`; keeping the API in a nearby Azure region keeps each database query fast.
  Students in Yogyakarta will see more latency to the web app instead. Team decision 11 in TODO.md.
- **API has a public address** (`apiExternalIngress=true`). The worker on the laptop and the smoke test both reach it from outside Azure,
  so do not set it to `false`. The ingest route is protected by the shared `Ingest--ApiKey`; keep that key out of git and chat.
- **Cost.** Container Apps bills by use and has a monthly free grant, but the API runs all day with one replica, which can be more than
  the free grant covers. Check the Azure pricing calculator before leaving it on and keep `webMinReplicas=0` when idle.
  The worker costs nothing in Azure because it runs on a laptop. Log Analytics and Key Vault cost little at this size.
- **Web is limited to one replica** because ASP.NET encryption keys are per instance; a second replica would break sign-in cookies.
