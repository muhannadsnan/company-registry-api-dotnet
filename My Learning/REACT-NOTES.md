# React + TypeScript: what I built and learned

Two frontends, both typed by me with an AI tutor guiding the steps:

- **Part 1 (22 Oct 2026):** `web/`, the real project's frontend: a company search page and a company page.
  Stack: Vite 8 · React 19 · TypeScript · react-router · ESLint.
- **Part 2 (29 Oct – 1 Nov 2026):** `practice/web/`, a second frontend for my practice API, built in 11
  steps: paging, URL state, a layout, an import page that polls, and a login with route guards.
  Stack: Vite 8 · React 19.3 · TypeScript 7 · react-router · Oxlint · Prettier.
- **Part 3 (from 14 Nov 2026):** `practice2/web/`, the frontend for the practice2 API (port 5020), in 13
  steps. Stack: Vite 8 · React 19.3 · TypeScript 6 · ESLint · Prettier. Its steps are **merged into the
  matching sections below**: one direct answer per topic, not a second version.

| Part 3 step | What | Status |
|---|---|---|
| 1 | Create the app (Vite, React, TypeScript), Prettier, a proxy to the API | ✅ |
| 2 | Login page: form, `POST /api/auth/login`, save the session in `localStorage` | ✅ |
| 3 | `apiFetch` sends the token, and a typed API client | ✅ |
| 4 | Search form with loading and error states | ✅ |
| 5 | Paging, with a component and props | ✅ |
| 6 | Router and the company page (400/404) | ✅ |
| 7 | Roles on the company page (and the company page public, like the API) | ✅ |
| 8 | Search kept in the URL | ✅ |
| 9 | Layout route (header, footer, `<Outlet />`) | ✅ |
| 10 | Auth context, route guard, username and Log out in the header | ✅ |
| 11 | Token expiry: automatic logout, and a 401 sends you to the login page | ✅ |
| 12 | Import page with polling (link only for users with `companies.import`) | ✅ |
| 13 | Tailwind: setup, basic styling per element, shared classes in `tw.ts` (13d) | ✅ |
| 14 | (main project, not React) RabbitMQ: a first quick try; practice3 repeats it | ⏳ |

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

### Routing: `main.tsx` + `App.tsx` (best version: Part 3, step 6)

```bash
npm install react-router@8.5.1   # v7+ merged react-router-dom into react-router
```

```tsx
// main.tsx: the router wraps the whole app
<StrictMode>
    <BrowserRouter>
        <App />
    </BrowserRouter>
</StrictMode>

// App.tsx: after the login check
<Routes>
    <Route path="/" element={<SearchPage />} />
    <Route path="/companies/:orgnr" element={<CompanyPage />} />
    <Route path="*" element={<NotFoundPage />} />
</Routes>

// SearchPage.tsx: a row links to the company page
<Link to={`/companies/${c.orgnr}`} key={c.orgnr}>...</Link>
```

- `<BrowserRouter>` follows the browser URL. `<Routes>` renders the `<Route>` that matches; `*` catches the rest.
- `:orgnr` is a URL parameter, like Nuxt's `pages/companies/[orgnr].vue`.
- `!` after `getElementById('root')` tells TypeScript "this is not null". `<StrictMode>` adds extra checks in development only.

#### Questions and answers

1. **Why `<Link to>` and not `<a href>`?** `<Link>` stops the browser's normal navigation, changes the URL with the History API, and React Router renders the matching `<Route>`: no server request. `<a href>` does a full page load: index.html and the JS download again and all React state is lost (the session survives only because it's in localStorage).
2. **`/companies/:orgnr` vs `/api/companies/:orgnr`?** The first is the address bar: Vite's dev server answers with `index.html` (that's why a refresh works), then React Router picks the component. The second is `fetch`: Vite's proxy forwards it to the .NET API on 5020, which answers with JSON. In production a reverse proxy or ASP.NET's static files play Vite's role.
3. **Open a company, click Back to search: where did the search go?** When the route changes, SearchPage **unmounts** and its `useState` values are destroyed. Back mounts a fresh SearchPage with the initial values (empty query, page 1). Fix: keep the search in the URL (`?name=fjord&page=3`), step 8.

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

### CompanyPage.tsx: URL parameter, 400 vs 404 (best version: Part 3, step 6)

```tsx
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { type CompanyDto, getCompany } from '../api'
import { errorMessage } from '../helpers'

export default function CompanyPage() {
    const { orgnr = '' } = useParams() // from the URL /companies/:orgnr; '' if missing
    const [company, setCompany] = useState<CompanyDto | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [loading, setLoading] = useState(true)

    useEffect(() => {
        let ignore = false
        getCompany(orgnr)
            .then((data) => { if (!ignore) setCompany(data) })
            .catch((e) => { if (!ignore) setError(errorMessage(e, orgnr)) })
            .finally(() => { if (!ignore) setLoading(false) })
        return () => { ignore = true }
    }, [orgnr])

    return (
        <>
            <Link to="/" className="btn">← Back to search</Link>
            {loading && <p className="loading">Loading...</p>}
            {!loading && error && <p role="alert">{error}</p>}
            {!loading && company && (
                <>
                    <h2>{company.name}</h2>
                    <div><strong>Orgnr:</strong> <span>{company.orgnr}</span></div>
                    <div><strong>Form:</strong> <span>{company.orgForm}</span></div>
                    <div><strong>Registered:</strong> <span>{company.registrationDate ?? '_'}</span></div>
                </>
            )}
        </>
    )
}
```

`src/helpers.ts`:

```ts
import { ApiError } from './api'

export function errorMessage(e: unknown, orgnr: string): string {
    if (!(e instanceof ApiError)) return 'Could not reach the server'
    if (e.status === 400) return `"${orgnr}" is not a valid organisation number`
    if (e.status === 404) return `No company with orgnr ${orgnr}`
    return `Loading Failed (${e.status})`
}
```

- **`const { orgnr = '' } = useParams()`** is destructuring with a default. PHP: `['orgnr' => $orgnr] = $params;`
- **Hooks** are the functions starting with `use`. Call them at the top of the component, never inside `if` or loops.
- **`<> … </>`** is a fragment: groups elements without an extra `<div>`. **`??`**: the right side if the left is null or undefined (same as PHP).
- The checks: `/companies/123` gives the 400 text; `/companies/123456785` passes MOD11 but doesn't exist, so the 404 text.

**Helper files.** React has no fixed folder (Nuxt auto-imports `utils/`; React doesn't, you import). A few helpers: one `src/helpers.ts` with named exports, `import { a, b } from '../helpers'`. When it grows, split **by topic** (`utils/errors.ts`, `utils/dates.ts`), not one file per function. `import * as helpers` also works; Vite drops unused exports in production (tree-shaking) either way. Helpers go in **`.ts`** files: the `react-refresh` ESLint rule warns when a `.tsx` file exports a component *and* something else, because hot reload can only swap component-only files. A more general alternative: `errorMessage(e, { 400: '…', 404: '…' })` with `Partial<Record<number, string>>`, so every page passes its own texts.

**Barrel files.** `import { SearchPage, CompanyPage } from './pages'` works with a `pages/index.ts` that re-exports (`export { default as SearchPage } from './SearchPage'`); braces because they're named exports. In apps I keep direct imports: a barrel is another file to maintain, can cause circular imports, and makes Vite's dev server load every page. Barrels suit libraries.

#### Questions and answers

1. **Why does 400 vs 404 matter?** 400 = your input is wrong (not 9 digits, or it fails MOD11): fix what you typed. 404 = the input is fine but no such company exists in our data. Different causes, different messages.
2. **Why is `errorMessage` outside the component?** It uses no state, props or hooks, so it doesn't need to live inside. Outside, it's created once instead of on every render, it's a pure function you can unit test alone, and the component stays short.

### RoleList.tsx and a public company page (best version: Part 3, step 7)

```ts
// api.ts: the type mirrors the C# RoleDto record
export type RoleDto = {
    roleType: string
    roleName: string
    personName: string | null // a person holds the role...
    birthYear: number | null
    unitOrgnr: string | null // ...or a unit (an auditing or accounting firm)
    unitName: string | null
    resigned: boolean
}
export function getRoles(orgnr: string) {
    return apiFetch<RoleDto[]>(`/api/companies/${encodeURIComponent(orgnr)}/roles`) // leading / !
}
```

```tsx
// components/RoleList.tsx: its own fetch, loading and error
export default function RoleList({ orgnr }: { orgnr: string }) {
    const [roles, setRoles] = useState<RoleDto[] | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [loading, setLoading] = useState(true)

    useEffect(() => {
        getRoles(orgnr)
            .then((data) => setRoles(data))
            .catch((e) => setError(errorMessage(e, orgnr)))
            .finally(() => setLoading(false))
    }, [orgnr])

    // hooks first, then early returns
    if (loading) return <p className="loading">Loading roles...</p>
    if (error) return <p role="alert">{error}</p>
    if (!roles || roles.length === 0) return <p>No roles registered.</p>

    return (/* heading + rows: roles.map((r, i) => <div key={i}>…{r.personName ?? r.unitName}…) */)
}
```

The company page is **public**, like the API (`GET /api/companies/{orgnr}` is `[AllowAnonymous]`); roles need a login:

```tsx
// App.tsx: no early return; the routes always render, only "/" depends on the session
const [session, setSession] = useState(loadSession)
...
{session ? (<>Logged in as {session.username} … Log out</>) : (<p>Anonymous user. <Link to="/">Login here</Link></p>)}
<Routes>
    <Route path="/" element={session ? <SearchPage /> : <LoginPage onLogin={setSession} />} />
    <Route path="/companies/:orgnr" element={<CompanyPage loggedIn={session !== null} />} />
    <Route path="*" element={<NotFoundPage />} />
</Routes>

// CompanyPage.tsx
export default function CompanyPage({ loggedIn }: { loggedIn: boolean }) {
...
{loggedIn ? <RoleList orgnr={company.orgnr} /> : <Link to="/">Login to see the roles</Link>}
```

- **Hiding in the frontend is UX; the API's 401 is the security.** Anyone can call the endpoint without the UI.
- **Early return vs `&&`:** early returns when the states replace the **whole** output (RoleList: loading OR error OR empty OR list); `{cond && …}` when part of the page stays (CompanyPage keeps "← Back" in every state). Rule: **all hooks first**, then returns. React counts hooks by call order; a return above a hook skips it on some renders and React throws.
- **Narrowing:** after `if (!session) return …`, TS knows `session` isn't null below. Remove the return and `session.username` fails with "possibly null"; `{session && …}` or `session ? … : …` narrows for that block only. C#: flow analysis after `if (x is null) return;`.
- **When `ignore` matters:** when a prop changes while the component stays mounted and an older response can arrive last. RoleList lives inside one company page and Back unmounts it, so it's left out. It would matter if a unit linked to its company (`/companies/A` → `/companies/B` keeps the same RoleList mounted).
- **Logging in from a company page** lands on the search, not back on the company. A route guard that remembers "where you came from" fixes it (step 10).

#### Questions and answers

1. **Why is RoleList a separate component with its own fetch?** Each one loads and fails on its own. The company endpoint is anonymous but roles need a token, so a 401/403 on roles shouldn't hide the company details; and the details show without waiting for the roles.
2. **Why is it rendered only after the company loaded?** On a 400 or 404 there's no company, so a roles request would fail with the same error and show it twice. It runs only once the orgnr is known to be valid and to exist.
3. **`r.personName ?? r.unitName`: why can either be null?** A role is held by a person (name, birth year) or a unit, like an auditing or accounting firm (orgnr, name). Exactly one side is filled. The API sorts units last.
4. **Roles have no id: is `key={i}` OK?** The key tells React which item is which between renders. An index is fine when the list is only replaced as a whole: never reordered, filtered or inserted into, and rows have no state of their own. It's a bug when you sort or delete with state in the rows (a checkbox): the state sticks to the position, not the item. A combined key `` `${r.roleType}-${r.personName ?? r.unitOrgnr}-${r.birthYear}` `` is better (use the side that exists, or units collide as `REVI-null-null`), but the same person can hold the same role twice (resigned + current). The proper fix is an id from the API.
5. **Which errors can a roles request really show?** Not 400/404: RoleList renders only after the company loaded. The realistic ones are 401 (token expired) and 403 (no `companies.read`), which fall through to "Loading Failed (…)", and network errors. Step 11 handles the 401.

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
| (Part 3) `e.status === 401` for the "No company" text: a missing company showed "Loading Failed (404)" | tsc can't catch it (any number is a `number`); only testing the 404 URL against the running API shows it. The endpoint is `[AllowAnonymous]`, so 401 can't even happen there |
| (Part 3) `ApiError` left in CompanyPage's import after moving `errorMessage` to helpers.ts: tsc TS6133 | Moving code moves its imports too: the import goes where the code that uses it went |
| (Part 3) `className="d-flex"` | A Bootstrap class name; without Bootstrap (or my own CSS rule) it does nothing |
| (Part 3) `apiFetch(\`api/companies/…/roles\`)` without the leading `/`: "Could not reach the server" | A relative URL: on `/companies/923609016` it became `/companies/api/…`, not proxied. Vite answered `index.html` with **200**, `response.json()` failed on HTML, and that error isn't an `ApiError`. The message lied about the cause |
| (Part 3) `bithYear` in `RoleDto`: "Born" always empty | The TS type is **my claim** about the JSON; nothing checks it at runtime, so a misspelled field is just `undefined`. Same as a misnamed C# DTO property (default value). Runtime validation: Zod |
| (Part 3) The old `if (!session) return <LoginPage/>` left in App: every URL showed the login | An early return above `<Routes>` means the routes never render. Removing it made tsc say `session` is "possibly null" → narrow with `session ? … : …` |
| (Part 3) `setSearchParams({ ...searchParams, page })`: Next on "fjord" gave `/?page=2` (all companies) | Spread copies own properties; `URLSearchParams` has none, so `name` was dropped. Copy with `new URLSearchParams(searchParams)`, then `set` |
| (Part 3) `useState('')` for the input after moving the search to the URL | After a refresh the table showed fjord but the input was empty: start it with `useState(query)` |
| (Part 3) Moved the search to `/search` (HomePage at `/`), but `backUrl` and the logged-out "Log in" still pointed at `/` | Moving a route means searching for every link to it (`grep -rn 'to="/"'` and `backUrl`): tsc can't know a path string is wrong |
| (Part 3) App kept its own `session` useState after the AuthProvider, and passed it to HomePage | A **second copy**: login changed the provider's session, App's stayed null, so HomePage said "click login" until a refresh. tsc flagged the leftover `handleLogout`. A context is one source of truth: read it with `useAuth()` |
| (Part 3) `const session = await response.json()` inside LoginPage, which also has `session` from `useAuth()` | Works (the inner name hides the outer one) but reads wrong: name it `newSession` |
| (Part 3) `canImport` computed in Layout but never used: the Imports link showed for everyone | tsc's "declared but never read" was the hint; the value has to wrap the link: `{canImport && …}` |
| (Part 3) `/imports` outside `<RequireAuth>`: logged out, it showed "Request failed (401)" | apiFetch raises the expiry event only when there *was* a session; a page that needs a login belongs inside the guard |
| (Part 3) The poll's `.catch((e) => messageFor(e))` | It computed the message and threw it away: a failed poll showed nothing. `setError(messageFor(e))` |

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

### State in the URL: `SearchPage.tsx` (best version: Part 3, step 8)

```tsx
const [searchParams, setSearchParams] = useSearchParams()
const query = searchParams.get('name') ?? '' // was useState
const page = Number(searchParams.get('page')) || 1 // missing, "abc" (NaN) or 0 → 1
const [name, setName] = useState(query) // the input starts with the URL's search

// useEffect: unchanged, still [query, page]; they just come from the URL now

function handleSubmit(e: SubmitEvent<HTMLFormElement>) {
    e.preventDefault()
    const trimmedName = name.trim()
    if (trimmedName === query && page === 1) return
    setLoading(true)
    setSearchParams(trimmedName ? { name: trimmedName } : {}) // no page param = page 1
}

function handlePageChange(newPage: number) {
    setLoading(true)
    const params = new URLSearchParams(searchParams) // a copy, so name stays
    params.set('page', String(newPage))
    setSearchParams(params) // = Vue's router.push({ query })
}

// a row: the sender builds the back URL and passes it invisibly
<Link to={`/companies/${c.orgnr}`} state={{ backUrl: `/?${searchParams}` }} key={c.orgnr}>
```

```tsx
// CompanyPage.tsx: the receiver uses it, with a fallback
const backUrl = useLocation().state?.backUrl ?? '/'
<Link to={backUrl} className="btn">← Back to search</Link>
```

- **Spread** `{ ...a, page: '2' }` copies an object's own properties (PHP `[...$a, 'page' => '2']`). It works on plain objects, but `URLSearchParams` keeps its entries hidden inside, so `{ ...searchParams }` is `{}`. Use its methods: `new URLSearchParams(copy)`, `get`, `set`, `toString`.
- **`location.state` is typed `any`**, so `state?.backUrl` needs no cast, but nothing is checked either (a typo is just `undefined`, like `bithYear`).
- **`state` vs the URL:** state is stored in the browser's history entry, invisible; the company URL stays clean. A link opened directly has no state → fallback `/`.
- **`navigate(-1)`** (`useNavigate`) = the browser's Back button: natural when you came from the search, but a directly opened link leaves the app or does nothing.
- **Hooks only in components or other hooks**, so a helper that calls `useLocation()` must be a **custom hook**: a function named `use…` (`useBackUrl()` in `src/hooks/`). React's way to reuse logic with hooks; Vue's composables.
- **Known gap:** search fjord, then bank, then the **browser** Back: the results follow the URL, but the input still says "bank" (`useState(query)` only runs on the first render) and "Loading..." doesn't show (no handler ran).

#### Questions and answers

1. **Where do `query` and `page` live, and why is `name` still a useState?** They're read from the URL on every render; the URL is the only place they live. `name` is what you're typing: it shouldn't change the URL on every keystroke, only on submit.
2. **Why did refresh, Back and a shared link lose the search before?** It was useState: memory that dies on a refresh or unmount, and a link only had the path. The URL is kept in history, reloaded and shared by the browser.
3. **`Number(searchParams.get('page')) || 1` for `abc`, `0`, missing, `5000`?** `Number('abc')` is NaN and `Number('0')` is 0, both falsy, so 1; missing is `Number(null)` = 0, so 1 (the URL keeps the wrong value, the results are page 1). `5000` passes the frontend; the API's `[Range(1, 1000)]` gives 400, "Search failed (400)".
4. **Why `new URLSearchParams(searchParams)` and not `{ ...searchParams, page }`?** Spread copies only own properties and URLSearchParams has none, so `name` is dropped (Next on "fjord" gave `/?page=2`, all companies). The copy constructor copies every entry; `set` changes only page.
5. **`state` on the Link vs the search in the company URL?** State is invisible and lives in the history entry; the URL stays clean. A directly opened link has no state, so Back goes to `/`. In the URL (`?from=…`) it would survive sharing, but it's ugly and every page must pass it on.

### An effect with cleanup: polling on `ImportsPage.tsx` (best version: Part 3, step 12)

```ts
// api.ts: apiFetch learns POST; the rest of it unchanged
async function apiFetch<T>(url: string, init?: RequestInit): Promise<T> {
    const session = loadSession()
    const headers = new Headers(init?.headers)
    if (session) headers.set('Authorization', `Bearer ${session.token}`)
    const response = await fetch(url, { ...init, headers }) // keeps method, puts the merged headers on top
    ...
}
export type ImportStatus = 'Queued' | 'Running' | 'Succeeded' | 'Failed' | 'Abandoned'
export type ImportRunDto = { id: number; status: ImportStatus; queuedAt: string; read: number; inserted: number; /* … the C# record */ }
export function listImports() { return apiFetch<ImportRunDto[]>('/api/imports') }
export function startImport() { return apiFetch<ImportRunDto>('/api/imports', { method: 'POST' }) } // 202 is ok (2xx)
```

```tsx
// pages/ImportsPage.tsx
const [runs, setRuns] = useState<ImportRunDto[] | null>(null)
const [error, setError] = useState<string | null>(null)
const [starting, setStarting] = useState(false)
const activeId = runs?.find(isActive)?.id // derived: a number, or undefined

useEffect(() => {                         // [] = once, after the first render
    listImports().then(setRuns).catch((e) => setError(messageFor(e)))
}, [])

useEffect(() => {                         // poll while a run is Queued/Running
    if (activeId === undefined) return
    const timer = setInterval(() => {
        listImports().then(setRuns).catch((e) => setError(messageFor(e)))
    }, 2000)
    return () => clearInterval(timer)
}, [activeId])

function handleStart() {
    setStarting(true)
    setError(null)
    startImport()
        .then((run) => setRuns((prev) => [run, ...(prev ?? [])])) // the function form: latest state
        .catch((e) => setError(messageFor(e)))                     // 403 not allowed, 409 already running
        .finally(() => setStarting(false))
}
// <button onClick={handleStart} disabled={starting || activeId !== undefined}>
```

```tsx
// App.tsx: inside <RequireAuth>, next to /search
<Route path="/imports" element={<ImportsPage />} />

// Layout.tsx: the link only with the scope
const canImport = session?.scopes.includes('companies.import') ?? false
{canImport && <> | <NavLink to="/imports">Imports</NavLink></>}
```

- **`.then(setRuns)`** passes the function; `.then` calls it with the data. Same as `.then((list) => setRuns(list))`. `.then(() => setRuns)` only *returns* the setter and sets nothing. C#: a method group, `Select(RoleDto.Fill)`.
- **`[item, ...list]`** is a **new** array with `item` first (the immutable "unshift"). `list.unshift(item)` mutates in place and returns the **length**. **Never mutate state:** React compares by reference, sees the same array, and doesn't re-render.
- **A setter takes a value or a function:** `setRuns(prev => [run, ...prev])` gets the latest state when React applies the update. Use it when the new state depends on the old.
- **`setInterval` vs a recursive `setTimeout`:** setTimeout fires once; polling with it means scheduling the next one after each response (never overlaps slow requests, sometimes better). Either way the cleanup must clear it: the cleanup undoes **anything the effect started**.
- **StrictMode** mounts twice in development, so the Network tab shows two `GET /api/imports` on load. Not in production.
- This is the client half of the API's **202 Accepted** pattern.

#### Questions and answers

1. **Why `[activeId]` and not `[runs]`?** React compares dependencies by **reference** (`Object.is`). Each poll sets a **new array**, so `[runs]` would count as changed after every poll: clear the interval, start a new one, forever. `activeId` is a number; the same id is unchanged, so the interval runs untouched until the id changes.
2. **How does the polling stop by itself?** The last poll returns Succeeded → `setRuns` → re-render → `activeId` recomputed as `undefined` → the dependency changed, so React runs the cleanup (`clearInterval`), then the effect, which returns early.
3. **Why `setRuns(prev => [run, ...(prev ?? [])])` and not `setRuns([run, ...runs])`?** `runs` in handleStart is the value from the render of the click. The POST takes a while; a poll's `setRuns` can land in between, and `[run, ...runs]` would overwrite that newer list with a stale one. `prev` is the latest state.
4. **A user without `companies.import` opens `/imports`?** No link in the header (UX). The page loads, the API answers **403 Forbidden** (401 means not logged in), and the page shows "You are not allowed to run imports". The **API's policy** `[Authorize(Policy = "companies.import")]` stops them; RequireAuth only checks the login, not the scope.
5. **Why 202 and not 200 with the result?** 202 = "accepted, not done". The import runs in a BackgroundService, independent of the request; the response carries the run (and a Location header), and the client polls. A 200 with the whole import inside the request would hang for minutes, hit proxy and browser timeouts, and die if the connection dropped.

### Layout route and nested routes: `Layout.tsx` + `App.tsx` (best version: Part 3, step 9)

```tsx
// Layout.tsx: header and footer drawn once; pages render at <Outlet />
export default function Layout({ session, onLogout }: { session: Session | null; onLogout: () => void }) {
    return (
        <>
            <header>
                <strong>Practice 2</strong>
                {session ? (
                    <nav>
                        <NavLink to="/">Home</NavLink> | <NavLink to="/search">Search</NavLink>
                        <span>Welcome, {session.username}</span>
                        <button className="logout" onClick={onLogout}>Log out</button>
                    </nav>
                ) : (
                    <nav>Anonymous user, <NavLink to="/search">Log in</NavLink></nav>
                )}
            </header>
            <main>
                <Outlet /> {/* the matching child route renders here */}
            </main>
            <footer><p>Public data from Brønnøysundregistrene</p></footer>
        </>
    )
}
```

```tsx
// App.tsx
const [session, setSession] = useState(loadSession)
function handleLogout() { clearSession(); setSession(null) }

<Routes>
    <Route element={<Layout session={session} onLogout={handleLogout} />}>   {/* no path: a wrapper */}
        <Route path="/" element={<HomePage />} />
        <Route path="/search" element={session ? <SearchPage /> : <LoginPage onLogin={setSession} />} />
        <Route path="/companies/:orgnr" element={<CompanyPage loggedIn={session !== null} />} />
        <Route path="*" element={<NotFoundPage />} />
    </Route>
</Routes>
```

- Part 3 moved the search from `/` to `/search` and added a HomePage at `/`; every link to the search (`backUrl`, "Log in", "Login to see the roles") follows.
- `NavLink to="/"` is a special case: active only on exactly `/`, not on every URL.

#### Questions and answers

1. **The outer `<Route>` has no `path`: what does it do?** It matches no URL itself; it wraps its child routes, and whichever child matches renders inside its element's `<Outlet />`. For a shared layout (or a guard), not navigation.
2. **What renders at `<Outlet />`, and the equivalents?** The matching child route (CompanyPage on `/companies/…`). Vue Router `<router-view />`; Nuxt `<slot />` in `layouts/default.vue` (or `<NuxtPage />`); Razor `@RenderBody()` in `_Layout.cshtml`.
3. **Is Layout re-created when you go from search to a company?** No: it stays **mounted** while you move between its child routes; only the Outlet part swaps. It unmounts only on a route outside it. So the header's state survives (an open menu, a search box), its data isn't fetched again, and nothing flickers.
4. **`NavLink` vs `Link`?** NavLink knows if it's **active**: when its `to` matches the URL it adds `class="active"` and `aria-current="page"`, to highlight the current menu item. Vue: `router-link-active`.
5. **Five components deep need the session: the problem?** **Prop drilling**: every component in between must accept and pass on props it doesn't use, and each new consumer changes the whole chain. Fix: a **context**, provided once at the top and read anywhere with `useContext` (Vue's provide/inject). Step 10.

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

**The auth context and the guard (best version: Part 3, step 10).** One source of truth for the session; no prop drilling:

```ts
// auth/AuthContext.ts (.ts: no component, so react-refresh is happy)
export type AuthContextValue = { session: Session | null; login: (session: Session) => void; logout: () => void }
export const AuthContext = createContext<AuthContextValue | null>(null)
export function useAuth() {
    const value = useContext(AuthContext)
    if (!value) throw new Error('useAuth must be used inside <AuthProvider>')
    return value
}
```

```tsx
// auth/AuthProvider.tsx: the session state moved here from App
export default function AuthProvider({ children }: { children: ReactNode }) {
    const [session, setSession] = useState(loadSession)
    function login(newSession: Session) { saveSession(newSession); setSession(newSession) }
    function logout() { clearSession(); setSession(null) }
    return <AuthContext value={{ session, login, logout }}>{children}</AuthContext>
}

// auth/RequireAuth.tsx: the guard, a layout route without a path
export default function RequireAuth() {
    const { session } = useAuth()
    const location = useLocation()
    if (session) return <Outlet />
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
}

// main.tsx: <BrowserRouter><AuthProvider><App /></AuthProvider></BrowserRouter>

// App.tsx: no state, no props, only routes
<Route element={<Layout />}>
    <Route path="/" element={<HomePage />} />
    <Route path="/login" element={<LoginPage />} />
    <Route path="/companies/:orgnr" element={<CompanyPage />} />
    <Route element={<RequireAuth />}>
        <Route path="/search" element={<SearchPage />} />
    </Route>
    <Route path="*" element={<NotFoundPage />} />
</Route>

// auth/LoginPage.tsx: no props; the re-render after login() does the redirect
const { session, login } = useAuth()
const from = useLocation().state?.from ?? '/search'
// ...useStates...
if (session) return <Navigate to={from} replace />   // after all hooks
// in handleSubmit: const newSession: Session = await response.json(); login(newSession)

// Layout, HomePage, CompanyPage: const { session } = useAuth()
// CompanyPage: <Link to="/login" state={{ from: `/companies/${orgnr}` }}>Log in to see the roles</Link>
```

- **`replace` on a redirect.** Without it the redirect **adds** a history entry: `[home, /search, /login]`, and after login `[…, /login, /search]`. Back lands on a page that redirects you forward again: stuck. With `replace` the redirecting entry is swapped out: `[home, /login]` → `[home, /search]`, and Back goes home. A redirect shouldn't stay in history (like PHP's `header('Location: …')`).

#### Questions and answers (step 10)

1. **What does AuthProvider provide, and who reads it?** It holds the session state and puts `{ session, login, logout }` into AuthContext. Any component below reads it with `useAuth()` (`useContext` inside). Nothing in between passes anything: App, Layout and CompanyPage lost their session props.
2. **Why does `useAuth()` throw on `null`?** Outside the provider it would get `null`. Throwing turns a silent bug ("session is null, so logged out?") into a clear error at once, and tells TypeScript the result is never null, so callers write `const { session } = useAuth()` without a check.
3. **Why two files, AuthContext.ts and AuthProvider.tsx?** The react-refresh rule: hot reload can only swap files that export **only components**. AuthContext.ts exports a context, a hook and a type; AuthProvider.tsx only the component.
4. **What does `replace` do on `<Navigate>`?** It swaps the current history entry instead of adding one, so Back doesn't land on the redirecting page (see above).
5. **Nothing calls `navigate()` after login: how do you get back to `from`?** `login()` sets the session in the provider; every `useAuth()` user re-renders, including LoginPage, and now its `if (session) return <Navigate to={from} replace />` is true. The **re-render** redirects. `from` came from RequireAuth (or the company page's Link) through the location state.

**Token expiry and 401 (best version: Part 3, step 11).** Two ways a session ends without clicking Log out:

```tsx
// AuthProvider.tsx, after the useState
// 1. The token's time runs out: log out at that moment.
useEffect(() => {
    if (!session) return
    const msLeft = new Date(session.expiresAt).getTime() - Date.now()
    const timer = setTimeout(() => {
        clearSession()
        setSession(null)
    }, msLeft)
    return () => clearTimeout(timer) // a new login or logout cancels the old timer
}, [session])

// 2. The API says 401: apiFetch raises a window event, the provider listens.
useEffect(() => {
    function handleExpired() {
        setSession(null)
    }
    window.addEventListener('auth:expired', handleExpired)
    return () => window.removeEventListener('auth:expired', handleExpired)
}, [])
```

```ts
// api.ts, in apiFetch after the fetch
if (response.status === 401 && session) {
    clearSession()
    window.dispatchEvent(new Event('auth:expired')) // AuthProvider listens
}
```

- **The cleanup (the function an effect returns) is the effect's undo.** React keeps it and calls it **before the next run** and **on unmount**. Each run is a new function call with its own variables, so a run can't reach the previous run's `timer`; the cleanup from the run that created it can. Order: session A → timer A, returns "clear A"; session B → React calls "clear A", then sets timer B.
- **add/removeEventListener** ≈ C# `event += handler` / `-= handler`; the returned cleanup ≈ `Dispose()`. Without it every mount adds another listener (StrictMode mounts twice in dev).
- **A named `handleExpired`**: `addEventListener(…, setSession(null))` would *call* it at once; and `removeEventListener` needs **the same function reference**, so two inline arrows wouldn't match and nothing would be removed.
- **React has no app-wide event system.** Inside React, events are props/callbacks (`onClick`, `onLogin`). `window` events are the browser's (DOM) system, a common bridge when non-React code must tell React something. Alternatives: a module-level callback, or a library's global error handler (React Query, Zustand).
- **A hook** is a function named `use…` that reaches React's per-component memory (state, effects, context). React finds them by **call order during a component's render**, so they only work at the top of a component or another hook. `apiFetch` is a plain function: no React memory, callable anywhere (in `.then`, in handlers), so it can't call `useAuth()`; it dispatches an event instead.
- **Can a token be revoked?** Not by default: a JWT is self-contained, and the API checks only its signature and expiry, with no lookup. "Revoking" means adding a check: a denylist of token ids (`jti`), a per-user "valid after" timestamp (ASP.NET Identity's security stamp), rotating the signing key (revokes **all** tokens), or short access tokens + **refresh tokens** that live in a database and can be deleted. Each one gives up some statelessness.

#### Questions and answers (step 11)

1. **Why inline `clearSession(); setSession(null)` instead of calling `logout`?** `logout` is declared in the component, so it's a **new function every render**. Used in the effect, ESLint would require `[session, logout]`, and the effect would rerun every render and restart the timer. `setSession` is stable and `clearSession` is an import, so `[session]` is enough.
2. **What does `return () => clearTimeout(timer)` protect against?** You log out and log in again: without it, the **old** timer still fires at the old expiry and logs out the **new** session early.
3. **Why can't apiFetch call `useAuth().logout()`?** Hooks only work in components or hooks, during render. apiFetch runs in `.then` chains and handlers, where React has no "current component". It dispatches a window event; AuthProvider (a component) listens and sets state.
4. **Why `&& session`, and why does LoginPage avoid apiFetch?** A 401 without a token isn't an expiry, just an endpoint that needs login; there's nobody to log out. A wrong password is also a 401: through apiFetch it would count as "session expired" instead of showing "Wrong username or password".
5. **Why isn't the timer enough?** It only knows the expiry time. A token can turn invalid earlier: the signing key changed (old signatures fail), localStorage was edited, or the server added a revocation check. Only the API knows, and says so with a 401.

### Tailwind (Part 3, step 13)

**What it is:** utility classes on the element (`className="px-4 py-2 bg-blue-600 rounded"`), one CSS rule each, instead of naming classes in a CSS file. The Vite plugin **reads the source files as plain text** at build time and generates CSS only for the class names it finds.

```bash
npm install -D tailwindcss@4.3.4 @tailwindcss/vite@4.3.4
git mv src/index.css src/legacy.css          # my hand-written CSS, kept for reference, not imported
```
```ts
// vite.config.ts
import tailwindcss from '@tailwindcss/vite'
plugins: [react(), tailwindcss()],
```
```css
/* src/index.css (main.tsx already imports it) */
@import 'tailwindcss';
```
VS Code: **Tailwind CSS IntelliSense** (autocomplete; hover shows the CSS). Every page looks plain at first: **Preflight** (Tailwind's reset) removes the browser defaults, so `h2` looks like text until styled.

| Class | CSS |
|---|---|
| `px-4` / `py-2` | horizontal / vertical padding, 1rem / 0.5rem (scale: 1 = 0.25rem) |
| `mx-auto` · `max-w-4xl` | centring · `max-width: 56rem` |
| `flex flex-col gap-4` · `grid grid-cols-[8rem_1fr]` | flex column with a gap · an **arbitrary value** in `[]`, `_` = space |
| `bg-gray-900` · `text-gray-400` | colours from a scale, 50 (light) to 950 (dark) |
| `hover:` `focus:` `disabled:` · `disabled:hover:…` | **variants**: only in that state · variants stack |
| `divide-y divide-gray-800` | a line **between** the children |
| `tabular-nums` · `overflow-x-auto` | equal-width digits (no jiggle while polling) · scroll a wide table sideways |

```tsx
// NavLink's className can be a function that gets { isActive }
function navClass({ isActive }: { isActive: boolean }) {
    return isActive ? 'font-semibold text-white' : 'text-gray-400 hover:text-white'
}
<NavLink to="/search" className={navClass}>Search</NavLink>

// A conditional class: both names appear in full
className={`flex justify-between px-4 py-2 ${r.resigned ? 'text-gray-500 line-through' : ''}`}

// A lookup for values: every full class name is in the file
const statusClass: Record<ImportStatus, string> = {
    Queued: 'text-yellow-400', Running: 'text-blue-400', Succeeded: 'text-green-400',
    Failed: 'text-red-400', Abandoned: 'text-gray-500',
}
<td className={`${cell} ${statusClass[r.status]}`}>
```

**Reusing class combinations:**

| Way | When |
|---|---|
| A React component (`<Button>`) | The recommended one: markup and behaviour (`type`, `disabled`) reused too |
| A shared constant (`TW.btn.primary` in `tw.ts`) | My pick for now: TS autocomplete, tsc catches `TW.btn.primry` |
| `@apply` in CSS (`.btn { @apply … }`) | Sparingly: back to naming CSS classes, typos fail silently |
| `@theme` variables (`--color-brand`) | Design tokens (colours, fonts), not class combinations |

#### Questions and answers

1. **Where does the CSS for `px-4` come from, and why is the file small?** The plugin scans the source as text and generates CSS only for the classes it finds, not for all of Tailwind.
2. **Why `` `${row} border-t` `` works but `` `bg-${color}-600` `` doesn't?** Tailwind never runs the JS. `row`'s classes are written out in full in the file; `bg-${color}-600` never appears as a whole name, so no CSS exists for it. Fix: a lookup with full names, `{ blue: 'bg-blue-600' }[color]`.
3. **Why does `statusClass[r.status]` work?** All five full names are in the file, so all are generated; at runtime the index just picks one.
4. **Why `Record<ImportStatus, string>`, not `Record<string, string>`?** It requires a key for every status: tsc errors on a missing or misspelled one. A new API status added to the type forces a colour; with `Record<string, string>` a missing key is silently `undefined` (no colour).
5. **`npm audit` showed 12 vulnerabilities after the install?** Not from Tailwind: minimatch (via typescript-eslint) and picomatch (via Vite), dev tools only, ReDoS in file-pattern matching on my machine. `npm audit --omit=dev` (what ships) showed 0. `npm audit fix` is safe; **never `--force`** (jumps major versions).

**Shared classes: `src/tw.ts` (step 13d).**
```ts
export const TW = {
    btn: {
        primary: 'rounded bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-500 disabled:opacity-50',
        secondary: 'rounded border border-gray-700 bg-gray-900 px-3 py-1 hover:bg-black disabled:opacity-40 disabled:hover:bg-gray-900',
    },
    input: 'rounded border border-gray-700 bg-gray-950 px-3 py-2 focus:border-blue-500 focus:outline-none',
    link: 'text-blue-400 hover:opacity-80',
    error: 'text-red-400',
    muted: 'text-gray-400',
}
// <button className={TW.btn.primary}>   <input className={`flex-1 ${TW.input}`} />
```
- **`TW` holds what an element *is*; the page adds where it *sits*** (`mt-6`, `flex-1`, `ml-2`). Spacing depends on the place, so it stays local.
- **Never add a conflicting class** to a `TW` value (`${TW.btn.secondary} bg-black`): which wins depends on the order in Tailwind's generated CSS, not in the string. Add a new variant in `tw.ts` instead.
- **Opacity:** `hover:opacity-80` fades the whole element by 20%; `text-blue-400/80` (a `/` after a colour) sets only that colour's alpha.
- VS Code user setting so IntelliSense works inside the strings: `"tailwindCSS.experimental.classRegex": ["TW\\s*=\\s*\\{([\\s\\S]*?)\\n\\}"]`.

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
