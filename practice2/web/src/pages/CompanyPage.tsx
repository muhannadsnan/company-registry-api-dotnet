import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { type CompanyDto, getCompany } from '../api'
import { errorMessage } from '../helpers'

export default function CompanyPage() {
    const { orgnr = '' } = useParams() // from the URL /companies/:orgnr
    const [company, setCompany] = useState<CompanyDto | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [loading, setLoading] = useState(true)

    useEffect(() => {
        let ignore = false
        getCompany(orgnr)
            .then((data) => {
                if (!ignore) setCompany(data)
            })
            .catch((e) => {
                if (!ignore) setError(errorMessage(e, orgnr))
            })
            .finally(() => {
                if (!ignore) setLoading(false)
            })
        return () => {
            ignore = true
        }
    }, [orgnr])

    return (
        <>
            <Link to="/" className="btn">
                ← Back to search
            </Link>
            {loading && <p className="loading">Loading...</p>}
            {!loading && error && <p role="alert">{error}</p>}
            {!loading && company && (
                <>
                    <h2>{company.name}</h2>
                    <div>
                        <strong>Orgnr:</strong>
                        <span>{company.orgnr}</span>
                    </div>
                    <div>
                        <strong>Form:</strong>
                        <span>{company.orgForm}</span>
                    </div>
                    <div>
                        <strong>Registered:</strong>
                        <span>{company.registrationDate ?? '_'}</span>
                    </div>
                </>
            )}
        </>
    )
}
