# Plan: Minimal File Manager on Azure (ASP.NET Core 10 + React/Vite + GitHub Actions)

## Context
Interview exercise (40–60 min). The folder `InterviewIsraelPost` is empty and not a git repo. Goal: a working end-to-end app: upload, list and download files, stored in Azure Blob Storage, served from ONE Linux App Service (React built into `wwwroot`), logging to App Insights, deployed by GitHub Actions on push to `main`. Minimal: no tests, no styling, no extras.

## Target layout
```
/infra/create-resources.ps1         # every az command, commented
/src/FileManager.Api/               # ASP.NET Core Web API (.NET 10)
  Controllers/FilesController.cs
  Services/IFileStorageService.cs
  Services/BlobFileStorageService.cs
  Options/StorageOptions.cs
  Program.cs, appsettings.json (ContainerName only, no secrets)
/src/web/                           # Vite React-TS; build outDir -> ../FileManager.Api/wwwroot
/.github/workflows/deploy.yml
/.gitignore                         # bin/obj/node_modules/wwwroot/publish/*.user, PublishSettings
```

## Stage 1 – Azure resources (run one command at a time, explain each first)
Variables at top of `infra/create-resources.ps1`: `$rg=rg-interview-rocheli-20261104`, `$loc=westeurope`, suffix `rocheli1104`.
Names: `asp-filemgr-rocheli1104`, `app-filemgr-rocheli1104`, `stfilemgrrocheli1104` (storage: lowercase, ≤24 chars), `log-filemgr-rocheli1104`, `appi-filemgr-rocheli1104`.

1. `az account show` + `az group show -n $rg` — confirm subscription/RG.
2. `az webapp list-runtimes --os linux` — confirm `DOTNETCORE:10.0` exists.
3. `az storage account create` — StorageV2, Standard_LRS, `--access-tier Hot`, `--min-tls-version TLS1_2`, `--allow-blob-public-access false`.
4. `az storage account show-connection-string` → `$conn` (kept in a PS variable only, never written to a file).
5. `az storage container create -n files --connection-string $conn`.
6. `az monitor log-analytics workspace create`.
7. `az monitor app-insights component create --workspace <workspaceId>` (may prompt to install `application-insights` extension — allow) → capture `connectionString`.
8. `az appservice plan create --is-linux --sku F1`; if it fails (quota/region), retry with `--sku B1`.
9. `az webapp create --runtime "DOTNETCORE:10.0"`.
10. `az webapp config appsettings set`: `Storage__ConnectionString=$conn`, `APPLICATIONINSIGHTS_CONNECTION_STRING=<ai conn>`.
11. Enable SCM basic auth (new apps have it off, publish-profile deploy needs it):
    `az resource update --resource-group $rg --namespace Microsoft.Web --resource-type basicPublishingCredentialsPolicies --parent sites/app-filemgr-rocheli1104 --name scm --set properties.allow=true`.

**Verify:** `az resource list -g $rg -o table` shows all 5 resources; `az storage container exists`; browsing `https://app-filemgr-rocheli1104.azurewebsites.net` shows the default placeholder page.

## Stage 2 – Backend
- `dotnet new webapi -n FileManager.Api -o src/FileManager.Api --use-controllers`; delete WeatherForecast sample.
- Packages: `Azure.Storage.Blobs`, `Azure.Monitor.OpenTelemetry.AspNetCore`.
- `StorageOptions { ConnectionString, ContainerName = "files" }` bound from section `Storage` via `builder.Services.Configure<StorageOptions>(...)`.
- `IFileStorageService`: `UploadAsync(string name, Stream content, string contentType, ct)`, `ListAsync(ct)` → `IReadOnlyList<FileItem(Name, Size, LastModified)>`, `DownloadAsync(name, ct)` → `(Stream, contentType)?` (null if missing).
- `BlobFileStorageService` (singleton): builds `BlobContainerClient` from `IOptions<StorageOptions>`; `Path.GetFileName` sanitizes names; upload with overwrite + content-type header; `ILogger` logs on upload (name, size), download, not-found, and exceptions.
- `FilesController` `[Route("api/files")]`: `POST` (IFormFile, 400 if empty) → 201; `GET` → list; `GET {name}` → `File(stream, contentType, name)` or 404.
- `Program.cs`: `if (!string.IsNullOrEmpty(config["APPLICATIONINSIGHTS_CONNECTION_STRING"])) builder.Services.AddOpenTelemetry().UseAzureMonitor();` (so local runs without it don't fail), `UseDefaultFiles`, `UseStaticFiles`, `MapControllers`, `MapFallbackToFile("index.html")`. Keep OpenAPI only in Development.
- Local secret: `dotnet user-secrets init` + `dotnet user-secrets set Storage:ConnectionString $conn` (stored outside the repo).

**Verify:** `dotnet run`, then `curl.exe -F file=@somefile.txt http://localhost:<port>/api/files`, `curl.exe http://localhost:<port>/api/files`, download by name; blob visible via `az storage blob list`.

## Stage 3 – Frontend
- `npm create vite@latest src/web -- --template react-ts` (answer "No" if it offers to install/start), `npm install`.
- `vite.config.ts`: `build.outDir: '../FileManager.Api/wwwroot'`, `emptyOutDir: true`; dev `server.proxy['/api'] = 'http://localhost:<api port>'`.
- Structured (interviewer wants proper code organization, not one file). Remove template CSS/assets.
  - `types/file.ts` — `FileItem { name, size, lastModified }`
  - `api/filesApi.ts` — only place with fetch: `listFiles`, `uploadFile` (FormData `file`), `getDownloadUrl`
  - `hooks/useFiles.ts` — `files`, `loading`, `error`, `refresh()`, `upload()`
  - `components/FileUpload.tsx` — form, `onUpload` prop, uploading state
  - `components/FileList.tsx` — table with download links, empty state
  - `pages/Dashboard.tsx` — composes hook + components, shows loading/error
  - `utils/format.ts` — `formatBytes`, `formatDate`
  - `App.tsx` → `<Dashboard />`; `index.css` minimal layout

**Verify:** `npm run build` → files in `wwwroot`; `dotnet run` and open `http://localhost:<port>/` — upload, see it listed, download works.

## Stage 4 – Git + GitHub + CI/CD
- `.gitignore` (dotnet + node + `wwwroot/`), `git init -b main`, first commit.
- Create repo on your personal GitHub (`gh repo create <name> --private --source . --push` if `gh` is installed and signed in — I'll confirm the name/visibility with you first; otherwise you create it in the browser and I add the remote).
- `.github/workflows/deploy.yml` (on push to `main` + `workflow_dispatch`, `ubuntu-latest`):
  `actions/checkout@v4` → `actions/setup-node@v4` (node 22, npm cache) → `npm ci && npm run build` in `src/web` → `actions/setup-dotnet@v4` (`10.0.x`) → `dotnet publish src/FileManager.Api -c Release -o publish` → `azure/webapps-deploy@v3` with `app-name: app-filemgr-rocheli1104`, `publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}`, `package: publish`.
- Publish profile: `az webapp deployment list-publishing-profiles -g $rg -n <app> --xml`, piped directly into `gh secret set AZURE_WEBAPP_PUBLISH_PROFILE` (never saved to disk/committed). If no `gh`, you paste it in GitHub → Settings → Secrets.

**Verify:** push → Actions run green; `https://app-filemgr-rocheli1104.azurewebsites.net` serves the React page; upload/list/download works live.

## Stage 5 – Final end-to-end check
- Upload + download a file on the live site; confirm blob in `files` container.
- App Insights → Logs (`traces` / `requests`) shows upload/download log lines (allow 2–5 min ingestion).
- `git grep -i "AccountKey\|InstrumentationKey"` returns nothing.

## Notes / risks
- F1: 60 CPU-min/day, no Always On, cold starts — fine for the demo.
- Connection string in app settings is the fallback; production would use Managed Identity + `Storage Blob Data Contributor` (needs role-assignment rights we don't have).
- Kestrel default request limit (~30 MB) left as is.
- Mention in the demo: no auth on endpoints (out of scope).
