import { loadSession } from './auth/session'

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
async function apiFetch<T>(url: string): Promise<T> {
    const session = loadSession()
    const headers = new Headers()
    if (session) headers.set('Authorization', `Bearer ${session.token}`)
    const response = await fetch(url, { headers })
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
