import { useEffect, useState } from 'react'
import { getRoles, type RoleDto } from '../api'
import { errorMessage } from '../helpers'

export default function RoleList({ orgnr }: { orgnr: string }) {
    const [roles, setRoles] = useState<RoleDto[] | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [loading, setLoading] = useState(true)

    useEffect(() => {
        getRoles(orgnr)
            .then((data) => setRoles(data))
            .catch((e) => setError(errorMessage(e, orgnr)))
            .finally(() => setLoading(false))
    }, [orgnr])

    if (loading) return <p className="loading">Loading roles...</p>
    if (error) return <p role="alert">{error}</p>
    if (!roles || roles.length === 0) return <p>No roles registered.</p>

    return (
        <>
            <h3>Roles</h3>

            <ul className="text-start">
                {roles.map((r, i) => (
                    <li key={`${i}-${r.roleType}-${r.personName ?? r.unitOrgnr}-${r.birthYear}`}>
                        <strong>{r.roleName}: </strong>
                        <span className="m-auto">
                            {r.personName ?? r.unitName}
                            {r.resigned && ' (resigned)'} {r.birthYear ? <small>({r.birthYear})</small> : ''}
                        </span>
                    </li>
                ))}
            </ul>
        </>
    )
}
