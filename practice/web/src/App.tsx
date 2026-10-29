import { useEffect, useState, type SubmitEvent } from 'react'
import { searchCompany, type CompanyDto, type PageResult } from './api'
import Paginator from './Paginator'

function App() {
	const [name, setName] = useState('')
	const [query, setQuery] = useState('')
	const [result, setResult] = useState<PageResult<CompanyDto> | null>(null)
	const [page, setPage] = useState(1)
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
				}, 400);
			}
		}
		load()
		console.log('here is useEffect!')
	}, [query, page])

	function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
		event.preventDefault()
		setQuery(name.trim())
		setPage(1)
	}

	return (
		<main>
			<h1>Companies</h1>

			<form onSubmit={handleSubmit}>
				<input value={name} onChange={e => setName(e.target.value)} type="text" placeholder="Company name"/>
				<button type="submit" className={loading ? "bg-loading" : ''}>{loading ? 'Searching...' : 'Search'}</button>
			</form>

			{loading && <p>Loading...</p>}
			{error && <p className="error">Error: {error}</p>}
			
			{result && (
				<>
					<p className="totalCount">{result.totalCount} companies</p>
					<ul>
						{result.items.map(company => (
							<li key={company.orgnr}>
								{company.name} ({company.orgForm}) {company.orgnr}
							</li>
						))}
					</ul>
					<Paginator 
						page={result.page}
						totalPages={Math.max(1, Math.ceil(result.totalCount / result.pageSize))}
						onPageChange={setPage}
					/>
				</>
			)}
			
		</main>
	)
}

export default App
