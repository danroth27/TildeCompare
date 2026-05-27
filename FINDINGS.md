# Tilde / Assets[] comparison — observed behavior

`PathBase = /myapp`. App hosts MVC Razor Pages + a View Component + a Blazor
Web App (Static SSR + Interactive Server + Interactive WebAssembly) side by
side under the same PathBase.

All numbers below are taken from `curl`'d HTML of the rendered pages, with
the documentation code samples filtered out.

## What MVC's `~/` does today (.cshtml)

| # | Source                                           | Rendered                                                | Notes                                                                |
|---|--------------------------------------------------|----------------------------------------------------------|----------------------------------------------------------------------|
| 1 | `<img src="~/images/logo.png">`                  | `src="/myapp/images/logo.4lpusj3pde.png"`               | PathBase **and** fingerprint. (img is in URL-resolution allowlist; the image tag helper fingerprints.) |
| 2 | `<link href="~/css/site.css">`                   | `href="/myapp/css/site.kel539m53m.css"`                 | PathBase + fingerprint (link tag helper).                            |
| 3 | `<script src="~/js/site.js">`                    | `src="/myapp/js/site.z3qhkpazfp.js"`                    | PathBase + fingerprint (script tag helper).                          |
| 4 | `<a href="~/images/logo.png">`                   | `href="/myapp/images/logo.4lpusj3pde.png"`              | PathBase + fingerprint (anchor tag helper resolves and fingerprints when target is in static asset manifest). |
| 5 | `<a href="~/counter">`                           | `href="/myapp/counter"`                                 | PathBase only. Page route, not a static asset.                       |
| 6 | `<a href="~/Compare?x=1">`                       | `href="/myapp/Compare?x=1"`                             | PathBase only.                                                       |
| 7 | `<form action="~/Submit">`                       | `action="/myapp/Submit"`                                | PathBase only.                                                       |
| 8 | `<source srcset="~/a.png 1x, ~/b.png 2x">`       | `srcset="/myapp/a.png 1x, ~/b.png 2x"` ⚠️                | **Only the first `~/` resolves.** UrlResolutionTagHelper doesn't parse srcset; this is a long-standing limitation. |
| 9 | `<div title="~/foo">`                            | `title="~/foo"`                                         | Pass-through; `title` is not in the allowlist.                       |
| 10| `<div data-url="~/foo">`                         | `data-url="~/foo"`                                      | Pass-through; arbitrary attribute names are not in the allowlist.    |
| 11| `@Url.Content("~/images/logo.png")`              | `/myapp/images/logo.png`                                | PathBase only (no fingerprinting in `Url.Content`).                  |
| 12| `@Url.Content("~/counter")`                      | `/myapp/counter`                                        | PathBase only.                                                       |
| 13| Partial: `<img src="~/images/logo.png">`         | `src="/myapp/images/logo.4lpusj3pde.png"`               | Tag helper runs inside partials (literal source).                    |
| 14| ViewComponent: `<img src="@Model.Source">` where `Model.Source = "~/images/logo.png"` | `src="~/images/logo.png"` ⚠️ | **`~/` does NOT resolve when the value comes from a C# expression.** The URL-resolution tag helper only fires on *literal* `~/` in the .cshtml source. |

### The core MVC rules — derived from this matrix

1. `~/` is a **static, syntactic** feature: it fires only when the literal
   text `~/...` appears as an attribute value in the `.cshtml` source.
   Runtime strings that happen to start with `~/` are *not* re-resolved.
2. It applies on an **attribute allowlist** (UrlResolutionTagHelper:
   `src, href, action, formaction, cite, data, poster, srcset, archive, icon, manifest, itemid`).
3. The expansion **always prepends PathBase**, producing a root-relative absolute URL.
4. **Fingerprinting** is an *additional* opt-in that link/script/image/anchor
   tag helpers layer on top — only when the resolved target is in the static
   asset manifest.
5. `Url.Content("~/...")` is the programmatic escape hatch — PathBase only,
   no fingerprinting.

## What Blazor does today (.razor)

All three render modes produced **identical** prerendered HTML:

| # | Source                                              | Rendered                                          | Notes                                                |
|---|-----------------------------------------------------|---------------------------------------------------|------------------------------------------------------|
| 1 | `<img src="~/images/logo.png">`                     | `src="~/images/logo.png"`                         | Literal pass-through. Today.                         |
| 2 | `<img src="@Assets["images/logo.png"]">`            | `src="images/logo.4lpusj3pde.png"`                | Fingerprinted. **Base-href-relative, no PathBase.**  |
| 3 | `<script src="@Assets["js/site.js"]">`              | `src="js/site.z3qhkpazfp.js"`                     | Same.                                                |
| 4 | `<source srcset="@Assets[\"a\"] 1x, @Assets[\"b\"] 2x">` | `srcset="images/logo.4lpusj3pde.png 1x, images/logo-2x.vsjc638qxn.png 2x"` | Works for srcset ✅ — better than MVC's `~/` here. |
| 5 | `<form action="~/Submit">`                          | `action="~/Submit"`                               | Literal.                                             |
| 6 | `<a href="~/counter">`                              | `href="~/counter"`                                | Literal. **In the browser, base-href turns this into `/myapp/~/counter`, which 404s.** |
| 7 | `<a href="counter">`                                | `href="counter"`                                  | Idiomatic Blazor. Resolves via `<base href>` to `/myapp/counter`. |
| 8 | `<a href="@Nav.ToAbsoluteUri("counter")">`          | `href="http://localhost:5095/myapp/counter"`      | Programmatic absolute.                               |
| 9 | `<a href="@Assets["counter"]">`                     | `href="counter"`                                  | ⚠️ `Assets[]` returns the **original key** for unknown entries — no exception. So this silently produces a working link (via base href), but has *no* PathBase in the rendered string. |
| 10| `<div title="~/foo">`                               | `title="~/foo"`                                   | Literal.                                             |
| 11| `<AssetCard Source="~/images/logo.png" />` and child re-emits it as `<img src="@Source">` | `src="~/images/logo.png"` | Component parameters carry the literal across without transformation. |
| 12| `<NavLink href="~/counter">`                        | `href="~/counter"`                                | Literal. **Broken navigation today.**                |

### The core Blazor rules — derived from this matrix

1. `~/` is **never** processed by Razor. It's just text.
2. `@Assets[key]` is fingerprinting only — **no PathBase**. Output is
   base-href-relative.
3. `Assets[key]` for an unknown key returns the key. Quiet failure mode.
4. Component parameters carry strings unchanged — no implicit transformation
   at either call site or callee.

## Where the syntaxes would and wouldn't line up under Option A

If we shipped Option A (compile-time `"~/x"` → `Assets["x"]` on any attribute):

| Scenario                              | MVC `~/`                          | Proposed Blazor `~/`                    | Aligned? |
|---------------------------------------|-----------------------------------|-----------------------------------------|----------|
| `<img src="~/logo.png">`              | `/myapp/logo.fp.png`              | `logo.fp.png` (base-relative)           | ❌ different string, same effective target |
| `<script src="~/site.js">`            | `/myapp/site.fp.js`               | `site.fp.js`                            | ❌ same as above |
| `<source srcset="~/a 1x, ~/b 2x">`    | `/myapp/a 1x, ~/b 2x` (bug)       | `a.fp 1x, b.fp 2x` (works)              | Blazor better, but different |
| `<a href="~/counter">` (page route)   | `/myapp/counter`                  | `Assets["counter"]` → `"counter"` (works by base-href accident, no PathBase) | ❌ semantically different |
| `<form action="~/Submit">`            | `/myapp/Submit`                   | `Assets["Submit"]` → `"Submit"` (only works for static asset–rooted routes that don't collide) | ❌ different |
| `<div title="~/foo">`                 | `~/foo` (allowlist excluded)      | `Assets["foo"]` → `"foo"` (Option A always-on transforms this!) | ❌ Option A is broader than MVC |
| `<MyComponent Url="~/x">` (param)     | n/a in MVC                        | `Assets["x"]` (Option A) or pass-through (Option B without `[AssetPath]`) | n/a |
| `@Model.Source = "~/x"` flowing into an attribute | NOT resolved (literal) | NOT resolved (literal — compile-time only) | ✅ matches MVC's "literal-only" rule |
| `Url.Content("~/...")` analog         | `/myapp/...`                      | None proposed                           | ❌ no Blazor equivalent for programmatic |

## Observations that should land in the thread

1. **MVC's `~/` is already a static, literal-only feature.** A runtime string
   starting with `~/` is never re-resolved — see the View Component row.
   This *matches* the prototype's compile-time-only semantics, and answers
   the "what about programmatic flow?" worry: MVC has lived with the same
   gap for 15+ years.

2. **The PathBase divergence is the only semantic that's truly different.**
   In MVC, `~/foo` becomes `/myapp/foo`. In the proposed Blazor design,
   `Assets["foo"]` becomes `foo` (base-href-relative). Both render visually
   correctly when the document has the right `<base href>`, but the literal
   strings differ — visible to JS, snapshot tests, CSP, SRI, streaming SSR
   fragments emitted before `<base href>` is in the document, and any
   non-document context (component parameters consumed by JS interop).

3. **MVC's allowlist is real and observable.** `<div title="~/foo">` and
   `<div data-url="~/foo">` are pass-through in MVC. Option A would
   *broaden* the scope vs. what MVC does today, which is a real
   user-facing divergence (not just a false-positive risk).

4. **`Assets[]`'s silent-pass-through-of-unknown-keys** (rule 3 above) is
   actually what makes Option A "kind of work" for page-route uses like
   `<a href="~/counter">` — the asset lookup quietly returns `"counter"`,
   the browser resolves it via `<base href>`, navigation succeeds. But it
   *doesn't* prepend PathBase, so the rendered href shape is still
   different from MVC. And nothing tells the user the key wasn't found.

5. **srcset is a place where `@Assets[]` is actually better than MVC's
   `~/`.** MVC's UrlResolutionTagHelper has a long-standing bug where it
   only resolves the leading `~/` in a `srcset` attribute. The Blazor
   prototype handles each `~/` independently.

6. **NavLink today is broken with `href="~/x"`.** The literal flows through
   and the browser tries to navigate to `<base href>~/x`, which 404s.
   So shipping Option A actually *fixes* a soft footgun here, *if* people
   intuit that `~/` is a tilde-prefix to a static asset path — but it
   silently breaks for page routes that happen to share a name with a
   static asset, and silently drops PathBase otherwise.

## What the test app actually verifies

- Surfaces under `PathBase = /myapp`:
  - `/Compare` (Razor Pages)
  - `/blazor/compare-ssr` (Static SSR)
  - `/blazor/compare-server` (Interactive Server, prerendered)
  - `/blazor/compare-wasm` (Interactive WebAssembly, prerendered)
  - `/counter` (Razor Pages route target)
  - `/blazor/counter` (Blazor route target)
- Scenarios per surface: img/link/script/source/form/a + non-URL attrs +
  component params + programmatic (`Url.Content`, `Nav.ToAbsoluteUri`,
  `Assets[]`) + View Component (MVC) + Partial (MVC).

To reproduce:

```pwsh
cd D:\Copilot\TildeCompare
dotnet run --project TildeCompare --launch-profile http
# then curl http://localhost:5095/myapp/Compare etc.
```

Snapshots of all surfaces are in `D:\Copilot\TildeCompare\snapshots\`.
