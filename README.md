## CI/CD and deployment

This project includes a GitHub Actions workflow for continuous integration and delivery. On pushes to `main`, the workflow installs the required .NET SDKs, runs build checks, and builds both Docker images for the CoffeeNChill services.

The workflow publishes:

- `${DOCKERHUB_USERNAME}/coffeenchill-backend:part3`
- `${DOCKERHUB_USERNAME}/coffeenchill-gateway:part3`

Add these GitHub repository secrets:

- `DOCKERHUB_USERNAME`
- `DOCKERHUB_TOKEN`

Optional deployment is available for a Google Cloud VPS. Enable it by setting:

- repository variable: `ENABLE_SSH_DEPLOY=true`

Then add:

- `GCP_VPS_HOST`
- `GCP_VPS_USER`
- `GCP_VPS_SSH_PRIVATE_KEY`
- `GCP_VPS_KNOWN_HOSTS`
- `GCP_VPS_SSH_PORT` (optional, default `22`)

The VPS deployment uses SSH and Docker Compose to pull the latest images and start the containers. It does not remove persistent storage or volumes, so existing data remains intact.

Create a secure `.env` file on the VPS with values such as:

```bash
DOCKERHUB_USERNAME=your_dockerhub_username
AZUREWEBJOBS_STORAGE=your_storage_connection_string
STORAGE_CONNECTION_STRING=your_storage_connection_string
JWT_SIGNING_KEY=your_base64_jwt_key
JWT_ISSUER=CoffeeNChill.Gateway
JWT_AUDIENCE=CoffeeNChill.Clients
```

Keep all credentials in GitHub Secrets or a protected VPS `.env` file. Never commit credentials to the repository.

