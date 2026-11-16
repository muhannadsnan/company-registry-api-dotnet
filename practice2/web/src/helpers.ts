import { ApiError } from './api'

export function errorMessage(e: unknown, orgnr: string): string {
    if (!(e instanceof ApiError)) return 'Could not reach the server'
    if (e.status === 400) return `"${orgnr}" is not a valid organisation number`
    if (e.status === 404) return `No company with orgnr ${orgnr}`
    return `Loading Failed (${e.status})`
}
