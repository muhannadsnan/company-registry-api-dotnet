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
            <Link to="/companies">← Back to search</Link>

            {error && <p role="alert">{error}</p>}
            {!company && !error && <p>Loading..</p>}

            {company && (
                <>
                    <h1>{company.name}</h1>
                    <h3>Orgnr: {company.organisationNumber}</h3>
                    <div>
                        <strong>Form: </strong>
                        <span>{company.organisationFormCode}</span> <br/>
                        <strong>Adresse: </strong>
                        <span>
                            {company.address.street}, {company.address.postalCode}, {company.address.postalPlace}
                        </span> <br/>
                        <strong>Kommune: </strong>
                        <span>{company.address.municipality}</span> <br/>
                        <strong>Registered: </strong>
                        <span>{company.registrationDate ?? 'Ukjent'}</span> <br/>
                    </div>
                </>
            )}

        </main>
    )
}