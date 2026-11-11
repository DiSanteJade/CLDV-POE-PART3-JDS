# CoffeeNChill — CI/CD

GitHub Actions builds the .NET 10 Gateway on pushes and pull requests to `main`. It builds the .NET 8 backend when its source is available, then publishes Docker images tagged `:part3` on pushes to `main`.

> The backend project is currently missing from this repository. Add `cldv part 3 copy/CoffeeNChill/CoffeeNChill.csproj` and its source to enable the backend build and image publish. Until then, the workflow reports a warning and skips that step; it cannot publish an image for absent code.

## Docker Hub setup

Add these repository secrets in **Settings → Secrets and variables → Actions**:

- `DOCKERHUB_USERNAME`
- `DOCKERHUB_TOKEN` (a Docker Hub access token)

Create the Docker Hub repositories `coffeenchill-backend` and `coffeenchill-gateway` under that account. The workflow publishes `coffeenchill-gateway:part3` and, when backend source is present, `coffeenchill-backend:part3`.

## Optional Google Cloud VPS deployment

Deployment is disabled by default. To enable it, set repository variable `ENABLE_SSH_DEPLOY` to `true`, then add these repository secrets:

- `GCP_VPS_HOST`, `GCP_VPS_USER`
- `GCP_VPS_SSH_PRIVATE_KEY`, `GCP_VPS_KNOWN_HOSTS`
- `GCP_VPS_SSH_PORT` (optional; defaults to `22`)

The VPS needs Docker Compose and `/opt/coffeenchill/.env` containing `DOCKERHUB_USERNAME`, `AZUREWEBJOBS_STORAGE`, `STORAGE_CONNECTION_STRING`, and `JWT_SIGNING_KEY`. Optional `JWT_ISSUER` and `JWT_AUDIENCE` default to `CoffeeNChill.Gateway` and `CoffeeNChill.Clients`. Restrict access to `.env` (for example, `chmod 600`) and never commit credentials.

Deployment pulls the images and runs `docker compose up -d`; it does not run volume-removal or cleanup commands. Persistent data is not deleted. The backend deployment will only run once its source has been added and published.

## Local Gateway build

```bash
cd "cldv part 3 copy"
dotnet restore CoffeeNChill.Gateway/CoffeeNChill.Gateway.csproj
dotnet build CoffeeNChill.Gateway/CoffeeNChill.Gateway.csproj --configuration Release --no-restore
```
