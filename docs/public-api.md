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

| Method | Path                    | Auth     | Description                                     |
|--------|-------------------------|----------|-------------------------------------------------|
| GET    | `/health`               |          | Health check — returns `{ status: "healthy" }`  |
| GET    | `/api/widgets`          |          | Widget catalog — returns all available widget types |
| GET    | `/api/whats-new`        |          | Release notes — returns entries newest-first    |
| POST   | `/api/whats-new`        | JWT      | Create a release notes entry                    |
| PUT    | `/api/whats-new/{id}`   | JWT      | Update a release notes entry                    |
| DELETE | `/api/whats-new/{id}`   | JWT      | Delete a release notes entry                    |
| POST   | `/api/auth/login`       |          | Obtain a JWT token for admin access             |

### `GET /api/widgets`

Returns the static catalog of all widget types available in Limitless Trading.

```
GET https://api.limitlessworkstation.com/api/widgets
```

Response shape:

```json
[
  {
    "type": "chart",
    "name": "Chart",
    "description": "Candlestick / line chart with drawing tools and indicators.",
    "tags": ["price", "technicals"],
    "previewImageUrl": "/gallery/previews/chart.png",
    "configSnippet": {
      "type": "chart",
      "symbol": "SPY",
      "width": 12,
      "height": 400
    }
  }
]
```

The catalog is served by `WidgetCatalogService` (static, no database reads). It powers the public widget gallery at `limitlessworkstation.com/gallery`.

### `GET /api/whats-new`

Returns release notes entries in descending `publishedAt` order. No authentication required.

```
GET https://api.limitlessworkstation.com/api/whats-new?limit=10&offset=0
```

Query params: `limit` (default 10, max 50), `offset` (default 0).

Response shape:

```json
[
  {
    "id": 1,
    "version": "0.2.0",
    "title": "Widgets Gallery",
    "body": "Browse and import community-shared widget layouts...",
    "publishedAt": "2026-05-05T00:00:00Z",
    "tags": ["workstation", "widgets"]
  }
]
```

Entries are managed via the admin panel at `admin.limitlessworkstation.com`. The Workstation home page and `limitlessworkstation.com/whats-new` both consume this endpoint.

### `POST /api/auth/login`

Authenticates the admin and returns a JWT token. Credentials are configured via `Admin:Email`, `Admin:Password`, and `Admin:JwtSecret` in app settings.

```
POST https://api.limitlessworkstation.com/api/auth/login
Content-Type: application/json

{ "email": "admin@example.com", "password": "..." }
```

Response shape:

```json
{ "token": "<jwt>" }
```

Returns 401 on invalid credentials. The token is valid for 30 days, signed with HS256, issuer and audience `"LimitlessTrading"`.

### `POST /api/whats-new` (auth required)

Creates a new release notes entry. Pass the JWT in the `Authorization: Bearer <token>` header.

Request body:

```json
{
  "version": "0.3.0",
  "title": "New feature",
  "body": "Description...",
  "publishedAt": "2026-05-05T00:00:00Z",
  "tags": ["workstation"]
}
```

Returns the created entry. Returns 401 if not authenticated.

### `PUT /api/whats-new/{id}` (auth required)

Updates an existing entry. Same request body as POST. Returns 404 if the entry is not found.

### `DELETE /api/whats-new/{id}` (auth required)

Deletes an entry. Returns 204 on success, 404 if the entry is not found.

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

> **Required before first deploy:** copy the `regcred` image pull secret into this namespace or the pod will fail with `ImagePullBackOff`.
>
> ```bash
> kubectl get secret regcred -n lt-web-namespace -o json | \
>   sed 's/"namespace": "lt-web-namespace"/"namespace": "lt-api-namespace"/' | \
>   kubectl apply -f -
> ```

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
| `release-api.yml` | Trigger (API path changes or `workflow_dispatch`); bumps patch version, commits it, then calls build + deploy |
| `build-and-publish-api.yml` | Builds and pushes Docker image to Docker Hub, then calls deploy |
| `deploy-api.yml` | Applies k8s manifests and restarts the deployment |

Required secrets: `DOCKER_HUB_USERNAME`, `DOCKER_HUB_PASSWORD`, `KUBE_CONFIG_PURE`.
