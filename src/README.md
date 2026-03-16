# Flux — Docker deployment

This repository contains the Flux API (.NET) and Flux Web (Angular). The `Dockerfile.multi` builds both projects and produces a single image that runs the API and the SPA served by nginx using `supervisord`.

Important files
- `Dockerfile.multi` — multi-stage image building the .NET API and Angular app; runtime image runs `supervisord` which starts both the API and nginx.
- `Flux.Web/nginx.multi.conf` — nginx configuration for serving SPA and proxying `/api/` to the local API process.
- `Flux.Web/supervisord.conf` — supervisord configuration to run `dotnet` and `nginx`.
- `docker-compose.yml` — alternative, recommended approach that runs API and web in separate containers.

Build & run (single-container)

From the `src` folder:

```bash
# Build the image
docker build -f Dockerfile.multi -t flux:multi .

# Run (exposes port 80 on host)
docker run --rm -p 80:80 -v $(pwd)/Flux.Api/data:/app/data flux:multi
```

Notes
- The container uses SQLite; the DB file is at `/app/data/bank.db`. The example run command mounts `Flux.Api/data` on the host to persist data.
- The single-container approach is convenient for testing and small deployments, but for production the `docker-compose.yml` (or separate containers) is recommended for better isolation and scaling.
- The image includes a Docker `HEALTHCHECK` which polls the API's `/api/bankaccounts` endpoint.

## JWT auth (local API)

The API is secured with `Microsoft.AspNetCore.Authentication.JwtBearer`.

Use the following local flow:

```bash
# Set credentials in env vars (no hardcoded secrets in commands)
export API_USERNAME="${API_USERNAME:?set API_USERNAME}"
export API_PASSWORD="${API_PASSWORD:?set API_PASSWORD}"
```

```bash
# 1) Register a user (or use /api/auth/login if the user already exists)
curl -s -X POST http://localhost:5271/api/auth/register \
	-H 'Content-Type: application/json' \
	-d "{\"username\":\"$API_USERNAME\",\"password\":\"$API_PASSWORD\"}"
```

```bash
# 2) Login and extract access token (macOS/Linux zsh/bash)
TOKEN=$(curl -s -X POST http://localhost:5271/api/auth/login \
	-H 'Content-Type: application/json' \
	-d "{\"username\":\"$API_USERNAME\",\"password\":\"$API_PASSWORD\"}" \
	| sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p')

echo "TOKEN length: ${#TOKEN}"
```

```bash
# 3) Call a protected endpoint
curl -s http://localhost:5271/api/bankaccounts \
	-H "Authorization: Bearer $TOKEN"
```

```bash
# Optional check: without token should return 401
curl -s -o /dev/null -w "%{http_code}" http://localhost:5271/api/bankaccounts
```

If you want me to add TLS termination, supervised logging rotation, or convert this to a Kubernetes manifest, say which you'd like next.
