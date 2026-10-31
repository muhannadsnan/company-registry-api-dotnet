import { useEffect, useState } from 'react'
import { Link, useParams, useLocation } from 'react-router'
import { ApiError, getCompany, type CompanyDto } from '../api'

function CompanyPage() {
    const { orgnr = '' } = useParams()
    const location = useLocation()
    const search = location.state?.search ?? ''
    const [company, setCompany] = useState<CompanyDto | null>(null)
    const [error, setError] = useState<string | null>(null)

    useEffect(() => {
        async function load() {
            setCompany(null)
            setError(null)
            try {
                setCompany(await getCompany(orgnr))
            } catch (e) {
                if (e instanceof ApiError && e.status === 400) {
                    setError('That is not a valid organisation number.')
                } else if (e instanceof ApiError && e.status === 404) {
                    setError('No company with that number.')
                } else {
                    setError('Something went wrong. Try again.')
                }
            }
        }
        load()
    }, [orgnr])

    return (
        <main>
            <p>
                <Link to={{ pathname: '/', search }}>← Back to search</Link>
            </p>

            {error && <p className="error">{error}</p>}
            {!company && !error && <p>Loading...</p>}

            {company && (
                <>
                    <h1>{company.name}</h1>
                    <div className="companyInfo">
                        <div>
                            <strong>Organisation number: </strong>
                            <span>{company.orgnr}</span>
                        </div>
                        <div>
                            <strong>Organisation form: </strong>
                            <span>{company.orgForm}</span>
                        </div>
                        <div>
                            <strong>Registered: </strong>
                            <span>{company.registrationDate ?? '-'}</span>
                        </div>
                    </div>
                </>
            )}
        </main>
    )
}

export default CompanyPage
