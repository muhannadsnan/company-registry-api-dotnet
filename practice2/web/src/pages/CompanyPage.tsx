import { useEffect, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router'
import { type CompanyDto, getCompany } from '../api'
import { errorMessage } from '../helpers'
import RoleList from '../components/RoleList'

export default function CompanyPage({ loggedIn }: { loggedIn: boolean }) {
    const { orgnr = '' } = useParams() // from the URL /companies/:orgnr
    const [company, setCompany] = useState<CompanyDto | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [loading, setLoading] = useState(true)

    const backUrl = useLocation().state?.backUrl ?? '/'

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
            <Link to={backUrl} className="btn">
                ← Back to search
            </Link>

            {loading && <p className="loading">Loading...</p>}
            {!loading && error && <p role="alert">{error}</p>}
            {!loading && company && (
                <>
                    <h2>{company.name}</h2>
                    <div className="details">
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
                    </div>
                    {/* Roles */}
                    {loggedIn ? <RoleList orgnr={company.orgnr} /> : <Link to="/">Login to see the roles</Link>}
                </>
            )}
        </>
    )
}
