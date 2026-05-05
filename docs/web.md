# LT Web

Limitless Trading includes a static public website (`LimitlessTrading.Web`) built with Astro and Tailwind CSS, deployed to Kubernetes at `limitlessworkstation.com`.

## File Structure

```
LimitlessTrading.Web/
├── astro.config.mjs
├── tailwind.config.mjs
├── tsconfig.json
├── package.json
├── Dockerfile
├── nginx.conf
├── public/
└── src/
    ├── components/
    ├── content/
    ├── data/
    ├── layouts/
    ├── pages/
    │   ├── index.astro
    │   ├── about.astro
    │   ├── features.astro
    │   ├── demos.astro
    │   ├── downloads.astro
    │   └── whats-new.astro
    └── styles/
```

## Development

```bash
cd src/LimitlessTrading/LimitlessTrading.Web

# Run locally
npm install
npm run dev
# → http://localhost:4321

# Build static output
npm run build

# Preview production build
npm run preview

# Build Docker image
docker build -t lt-web:local .
docker run -p 8080:80 lt-web:local
# → http://localhost:8080
```

The site is built as a fully static output (`output: 'static'`) and served via nginx.

## Kubernetes

Manifests live in `k8s/` and use `envsubst` for environment substitution:

```
k8s/
├── namespaces/lt-web-namespace.yaml    # namespace: lt-web-namespace
├── deployments/lt-web-deployment.yaml  # image: limitlesssoft/lt-web
├── services/lt-web-service.yaml        # NodePort 80 → 80
└── ingress/lt-web-ingress.yaml         # host: limitlessworkstation.com
```

**First-time setup** — create the `regcred` image pull secret in the namespace (copy from an existing namespace):
```bash
kubectl get secret regcred -n <source-namespace> -o json | \
  sed 's/"namespace": "<source-namespace>"/"namespace": "lt-web-namespace"/' | \
  kubectl apply -f -
```

Apply manually:
```bash
export DEPLOY_ENV=production
envsubst < k8s/namespaces/lt-web-namespace.yaml | kubectl apply -f -
envsubst < k8s/services/lt-web-service.yaml | kubectl apply -f -
envsubst < k8s/deployments/lt-web-deployment.yaml | kubectl apply -f -
envsubst < k8s/ingress/lt-web-ingress.yaml | kubectl apply -f -
kubectl rollout restart -n lt-web-namespace deployment $DEPLOY_ENV-lt-web-deployment
```

## CI/CD

Three GitHub Actions workflows handle automated deployment:

| File | Purpose |
|------|---------|
| `release-web.yml` | Triggers on push to `master` (Web paths) or `workflow_dispatch` |
| `build-and-publish-web.yml` | Builds and pushes Docker image to Docker Hub, then calls deploy |
| `deploy-web.yml` | Applies k8s manifests and restarts the deployment |

Required secrets: `DOCKER_HUB_USERNAME`, `DOCKER_HUB_PASSWORD`, `KUBE_CONFIG_PURE`.
