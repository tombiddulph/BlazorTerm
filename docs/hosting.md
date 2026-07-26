# Hosting and CDN

The application keeps `/` on the origin because its prerendered response contains
Interactive Server state. Circuit-free content sends separate browser and edge
cache policies:

```text
Cache-Control: public, max-age=300, stale-if-error=604800
CDN-Cache-Control: public, max-age=86400, stale-if-error=604800
```

The policy applies to:

- `/resume`
- `/projects` and `/projects/*`
- `/timeline`
- `/contact`
- `/llms.txt`
- `/sitemap.xml`

It is intentionally omitted from `/`, error responses, and unknown projects.

## Cloudflare Rule

Create a Cache Rule for `terminal.tommyb.dev` with this expression:

```text
(http.host eq "terminal.tommyb.dev" and
  (http.request.uri.path in {"/resume" "/timeline" "/contact" "/llms.txt" "/sitemap.xml"} or
   starts_with(http.request.uri.path, "/projects")))
```

Set **Cache eligibility** to **Eligible for cache** and allow the origin cache
headers to control browser and edge TTLs. Do not add a broad "cache everything"
rule for the hostname: caching `/` can distribute per-response Blazor state.

After enabling the rule, verify the static routes return `CF-Cache-Status: HIT`
on the second request while `/` remains `DYNAMIC`.

The private Talos deployment already runs one application replica with readiness
and liveness probes, resource limits, non-root execution, and two Cloudflare
Tunnel connectors. Scaling the application above one replica requires session
affinity and shared circuit persistence.
