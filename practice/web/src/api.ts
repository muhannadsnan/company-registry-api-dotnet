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
    const response = await fetch(`/api/companies?${params}`)
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}`)
    }
    return response.json()
}

export async function getCompany(orgnr: string): Promise<CompanyDto> {
    const response = await fetch(`/api/companies/${encodeURIComponent(orgnr)}`)
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
    const response = await fetch('/api/imports', { method: 'POST' })
    if (!response.ok) {
        throw new ApiError(response.status)
    }
    return response.json()
}

export async function getImport(id: number): Promise<ImportRun> {
    const response = await fetch(`/api/imports/${id}`)
    if (!response.ok) {
        throw new ApiError(response.status)
    }
    return response.json()
}

export async function listImports(): Promise<ImportRun[]> {
    const response = await fetch('/api/imports')
    if (!response.ok) {
        throw new ApiError(response.status)
    }
    return response.json()
}
