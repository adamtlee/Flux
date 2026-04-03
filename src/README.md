# Flux — Docker deployment

This repository contains three services: the Flux Auth API (standalone microservice), the Flux API (.NET), and the Flux Web (Angular SPA). The `Dockerfile.multi` builds all projects and produces a single image that runs the auth service, API, and SPA served by nginx using `supervisord`. The `docker-compose.yml` provides a recommended multi-container alternative for better isolation.

## Architecture

- **Flux.Auth.Api** — Standalone microservice handling user registration and authentication (JWT token issuance). Runs on port 5001 (compose) or 5272 (local dev).
- **Flux.Api** — Main API service providing bank account and analytics endpoints. Runs on port 5000. No longer hosts auth endpoints; proxies to auth service when configured.
- **Flux.Web** — Angular SPA served by nginx, reverse-proxying auth requests to the auth service and other requests to the API.

Important files
- `Dockerfile.auth` — builds the Flux.Auth.Api microservice.
- `Dockerfile.api` — builds (legacy) Flux.Api main service.
- `Dockerfile.multi` — multi-stage image building the .NET Auth API, .NET API, and Angular app; runtime runs `supervisord` starting auth, API, and nginx.
- `Flux.Web/nginx.multi.conf` — nginx configuration for serving SPA and proxying `/api/auth/` to auth service and `/api/` to main API.
- `Flux.Web/nginx.conf` — nginx configuration for separate container deployment, proxying to `auth:5001` and `api:5000`.
- `Flux.Web/proxy.conf.json` — Angular dev proxy routing auth requests to `localhost:5272` and other API requests to `localhost:5271`.
- `Flux.Web/supervisord.conf` — supervisord configuration to run `dotnet` (auth), `dotnet` (API), and `nginx`.
- `docker-compose.yml` — recommended approach that runs auth, API, and web in separate containers with proper networking.

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
- Inside the single-container image, processes run on: auth service (5001), API (5000), nginx (80). nginx internally proxies `/api/auth/` to auth and `/api/` to API.
- The image includes a Docker `HEALTHCHECK` which polls the API's `/api/bankaccounts` endpoint.

## Build & run (multi-container with docker-compose) — RECOMMENDED

From the `src` folder:

```bash
# Start all three services
docker-compose up --build

# Or run in background
docker-compose up -d --build

# View logs
docker-compose logs -f

# Stop
docker-compose down
```

Service port mappings:
- **Auth service**: `http://localhost:5001` (container) — accessible from host but typically hit through nginx proxy.
- **API service**: `http://localhost:5000` (container) — accessible from host but typically hit through nginx proxy.
- **Web/nginx**: `http://localhost` (port 80 on host) — main public interface.

The compose file shares the SQLite data volume `./Flux.Api/data` across both auth and API services so they use the same database.

## API endpoints

> All `/api/bankaccounts` endpoints require `Authorization: Bearer <token>`.

### Auth (`/api/auth`)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| **POST** | `/api/auth/register` | Register a new user and return JWT token response |
| **POST** | `/api/auth/login` | Authenticate an existing user and return JWT token response |

### Bank accounts (`/api/bankaccounts`)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| **GET** | `/api/bankaccounts` | Retrieve all bank accounts |
| **GET** | `/api/bankaccounts/{id}` | Retrieve a specific account by GUID |
| **POST** | `/api/bankaccounts` | Create a new bank account |
| **PUT** | `/api/bankaccounts/{id}` | Update an existing bank account |
| **DELETE** | `/api/bankaccounts/{id}` | Delete a bank account |

## JWT auth (local development)

The API and auth service are secured with `Microsoft.AspNetCore.Authentication.JwtBearer`.

### Running locally with separate processes

Start the three services in separate terminals from the `src` folder:

```bash
# Terminal 1: Start the Auth service (listens on http://localhost:5272)
cd Flux.Auth.Api && dotnet run

# Terminal 2: Start the API service (listens on http://localhost:5271)
cd Flux.Api && dotnet run

# Terminal 3: Start the Angular dev server with proxy to local services (http://localhost:4200)
cd Flux.Web && npm start
```

The Angular app's `proxy.conf.json` routes:
- `/api/auth/*` → `http://localhost:5272` (auth service)
- `/api/*` → `http://localhost:5271` (API service)

### Testing the services locally

```bash
# Set credentials in env vars (no hardcoded secrets in commands)
export API_USERNAME="${API_USERNAME:?set API_USERNAME}"
export API_PASSWORD="${API_PASSWORD:?set API_PASSWORD}"
```

```bash
# 1) Register a user (calls the auth microservice)
curl -s -X POST http://localhost:5272/api/auth/register \
	-H 'Content-Type: application/json' \
	-d "{\"username\":\"$API_USERNAME\",\"password\":\"$API_PASSWORD\"}"
```

```bash
# 2) Login and extract access token (macOS/Linux zsh/bash)
TOKEN=$(curl -s -X POST http://localhost:5272/api/auth/login \
	-H 'Content-Type: application/json' \
	-d "{\"username\":\"$API_USERNAME\",\"password\":\"$API_PASSWORD\"}" \
	| sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p')

echo "TOKEN length: ${#TOKEN}"
```

```bash
# 3) Call a protected endpoint (API service validates JWT from auth service)
curl -s http://localhost:5271/api/bankaccounts \
	-H "Authorization: Bearer $TOKEN"
```

```bash
# Optional check: without token should return 401
curl -s -o /dev/null -w "%{http_code}" http://localhost:5271/api/bankaccounts
```

## Port reference

| Service | Docker Compose | Local Dev | Notes |
| :--- | :--- | :--- | :--- |
| **Auth API** | 5001 | 5272 | JWT token issuance (register, login) |
| **Main API** | 5000 | 5271 | Bank accounts, analytics (requires auth token) |
| **Web (nginx)** | 80 | 4200 | SPA entry point, reverse proxy to auth & API |
| **Web (Angular dev)** | — | 4200 | Dev server with hot reload, proxies to 5272 & 5271 |
