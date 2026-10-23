# CI/CD by hand, then automated

What a person would do to get this project from code to a live site with no pipeline,
step by step, and how each step became part of `.github/workflows/ci.yml`.

Project: https://github.com/muhannadsnan/company-registry-api-dotnet
Live: https://company-registry-api-dotnet-latest.onrender.com

---

## 0. The idea in four lines

- **CI (continuous integration):** every change is checked automatically: style, security, build, tests.
- **CD (continuous delivery/deployment):** a change that passes CI is packaged and shipped automatically.
- **The pipeline** is the written-down list of those steps, run by a machine every time, in the same order.
- Doing it by hand works, but a person skips steps when tired. The pipeline never does.

---

## Part A: one-time setup (done once, by hand, even with a pipeline)

| # | What | How (this project) | Azure equivalent |
|---|---|---|---|
| A1 | Code repository | GitHub repo, branch `main` | Azure Repos / GitHub |
| A2 | Database | Neon project in Frankfurt, connection string → `Host=…;Database=…;Username=…;Password=…;SslMode=Require` | Azure Database for PostgreSQL |
| A3 | Container registry | GitHub Container Registry (`ghcr.io/<user>/<repo>`). Image made public so the host can pull it | Azure Container Registry (ACR) |
| A4 | Host | Render Web Service → "Existing image" → `ghcr.io/…:latest`, Frankfurt, health check `/health` | Azure Container Apps / App Service |
| A5 | Configuration + secrets | Render environment variables (`ConnectionStrings__Companies`, `Auth__SigningKey`, …). Secrets generated with `openssl rand` | App settings + Key Vault |
| A6 | Deploy trigger | Render deploy hook URL, stored as GitHub secret `RENDER_DEPLOY_HOOK_URL` | Pipeline service connection (OIDC) |

```bash
# Generate secrets (A5)
openssl rand -base64 48        # signing key
openssl rand -hex 24           # client secret

# Store the deploy hook in GitHub (A6); paste when asked
gh secret set RENDER_DEPLOY_HOOK_URL --repo muhannadsnan/company-registry-api-dotnet
```

---

## Part B: one release, fully by hand

### B1. Start from a clean, current copy

```bash
cd ~/Documents/dotnet-demo
git switch main && git pull
git status                      # must be clean: release exactly what is committed
SHA=$(git rev-parse HEAD)       # the commit this release is made from
```

### B2. Check the backend

```bash
dotnet restore
dotnet format --verify-no-changes                          # style
dotnet list package --vulnerable --include-transitive      # known security holes in NuGet packages
dotnet build --configuration Release -warnaserror          # compiles, no warnings
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/CompanyRegistry.Api   # forgot a migration?
dotnet test                                                # unit + integration (Docker must run)
```
Any failure: **stop**. Nothing below happens.

### B3. Check the frontend

```bash
cd web
npm ci                          # exact versions from package-lock.json (not npm install)
npm run lint                    # ESLint
npm audit --audit-level=high    # known security holes in npm packages
npm run build                   # tsc type-check + production build
cd ..
```

### B4. Build the image

```bash
IMAGE=ghcr.io/muhannadsnan/company-registry-api-dotnet
docker build --tag "$IMAGE:$SHA" --tag "$IMAGE:latest" .
```
- Tag with the **commit SHA**: you always know exactly which code is running, and can go back to it.
- `latest` is just a moving pointer to the newest.

### B5. Smoke-test the image before shipping it

```bash
docker run --rm -d --name smoke -p 8090:8080 --env-file render.env "$IMAGE:$SHA"
curl -s localhost:8090/health                                   # Healthy
curl -s -o /dev/null -w '%{http_code}\n' localhost:8090/       # 200 (the React app)
curl -s -o /dev/null -w '%{http_code}\n' localhost:8090/api/companies   # 401 (needs a token)
docker rm -f smoke
```

### B6. Push the image to the registry

```bash
# A GitHub personal access token with write:packages scope
echo "$GITHUB_PAT" | docker login ghcr.io --username muhannadsnan --password-stdin
docker push --all-tags "$IMAGE"
```

### B7. Database migrations

Two options:
- **On start (this project):** `Database__MigrateOnStartup=true`, so the new container applies pending migrations before serving. Simple; fine for one instance.
- **Before deploy (larger systems):** build a migration bundle and run it against production first:
  ```bash
  dotnet ef migrations bundle --project src/CompanyRegistry.Api --output efbundle
  ./efbundle --connection "$PRODUCTION_CONNECTION_STRING"
  ```
  For schema changes, use **expand/contract** so the old version keeps working during the switch:
  add new → write both → read new → drop old in a later release.

### B8. Deploy

```bash
curl --fail -X POST "$RENDER_DEPLOY_HOOK_URL&imgURL=$IMAGE:$SHA"
```
Or in Render: **Manual Deploy → Deploy an existing image**. Render pulls the image, starts it, waits for
`/health` to answer, then switches traffic. If health never answers, the old version keeps running.

### B9. Verify live

```bash
B=https://company-registry-api-dotnet-latest.onrender.com
curl -s $B/health
curl -s -o /dev/null -w '%{http_code}\n' $B/
```
- Open the site, run a search.
- Render **Logs**: no errors after the start.
- Neon: `__EFMigrationsHistory` lists the new migration.

### B10. Roll back if something is wrong

Deploy the previous good image by its SHA tag:
```bash
curl --fail -X POST "$RENDER_DEPLOY_HOOK_URL&imgURL=$IMAGE:<previous-sha>"
```
Code rolls back easily; **data does not**. That's why destructive migrations (drop column) come in a
later release, once you're sure.

---

## Part C: the same steps in the pipeline

| Manual step | In `ci.yml` |
|---|---|
| B1 clean checkout | trigger `on: push` to `main` / pull request; `actions/checkout` |
| B2 backend checks | job **backend**: restore, format, vulnerable packages, build `-warnaserror`, missing migrations, test |
| B3 frontend checks | job **frontend** (runs in parallel): `npm ci`, lint, audit, build |
| stop on failure | `deploy: needs: [backend, frontend]`, so deploy never starts if either fails |
| B4 build image | job **deploy**: `docker build` with SHA + `latest` tags (the Dockerfile has a Node stage, an SDK stage and a runtime stage) |
| B5 smoke test | covered by the integration tests + Render's `/health` check before switching |
| B6 push | `docker login ghcr.io` with the built-in `GITHUB_TOKEN` (no personal token needed), `docker push` |
| B7 migrations | on start (`MigrateOnStartup`) |
| B8 deploy | `curl` the deploy hook from the `RENDER_DEPLOY_HOOK_URL` **secret** with `imgURL=…:<sha>` |
| B9 verify | Render health check; I check logs and the site |
| B10 rollback | deploy hook with an older SHA (manual, on purpose) |

What automation adds beyond saving time:
- **No skipped steps:** the same checks, in the same order, every time.
- **The same environment:** a fresh Ubuntu runner, not "works on my machine".
- **Traceability:** every running image is tagged with the commit it was built from, and each run is logged in GitHub Actions.
- **Gates:** nothing reaches `main`'s live site without passing every check.
- **Secrets handled properly:** they live in GitHub/Render, never in the repo or on someone's laptop.

---

## Part D: vocabulary

| Word | Meaning |
|---|---|
| Pipeline / workflow | The automated list of steps (`.github/workflows/ci.yml`) |
| Trigger | What starts it (`push`, `pull_request`) |
| Runner | The fresh machine GitHub starts to run a job (`ubuntu-latest`) |
| Job | A group of steps on one runner; jobs run in parallel unless `needs:` orders them |
| Step | One command or action inside a job |
| Action | A reusable step (`actions/checkout`, `actions/setup-node`) |
| Gate | A condition that must pass before the next stage (`needs:`, `if: github.ref == 'refs/heads/main'`) |
| Artifact | A file a run keeps (our test results) |
| Image | The packaged app + runtime + minimal OS, built from the Dockerfile |
| Multi-stage build | Build tools in early stages, only the result in the final image (smaller, safer) |
| Registry | Where images are stored and pulled from (ghcr.io, ACR, Docker Hub) |
| Tag | A name for an image version (`:<sha>`, `:latest`) |
| Secret | A value the pipeline can use but never prints (`secrets.RENDER_DEPLOY_HOOK_URL`) |
| Deploy hook | A URL that tells the host "deploy now" |
| Health check | An endpoint the host polls to decide if the app is alive (`/health`) |
| Rollback | Going back to the previous working version |
| Continuous delivery vs deployment | Delivery: every change *could* go live (a person presses the button). Deployment: it goes live automatically. This project does **deployment** |

---

## Part E: how to say it

> "Every push runs two CI jobs in parallel: the backend job checks formatting, scans NuGet packages for
> vulnerabilities, builds with warnings as errors, fails on a forgotten EF migration and runs the tests
> against a real Postgres. The frontend job lints, audits and type-checks the React app. Only if both pass,
> on main, the deploy job builds one Docker image with the frontend inside, tags it with the commit SHA,
> pushes it to the registry and tells the host to deploy exactly that tag. Migrations run on start, and the
> host only switches traffic once the health check answers. On Azure the same design would be ACR plus
> Container Apps, with the pipeline signing in through OIDC instead of a stored secret."
