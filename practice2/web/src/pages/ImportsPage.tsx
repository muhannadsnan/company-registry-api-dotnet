import { useEffect, useState } from 'react'
import { ApiError, listImports, startImport, type ImportRunDto } from '../api'

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

    return (
        <>
            <div id="importPage">
                <h2>IMPORTS</h2>
                <div className="start">
                    <button onClick={handleStart} disabled={starting || activeId !== undefined} className="btn">
                        {activeId !== undefined ? 'Import running...' : 'Start import'}
                    </button>
                </div>
                {error && <p role="alert">{error}</p>}
                {runs === null && !error && <p className="loading">Loading...</p>}
                {runs && (
                    <table>
                        <thead>
                            <tr>
                                <th>Id</th>
                                <th>Status</th>
                                <th>Queued</th>
                                <th>Read</th>
                                <th>Inserted</th>
                                <th>Updated</th>
                                <th>Unchanged</th>
                                <th>Rejected</th>
                                <th>Roles</th>
                            </tr>
                        </thead>
                        <tbody>
                            {runs.map((r) => (
                                <tr key={r.id} className={activeId === r.id ? 'running' : ''}>
                                    <td>{r.id}</td>
                                    <td>
                                        {r.status}
                                        {r.error && ` (${r.error})`}
                                    </td>
                                    <td>{new Date(r.queuedAt).toLocaleString()}</td>
                                    <td>{r.read}</td>
                                    <td>{r.inserted}</td>
                                    <td>{r.updated}</td>
                                    <td>{r.unchanged}</td>
                                    <td>{r.rejected}</td>
                                    <td>{r.rolesSaved}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
            </div>
        </>
    )
}
