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

    if (loading) return <p className="mt-6 text-gray-400">Loading roles...</p>
    if (error)
        return (
            <p role="alert" className="mt-6 text-red-400">
                {error}
            </p>
        )
    if (!roles || roles.length === 0) return <p className="mt-6 text-gray-400">No roles registered.</p>

    return (
        <>
            <h3 className="mt-6 mb-2 text-lg font-semibold">Roles</h3>
            <ul className="divide-y divide-gray-800 rounded-lg border border-gray-800">
                {roles.map((r, i) => (
                    <li
                        key={`${i}-${r.roleType}-${r.personName ?? r.unitOrgnr}-${r.birthYear}`}
                        className={`flex justify-between px-4 py-2 ${r.resigned ? 'text-gray-500 line-through' : ''}`}
                    >
                        <span>
                            <span className="text-gray-400">{r.roleName}:</span> {r.personName ?? r.unitName}
                        </span>
                        {r.birthYear && <span className="text-sm text-gray-500">{r.birthYear}</span>}
                    </li>
                ))}
            </ul>
        </>
    )
}
