# WASM deployment

The WebAssembly head is hosted on **Cloudflare Workers static assets** as an assets-only
Worker named `gc-toolkit`. `.github/workflows/wasm-deploy.yml` builds and ships it.

| | Trigger | URL |
|---|---|---|
| **Production** (Prod channel) | push to `release/v**` | `https://gc-toolkit.<subdomain>.workers.dev` |
| **PR preview** (Dev channel) | PR to `main`, public repo only | `https://pr-<number>-gc-toolkit.<subdomain>.workers.dev` |

`<subdomain>` is the account-wide `workers.dev` subdomain, chosen when the account is created.

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
- **`src/GcToolkit/Platforms/WebAssembly/wwwroot/_headers`** carries the cache policy. Note that
  Cloudflare *joins* duplicate headers from overlapping rules with a comma rather than overriding
  them — so the rules there are deliberately non-overlapping, and everything not listed keeps the
  Workers default of `public, max-age=0, must-revalidate`.

MIME types (`application/wasm`) and Brotli/gzip compression are handled at the edge; no
pre-compressed files or MIME mapping are needed.

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
