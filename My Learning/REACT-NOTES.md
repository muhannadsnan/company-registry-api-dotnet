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
| 3 | `apiFetch` sends the token, and a typed API client | ⏳ |
| 4 | Search form with loading and error states | ⏳ |
| 5 | Paging, with a component and props | ⏳ |
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

### src/api.ts: the typed API client

```ts
export type Address = {
    street: string | null
    postalCode: string | null
    postalPlace: string | null
    municipalityNumber: string | null
    municipality: string | null
}

export type Company = {
    organisationNumber: string
    name: string
    organisationFormCode: string
    address: Address
    registrationDate: string | null
    lastChangedAt: string
}

export type PagedResult<T> = {
    items: T[]
    page: number
    pageSize: number
    totalCount: number
}

let token: string | null = null

async function getToken(): Promise<string> {
    if(token) return token

    const response = await fetch('/connect/token', {
        method: 'POST',
        body: new URLSearchParams({
            grant_type: 'client_credentials',
            client_id: 'demo-reader',
            client_secret: 'reader-secret'
        })
    })

    if(!response.ok) throw new Error(`Token request failed: ${response.status}`)

    const data: {access_token: string} = await response.json()
    token = data.access_token
    return token
}

async function get<T>(path: string): Promise<T> {
    const response = await fetch(path, {
        headers: { Authorization: `Bearer ${await getToken()}`}
    })
    if(!response.ok) throw new Error(`${response.status} ${response.statusText}`)
    return response.json()
}

export function searchCompanies(name: string, page = 1) {
    const query = new URLSearchParams({ name, page: String(page), pageSize: '20'})
    return get<PagedResult<Company>>(`/api/companies?${query}`)
}

export function getCompany(orgnr: string) {
    return get<Company>(`/api/companies/${orgnr}`)
}
```

- **The types mirror the C# DTOs** (`CompanyDto`, `AddressDto`, `PagedResult<T>`): both sides of the API contract. `string | null` = C#'s `string?`.
- **Generics:** `PagedResult<T>` has a fixed shape, but the item type is a placeholder filled in when used: `PagedResult<Company>` gives `items: Company[]`. A parameter, but for a type. `get<T>` works the same way: the caller says what the JSON will be.
- **`URLSearchParams` as the body** sends form-encoded data, the same as `curl -d`.
- **The token is cached** in `let token`, so it's fetched once. It expires after 1 hour; handling that is a later improvement.
- **Only `searchCompanies` and `getCompany` are exported.** The pages never deal with tokens.
- **Demo shortcut:** a real frontend never holds a client secret. Real users log in through **ID-porten** (BankID, MinID, … are methods *inside* ID-porten; OIDC authorization code + PKCE) and get their own token. Often a **BFF** (backend-for-frontend) keeps the token on the server and gives the browser only a secure cookie.

### src/page/SearchPage.tsx: state, form, list

```tsx
import {Link} from 'react-router'
import {useState, type FormEvent} from 'react'
import {searchCompanies, type Company} from '../api'

export default function SearchPage() {
    const [name, setName] = useState('')
    const [companies, setCompanies] = useState<Company[]>([])
    const [totalCount, setTotalCount] = useState<number | null>(null)
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    async function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault()
        setLoading(true)
        setError(null)
        try {
            const result = await searchCompanies(name)
            setCompanies(result.items)
            setTotalCount(result.totalCount)
        } catch(e) {
            setError(e instanceof Error ? e.message : 'Something went wrong')
        } finally {
            setLoading(false)
        }
    }

    return (
        <main>
            <h1>Company search</h1>

            <form onSubmit={handleSubmit}>
                <input
                    value={name}
                    onChange={ (e) => setName(e.target.value) }
                    placeholder='Company name, e.g. bil'
                />
                <button type="submit" disabled={loading}>
                    {loading ? 'Searching...' : 'Search'}
                </button>
            </form>

            {error && <p role="alert">{error}</p>}
            {totalCount !== null && <p>{totalCount} companies found</p>}

            <table>
                <thead>
                    <tr>
                        <th>Orgnr</th>
                        <th>Navn</th>
                        <th>Form</th>
                        <th>Kommune</th>
                    </tr>
                </thead>
                <tbody>
                    {companies.map((c) => (
                        <tr key={c.organisationNumber}>
                            <td>{c.organisationNumber}</td>
                            <td>
                                <Link to={`/companies/${c.organisationNumber}`}>{c.name}</Link>
                            </td>
                            <td>{c.organisationFormCode}</td>
                            <td>{c.address.postalPlace}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </main>
    )
}
```

- **`useState`** = Vue's `ref()`. `[value, setValue]`; never assign directly, because calling the setter is what makes React redraw.
- **`useState<Company[]>([])`**: TypeScript can't tell what an empty `[]` will hold, so the type is given.
- **`value` + `onChange`** = `v-model`, written out (a "controlled input").
- **`event.preventDefault()`** = `@submit.prevent`; stops the page reload.
- **`try / catch / finally`** gives three UI states: loading, error, result.
- **`{cond && <p/>}`** = `v-if`. **`array.map(...)`** = `v-for`. **`key`** = `:key`.
- **`type Company` in an import** is a type-only import; it disappears from the compiled JavaScript.
- To fix: the "Kommune" column shows `postalPlace` (poststed). Rename it to "Poststed", or show `municipality`.

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

### Props and a callback: `Paginator.tsx`

```tsx
type PaginatorProps = {
    page: number
    totalPages: number
    onPageChange: (page: number) => void
}

function Paginator({ page, totalPages, onPageChange }: PaginatorProps) {
    return (
        <nav className="paginator">
            <button disabled={page <= 1} onClick={() => onPageChange(page - 1)}>Previous</button>
            <span>Page {page} of {totalPages}</span>
            <button disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>Next</button>
        </nav>
    )
}
```

- **Props** are the component's typed parameters (Vue's `defineProps`).
- **`onPageChange`** is Vue's `emit`: the child doesn't change the page, it *asks* the parent to.
  Data flows down; changes flow up through callbacks.

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
