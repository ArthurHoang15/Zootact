# Zootact OCI Always Free + Cloudflare Pages Deployment

This runbook deploys Zootact with:

- Frontend: Cloudflare Pages
- Backend API + SignalR: OCI Always Free Ampere A1 VM
- PostgreSQL: Docker container on the OCI VM
- Redis: Docker container on the OCI VM
- Reverse proxy and HTTPS: Caddy on the OCI VM
- AI service: disabled for production v1

## 1. OCI VM

Create an Always Free VM:

- Shape: `VM.Standard.A1.Flex`
- Target size: `4 OCPU / 24 GB RAM`
- Fallback size: `2 OCPU / 12 GB RAM`
- OS: Ubuntu 24.04 ARM64
- Boot volume: about `100 GB`

Open only these public ports in the OCI security list:

- `80/tcp`
- `443/tcp`
- `22/tcp` from your own IP if possible

Do not expose `5432`, `6379`, or `8080`.

## 2. Backend Hostname

Point a hostname to the OCI VM public IP. A free DDNS host is fine for v1:

```text
zootact-api.example.com -> <OCI public IP>
```

This value becomes `PUBLIC_HOST` in `.env.production`.

## 3. Server Setup

SSH into the VM and install Docker:

```bash
sudo apt update
sudo apt install -y ca-certificates curl git ufw
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker "$USER"
```

Log out and SSH back in so the Docker group applies.

Enable a minimal firewall:

```bash
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw allow from <trusted_ip_or_cidr> to any port 22
sudo ufw --force enable
```

Clone and prepare the repo:

```bash
sudo mkdir -p /opt/zootact
sudo chown "$USER":"$USER" /opt/zootact
git clone <your-repo-url> /opt/zootact/app
cd /opt/zootact/app
cp deploy/oci.env.example .env.production
```

Fill `.env.production` with real values.

For Firebase Admin credentials, base64-encode the service-account JSON:

```bash
base64 -w 0 firebase-adminsdk.json
```

Put the output into `FIREBASE_SERVICE_ACCOUNT_JSON_BASE64`.

## 4. Start Backend Stack

From `/opt/zootact/app`:

```bash
docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build
```

Check status:

```bash
docker compose -f docker-compose.prod.yml --env-file .env.production ps
docker compose -f docker-compose.prod.yml --env-file .env.production logs -f backend
```

Health checks:

```bash
curl -fsS https://$PUBLIC_HOST/health/live
curl -fsS https://$PUBLIC_HOST/health/ready
```

## 5. Cloudflare Pages Frontend

Create a Cloudflare Pages project from the repo:

- Root directory: `frontend`
- Build command: `bun run build`
- Build output directory: `dist`

Set production env vars:

```env
VITE_API_BASE_URL=https://zootact-api.example.com/api
VITE_SIGNALR_URL=https://zootact-api.example.com/game-hub
VITE_PUBLIC_APP_URL=https://zootact.pages.dev
VITE_ENABLE_AI_ANALYSIS=false
VITE_FIREBASE_API_KEY=<firebase-client-value>
VITE_FIREBASE_AUTH_DOMAIN=<firebase-client-value>
VITE_FIREBASE_PROJECT_ID=<firebase-client-value>
VITE_FIREBASE_STORAGE_BUCKET=<firebase-client-value>
VITE_FIREBASE_MESSAGING_SENDER_ID=<firebase-client-value>
VITE_FIREBASE_APP_ID=<firebase-client-value>
```

Add the Pages frontend host to Firebase Authorized Domains:

```text
zootact.pages.dev
```

If email-link auth is enabled, use this callback URL:

```text
https://zootact.pages.dev/auth/email-link
```

## 6. Smoke Test

Verify these flows after both sides deploy:

1. Load the Cloudflare Pages frontend.
2. Firebase login succeeds.
3. Backend auth/profile API calls succeed through `VITE_API_BASE_URL`.
4. Create and join a private lobby.
5. Start a match and confirm SignalR connects through `VITE_SIGNALR_URL`.
6. Refresh `/game` and confirm active match recovery.
7. Confirm AI analysis is hidden or disabled.

## 7. Backup

Run a manual backup from the repo directory on the VM:

```bash
bash deploy/backup-postgres.sh
```

Add a daily cron job:

```bash
crontab -e
```

```cron
15 2 * * * cd /opt/zootact/app && BACKUP_DIR=/opt/zootact/backups/postgres bash deploy/backup-postgres.sh >> /opt/zootact/backups/postgres/backup.log 2>&1
```

Restore into a clean database only after stopping the backend:

```bash
gunzip -c /opt/zootact/backups/postgres/zootact-postgres-YYYYMMDDTHHMMSSZ.sql.gz | docker compose -f docker-compose.prod.yml --env-file .env.production exec -T postgres sh -c 'psql -U "$POSTGRES_USER" "$POSTGRES_DB"'
```

## 8. AI Service Later

Production v1 keeps AI disabled:

```env
AI_SERVICE_ENABLED=false
VITE_ENABLE_AI_ANALYSIS=false
```

When ready, add an internal `ai-service` container, set:

```env
AI_SERVICE_ENABLED=true
AI_SERVICE_BASE_URL=http://ai-service:8001
VITE_ENABLE_AI_ANALYSIS=true
```

Then redeploy backend and frontend.
