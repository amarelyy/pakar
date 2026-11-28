# PAKAR on one virtual machine

Web (Caddy), API and video worker run with Docker Compose on one Ubuntu server. The database is on Neon.

Course rules: one `Standard_B2ls_v2` machine with one disk of at most 32 GiB, and it runs only from one day before the
presentation (earlier only with the assistant's permission). Create no other Azure resource.

Replace every `<...>`. Commands run on your computer (PowerShell, repository root) unless marked **server**.

## 0. Before the day

- `az login`, Docker Desktop and an SSH key pair that the machine accepts.
- `infra/vm/.env.production`, a copy of `.env.example` with production values:
  - `SITE_ADDRESS=<label>.<region>.cloudapp.azure.com`, `SITE_URL=https://<same address>`
  - `ASSETS_DIR=/opt/pakar/assets`, no `HTTP_PORT` or `HTTPS_PORT`
  - `IMAGE_PREFIX=ghcr.io/<owner in lower case>/pakar`, `IMAGE_TAG=<full 40-character commit hash>`
  - new `Jwt__Key` and `Ingest__ApiKey`, the production `DATABASE_URL`, an admin password of 12 characters or more
- `artifacts/vm-assets/models/best.onnx` and the videos in `artifacts/vm-assets/videos/`. The video file names must
  match the names set on the admin Cameras page.

## 1. Start the machine

```powershell
az resource list -g <resource group> -o table      # names of the machine, public IP and NSG
az vm start -g <resource group> -n <vm name>
az vm show -d -g <resource group> -n <vm name> --query "{size:hardwareProfile.vmSize,power:powerState,ip:publicIps}"
az disk list -g <resource group> -o table          # one disk, Standard SSD or HDD (LRS), 32 GiB or less
```

## 2. Address and firewall (once)

```powershell
az network public-ip update -g <resource group> -n <public ip name> --dns-name <label>
az network nsg rule create -g <resource group> --nsg-name <nsg name> -n web --priority 310 --protocol Tcp --access Allow --destination-port-ranges 80 443
az network nsg rule create -g <resource group> --nsg-name <nsg name> -n ssh-team --priority 320 --protocol Tcp --access Allow --destination-port-ranges 22 --source-address-prefixes <your public ip>
az network nsg rule list -g <resource group> --nsg-name <nsg name> -o table
```

An older rule that opens port 22 to everyone wins over `ssh-team` if its priority number is lower. After `ssh-team`
exists, remove it: `az network nsg rule delete -g <resource group> --nsg-name <nsg name> -n <old rule name>`. If a
priority is already taken, use another free number.

## 3. Upload

```powershell
./infra/vm/upload.ps1 -VmHost <address> -User <user>
ssh -i $HOME/.ssh/id_ed25519 <user>@<address>
```

If your key is another file (for example a `.pem` from Azure), add `-Key <path>` to the first command and use the same
path after `-i`.

## 4. Install and start (**server**)

```bash
cd ~/pakar-upload
sed -i 's/\r$//' setup.sh deploy.sh compose.yml .env
bash setup.sh
cp compose.yml deploy.sh /opt/pakar/
install -m 600 .env /opt/pakar/.env && rm .env
cp -r vm-assets/. /opt/pakar/assets/
```

Only if the images are private, log in with a read-only token (paste it at the password prompt):

```bash
sg docker -c 'docker login ghcr.io -u <github user>'
```

Start:

```bash
sg docker -c 'bash /opt/pakar/deploy.sh'
```

## 5. Check

```powershell
curl.exe https://<address>/health/ready                              # Healthy
curl.exe -s -o NUL -w "%{http_code}" https://<address>/test-db       # 404
```

Then in a browser on a phone with mobile data: map, login, admin pages, every video source updating.

**server** (first `exit` and connect again, so that your user can run `docker`):

```bash
docker compose -f /opt/pakar/compose.yml ps
docker compose -f /opt/pakar/compose.yml logs --since 5m
docker stats --no-stream
df -h /
```

Reboot once (`sudo reboot`), wait 3 minutes and check again; nothing needs to be started by hand.

## Change or roll back

Edit `IMAGE_TAG` in `/opt/pakar/.env`, then run `bash /opt/pakar/deploy.sh` (**server**).

## Stop

```powershell
az vm deallocate -g <resource group> -n <vm name>
```

Shutting down from inside the machine does not release it. Configuration, files and the static address are kept.

## Problems

Run the `docker compose` commands below on the server inside `/opt/pakar`.

| Symptom | Check |
| --- | --- |
| No certificate | DNS label equals `SITE_ADDRESS`; port 80 open; `docker compose logs web` |
| `api` unhealthy | `DATABASE_URL`; `docker compose logs api` |
| `ai` restarts | `assets/models/best.onnx` present and readable |
| Map stays grey | Camera video names match the files; slots drawn; `docker compose logs ai` |
| Cannot reach the server | Machine running; NSG rules; your IP changed |
