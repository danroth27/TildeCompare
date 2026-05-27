# TildeCompare

A small ASP.NET Core 10 sample that places **MVC / Razor Pages**, a **View Component**, and a **Blazor Web App** (Static SSR + Interactive Server + Interactive WebAssembly) side by side under a non-trivial `PathBase` (`/myapp`), so the behavior of `~/` (in `.cshtml`) can be compared directly with `@Assets[...]` (in `.razor`) for an identical scenario matrix.

The motivating question: if Blazor adds compile-time `~/` → `Assets[...]` expansion, will it mean the **same thing** as `~/` in `.cshtml`? This app makes the answer concrete.

## Run it

```pwsh
dotnet run --project TildeCompare --launch-profile http
```

Then visit:

| Surface | URL |
|---|---|
| Landing page | <http://localhost:5095/myapp/> |
| MVC / Razor Pages compare | <http://localhost:5095/myapp/Compare> |
| Blazor — Static SSR | <http://localhost:5095/myapp/blazor/compare-ssr> |
| Blazor — Interactive Server | <http://localhost:5095/myapp/blazor/compare-server> |
| Blazor — Interactive WebAssembly | <http://localhost:5095/myapp/blazor/compare-wasm> |
| Razor Pages `/counter` (page-route target) | <http://localhost:5095/myapp/counter> |
| Blazor `/counter` (page-route target) | <http://localhost:5095/myapp/blazor/counter> |

The app is intentionally mounted under `PathBase = /myapp` (see `Program.cs`) so PathBase-prefixing behavior is visible in the rendered HTML.

## Scenario matrix (per surface)

- Static asset URLs across the URL-resolution allowlist: `<img src>`, `<link href>`, `<script src>`, `<a href>`, `<source srcset>`, `<form action>`.
- Page-route targets: `<a href="~/counter">`, `<a href="~/Compare?x=1">`.
- Non-URL attributes (outside the allowlist): `<div title>`, `<div data-url>`.
- Programmatic features: `Url.Content("~/...")` (MVC), `NavigationManager.ToAbsoluteUri(...)` (Blazor), `Assets["..."]` (Blazor), a `[ViewComponent]` that re-emits a `~/`-prefixed string from a C# model (MVC), and a partial view (MVC).
- Component parameter forwarding: a small `AssetCard` Blazor component that re-emits whatever its caller passed to it.

## Findings

A written-up summary of what the rendered HTML actually shows (with the surprising and the unsurprising parts called out) is in [`FINDINGS.md`](./FINDINGS.md). Raw rendered HTML for each surface is in [`snapshots/`](./snapshots/).
