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

## Database

A self-hosted PostgreSQL 16 StatefulSet runs in `lt-api-namespace` alongside the API. Data is persisted via a 1 Gi PersistentVolumeClaim. The API receives the connection string via the `ConnectionStrings__DefaultConnection` environment variable, injected from the `lt-api-postgres-secret` Secret.

```
k8s/
├── secrets/lt-api-postgres-secret.yaml     # credentials + connection string
├── storage/lt-api-postgres-pvc.yaml        # 1 Gi PVC
├── statefulsets/lt-api-postgres-statefulset.yaml
└── services/lt-api-postgres-service.yaml   # ClusterIP on port 5432
```

The connection string is accessible in the .NET app via `IConfiguration["ConnectionStrings:DefaultConnection"]` (or `builder.Configuration.GetConnectionString("DefaultConnection")`).

## Kubernetes

Manifests live in `k8s/` and use `envsubst` for environment substitution:

```
k8s/
├── namespaces/lt-api-namespace.yaml    # namespace: lt-api-namespace
├── secrets/lt-api-postgres-secret.yaml
├── storage/lt-api-postgres-pvc.yaml
├── statefulsets/lt-api-postgres-statefulset.yaml
├── deployments/lt-api-deployment.yaml  # image: limitlesssoft/limitless-trading-api
├── services/lt-api-service.yaml        # NodePort 80 → 8080
├── services/lt-api-postgres-service.yaml
└── ingress/lt-api-ingress.yaml         # host: api.limitlessworkstation.com
```

**First-time setup** — create the `regcred` image pull secret in the namespace (copy from an existing namespace):
```bash
kubectl get secret regcred -n <source-namespace> -o json | \
  sed 's/"namespace": "<source-namespace>"/"namespace": "lt-api-namespace"/' | \
  kubectl apply -f -
```

Apply manually:
```bash
export DEPLOY_ENV=production
export POSTGRES_USER=ltuser
export POSTGRES_PASSWORD=<your-password>

envsubst < k8s/namespaces/lt-api-namespace.yaml | kubectl apply -f -
envsubst < k8s/secrets/lt-api-postgres-secret.yaml | kubectl apply -f -
kubectl apply -f k8s/storage/lt-api-postgres-pvc.yaml
kubectl apply -f k8s/statefulsets/lt-api-postgres-statefulset.yaml
kubectl apply -f k8s/services/lt-api-postgres-service.yaml
envsubst < k8s/services/lt-api-service.yaml | kubectl apply -f -
envsubst < k8s/deployments/lt-api-deployment.yaml | kubectl apply -f -
envsubst < k8s/ingress/lt-api-ingress.yaml | kubectl apply -f -
kubectl rollout restart -n lt-api-namespace deployment $DEPLOY_ENV-lt-api-deployment
```

## Versioning

The API version is stored in the `<Version>` tag of `LT.Public.Api.csproj`. CI bumps it automatically on every release (patch). To bump manually:

```bash
node scripts/set-api-version.js           # patch
node scripts/set-api-version.js --minor
node scripts/set-api-version.js --major
```

## CI/CD

Three GitHub Actions workflows handle automated deployment. `release-api.yml` orchestrates the full pipeline:

| File | Purpose |
|------|---------|
| `release-api.yml` | Trigger (API path changes or `workflow_dispatch`); bumps patch version, commits it, then calls build + deploy, then creates a GitHub release tagged `api-v{version}` |
| `build-and-publish-api.yml` | Builds and pushes Docker image to Docker Hub, then calls deploy |
| `deploy-api.yml` | Applies k8s manifests and restarts the deployment |

Required secrets: `DOCKER_HUB_USERNAME`, `DOCKER_HUB_PASSWORD`, `KUBE_CONFIG_PURE`.
