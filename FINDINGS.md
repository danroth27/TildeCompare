# Tilde / Assets[] comparison — observed behavior

`PathBase = /myapp`. App hosts MVC Razor Pages + a View Component + a Blazor
Web App (Static SSR + Interactive Server + Interactive WebAssembly) side by
side under the same PathBase.

All numbers below are taken from `curl`'d HTML of the rendered pages, with
the documentation code samples filtered out.

## What MVC's `~/` does today (.cshtml)

| # | Source                                           | Rendered                                                | Notes                                                                |
|---|--------------------------------------------------|----------------------------------------------------------|----------------------------------------------------------------------|
| 1 | `<img src="~/images/logo.png">`                  | `src="/myapp/images/logo.4lpusj3pde.png"`               | PathBase **and** fingerprint, both applied by `UrlResolutionTagHelper` itself. |
| 2 | `<link href="~/css/site.css">`                   | `href="/myapp/css/site.kel539m53m.css"`                 | Same.                                                                 |
| 3 | `<script src="~/js/site.js">`                    | `src="/myapp/js/site.z3qhkpazfp.js"`                    | Same.                                                                 |
| 4 | `<a href="~/images/logo.png">`                   | `href="/myapp/images/logo.4lpusj3pde.png"`              | Same. Anchors are in the allowlist, so the static-asset target gets fingerprinted too. |
| 5 | `<a href="~/counter">`                           | `href="/myapp/counter"`                                 | PathBase only. `counter` is not a key in the static-asset manifest, so `ResourceAssetCollection["counter"]` returns the key unchanged and no fingerprinting happens. |
| 6 | `<a href="~/Compare?x=1">`                       | `href="/myapp/Compare?x=1"`                             | PathBase only. `Compare?x=1` is taken as the asset key (the tag helper does not split the query) — no manifest hit, no fingerprint. |
| 7 | `<form action="~/Submit">`                       | `action="/myapp/Submit"`                                | PathBase only.                                                       |
| 8 | `<source srcset="~/a.png 1x, ~/b.png 2x">`       | `srcset="/myapp/a.png 1x, ~/b.png 2x"` ⚠️                | **Only the first `~/` resolves.** `TryCreateTrimmedString` requires the whole attribute value to start with `~/`; the comma-separated tail is left alone. Long-standing behavior. |
| 9 | `<div title="~/foo">`                            | `title="~/foo"`                                         | Pass-through; `title` is not in any allowlisted element+attribute pair. |
| 10| `<div data-url="~/foo">`                         | `data-url="~/foo"`                                      | Pass-through; arbitrary attribute names aren't in the allowlist.     |
| 11| `@Url.Content("~/images/logo.png")`              | `/myapp/images/logo.png`                                | PathBase only. `Url.Content` doesn't consult the asset manifest. To fingerprint here you need `asp-append-version="true"` or use the tag helper. |
| 12| `@Url.Content("~/counter")`                      | `/myapp/counter`                                        | PathBase only.                                                       |
| 13| Partial: `<img src="~/images/logo.png">`         | `src="/myapp/images/logo.4lpusj3pde.png"`               | Tag helper runs inside partials (the literal `~/` is in the partial's `.cshtml` source). |
| 14| ViewComponent: `<img src="@Model.Source">` where `Model.Source = "~/images/logo.png"` | `src="~/images/logo.png"` ⚠️ | **`~/` does NOT resolve when the value comes from a C# expression.** This is enforced by the Razor compiler's tag-helper matching (`[src^='~/']`), not by a runtime branch in the tag helper — the helper is never even applied to attributes whose syntactic value doesn't start with `~/`. |

### The core MVC rules — derived from this matrix *and* the source

1. `~/` is a **static, syntactic** feature: it only applies when the literal
   text `~/...` appears as an attribute value in the `.cshtml` source. The
   gate is the tag-helper matcher `[<attr>^='~/']` in the
   `UrlResolutionTagHelper` `[HtmlTargetElement]` attributes; that is a
   Razor-compile-time predicate, so runtime strings that happen to start
   with `~/` are never re-resolved.
2. It applies on an **element+attribute allowlist** (see
   `src/Mvc/Mvc.Razor/src/TagHelpers/UrlResolutionTagHelper.cs`). Notable
   pairs: `a/href`, `area/href`, `audio/src`, `form/action`, `img/src`,
   `img/srcset`, `link/href`, `script/src`, `source/src`, `source/srcset`,
   `video/src`, `video/poster`, `iframe/src`, `embed/src`, `track/src`,
   `input/src`, `input/formaction`, `button/formaction`,
   `blockquote/cite`, `q/cite`, `del/cite`, `ins/cite`, `object/data`,
   `object/archive`, `applet/archive`, `html/manifest`, `menuitem/icon`,
   `base/href`, plus `itemid` on **any** element.
3. The expansion **always prepends PathBase** via `IUrlHelper.Content(...)`,
   producing a root-relative absolute URL.
4. **Fingerprinting** is performed by `UrlResolutionTagHelper.GetVersionedResourceUrl`,
   which reads the `ResourceAssetCollection` from the current endpoint's
   metadata (same collection Blazor's `Assets[]` uses). If the asset
   substring after `~/` is a key in the manifest, the URL is rewritten to
   the fingerprinted name *before* PathBase is prepended; otherwise the
   key passes through. Same code path for `<img>`, `<a>`, `<link>`,
   `<script>`, etc. — there are no separate per-element fingerprinting
   helpers in 9.0+.
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
3. **`Assets[]` returns the key on miss.** `ResourceAssetCollection`'s
   indexer is literally
   `_uniqueUrlMappings.TryGetValue(key, out var value) ? value.Url : key`
   — no exception, no fallback log. This is intentional, but it means
   any tooling that wants to flag "you wrote `@Assets[\"counter\"]` but
   `counter` isn't a static asset" has to do that work itself.
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
   This *matches* the proposed compile-time `~/` → `Assets[...]` rewrite's
   literal-only semantics, and answers
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
   proposed `~/` rewrite handles each `~/` independently.

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

## Source references

Cross-checked against `dotnet/aspnetcore` `main`:

- `src/Mvc/Mvc.Razor/src/TagHelpers/UrlResolutionTagHelper.cs` — the
  `[HtmlTargetElement(... Attributes = "[<attr>^='~/']")]` directives
  define the element+attribute matrix, `TryCreateTrimmedString` enforces
  the leading-`~/`-only behavior, `GetVersionedResourceUrl` performs
  fingerprinting against `ResourceAssetCollection` *before* PathBase is
  prepended via `IUrlHelper.Content`.
- `src/Components/Components/src/ResourceAssetCollection.cs` — the
  indexer returns the key unchanged on miss; the same instance is
  exposed to both MVC (via endpoint metadata) and Blazor (via the Razor
  compiler's `Assets` cascading).

