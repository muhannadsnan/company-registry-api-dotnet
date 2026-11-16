import { useEffect, useState, type SubmitEvent } from 'react'
import { ApiError, searchCompanies, type CompanyDto, type PageResult } from '../api'
import Paginator from '../components/Paginator'
import { Link, useSearchParams } from 'react-router'

export default function SearchPage() {
    const [searchParams, setSearchParams] = useSearchParams()
    const query = searchParams.get('name') ?? ''
    const page = Number(searchParams.get('page')) || 1 // was useState; missing, "abc" or 0 → 1

    const [name, setName] = useState(query) // what's in the input
    const [result, setResult] = useState<PageResult<CompanyDto> | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [loading, setLoading] = useState(true) // true: the first search starts right away

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
        setSearchParams(trimmedName ? { name: trimmedName } : {}) // no page param = page 1
    }

    function handlePageChange(newPage: number) {
        setLoading(true)
        const params = new URLSearchParams(searchParams) // a copy, so name stays
        params.set('page', String(newPage))
        setSearchParams(params)
    }

    return (
        <>
            <div id="searchPage">
                <form onSubmit={handleSubmit}>
                    <input
                        type="text"
                        value={name}
                        onChange={(e) => setName(e.target.value)}
                        placeholder="Company name"
                    />
                    <button type="submit">Search</button>
                </form>
                {loading && <p className="loading">Loading...</p>}
                {!loading && error && <p role="alert">{error}</p>}
                {!loading && result && (
                    <>
                        <p className="totalCount">{result.totalCount} companies</p>
                        <div className="results">
                            <div className="item bg-dark">
                                <strong>Orgnr</strong>
                                <strong className="name text-center">Name</strong>
                                <strong>Form</strong>
                                <strong>Registered</strong>
                            </div>
                            {result.items.map((c) => (
                                <Link
                                    to={`/companies/${c.orgnr}`}
                                    state={{ backUrl: `/search?${searchParams}` }}
                                    key={c.orgnr}
                                >
                                    <div className="item">
                                        <span>{c.orgnr}</span>
                                        <span className="name">{c.name}</span>
                                        <span>{c.orgForm}</span>
                                        <span>{c.registrationDate ?? '_'}</span>
                                    </div>
                                </Link>
                            ))}
                        </div>

                        <Paginator
                            page={result.page}
                            pageSize={result.pageSize}
                            totalCount={result.totalCount}
                            onPageChange={handlePageChange}
                        />
                    </>
                )}
            </div>
        </>
    )
}
