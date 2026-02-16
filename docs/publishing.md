# Publishing a New Version to NuGet

This guide covers how to publish new versions of **ElBruno.LocalEmbeddings**, **ElBruno.LocalEmbeddings.ImageEmbeddings**, **ElBruno.LocalEmbeddings.KernelMemory**, and **ElBruno.LocalEmbeddings.VectorData** to NuGet.org using GitHub Actions and NuGet Trusted Publishing (keyless, OIDC-based).

## Packages

| Package | Project | Description |
|---------|---------|-------------|
| `ElBruno.LocalEmbeddings` | `src/ElBruno.LocalEmbeddings/` | Core library — local ONNX embedding generation |
| `ElBruno.LocalEmbeddings.ImageEmbeddings` | `src/ElBruno.LocalEmbeddings.ImageEmbeddings/` | Companion — CLIP-based image embeddings |
| `ElBruno.LocalEmbeddings.KernelMemory` | `src/ElBruno.LocalEmbeddings.KernelMemory/` | Companion — Kernel Memory adapter + extensions |
| `ElBruno.LocalEmbeddings.VectorData` | `src/ElBruno.LocalEmbeddings.VectorData/` | Companion — Microsoft.Extensions.VectorData integration |

> **Maintenance rule:** If a new packable library is added under `src/`, update `.github/workflows/publish.yml` in the same PR so the new project is packed/pushed, and add a matching NuGet Trusted Publishing policy.

## Prerequisites (One-Time Setup)

These steps only need to be done once.

### 1. Configure NuGet.org Trusted Publishing Policies

1. Sign in to [nuget.org](https://www.nuget.org)
2. Click your username → **Trusted Publishing**
3. Add a policy for **each** package with these values:

| Setting | Value |
|---------|-------|
| **Repository Owner** | `elbruno` |
| **Repository** | `elbruno.localembeddings` |
| **Workflow File** | `publish.yml` |
| **Environment** | `release` |

   You need to create this policy **four times** — once per package:

- `ElBruno.LocalEmbeddings`
- `ElBruno.LocalEmbeddings.ImageEmbeddings`
- `ElBruno.LocalEmbeddings.KernelMemory`
- `ElBruno.LocalEmbeddings.VectorData`

   > **Note:** For new packages that don't exist on NuGet.org yet, you must first push them once (the workflow handles this). After the initial push, add the Trusted Publishing policy so future publishes are keyless.

### 2. Configure GitHub Repository

1. Go to the repo **Settings** → **Environments**
2. Create an environment called **`release`**
   - Optionally add **required reviewers** if you want a manual approval gate before publishing
3. Go to **Settings** → **Secrets and variables** → **Actions**
4. Add a repository secret:
   - **Name:** `NUGET_USER`
   - **Value:** `elbruno` (your NuGet.org profile name — **not** your email)

## Publishing a New Version

### Option A: Create a GitHub Release (Recommended)

This is the standard workflow — the version is derived from the release tag.

1. **Update the version** in all four csproj files:

   - `src/ElBruno.LocalEmbeddings/ElBruno.LocalEmbeddings.csproj`
   - `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ElBruno.LocalEmbeddings.ImageEmbeddings.csproj`
   - `src/ElBruno.LocalEmbeddings.KernelMemory/ElBruno.LocalEmbeddings.KernelMemory.csproj`
   - `src/ElBruno.LocalEmbeddings.VectorData/ElBruno.LocalEmbeddings.VectorData.csproj`

   ```xml
   <Version>1.2.0</Version>
   ```

2. **NuGet icon source** (already configured):

   - `nuget_images/icon_03.png`
   - Packed into each `.nupkg` as `icon_03.png` via `<PackageIcon>icon_03.png</PackageIcon>`

3. **Commit and push** the version change to `main`
4. **Create a GitHub Release:**
   - Go to the repo → **Releases** → **Draft a new release**
   - Create a new tag: `v1.2.0` (must match the version in the csproj)
   - Fill in the release title and notes
   - Click **Publish release**
5. The **Publish to NuGet** workflow runs automatically:
   - Strips the `v` prefix from the tag → uses `1.2.0` as the package version
   - Builds, tests, packs, and pushes to NuGet.org

### Option B: Manual Dispatch

Use this as a fallback or for testing.

1. Go to the repo → **Actions** → **Publish to NuGet**
2. Click **Run workflow**
3. Optionally enter a version (if left empty, the version from the csproj is used)
4. Click **Run workflow**

## How It Works

The workflow (`.github/workflows/publish.yml`) uses **NuGet Trusted Publishing** — no long-lived API keys are needed.

```
GitHub Release created (e.g. v1.2.0)
  → GitHub Actions triggers publish.yml
    → Builds + tests all projects
   → Packs four .nupkg files (LocalEmbeddings, ImageEmbeddings, KernelMemory, VectorData)
    → Requests an OIDC token from GitHub
    → Exchanges the token with NuGet.org for a temporary API key (valid 1 hour)
    → Pushes all packages to NuGet.org
    → Temp key expires automatically
```

### Version Resolution Priority

The workflow determines the package version in this order:

1. **Release tag** — if triggered by a GitHub Release (strips leading `v`)
2. **Manual input** — if triggered via workflow dispatch with a version specified
3. **csproj fallback** — reads `<Version>` from `src/ElBruno.LocalEmbeddings/ElBruno.LocalEmbeddings.csproj`

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Workflow fails at "NuGet login" | Verify the Trusted Publishing policy on nuget.org matches the repo owner, repo name, workflow file, and environment exactly |
| `NUGET_USER` secret not found | Add the secret in GitHub repo Settings → Secrets → Actions |
| Package already exists | The `--skip-duplicate` flag prevents failures when re-pushing an existing version. Bump the version number instead |
| OIDC token errors | Ensure `id-token: write` permission is set in the workflow job |

## Reference Links

- [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) — Official docs on keyless OIDC-based publishing
- [NuGet/login GitHub Action](https://github.com/NuGet/login) — The action that exchanges OIDC tokens for temporary NuGet API keys
- [OpenID Connect (OIDC) in GitHub Actions](https://docs.github.com/en/actions/security-for-github-actions/security-hardening-your-deployments/about-security-hardening-with-openid-connect) — How GitHub Actions OIDC tokens work
- [GitHub Actions: Creating and Using Environments](https://docs.github.com/en/actions/managing-workflow-runs-and-deployments/managing-deployments/managing-environments-for-deployment) — How to configure the `release` environment with approval gates
- [NuGet Package Versioning](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning) — Best practices for SemVer versioning
- [ElBruno.LocalEmbeddings on NuGet.org](https://www.nuget.org/packages/ElBruno.LocalEmbeddings) — The published package page
