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

export async function searchCompany(name: string, page = 1) : Promise<PageResult<CompanyDto>> {
    const params = new URLSearchParams({ name, page: String(page) })
    const response = await fetch(`api/companies?${params}`)
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}`)
    }
    return response.json()
}