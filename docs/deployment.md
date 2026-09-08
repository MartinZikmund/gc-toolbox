# WASM deployment

The WebAssembly head is hosted on **Cloudflare Workers static assets** as an assets-only
Worker named `gc-toolkit`. `.github/workflows/wasm-deploy.yml` builds and ships it.

| | Trigger | URL |
|---|---|---|
| **Production** (Prod channel) | push to `release/v**` | `https://gc-toolkit.martin-75d.workers.dev` |
| **PR preview** (Dev channel) | PR to `main`, public repo only | `https://pr-<number>-gc-toolkit.martin-75d.workers.dev` |

`martin-75d` is the account-wide `workers.dev` subdomain, chosen when the account was created.

wrangler prints *two* URLs per preview: a per-version one that changes on every push, and the
stable `pr-<number>` alias. The workflow deliberately scrapes the alias — the wrangler-action
`deployment-url` output carries the per-version URL, which would make the comment link move.

Previews are uploaded with `wrangler versions upload --preview-alias pr-<number>`, which
publishes a version **without** making it live. The alias is stable, so every push to a PR
refreshes the same URL, and the workflow posts it as a sticky PR comment. Cloudflare retains
the 1000 most recent aliases, so there is no teardown job to run when a PR closes.

## One-time setup

1. Create the `workers.dev` subdomain on the Cloudflare account (Workers & Pages → Subdomain).
   Preview URLs only work on `workers.dev`, never on a custom domain.
2. Create an API token from the **Edit Cloudflare Workers** template.
3. Add two repository secrets: `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID`. Without them
   both deploy steps skip themselves and the workflow still passes, so forks stay green.
4. Run `npx wrangler deploy` once (or merge a release) to create the Worker — `versions upload`
   uploads into an existing Worker.

## Configuration

- **`wrangler.jsonc`** (repo root) points at the published `wwwroot` and sets
  `not_found_handling: "single-page-application"`, so real files are served when they exist and
  everything else falls back to `index.html` for Uno's client-side navigation.
- **`src/GcToolkit/Platforms/WebAssembly/wwwroot/_headers`** carries the cache policy. Cloudflare
  *joins* duplicate headers from overlapping rules with a comma rather than letting the specific
  one win, so the two rules are deliberately disjoint: `/_framework/*` and `/package_*` are both
  fully content-addressed and frozen for a year, while the root (`index.html`, `service-worker.js`,
  `manifest.webmanifest`) keeps the Workers default of `public, max-age=0, must-revalidate` so a
  new deploy is picked up on the next visit.

MIME types (`application/wasm`) and Brotli/gzip compression are handled at the edge, so no MIME
mapping is needed. The SDK still emits a `.br`/`.gz` sibling beside most assets for servers that
content-negotiate by rewriting to them — Workers never serves those, so the workflow strips them
before upload. Measured on a Dev publish, that takes the upload from **578 files / 89 MB** to
**246 files / 52 MB**, against Cloudflare's free limits of 20,000 files and 25 MiB per file (the
largest asset, `dotnet.native.<hash>.wasm`, is ~9.9 MB). The workflow fails the run if any single
asset ever crosses 25 MiB.

Two behaviours worth knowing: `/index.html` 307-redirects to `/` (Workers canonicalises it), and
because SPA fallback returns the shell for anything not on disk, a *missing* asset comes back as
`200` + `index.html` rather than a `404`. `Platforms/WebAssembly/wwwroot/web.config` is a leftover
from IIS/Azure hosting and is inert here.

## Local dry run

```bash
dotnet publish src/GcToolkit/GcToolkit.csproj -c Release -f net10.0-browserwasm -p:SingleTargetFramework=net10.0-browserwasm
npx wrangler dev          # serve the built output locally
npx wrangler versions upload --dry-run
```

## Custom domain

A Worker custom domain requires the zone to be on Cloudflare DNS. `mzikmund.dev` currently uses
Azure DNS, so pointing e.g. `gctoolkit.mzikmund.dev` at the Worker means delegating that
subdomain (or the whole zone) to Cloudflare first.
