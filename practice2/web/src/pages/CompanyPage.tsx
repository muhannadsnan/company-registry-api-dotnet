import { useEffect, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router'
import { type CompanyDto, getCompany } from '../api'
import { errorMessage } from '../helpers'
import RoleList from '../components/RoleList'
import { useAuth } from '../auth/AuthContext'

export default function CompanyPage() {
    const { session } = useAuth()
    const { orgnr = '' } = useParams() // from the URL /companies/:orgnr
    const [company, setCompany] = useState<CompanyDto | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [loading, setLoading] = useState(true)

    const backUrl = useLocation().state?.backUrl ?? '/search'

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
            <div className="d-flex">
                <Link to={backUrl} className="btn">
                    ← Back to search
                </Link>
            </div>

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
                    {session ? (
                        <RoleList orgnr={company.orgnr} />
                    ) : (
                        <Link to="/login" state={{ from: `/companies/${orgnr}` }}>
                            Login to see the roles
                        </Link>
                    )}
                </>
            )}
        </>
    )
}
