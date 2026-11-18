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
            <Link to={backUrl} className="text-sm text-blue-400 hover:bg-gray-700 p-4 bg-gray-800 rounded">
                ← Back to search
            </Link>

            {loading && <p className="mt-4 text-gray-400">Loading...</p>}
            {!loading && error && (
                <p role="alert" className="mt-4 text-red-400">
                    {error}
                </p>
            )}
            {!loading && company && (
                <>
                    <h2 className="mt-4 mb-4 text-2xl text-center font-semibold">{company.name}</h2>
                    <dl className="grid max-w-md mx-auto grid-cols-[8rem_1fr] gap-x-4 gap-y-2 rounded-lg border border-gray-800 bg-gray-900 p-4">
                        <dt className="text-gray-400">Orgnr</dt>
                        <dd className="font-mono">{company.orgnr}</dd>
                        <dt className="text-gray-400">Form</dt>
                        <dd>{company.orgForm}</dd>
                        <dt className="text-gray-400">Registered</dt>
                        <dd>{company.registrationDate ?? '–'}</dd>
                    </dl>

                    {session ? (
                        <RoleList orgnr={company.orgnr} />
                    ) : (
                        <Link
                            to="/login"
                            state={{ from: `/companies/${orgnr}` }}
                            className="mt-6 inline-block text-blue-400 hover:underline"
                        >
                            Log in to see the roles
                        </Link>
                    )}
                </>
            )}
        </>
    )
}
