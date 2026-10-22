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