# React + TypeScript: what I built and learned (22 Oct 2026)

A small frontend for my .NET API: a company search page and a company page, in `web/`.
Stack: Vite 8 · React 19 · TypeScript · react-router · ESLint.

---

## 1. Setup commands

```bash
# Node 24 (current LTS). Project A keeps Node 20 through its own systemd PATH.
nvm install 24
nvm alias default 24      # new terminals start on 24
nvm use 24                # switch an already-open terminal

# Create the project (from the repo root)
npm create vite@latest web -- --template react-ts
#   "--" passes the flags through npm to create-vite
#   Linter question: ESLint (the standard; Oxlint is newer and faster, smaller ecosystem)

cd web
npm install               # install dependencies (like composer install)
npm install react-router  # routing
npm run dev               # dev server on http://localhost:5173
```

**Day-to-day: two terminals**

```bash
# Terminal 1: the .NET API, rebuilds on C# changes
cd ~/Documents/dotnet-demo/src/CompanyRegistry.Api
dotnet watch

# Terminal 2: the React dev server
cd ~/Documents/dotnet-demo/web
npm run dev
```

**Checks**

```bash
npx tsc -b        # type-check (Vite does NOT type-check while developing)
npx eslint src    # lint
npm run build     # type-check + production build
```

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

---

## 6. Things learned around the edges

- **`dotnet run`** compiles once and runs that build; it doesn't follow changes. **`dotnet watch`** watches `.cs` files: Hot Reload for small edits, rebuild and restart for bigger ones. C# is compiled, unlike PHP (read on every request) or Vite (hot module replacement).
- **API logs** go to the console as JSON lines: the terminal running `dotnet watch`, or Render's **Logs** tab when live.
- **inotify** is the Linux kernel feature that tells programs when files change. The default limit was 128 watchers per user (VS Code alone used 33); raised to 512 in `/etc/sysctl.d/60-inotify.conf`. The alternative is polling: `DOTNET_USE_POLLING_FILE_WATCHER=1 dotnet watch`.
- **`--`** in `npm create vite@latest web -- --template react-ts` means "the rest goes to the tool, not to npm" (same as `git checkout -- file`).
- **Node versions:** `nvm alias default` only affects new terminals. Project A's nightly jobs use an exact Node 20 path, so never uninstall `v20.19.6`.

---

## 7. Next

1. ~~Build the frontend into the same Docker image, served by ASP.NET Core; add frontend lint + build to CI.~~ Done 23 Oct (see CICD-NOTES.md).
2. Pagination: `page` state + `useEffect(…, [name, page])`, Previous/Next buttons.
3. Clean-up: delete `App.tsx`/`App.css`, one UI language, the "Kommune"/"Poststed" column.
