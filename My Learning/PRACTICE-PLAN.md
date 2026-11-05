# Practice plan

The same API and frontend, built again from an empty folder, step by step. Each round adds a little.

| Round | Folder | How |
|---|---|---|
| practice | `practice/` | Done (Oct–Nov 2026). Guided by an AI tutor, I typed every line. |
| practice2 | `practice2/` | Guided again, with the new `roles` table, and the login split into small steps. |
| practice3 | `practice3/` | Fewer hints, persons derived from roles, timed drills. |

---

## practice2: API

| Step | What | New in this round |
|---|---|---|
| 1 | Empty Web API with controllers | |
| 2 | `Company` entity, `AppDbContext`, first migration | |
| 3 | `GET /api/companies/{orgnr}` with a DTO | |
| 4 | Search with filters and paging (LINQ) | |
| 5 | MOD11 validation with xUnit tests | |
| 6 | Typed Brreg client with resilience | |
| 7 | Importer: validate, then upsert page by page | |
| 8 | Queue, background worker and the `import_runs` log | Runs left Running/Queued are marked Abandoned on startup |
| 9 | **Roles** | ✅ new |
| 9a | `Role` entity and migration: belongs to a company; role type (e.g. `LEDE`, Styrets leder); either a person's name or another unit's orgnr; resigned or not | |
| 9b | Roles client and import: `GET /enhetsregisteret/api/enheter/{orgnr}/roller`, one call per company, replace that company's roles | |
| 9c | `GET /api/companies/{orgnr}/roles` | |
| 9d | `GET /api/roles` search: filters `name`, `roleType` (e.g. `DAGL`, `LEDE`, `MEDL`), `birthYear` (or a from/to range), `orgnr`, with paging; the same `IQueryable` pattern as the company search | |
| 10 | `users` table with hashed passwords, inserted by hand | |
| 11 | **Login**, split into small steps | ✅ split |
| 11a | `JwtOptions`: issuer and audience in appsettings, signing key in user secrets, validated on start | |
| 11b | `TokenService`: claims, expiry, signature | |
| 11c | `AuthController`: `POST /api/auth/login`, verify the hash, return the token | |
| 11d | JWT validation, scope policies on the endpoints, 401/403 | |
| 12 | Correlation id middleware and structured logs | |
| 13 | Integration tests | Also a login test and a roles test |

**A decision to make at step 9a: personal data.** A role can hold a person's full name and full birth
date (`fodselsdato`). That's public data in Brreg, but storing it makes it personal data in my database
(GDPR). Store only what the app needs: probably the name, and the birth *year* at most, or the full
date only if practice3 needs it to tell persons apart.

## practice2: React

| Step | What | New in this round |
|---|---|---|
| 1 | Create the app (Vite, React, TypeScript), Prettier, and a proxy to the API | Proxy without tokens from the start |
| 2 | Login page: form, `POST /api/auth/login`, save the session in `localStorage` | ✅ from the old step 11 |
| 3 | `apiFetch` sends the token, and a typed API client | ✅ from the old step 11 |
| 4 | Search form with loading and error states | |
| 5 | Paging, with a component and props | |
| 6 | Router and the company page (400/404) | |
| 7 | **Roles on the company page** | ✅ new |
| 8 | Search kept in the URL | |
| 9 | Layout route (header, footer, `<Outlet />`) | |
| 10 | Auth context, route guard, username and Log out in the header | ✅ from the old step 11 |
| 11 | Token expiry: automatic logout, and a 401 sends you to the login page | ✅ from the old step 11 |
| 12 | Import page with polling (link only for users with `companies.import`) | |

---

## practice3 (ideas)

- **Persons derived from roles:** a `persons` table, and roles that point to it.
  - The hard part: Brreg gives no national ID number, so the same person can only be matched by name +
    birth date, and that isn't always unique. A real data-modelling problem.
  - Search a person and see all their companies.
- Fewer hints: I write each step first, then compare with the guide.
- Timed drills, 45 minutes each: a CRUD endpoint with EF Core and a test; a React page that fetches,
  lists and filters.
- Mock interviews: a task without code, then a review like an interviewer would give.

## practice4 (ideas)

- **History tables:** when a role appeared or disappeared, and a company's earlier names. Brreg gives no
  start or end dates for roles (only `avregistrert`), so the importer records when it *saw* a change.
