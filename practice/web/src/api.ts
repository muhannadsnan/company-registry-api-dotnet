import { clearSession, loadSession, type Session } from './auth/session'


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

export async function searchCompany(name: string, page = 1): Promise<PageResult<CompanyDto>> {
    const params = new URLSearchParams({ name, page: String(page) })
    const response = await apiFetch(`/api/companies?${params}`)
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}`)
    }
    return response.json()
}

export async function getCompany(orgnr: string): Promise<CompanyDto> {
    const response = await apiFetch(`/api/companies/${encodeURIComponent(orgnr)}`)
    if (!response.ok) throw new ApiError(response.status)
    return response.json()
}

//===========================================

export class ApiError extends Error {
    status: number

    constructor(status: number) {
        super(`HTTP ${status}`)
        this.status = status
    }
}

//===========================================

export type ImportStatus = 'Queued' | 'Running' | 'Succeeded' | 'Failed'

export type ImportRun = {
    id: number
    status: ImportStatus
    queuedAt: string
    startedAt: string | null
    finishedAt: string | null
    read: number
    inserted: number
    updated: number
    unchanged: number
    rejected: number
    error: string | null
}

export async function startImport(): Promise<ImportRun> {
    const response = await apiFetch('/api/imports', { method: 'POST' })
    if (!response.ok) {
        throw new ApiError(response.status)
    }
    return response.json()
}

export async function getImport(id: number): Promise<ImportRun> {
    const response = await apiFetch(`/api/imports/${id}`)
    if (!response.ok) {
        throw new ApiError(response.status)
    }
    return response.json()
}

export async function listImports(): Promise<ImportRun[]> {
    const response = await apiFetch('/api/imports')
    if (!response.ok) {
        throw new ApiError(response.status)
    }
    return response.json()
}




// fetch, plus the logged-in user's token. A 401 means the token is missing or expired: log out.
async function apiFetch(url: string, init?: RequestInit): Promise<Response> {
    const session = loadSession()
    const headers = new Headers(init?.headers)
    if (session) {
        headers.set('Authorization', `Bearer ${session.accessToken}`)
    }
    const response = await fetch(url, { ...init, headers })
    if (response.status === 401) {
        clearSession()
        window.location.assign('/login')
    }
    return response
}

export async function login(username: string, password: string): Promise<Session> {
    const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password }),
    })
    if (!response.ok) {
        throw new ApiError(response.status)
    }
    return response.json()
}
