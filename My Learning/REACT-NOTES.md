# React + TypeScript: what I built and learned

Two frontends, both typed by me with an AI tutor guiding the steps:

- **Part 1 (22 Oct 2026):** `web/`, the real project's frontend: a company search page and a company page.
  Stack: Vite 8 · React 19 · TypeScript · react-router · ESLint.
- **Part 2 (29 Oct – 1 Nov 2026):** `practice/web/`, a second frontend for my practice API, built in 11
  steps: paging, URL state, a layout, an import page that polls, and a login with route guards.
  Stack: Vite 8 · React 19.3 · TypeScript 7 · react-router · Oxlint · Prettier.
- **Part 3 (from 14 Nov 2026):** `practice2/web/`, the frontend for the practice2 API (port 5020), in 12
  steps. Stack: Vite 8 · React 19.3 · TypeScript 6 · ESLint · Prettier. Its steps are **merged into the
  matching sections below**: one direct answer per topic, not a second version.

| Part 3 step | What | Status |
|---|---|---|
| 1 | Create the app (Vite, React, TypeScript), Prettier, a proxy to the API | ✅ |
| 2 | Login page: form, `POST /api/auth/login`, save the session in `localStorage` | ✅ |
| 3 | `apiFetch` sends the token, and a typed API client | ✅ |
| 4 | Search form with loading and error states | ✅ |
| 5 | Paging, with a component and props | ✅ |
| 6 | Router and the company page (400/404) | ⏳ |
| 7 | Roles on the company page | ⏳ |
| 8 | Search kept in the URL | ⏳ |
| 9 | Layout route (header, footer, `<Outlet />`) | ⏳ |
| 10 | Auth context, route guard, username and Log out in the header | ⏳ |
| 11 | Token expiry: automatic logout, and a 401 sends you to the login page | ⏳ |
| 12 | Import page with polling (link only for users with `companies.import`) | ⏳ |

# Part 1: `web/`

---

## 1. Setup: create the app, format, proxy, checks

```bash
# Node 24 (current LTS). Project A keeps Node 20 through its own systemd PATH: never uninstall v20.19.6.
nvm install 24 && nvm alias default 24     # new terminals start on 24 (nvm use 24 for an open one)

npm create vite@latest web -- --template react-ts   # "--": the rest goes to create-vite, not npm
cd web
npm install                  # download what package.json lists into node_modules (= composer install)
npm install -D prettier      # the formatter, pinned per project (a devDependency)
cp ../../practice/web/.prettierrc.json .   # 4 spaces, no semicolons, single quotes, width 120
```

**`package.json`: commands go only under `"scripts"`** (npm ignores other top-level keys for `npm run`):
```json
"scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "lint": "eslint .",
    "format": "prettier --write src vite.config.ts",
    "preview": "vite preview"
}
```

**`vite.config.ts`: the proxy** (Vite's settings live here, never in `package.json`):
```ts
export default defineConfig({
    plugins: [react()],
    server: {
        proxy: {
            '/api': 'http://localhost:5020', // practice2 API (practice: 5010, web/: 5080)
        },
    },
})
```
Test: `http://localhost:5173/api/companies/926056522` gives the company JSON (public);
`/api/companies` gives **401** (it reached .NET, no token). Both prove the proxy works.

**Day-to-day: two terminals**
```bash
cd practice2/Practice2.Api && dotnet watch   # the API, rebuilds on C# changes
cd practice2/web && npm run dev              # Vite on http://localhost:5173
```

**Before a commit**
```bash
npx tsc -b        # type-check: Vite does NOT type-check in dev, it only strips the types
npm run lint      # ESLint: finds bugs
npm run format    # Prettier: fixes layout only (also runs on save, see .vscode/settings.json)
npm run build     # tsc -b + a production bundle in dist/ (what CI runs)
```

### Questions and answers
- **Why a proxy, not `fetch("http://localhost:5020/...")`?** A different port is a different **origin**,
  and the browser blocks the page from reading the response (CORS) unless the API sends CORS headers.
  With the proxy the browser only talks to 5173, and **Vite's dev server** (a Node process) forwards
  `/api` to 5020, server to server, where CORS doesn't apply. Like Nginx `proxy_pass` or Nuxt `devProxy`.
- **Why no CORS setup in the API?** The browser sees one origin. In production a **reverse proxy**
  (nginx, Caddy, the host) serves the built files and `/api` on the same domain, or the API serves the
  files itself (the real project does this).
- **Which endpoints work without a token?** Login and the company page: `[AllowAnonymous]` on them
  overrides the fallback policy that requires a login everywhere else.
- **Does `npm run dev` type-check?** No. `npx tsc -b` does (also inside `npm run build`); the editor shows
  type errors live.
- **ESLint or Oxlint?** ESLint is the most used in existing codebases; Oxlint is newer and faster.
  `web/` and `practice2/web/` use ESLint, `practice/web/` Oxlint.

---

## 2. Project structure

```
web/
  index.html          the one HTML page; React draws inside <div id="root">
  vite.config.ts      dev server + proxy to the .NET API
  eslint.config.js    lint rules (incl. React hooks rules)
  tsconfig*.json      TypeScript settings (app code / config files)
  package.json        dependencies + scripts (dev, build, lint)
  src/
    main.tsx          entry: starts React, declares the routes
    api.ts            all calls to the API + the TypeScript types for its JSON
    page/
      SearchPage.tsx  /                   search form + results table
      CompanyPage.tsx /companies/:orgnr   one company's details
```

---

## 3. The files I wrote

### vite.config.ts: the proxy

```ts
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
        '/api': 'http://localhost:5080',
        '/connect': 'http://localhost:5080'
    }
  }
})
```

- The browser only talks to Vite (5173). Requests starting with `/api` or `/connect` are forwarded to .NET (5080).
- One origin for the browser, so no CORS setup is needed. The same idea as Nginx `proxy_pass` or Nuxt `devProxy`.
- Test: `http://localhost:5173/api/companies` gives **401** (it reached .NET, no token). That's the expected answer.

### src/main.tsx: routing

```tsx
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Route, Routes } from 'react-router'
import './index.css'
import SearchPage from './page/SearchPage.tsx'
import CompanyPage from './page/CompanyPage.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
        <Routes>
            <Route path="/" element={<SearchPage />} />
            <Route path="/companies/:orgnr" element={<CompanyPage />} />
        </Routes>
    </BrowserRouter>
  </StrictMode>,
)
```

- `<BrowserRouter>` follows the browser URL. `<Routes>` picks the first `<Route>` that matches.
- `:orgnr` is a URL parameter, like Nuxt's `pages/companies/[orgnr].vue`.
- `!` after `getElementById('root')` tells TypeScript "this is not null".
- `<StrictMode>` adds extra checks in development only.

### src/api.ts: the typed API client (best version: Part 3, step 3)

The **only** file that calls the API: the types, the token, the errors.
```ts
import { loadSession } from './auth/session'

// The C# records as the JSON arrives: camelCase, `T?` → `T | null`, DateOnly → "2020-01-15" (a string)
export type CompanyDto = {
    orgnr: string
    name: string
    orgForm: string
    registrationDate: string | null
}

export type PageResult<T> = { items: T[]; page: number; pageSize: number; totalCount: number }

// fetch doesn't throw on 4xx/5xx; this does, and carries the status for the UI
export class ApiError extends Error {
    status: number
    constructor(status: number, message: string) {
        super(message)
        this.status = status
    }
}

async function apiFetch<T>(url: string): Promise<T> {
    const session = loadSession()
    const headers = new Headers()
    if (session) headers.set('Authorization', `Bearer ${session.token}`)   // only when logged in
    const response = await fetch(url, { headers })
    if (!response.ok) throw new ApiError(response.status, `${response.status} ${response.statusText}`)
    return response.json()           // no await needed: the caller awaits the same Promise
}

export function searchCompanies(name: string, page = 1, pageSize = 20) {
    const params = new URLSearchParams({ name, page: String(page), pageSize: String(pageSize) })
    return apiFetch<PageResult<CompanyDto>>(`/api/companies?${params}`)   // ? starts the query, not /
}

export function getCompany(orgnr: string) {
    return apiFetch<CompanyDto>(`/api/companies/${encodeURIComponent(orgnr)}`)
}
```

**Questions and answers**
- **Why one API file?** One place for the URLs, the token and error handling: reuse, and one place to
  change. Like a shared axios instance or composable in Vue, or a service class around `Http::` in Laravel.
- **The types mirror the C# DTOs:** both sides of the contract. `string?` → `string | null`;
  `DateOnly?` → `string | null` (JSON has no date type). Only `int? BirthYear` is a number.
- **Generics:** `PageResult<T>` has a fixed shape, the item type is filled in when used:
  `PageResult<CompanyDto>` gives `items: CompanyDto[]`. `apiFetch<T>`: the caller says what the JSON is.
- **Why the login keeps plain `fetch`:** in step 11, `apiFetch` treats **every 401 as "token expired"**
  and goes to the login page; at login a 401 means "wrong password" and must show a message.
- **Why `ApiError` with the status:** the **UI** decides by it: 404 → "Company not found", 400 → "Invalid
  number", 401 → log in again. (Extension: carry `X-Correlation-ID` so the page can show an error ref.)
- **`return response.json()` without `await`:** in an `async` function, returning a Promise is the same
  for the caller as returning its value. `await` is needed only when **this** function uses the value
  (step 2 saved the session), or inside `try/catch` to catch it there.
- **`encodeURIComponent`:** escapes `/ ? # &` and spaces so user input stays one path segment
  (`"12/34"` would be two). `URLSearchParams` does the same for query values.
- **Part 1 (`web/`)** fetched a token with a client secret from the browser: a demo shortcut. Real users
  log in (ID-porten: OIDC code + PKCE), often through a **BFF** that keeps the token on the server.

**Using it in a component** (App.tsx, step 3's proof):
```tsx
const [total, setTotal] = useState<number | null>(null)
useEffect(() => {
    if (!session) return
    searchCompanies('').then((result) => setTotal(result.totalCount))
}, [session])                          // runs again when session changes; hooks before any early return
```

### SearchPage.tsx: state, form, list (best version: Part 3, step 4)

```tsx
export default function SearchPage() {
    const [name, setName] = useState('')    // the input: changes on every keystroke
    const [query, setQuery] = useState('')  // what was searched: changes only on Submit
    const [result, setResult] = useState<PageResult<CompanyDto> | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [loading, setLoading] = useState(true) // true: the first search starts on mount

    useEffect(() => {
        let ignore = false                  // this run's own flag
        searchCompanies(query)
            .then((data) => { if (!ignore) { setResult(data); setError(null) } })
            .catch((e) => {
                if (!ignore) {
                    setResult(null)         // no stale table under the error
                    setError(e instanceof ApiError ? `Search failed (${e.status})` : 'Could not reach the server')
                }
            })
            .finally(() => { if (!ignore) setLoading(false) })
        return () => { ignore = true }      // cleanup: before the next run, or when the page goes away
    }, [query])

    function handleSubmit(e: SubmitEvent<HTMLFormElement>) {
        e.preventDefault()
        const trimmedName = name.trim()
        if (trimmedName === query) return   // same query: the effect won't rerun, loading would hang
        setLoading(true)                    // setState in an event handler: the right place
        setQuery(trimmedName)
    }
    // render: the form; {loading && …}; {!loading && error && <p role="alert">…}; the table with key={c.orgnr}
}
```

**Questions and answers**
- **Why `name` and `query`?** `name` follows every keystroke; `query` changes only on Submit. The effect
  depends on `[query]`, so typing sends no requests, only Search does.
- **Loading, error, data:** without loading the page looks frozen and people click again; without error
  a failure looks like "no results".
- **The race (`ignore`):** search "A", then quickly "B". If A answers last it would overwrite B's table.
  When `query` changes, React runs the old effect's cleanup first (`ignore = true` for *that* run), so
  A's late answer is dropped.
- **`key`:** lets React match rows between renders (Vue's `:key`); without it the wrong DOM is reused.
- **Why `loading` isn't set inside the effect:** `setLoading(true)` directly in an effect's body is
  rejected by ESLint (react-hooks `set-state-in-effect`: an extra render). `setX` in a callback that runs
  **later** (`.then`, after `await`, a timer, a subscription) or in an **event handler** is fine.
- **The derived alternative (also fine, can't get out of sync):** one state `loaded = { query, result?,
  error? }` set in `.then`/`.catch`, and `const loading = loaded?.query !== query`, a Vue `computed`:
  read-only, recalculated each render. No "same query" trap.
- **`{ query, result }`** is shorthand for `{ query: query, result: result }`: the variable names become
  the keys, so they must match the type's properties (or write `result: data`). TS catches a mismatch.
- **`typeof` vs `instanceof`:** `typeof e` is just `'object'` for any error; `e instanceof ApiError`
  checks the class (PHP's `instanceof`).
- **`useState` returns a pair, named by position:** `[total, setTotal]` is a convention;
  `const [setTotal] = useState()` would name the *value*. Skip the value with `const [, setTotal]`.
- **`useState`** = Vue's `ref()`. `[value, setValue]`; never assign directly, because calling the setter is what makes React redraw.
- **`useState<Company[]>([])`**: TypeScript can't tell what an empty `[]` will hold, so the type is given.
- **`value` + `onChange`** = `v-model`, written out (a "controlled input").
- **`event.preventDefault()`** = `@submit.prevent`; stops the page reload.
- **`try / catch / finally`** gives three UI states: loading, error, result.
- **`{cond && <p/>}`** = `v-if`. **`array.map(...)`** = `v-for`. **`key`** = `:key`.
- **`type Company` in an import** is a type-only import; it disappears from the compiled JavaScript.

### src/page/CompanyPage.tsx: URL parameter + loading data

```tsx
import {Link, useParams} from 'react-router'
import { useEffect, useState } from 'react'
import { getCompany, type Company } from '../api'

export default function CompanyPage() {
    const {orgnr} = useParams()
    const [company, setCompany] = useState<Company | null>(null)
    const [error, setError] = useState<string | null>(null)

    useEffect( () => {
        if(!orgnr) return
        getCompany(orgnr)
            .then(setCompany)
            .catch( (e) => setError(e instanceof Error ? e.message : 'Something went wrong'))
    }, [orgnr])

    return (
        <main>
            <Link to="/">← Back to search</Link>

            {error && <p role="alert">{error}</p>}
            {!company && !error && <p>Loading..</p>}

            {company && (
                <>
                    <h1>{company.name}</h1>
                    <dl>
                        <dt>Orgnr</dt>
                        <dd>{company.organisationNumber}</dd>
                        <dt>Form</dt>
                        <dd>{company.organisationFormCode}</dd>
                        <dt>Adresse</dt>
                        <dd>
                            {company.address.street}, {company.address.postalCode}, {company.address.postalPlace}
                        </dd>
                        <dt>Kommune</dt>
                        <dd>{company.address.municipality}</dd>
                        <dt>Registered</dt>
                        <dd>{company.registrationDate ?? 'Ukjent'}</dd>
                    </dl>
                </>
            )}
        </main>
    )
}
```

- **`const { orgnr } = useParams()`** is destructuring: take `orgnr` out of the params object (`{ orgnr: "912855414" }`). PHP: `['orgnr' => $orgnr] = $params;`
- **Hooks** are the functions starting with `use` (`useState`, `useEffect`, `useParams`). Call them at the top of the component, never inside `if` or loops.
- **`useEffect(fn, [orgnr])`**: run after render, and again when `orgnr` changes. Fetching is a side effect, so it goes here, not in the component body.
- **`.then(setCompany)`** passes the setter directly; the same as `.then(c => setCompany(c))`.
- **`<> … </>`** is a fragment: groups elements without an extra `<div>`.
- **`??`**: use the right side if the left is null or undefined (same as PHP).
- **`<dl>/<dt>/<dd>`** is a semantic label/value list; good for screen readers.
- To fix: one UI language ("Registered" should be "Registrert").

---

## 4. Vue to React

| Vue / Nuxt | React |
|---|---|
| `.vue` file with `<template>` | `.tsx` function that **returns JSX** |
| `{{ x }}` | `{x}` |
| `ref(0)` | `const [x, setX] = useState(0)` |
| `onMounted` | `useEffect(fn, [])` |
| `watch(x)` | `useEffect(fn, [x])` |
| `v-if` | `{cond && <el/>}` or `cond ? a : b` |
| `v-for` + `:key` | `list.map(i => <el key={…}/>)` |
| `v-model` | `value={x} onChange={e => setX(e.target.value)}` |
| `@submit.prevent` | `onSubmit` + `event.preventDefault()` |
| `pages/` folder routing | `<Routes><Route path=… element=…/></Routes>` |
| `useRoute().params` | `useParams()` |
| `<NuxtLink to>` | `<Link to>` |

### useEffect dependency list

| Dependency list | Runs | Vue |
|---|---|---|
| `[name, page]` | first render, then when **any** of them changes | `watch([name, page], …, { immediate: true })` |
| `[]` | once, after the first render | `onMounted` |
| none | after **every** render | rarely wanted |

Rule: every value from the component that the effect *uses* goes in the list. ESLint's `react-hooks/exhaustive-deps` warns if one is missing.

---

## 5. Mistakes that taught me something

| What happened | Lesson |
|---|---|
| `return token` gave TS error *`string \| null` not assignable to `string`* | `response.json()` is `any`, so TS can't know the value isn't null. Fix: type the JSON, `const data: { access_token: string } = …`. Same idea as C# nullable warnings |
| `` `/api/companies/${query}` `` (a `/` instead of `?`) | TypeScript checks types, not URLs. This only shows when you test against the running API (it would have given 400 "invalid organisation number") |
| `type="submnit"` | TS caught it ("Did you mean 'submit'?"). It still "worked" in the browser because unknown button types fall back to submit, and **Vite doesn't type-check in dev**. `npm run build` / CI must run `tsc` |
| Folder `page/` vs import `./pages/…` | "Cannot find module". Names must match exactly |
| `localhost:5173/api/companies/912855414` gave 401 | That's the **API** URL (needs a token); the **page** URL is `/companies/912855414`. Only `fetch` in `api.ts` sends the token |
| Search gave **500**, log said `column c.organisation_number does not exist` | The running API was old code; the database had been migrated (column renamed to `orgnr`). Old code + new schema breaks, which is why **expand/contract migrations** exist. Restarting the API fixed it |
| `npm run dev` gave ENOENT `package.json` | Wrong folder; run it inside `web/` |
| (Part 3) The proxy in `package.json` under `"server"`: the page showed the app, not JSON | Vite never reads `package.json` for settings (that was Create React App). Vite's settings go in `vite.config.ts` |
| (Part 3) `"format": "prettier"`, then the full command added at the top level of `package.json` | `prettier` without files and `--write` does nothing; and `npm run` only reads `"scripts"` |
| (Part 3) The `return (<form>…)` inside `handleSubmit`: "LoginPage cannot be used as a JSX component" | The handler ends with its own `}`; the **component** returns the JSX |
| (Part 3) `FormEvent` struck through in the editor | Deprecated in `@types/react` 19.3: use `SubmitEvent<HTMLFormElement>`. Strike-through = deprecated |
| (Part 3) `class="logout"` on a button | JSX uses `className` (`class` is a JS keyword); tsc rejects `class` |
| (Part 3) `role="error"`, then `role="akert"` | `role` is a plain string, so tsc can't catch it; the ARIA role is `alert` |
| (Part 3) `` `/api/companies/${params}` ``: the API log showed **400** | Again `/` instead of `?` (as in Part 1): the query became an orgnr and failed MOD11. TS checks types, not URLs |
| (Part 3) Step 3's proof (`total` + its effect) left in App.tsx: tsc "declared but never read" | Remove what a step replaces; an effect with only `if (...) return` does nothing |
| (Part 3) `app.ts` for the API client | Name it `api.ts`: `app.ts` next to `App.tsx` invites import mix-ups (case-insensitive file systems) |
| (Part 3) Search guard `if (trimmedName === query) return` without `&& page === 1` | On page 3, Search with the same text did nothing. The guard must check **every** state the submit changes (query and page) |
| (Part 3) Quiz: "what if `page` is missing from the deps" answered as if it were there | Missing dep = the effect doesn't rerun when it changes: Next shows "Loading..." forever, no fetch |

---

## 6. Things learned around the edges

- **`dotnet run`** compiles once and runs that build; it doesn't follow changes. **`dotnet watch`** watches `.cs` files: Hot Reload for small edits, rebuild and restart for bigger ones. C# is compiled, unlike PHP (read on every request) or Vite (hot module replacement).
- **API logs** go to the console as JSON lines: the terminal running `dotnet watch`, or Render's **Logs** tab when live.
- **inotify** is the Linux kernel feature that tells programs when files change. The default limit was 128 watchers per user (VS Code alone used 33); raised to 512 in `/etc/sysctl.d/60-inotify.conf`. The alternative is polling: `DOTNET_USE_POLLING_FILE_WATCHER=1 dotnet watch`.
- **`--`** in `npm create vite@latest web -- --template react-ts` means "the rest goes to the tool, not to npm" (same as `git checkout -- file`).
- **Node versions:** `nvm alias default` only affects new terminals. Project A's nightly jobs use an exact Node 20 path, so never uninstall `v20.19.6`.

---

## 7. Next for `web/`

1. ~~Build the frontend into the same Docker image, served by ASP.NET Core; add frontend lint + build to CI.~~ Done 23 Oct (see CICD-NOTES.md).
2. Pagination: `page` state + `useEffect(…, [name, page])`, Previous/Next buttons. (Done in Part 2's practice app.)
3. Clean-up: delete `App.tsx`/`App.css`, one UI language, the "Kommune"/"Poststed" column.

---

# Part 2: `practice/web/` (29 Oct – 1 Nov 2026)

## 8. The steps

| Step | What I built | The main idea |
|---|---|---|
| 1 | Created the app | `index.html` → `main.tsx` mounts `<App />` into `#root` |
| 2 | Dev proxy that added the token, first `fetch` | The browser never saw the token (the BFF idea). Later removed |
| 3 | Typed API client `api.ts` | TS types mirror the C# records; JSON is camelCase |
| 4 | Search form with loading and error states | Two states: `name` (the input) and `query` (what was searched) |
| 5 | Paging with my own `Paginator` component | Props down, a callback up |
| 6 | Router and a company page | `useParams`, `ApiError` with the status code for 400/404 |
| 7 | Search kept in the URL | `useSearchParams`: refresh, Back and shared links keep the search |
| 8 | Layout route | `<Outlet />` = Razor's `@RenderBody()`, Vue's `<slot />` |
| 9 | Import page that polls the background job | `setInterval` in an effect, **with cleanup** |
| 10 | (backend) `users` table with hashed passwords | |
| 11 | Login, auth context, route guard, automatic logout | Context = Vue's provide/inject |

**Commands new in Part 2:**

```bash
npm create vite@latest web -- --template react-ts --no-interactive   # no questions asked
npm run lint           # the template now uses Oxlint, not ESLint
npm run format         # Prettier: 4 spaces, no semicolons, single quotes (.prettierrc.json)
```

Format on save is set in `.vscode/settings.json` with `"prettier.requireConfig": true`, so only folders
with a Prettier config are formatted (the real `web/` keeps its 2 spaces).

## 9. The code worth remembering

### Props and a callback: `Paginator.tsx` (best version: Part 3, step 5)

`src/components/Paginator.tsx` (`components/` = reusable pieces, `pages/` = whole screens):

```tsx
type Props = {
    page: number
    pageSize: number
    totalCount: number
    onPageChange: (page: number) => void // only the TYPE: "give me a function that takes a number"
}

export default function Paginator({ page, pageSize, totalCount, onPageChange }: Props) {
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))
    return (
        <nav className="paginator">
            <button onClick={() => onPageChange(page - 1)} disabled={page <= 1}>Prev</button>
            <span>Page {page} of {totalPages}</span>
            <button onClick={() => onPageChange(page + 1)} disabled={page >= totalPages}>Next</button>
        </nav>
    )
}
```

The changes in `SearchPage.tsx`:

```tsx
const [page, setPage] = useState(1)

useEffect(() => {
    // ... searchCompanies(query, page) ...
}, [query, page]) // rerun when either changes

function handleSubmit(e: SubmitEvent<HTMLFormElement>) {
    e.preventDefault()
    const trimmedName = name.trim()
    if (trimmedName === query && page === 1) return // nothing would change: the effect wouldn't rerun
    setLoading(true)
    setQuery(trimmedName)
    setPage(1) // a new search starts at page 1
}

function handlePageChange(newPage: number) {
    setLoading(true) // setState in an event handler is fine
    setPage(newPage)
}

// under the table:
<Paginator page={result.page} pageSize={result.pageSize} totalCount={result.totalCount} onPageChange={handlePageChange} />
```

**Where `page` really changes.** The component that owns a state is the only one that changes it. `page` lives in SearchPage (it needs it for the fetch); Paginator gets a read-only copy as a prop.

```
SearchPage:  onPageChange={handlePageChange}          "here's my function"
Paginator:   onClick={() => onPageChange(page + 1)}   calls it with 4 → runs handlePageChange(4)
             → setPage(4) → SearchPage re-renders → the effect sees page changed → fetch page 4
             → the new result.page = 4 flows back into Paginator as a prop
```

**Vue:** `this.page += 1` works for a component's **own** data. For a prop, Vue warns too ("avoid mutating a prop") and you `emit('page-change', page + 1)`, the parent listens with `@page-change`. React has no emit: the parent passes a function as a prop (named `onX` by convention) and the child calls it. Props down, callbacks up. The payoff: Paginator knows nothing about searching, so it's reusable on any list.

#### Questions and answers

1. **Why doesn't Paginator call `setPage` itself?** page is SearchPage's state, so only SearchPage changes it. Paginator gets the value as a prop and a callback `onPageChange`; it calls the callback and the parent runs setPage. Props down, callbacks up. In Vue: emit.
2. **Why does a new search go back to page 1?** A new query has a different number of results; staying on page 50 could be past the end (empty, or a 400).
3. **What if `page` is missing from the deps `[query, page]`?** The effect only reruns on query changes. Clicking Next changes page and shows "Loading...", but no fetch happens, so the table never updates. ESLint's `exhaustive-deps` warns about it.
4. **Why disable the buttons at the ends? What does the API answer for `page=0`?** There's no data there. The API has `[Range(1, 1000)]` on Page, so page=0 gives **400 Bad Request**. Disabling is a UX guard; the API validation is the real one.

### State in the URL: `SearchPage.tsx`

```tsx
const [searchParams, setSearchParams] = useSearchParams()
const query = searchParams.get('name') ?? ''
const page = Number(searchParams.get('page')) || 1      // missing or "abc" → 1

function handlePageChange(newPage: number) {
    const params = new URLSearchParams(searchParams)     // copy, so `name` stays
    params.set('page', String(newPage))
    setSearchParams(params)                               // = router.push({ query })
}
```

- `query` and `page` are **read from the URL** on every render; the URL is the only place they live.
- Changing the URL re-renders, the effect sees `[query, page]` change, and fetches. The effect itself
  didn't change when I moved the state.
- `<Link to=… state={{ search: searchParams.toString() }}>` carries the search to the company page
  invisibly, so "← Back to search" returns to it.

### An effect with cleanup: polling on `ImportsPage.tsx`

```tsx
useEffect(() => {
    if (!isActive || runId === undefined) {
        return
    }
    const timer = setInterval(async () => {
        setRun(await getImport(runId))
    }, 1000)
    return () => clearInterval(timer)           // the cleanup
}, [isActive, runId])
```

- React runs the cleanup **before the effect runs again** and **when the component disappears**
  (Vue's `onUnmounted`, attached to the effect).
- The polling stops by itself: the status becomes `Succeeded`, `isActive` turns false, React cleans up the
  timer, and the effect returns early.
- Without the cleanup, leaving the page mid-import leaves a timer running forever. StrictMode's double run
  in development exists to expose exactly this.
- This is the client half of the API's **202 Accepted** pattern: "accepted, here's the run id, poll it".

### Layout route and nested routes: `App.tsx`

```tsx
<Routes>
    <Route element={<Layout />}>                         {/* draws header/footer + <Outlet /> */}
        <Route path="/login" element={<LoginPage />} />
        <Route element={<RequireAuth />}>                {/* the guard: <Outlet /> or redirect */}
            <Route path="/" element={<SearchPage />} />
            <Route path="/companies/:orgnr" element={<CompanyPage />} />
            <Route path="/import" element={<ImportsPage />} />
        </Route>
        <Route path="*" element={<p>Page not found.</p>} />
    </Route>
</Routes>
```

- A `<Route>` **without a path** matches no URL itself; it wraps the routes inside it.
- The layout stays mounted while pages change, so its state survives navigation.

### Login: `src/auth/`

| File | Role | Vue equivalent |
|---|---|---|
| `session.ts` | Load, save and clear the session in `localStorage`; an expired one counts as none | |
| `LoginPage.tsx` | The form; posts to the login endpoint, saves the session, calls `onLogin` | a component that `emit`s |
| `AuthContext.ts` | `createContext` + my custom hook `useAuth()` | `inject()` |
| `AuthProvider.tsx` | Holds `session` state, `login`, `logout`, and the expiry timer; wraps the app | `provide()` |
| `RequireAuth.tsx` | `session ? <Outlet /> : <Navigate to="/login" replace />` | `router.beforeEach` |

```tsx
// AuthProvider.tsx: log out by itself when the token expires
useEffect(() => {
    if (!session) {
        return
    }
    const msLeft = new Date(session.expiresAt).getTime() - Date.now()
    const timer = setTimeout(logout, msLeft)
    return () => clearTimeout(timer)
}, [session])

return <AuthContext value={{ session, login, logout }}>{children}</AuthContext>
```

```ts
// api.ts: every API call goes through this
async function apiFetch(url: string, init?: RequestInit): Promise<Response> {
    const session = loadSession()
    const headers = new Headers(init?.headers)
    if (session) {
        headers.set('Authorization', `Bearer ${session.accessToken}`)
    }
    const response = await fetch(url, { ...init, headers })
    if (response.status === 401) {          // token missing or expired
        clearSession()
        window.location.assign('/login')
    }
    return response
}
```

- **`children`** is whatever sits between `<AuthProvider>` and `</AuthProvider>` (Vue's default `<slot />`).
- **`apiFetch`** is the one place that knows about tokens, like an axios interceptor. `login()` uses the
  plain `fetch`, because a wrong password is also a 401 and must show a message, not redirect.
- **Hooks before any early `return`.** React needs the same hooks in the same order on every render.
- **Context and component in separate files:** a `.tsx` file should export only components, or hot
  reload stops working for it (Oxlint's `only-export-components`).
- **Hiding the Imports link is convenience, not security.** The API's policy is the real check (403).

**The login page (Part 3, step 2).** `Session` mirrors the API's `LoginResponse`: `{ token, expiresAt,
username, scopes }` (Part 2's API answered `accessToken`). Before there's a context, `App` holds the session:
```tsx
// LoginPage.tsx: the handler is a function INSIDE the component; the component returns the form
export default function LoginPage({ onLogin }: Props) {
    const [username, setUsername] = useState('')   // '' not null: an input always holds a string
    const [error, setError] = useState<string | null>(null)
    // ...password, busy

    async function handleSubmit(e: SubmitEvent<HTMLFormElement>) {   // not FormEvent (deprecated)
        e.preventDefault()                      // no full page reload
        const response = await fetch('/api/auth/login', { method: 'POST',
            headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ username, password }) })
        if (response.status === 401) return setError('Wrong username or password')
        const session: Session = await response.json()   // await: the body arrives after the headers
        saveSession(session)
        onLogin(session)                        // tell the parent: Vue's emit('login', session)
    }

    return <form onSubmit={handleSubmit}>...<input value={username} onChange={(e) => setUsername(e.target.value)} />...</form>
}

// App.tsx
const [session, setSession] = useState(loadSession)   // a function: runs once, on the first render
if (!session) return <LoginPage onLogin={setSession} />
```
- **`value` + `onChange`** = Vue's `v-model`: the state is the truth, the input shows it.
- **`onLogin={setSession}`:** the child calls the parent's setter; App re-renders with a session.
- **localStorage vs a cookie:** localStorage is never sent anywhere, only JS reads it, so any script on
  the page (an XSS bug, a bad package) can steal the token. The alternative: an **HttpOnly cookie** set
  by the server (JS can't read it; needs CSRF protection), or a **BFF** (the token stays on a server).
  localStorage is the common SPA choice for a short-lived token (120 minutes here); React escapes text.
- **The same message for both failures:** stops **user enumeration** (finding which usernames exist).

## 10. More Vue to React

| Vue / Nuxt | React |
|---|---|
| `defineProps<{…}>()` | Typed props: `function X({ a, b }: XProps)` |
| `emit('change', n)` | A callback prop: `onChange(n)` |
| `provide()` / `inject()` | `<MyContext value={…}>` / `useContext(MyContext)` |
| Default `<slot />` | `children` |
| Layout `<slot />` / `<NuxtPage />` | `<Outlet />` in a layout route |
| `route.query`, `router.push({ query })` | `useSearchParams()` |
| `router-link-active` | `NavLink` adds the class `active` |
| `router.beforeEach` guard | A guard component: `<Outlet />` or `<Navigate />` |
| `onUnmounted` | The function returned from `useEffect` |
| `:style="{ color: 'red' }"` | `style={{ color: 'red' }}` (object only, camelCase properties) |
| `class="x"` | `className="x"` |

## 11. Things I learned on the way

- **`export default X`** is imported without braces (`import X from './X'`); **named exports** with
  braces and the exact name (`import { searchCompany } from './api'`).
- **StrictMode** in development renders every component twice and runs every effect twice, to catch
  effects that aren't safe to repeat. A production build runs them once.
- **React Developer Tools** (browser extension): the Components tab shows each component's props and
  state, and lets me edit state live. State can't be changed from the console, because it's local to
  the component function.
- **`FormEvent` is deprecated** in React 19.3's types: use `SubmitEvent` for a form submit.
- **A relative fetch URL** (`api/companies`) depends on the current page: on `/companies/123` it becomes
  `/companies/api/companies`. Always start API URLs with `/`.
- **`localStorage` is not a cookie.** It's never sent automatically (`apiFetch` adds the header), and
  JavaScript can always read it, so an XSS bug could steal the token. An httpOnly cookie can't be read
  by JavaScript; that's why production systems often prefer it, or a BFF.
- **`window.location = x`, `.href = x` and `.assign(x)`** are the same; **`.replace(x)`** navigates
  without adding a history entry.
- **Types are a promise, not a check.** If a C# DTO changes, TypeScript doesn't know. Bigger projects
  generate the TS types from the API's OpenAPI document (`openapi-typescript`, orval, Kiota, NSwag).

## 12. Mistakes in Part 2

| What happened | Lesson |
|---|---|
| `import { Paginator } from './Paginator'` | Braces look for a named export; `export default` is imported without them |
| `` fetch(`api/companies?…`) `` worked on `/` but not on `/companies/…` | A relative URL resolves against the current page; start with `/` |
| `Authorization: Bearer: …` for the importer token | One colon too many, so every `/api/imports` call was 401. Unused routes hide bugs |
| `prompt('Start a new import?')` | `prompt` shows a text input and returns what was typed (`''` on an empty OK); `confirm` returns `true`/`false` |
| The guarded group used `<Layout />` instead of `<RequireAuth />` | Nothing was guarded and the header appeared twice; the build also failed on the unused import |
| The old nav line was left in `Layout` | Imports showed twice and Log out showed on the login page; wrap it in `{session && …}` |
| Imports failed with "Could not load file or assembly System.Net.Http.Json" | An apt update replaced .NET 10.0.13 with 10.0.14 while the API was running; the first lazy load looked in a folder that no longer existed. Restart processes after a runtime update |
