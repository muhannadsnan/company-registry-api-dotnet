import { clearSession, loadSession } from './auth/session'

// The C# records, as the JSON arrives (camelCase).
export type CompanyDto = {
    orgnr: string
    name: string
    orgForm: string
    registrationDate: string | null
}

export type PageResult<T> = {
    items: T[]
    page: number
    pageSize: number
    totalCount: number
}

// fetch doesn't throw on 4xx/5xx; this does, and carries the status.
export class ApiError extends Error {
    status: number
    constructor(status: number, message: string) {
        super(message)
        this.status = status
    }
}

export type RoleDto = {
    roleType: string
    roleName: string
    personName: string | null
    birthYear: number | null
    unitOrgnr: string | null
    unitName: string | null
    resigned: boolean
}

//====================================================

// Every API call goes through here: the one place that knows about the token.
async function apiFetch<T>(url: string, init?: RequestInit): Promise<T> {
    const session = loadSession()
    const headers = new Headers(init?.headers)
    if (session) headers.set('Authorization', `Bearer ${session.token}`)
    const response = await fetch(url, { ...init, headers })
    // Session expired or invalidated, not wrong username/password..
    // Therefore we don't use apiFetch in LoginPage
    if (response.status === 401 && session) {
        clearSession()
        window.dispatchEvent(new Event('auth:expired')) // which AuthProvider listens to
    }
    if (!response.ok) throw new ApiError(response.status, `${response.status} ${response.statusText}`)
    return response.json()
}

export function searchCompanies(name: string, page = 1, pageSize = 20) {
    const params = new URLSearchParams({ name, page: String(page), pageSize: String(pageSize) })
    return apiFetch<PageResult<CompanyDto>>(`/api/companies?${params}`)
}

export function getCompany(orgnr: string) {
    return apiFetch<CompanyDto>(`/api/companies/${encodeURIComponent(orgnr)}`)
}

export function getRoles(orgnr: string) {
    return apiFetch<RoleDto[]>(`/api/companies/${encodeURIComponent(orgnr)}/roles`)
}

//=========================== IMPORT

export type ImportStatus = 'Queued' | 'Running' | 'Succeeded' | 'Failed' | 'Abandoned'

export type ImportRunDto = {
    id: number
    correlationId: string | null
    status: ImportStatus
    queuedAt: string
    startedAt: string | null
    finishedAt: string | null
    read: number
    inserted: number
    updated: number
    unchanged: number
    rejected: number
    rolesSaved: number
    error: string | null
}

export function listImports() {
    return apiFetch<ImportRunDto[]>('/api/imports')
}

export function startImport() {
    return apiFetch<ImportRunDto>('/api/imports', { method: 'POST' })
}
