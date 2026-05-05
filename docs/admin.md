# LT Admin

LT Admin is a React + Vite SPA used to manage "What's New" release notes entries. It is deployed to Kubernetes at `admin.limitlessworkstation.com` and authenticates against the LT Public API using JWT.

## File Structure

```
LimitlessTrading.Admin/
├── package.json
├── vite.config.js
├── index.html
├── Dockerfile
├── nginx.conf
└── src/
    ├── main.jsx             # Chakra UI + React Router entry point
    ├── App.jsx              # Routes: /login → LoginPage, / → WhatsNewPage
    ├── api.js               # fetch helpers — reads VITE_API_BASE
    ├── pages/
    │   ├── LoginPage.jsx    # Email/password form, stores JWT in localStorage
    │   └── WhatsNewPage.jsx # Table of entries with create/edit/delete
    └── components/
        └── WhatsNewModal.jsx # Chakra UI Modal for create/edit
```

## Development

```bash
cd src/LimitlessTrading/LimitlessTrading.Admin

npm install
npm run dev
# → http://localhost:5174
```

The dev server proxies `/api` requests to `http://localhost:5096` (the Public API). Start the API first:

```bash
cd src/LimitlessTrading/LimitlessTrading.Public/LT.Public.Api
dotnet run --project LT.Public.Api/LT.Public.Api.csproj
```

Default dev credentials (set in `appsettings.Development.json`):
- Email: `aristiccitsira@gmail.com`
- Password: `admin`

## Docker

```bash
docker build \
  --build-arg VITE_API_BASE=https://api.limitlessworkstation.com \
  -f Dockerfile \
  -t limitlesssoft/lt-admin:local \
  .
docker run -p 8080:80 limitlesssoft/lt-admin:local
```

## Kubernetes

Manifests live in `k8s/` under the `lt-admin-namespace` namespace:

```
k8s/
├── namespaces/lt-admin-namespace.yaml
├── deployments/lt-admin-deployment.yaml   # image: limitlesssoft/lt-admin
├── services/lt-admin-service.yaml         # NodePort 80
└── ingress/lt-admin-ingress.yaml          # host: admin.limitlessworkstation.com
```

**First-time setup** — create the `regcred` image pull secret in the namespace (copy from an existing namespace):

```bash
kubectl get secret regcred -n lt-api-namespace -o json | \
  sed 's/"namespace": "lt-api-namespace"/"namespace": "lt-admin-namespace"/' | \
  kubectl apply -f -
```

Apply manually:

```bash
export DEPLOY_ENV=production

envsubst < k8s/namespaces/lt-admin-namespace.yaml | kubectl apply -f -
envsubst < k8s/services/lt-admin-service.yaml | kubectl apply -f -
envsubst < k8s/deployments/lt-admin-deployment.yaml | kubectl apply -f -
envsubst < k8s/ingress/lt-admin-ingress.yaml | kubectl apply -f -
kubectl rollout restart -n lt-admin-namespace deployment $DEPLOY_ENV-lt-admin-deployment
```

## CI/CD

Three GitHub Actions workflows handle automated deployment:

| File | Purpose |
|------|---------|
| `release-admin.yml` | Trigger (Admin path changes or `workflow_dispatch`); calls build + deploy |
| `build-and-publish-admin.yml` | Builds and pushes Docker image to Docker Hub, then calls deploy |
| `deploy-admin.yml` | Applies k8s manifests and restarts deployment |

Required secrets: `DOCKER_HUB_USERNAME`, `DOCKER_HUB_PASSWORD`, `KUBE_CONFIG_PURE`.
