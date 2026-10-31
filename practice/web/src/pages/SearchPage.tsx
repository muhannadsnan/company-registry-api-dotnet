import { useEffect, useState, type SubmitEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { searchCompany, type CompanyDto, type PageResult } from '../api'
import Paginator from '../Paginator'

function SearchPage() {
    const [searchParams, setSearchParams] = useSearchParams()
    const query = searchParams.get('name') ?? ''
    const [name, setName] = useState(query)
    const [result, setResult] = useState<PageResult<CompanyDto> | null>(null)
    const page = Number(searchParams.get('page')) || 1
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    useEffect(() => {
        async function load() {
            setLoading(true)
            setError(null)
            try {
                setResult(await searchCompany(query, page))
            } catch (e) {
                setError(e instanceof Error ? e.message : String(e))
            } finally {
                setTimeout(() => {
                    console.log('abc')
                    setLoading(false)
                }, 400)
            }
        }
        load()
        console.log('here is useEffect!')
    }, [query, page])

    function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
        event.preventDefault()
        const trimmedName = name.trim()
        setSearchParams(trimmedName ? { name: trimmedName } : {})
    }

    function handlePageChange(newPage: number) {
        const params = new URLSearchParams(searchParams)
        params.set('page', String(newPage))
        setSearchParams(params)
    }

    return (
        <main id="searchPage">
            <h1>Company Search</h1>

            <form onSubmit={handleSubmit}>
                <input value={name} onChange={(e) => setName(e.target.value)} type="text" placeholder="Company name" />
                <button type="submit" className={loading ? 'bg-loading' : ''}>
                    {loading ? 'Searching...' : 'Search'}
                </button>
            </form>

            {loading && <p>Loading...</p>}
            {error && <p className="error">Error: {error}</p>}

            {result && !loading && (
                <>
                    <p className="totalCount">{result.totalCount} companies</p>
                    <div className="results">
                        {result.items.map((company) => (
                            <div key={company.orgnr}>
                                <Link to={`/companies/${company.orgnr}`} state={{ search: searchParams.toString() }} className="item">
                                    <span>{company.name}</span>
                                    <span>
                                        {company.orgnr} ({company.orgForm})
                                    </span>
                                </Link>
                            </div>
                        ))}
                    </div>
                    <Paginator
                        page={result.page}
                        totalPages={Math.max(1, Math.ceil(result.totalCount / result.pageSize))}
                        onPageChange={handlePageChange}
                    />
                </>
            )}
        </main>
    )
}

export default SearchPage
