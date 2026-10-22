import {Link} from 'react-router'
import {useState, type FormEvent} from 'react'
import  {searchCompanies, type Company} from '../api'

export default function SearchPage() {
    const [name, setName] = useState('')
    const [companies, setCompanies] = useState<Company[]>([])
    const [totalCount, setTotalCount] = useState<number | null>(null)
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    async function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault()
        setLoading(true)
        setError(null)
        try {
            const result = await searchCompanies(name)
            setCompanies(result.items)
            setTotalCount(result.totalCount)
        } catch(e) {
            setError(e instanceof Error ? e.message : 'Something went wrong')
        } finally {
            setLoading(false)
        }
    }

    return (
        <main>
            <h1>Company search</h1>

            <form onSubmit={handleSubmit}>
                <input 
                    value={name}
                    onChange={ (e) => setName(e.target.value) }
                    placeholder='Company name, e.g. bil'
                />
                <button type="submit" disabled={loading}>
                    {loading ? 'Searching...' : 'Search'}
                </button>
            </form>

            {error && <p role="alert">{error}</p>}
            {totalCount !== null && <p>{totalCount} companies found</p>}

            <table>
                <thead>
                    <tr>
                        <th>Orgnr</th>
                        <th>Navn</th>
                        <th>Form</th>
                        <th>Kommune</th>
                    </tr>
                </thead>
                <tbody>
                    {companies.map((c) => (
                        <tr key={c.organisationNumber}>
                            <td>{c.organisationNumber}</td>
                            <td>
                                <Link to={`/companies/${c.organisationNumber}`}>{c.name}</Link>
                            </td>
                            <td>{c.organisationFormCode}</td>
                            <td>{c.address.postalPlace}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </main>
    )
}