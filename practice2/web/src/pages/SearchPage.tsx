import { useEffect, useState, type SubmitEvent } from 'react'
import { ApiError, searchCompanies, type CompanyDto, type PageResult } from '../api'
import Paginator from '../components/Paginator'

export default function SearchPage() {
    const [name, setName] = useState('') // what's in the input
    const [query, setQuery] = useState('') // what was searched
    const [result, setResult] = useState<PageResult<CompanyDto> | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [loading, setLoading] = useState(true) // true: the first search starts right away

    const [page, setPage] = useState(1)

    useEffect(() => {
        let ignore = false // set when this search is outdated
        searchCompanies(query, page)
            .then((data) => {
                if (!ignore) {
                    setResult(data)
                    setError(null)
                }
            })
            .catch((e) => {
                if (!ignore) {
                    setResult(null)
                    setError(e instanceof ApiError ? `Search failed (${e.status})` : 'Could not reach the server')
                }
            })
            .finally(() => {
                if (!ignore) setLoading(false)
            })
        return () => {
            ignore = true // cleanup: runs before the next search, or when the page goes away
        }
    }, [query, page])

    function handleSubmit(e: SubmitEvent<HTMLFormElement>) {
        e.preventDefault()
        const trimmedName = name.trim()
        if (trimmedName === query && page === 1) return // same query: the effect won't rerun, so loading would never end
        setLoading(true)
        setQuery(trimmedName)
        setPage(1) //    a new search starts at page 1
    }

    function handlePageChange(newPage: number) {
        setLoading(true)
        setPage(newPage)
    }

    return (
        <>
            <form onSubmit={handleSubmit}>
                <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Company name" />
                <button type="submit">Search</button>
            </form>

            {loading && <p className="loading">Loading...</p>}
            {!loading && error && <p role="alert">{error}</p>}
            {!loading && result && (
                <>
                    <p>{result.totalCount} companies</p>
                    <table>
                        <thead>
                            <tr>
                                <th>Orgnr</th>
                                <th>Name</th>
                                <th>Form</th>
                                <th>Registered</th>
                            </tr>
                        </thead>
                        <tbody>
                            {result.items.map((c) => (
                                <tr key={c.orgnr}>
                                    <td>{c.orgnr}</td>
                                    <td>{c.name}</td>
                                    <td>{c.orgForm}</td>
                                    <td>{c.registrationDate ?? '_'}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>

                    <Paginator
                        page={result.page}
                        pageSize={result.pageSize}
                        totalCount={result.totalCount}
                        onPageChange={handlePageChange}
                    />
                </>
            )}
        </>
    )
}
