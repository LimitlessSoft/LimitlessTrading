# LT Public API

Limitless Trading includes a .NET 9 Web API (`LT.Public.Api`) that runs alongside the existing Node/Express backend and is deployed to Kubernetes.

## File Structure

```
LT.Public.Api/
├── LT.Public.Api.sln
└── LT.Public.Api/
    ├── LT.Public.Api.csproj
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
cd LT.Public.Api

# Run locally
dotnet run --project LT.Public.Api/LT.Public.Api.csproj
# → http://localhost:5000/health

# Build Docker image
docker build -f LT.Public.Api/Dockerfile -t limitless-trading-api:local .
docker run -p 8080:8080 limitless-trading-api:local
# → http://localhost:8080/health
```

## Kubernetes

Manifests live in `k8s/` and use `envsubst` for environment substitution:

```
k8s/
├── namespaces/lt-api-namespace.yaml    # namespace: lt-api-namespace
├── deployments/lt-api-deployment.yaml  # image: limitlesssoft/limitless-trading-api
├── services/lt-api-service.yaml        # NodePort 80 → 8080
└── ingress/lt-api-ingress.yaml         # host: api.limitlessworkstation.com
```

Apply manually:
```bash
export DEPLOY_ENV=production
envsubst < k8s/namespaces/lt-api-namespace.yaml | kubectl apply -f -
envsubst < k8s/services/lt-api-service.yaml | kubectl apply -f -
envsubst < k8s/deployments/lt-api-deployment.yaml | kubectl apply -f -
envsubst < k8s/ingress/lt-api-ingress.yaml | kubectl apply -f -
```

## CI/CD

Three GitHub Actions workflows handle automated deployment:

| File | Purpose |
|------|---------|
| `release-api.yml` | Triggers on push to `master` (API paths) or `workflow_dispatch` |
| `build-and-publish-api.yml` | Builds and pushes Docker image to Docker Hub, then calls deploy |
| `deploy-api.yml` | Applies k8s manifests and restarts the deployment |

Required secrets: `DOCKER_HUB_USERNAME`, `DOCKER_HUB_PASSWORD`, `KUBE_CONFIG_PURE`.
