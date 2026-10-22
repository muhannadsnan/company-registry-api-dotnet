import {Link, useParams} from 'react-router'
import { useEffect, useState } from 'react'
import { getCompany, type Company } from '../api'

export default function CompanyPage() {
    const {orgnr} = useParams()
    const [company, setCompany] = useState<Company | null>(null)
    const [error, setError] = useState<string | null>(null)

    useEffect( () => {
        if(!orgnr) return
        getCompany(orgnr)
            .then(setCompany)
            .catch( (e) => setError(e instanceof Error ? e.message : 'Something went wrong'))
    }, [orgnr]) 

    return (
        <main>
            <Link to="/">← Back to search</Link>

            {error && <p role="alert">{error}</p>}
            {!company && !error && <p>Loading..</p>}

            {company && (
                <>
                    <h1>{company.name}</h1>
                    <dl>
                        <dt>Orgnr</dt>
                        <dd>{company.organisationNumber}</dd>
                        <dt>Form</dt>
                        <dd>{company.organisationFormCode}</dd>
                        <dt>Adresse</dt>
                        <dd>
                            {company.address.street}, {company.address.postalCode}, {company.address.postalPlace}
                        </dd>
                        <dt>Kommune</dt>
                        <dd>{company.address.municipality}</dd>
                        <dt>Registered</dt>
                        <dd>{company.registrationDate ?? 'Ukjent'}</dd>
                    </dl>
                </>
            )}

        </main>
    )
}