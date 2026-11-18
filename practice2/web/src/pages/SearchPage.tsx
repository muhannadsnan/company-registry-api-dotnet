import { useEffect, useState, type SubmitEvent } from 'react'
import { ApiError, searchCompanies, type CompanyDto, type PageResult } from '../api'
import Paginator from '../components/Paginator'
import { Link, useSearchParams } from 'react-router'
import { TW } from '../tw'

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

    const row = 'grid grid-cols-[8rem_1fr_4rem_7rem] gap-4 px-4 py-2'

    return (
        <>
            <form onSubmit={handleSubmit} className="mb-4 flex gap-2">
                <input
                    type="text"
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    placeholder="Company name"
                    className={`flex-1 ${TW.input}`}
                />
                <button type="submit" className={TW.btn.primary}>
                    Search
                </button>
            </form>

            {loading && <p className={TW.muted}>Loading...</p>}
            {!loading && error && (
                <p role="alert" className={TW.error}>
                    {error}
                </p>
            )}
            {!loading && result && (
                <>
                    <p className="mb-2 text-sm text-gray-400">{result.totalCount} companies</p>
                    <div className="overflow-hidden rounded-lg border border-gray-800">
                        <div className={`${row} bg-gray-700 font-semibold text-gray-400`}>
                            <span>Orgnr</span>
                            <span>Name</span>
                            <span>Form</span>
                            <span>Registered</span>
                        </div>
                        {result.items.map((c) => (
                            <Link
                                to={`/companies/${c.orgnr}`}
                                state={{ backUrl: `/search?${searchParams}` }}
                                key={c.orgnr}
                                className={`${row} border-t border-gray-800 hover:bg-gray-800 text-gray-400`}
                            >
                                <span className="font-mono text-sm">{c.orgnr}</span>
                                <span>{c.name}</span>
                                <span>{c.orgForm}</span>
                                <span>{c.registrationDate ?? '–'}</span>
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
        </>
    )
}
