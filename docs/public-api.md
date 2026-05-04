# AR Public API

ARTradingTools includes a .NET 9 Web API (`AR.Public.Api`) that runs alongside the existing Node/Express backend and is deployed to Kubernetes.

## File Structure

```
AR.Public.Api/
├── AR.Public.Api.sln
└── AR.Public.Api/
    ├── AR.Public.Api.csproj
    ├── Program.cs
    ├── Dockerfile
    ├── appsettings.json
    ├── appsettings.Development.json
    ├── Controllers/
    │   └── HealthController.cs     # GET /health
    └── Properties/
        └── launchSettings.json
```

## Endpoints

| Method | Path      | Description       |
|--------|-----------|-------------------|
| GET    | `/health` | Health check — returns `{ status: "healthy" }` |

## Development

```bash
cd AR.Public.Api

# Run locally
dotnet run --project AR.Public.Api/AR.Public.Api.csproj
# → http://localhost:5000/health

# Build Docker image
docker build -f AR.Public.Api/Dockerfile -t ar-trading-tools-api:local .
docker run -p 8080:8080 ar-trading-tools-api:local
# → http://localhost:8080/health
```

## Kubernetes

Manifests live in `k8s/` and use `envsubst` for environment substitution:

```
k8s/
├── namespaces/ar-api-namespace.yaml    # namespace: ar-api-namespace
├── deployments/ar-api-deployment.yaml  # image: limitlesssoft/ar-trading-tools-api
├── services/ar-api-service.yaml        # NodePort 80 → 8080
└── ingress/ar-api-ingress.yaml         # host: api.${DEPLOY_ENV}.aleksaristic.com
```

Apply manually:
```bash
export DEPLOY_ENV=production
envsubst < k8s/namespaces/ar-api-namespace.yaml | kubectl apply -f -
envsubst < k8s/services/ar-api-service.yaml | kubectl apply -f -
envsubst < k8s/deployments/ar-api-deployment.yaml | kubectl apply -f -
envsubst < k8s/ingress/ar-api-ingress.yaml | kubectl apply -f -
```

## CI/CD

Three GitHub Actions workflows handle automated deployment:

| File | Purpose |
|------|---------|
| `release-api.yml` | Triggers on push to `master` (API paths) or `workflow_dispatch` |
| `build-and-publish-api.yml` | Builds and pushes Docker image to Docker Hub, then calls deploy |
| `deploy-api.yml` | Applies k8s manifests and restarts the deployment |

Required secrets: `DOCKER_HUB_USERNAME`, `DOCKER_HUB_PASSWORD`, `KUBE_CONFIG_PURE`.
