# PAKAR infrastructure (Azure)

> **Note (2026-11-23):** the team decided not to use Azure. The web app is now Blazor WebAssembly on Vercel and the API runs on another host;
> see `infra/vercel/README.md` and `decisions.md`. The Bicep files below, including the Web container app, are kept only as a reference.

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

The worker is `src/Pakar.AiInference` (built by Nabil on the `ai-dotnet` branch). It is a normal .NET program; the model (`.onnx`) and the video files stay
on the laptop, and only the results go to the API. Full details and the validated model are in `docs/ai/model-runtime.md` and `docs/ai/runtime-validation.md` of that branch.

Settings (environment variables or `dotnet user-secrets`; these are the real key names the code reads):

| Setting | Meaning |
| --- | --- |
| `Worker__Enabled=true` | **Must be set.** It defaults to `false`, and then the program starts but does nothing |
| `Worker__ApiBaseUrl` | The API address, for example `http://localhost:5249/` or the `apiUrl` printed by the deployment |
| `Worker__ApiKey` | The same value as the API's `Ingest:ApiKey` (at least 32 characters) |
| `Worker__VideoRoot` | Folder that holds the video files |
| `Model__Path` | The exported model, for example `/assets/best.onnx` |

```bash
# example (Linux/macOS shell); on Windows use $env:Name="value"
export Worker__Enabled=true Worker__ApiBaseUrl="https://<apiUrl>/" Worker__ApiKey="<Ingest--ApiKey value>"
export Worker__VideoRoot=/assets/videos Model__Path=/assets/best.onnx
dotnet run --project src/Pakar.AiInference --no-launch-profile
```
The worker asks the API every 10 seconds which videos and polygons to use (`GET /api/internal/video-sources`), plays each video in a loop at about 2 frames per second,
and posts changes plus a heartbeat every 15 seconds. While it runs, the video source is active; when it stops, the source turns inactive after 2 minutes.
Nothing is shown to users until the worker has been started by someone on the team; the website itself works without it and shows the last known status.

**Container (optional, later):** the same program can run as a container in Azure. `src/Pakar.AiInference/Dockerfile` is in the `ai-dotnet` branch and needs
`libdrm2 libatomic1` added to its `apt-get install` line (see TODO.md, branch review). The `.onnx` and the videos are not in the image (`.dockerignore` excludes
them), so they must be mounted, which needs Azure Files storage that the Bicep no longer contains.

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
