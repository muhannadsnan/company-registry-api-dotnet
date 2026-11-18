import { useEffect, useState } from 'react'
import { ApiError, listImports, startImport, type ImportRunDto, type ImportStatus } from '../api'

function messageFor(e: unknown) {
    if (!(e instanceof ApiError)) return 'Could not reach the server'
    if (e.status === 403) return 'You are not allowed to run imports'
    if (e.status === 409) return 'An import is already queued or running'
    return `Request failed (${e.status})`
}

export default function ImportsPage() {
    const [runs, setRuns] = useState<ImportRunDto[] | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [starting, setStarting] = useState(false)

    const activeId = runs?.find((run) => run.status === 'Queued' || run.status === 'Running')?.id
    // derived: a number, or undefined

    // load the list once
    useEffect(() => {
        listImports()
            .then(setRuns)
            .catch((e) => setError(messageFor(e)))
    }, [])

    // poll while a run is active; stop when it isn't
    useEffect(() => {
        if (activeId === undefined) return
        const timer = setInterval(() => {
            listImports()
                .then(setRuns) // .then((list) => setRuns(list))
                .catch((e) => setError(messageFor(e)))
        }, 2000)
        return () => clearInterval(timer)
    }, [activeId])

    function handleStart() {
        setStarting(true)
        setError(null)
        startImport()
            .then((run) => setRuns((prev) => [run, ...(prev ?? [])]))
            .catch((e) => setError(messageFor(e)))
            .finally(() => setStarting(false))
    }

    const statusClass: Record<ImportStatus, string> = {
        Queued: 'text-yellow-400',
        Running: 'text-blue-400',
        Succeeded: 'text-green-400',
        Failed: 'text-red-400',
        Abandoned: 'text-gray-500',
    }

    const cell = 'px-3 py-2'
    const num = `${cell} text-right tabular-nums`

    return (
        <>
            <div className="mb-4 flex items-center justify-between">
                <h2 className="text-2xl font-semibold">Imports</h2>
                <button
                    onClick={handleStart}
                    disabled={starting || activeId !== undefined}
                    className="rounded bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-500 disabled:opacity-50"
                >
                    {activeId !== undefined ? 'Import running…' : 'Start import'}
                </button>
            </div>

            {error && (
                <p role="alert" className="mb-4 text-red-400">
                    {error}
                </p>
            )}
            {runs === null && !error && <p className="text-gray-400">Loading...</p>}
            {runs && (
                <div className="overflow-x-auto rounded-lg border border-gray-800">
                    <table className="w-full text-sm">
                        <thead className="bg-gray-900 text-gray-400">
                            <tr>
                                <th className={`${cell} text-left`}>Id</th>
                                <th className={`${cell} text-left`}>Status</th>
                                <th className={`${cell} text-left`}>Queued</th>
                                <th className={num}>Read</th>
                                <th className={num}>Inserted</th>
                                <th className={num}>Updated</th>
                                <th className={num}>Unchanged</th>
                                <th className={num}>Rejected</th>
                                <th className={num}>Roles</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-800">
                            {runs.map((r) => (
                                <tr key={r.id}>
                                    <td className={cell}>{r.id}</td>
                                    <td className={`${cell} ${statusClass[r.status]}`}>
                                        {r.status}
                                        {r.error && <span className="block text-xs text-red-400">{r.error}</span>}
                                    </td>
                                    <td className={cell}>{new Date(r.queuedAt).toLocaleString()}</td>
                                    <td className={num}>{r.read}</td>
                                    <td className={num}>{r.inserted}</td>
                                    <td className={num}>{r.updated}</td>
                                    <td className={num}>{r.unchanged}</td>
                                    <td className={num}>{r.rejected}</td>
                                    <td className={num}>{r.rolesSaved}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}
        </>
    )
}
