import { useEffect, useState } from 'react'
import { ApiError, getImport, listImports, startImport, type ImportRun } from '../api'

function ImportsPage() {
    const [run, setRun] = useState<ImportRun | null>(null)
    const [history, setHistory] = useState<ImportRun[]>([])
    const [error, setError] = useState<string | null>(null)

    const isActive = run !== null && (run.status === 'Queued' || run.status === 'Running')
    const runId = run?.id
    const runStatus = run?.status

    // Ask the API for the run's status every second, while it is queued or running.
    useEffect(() => {
        if (!isActive || runId === undefined) {
            return
        }
        const timer = setInterval(async () => {
            try {
                setRun(await getImport(runId))
            } catch {
                // A failed poll is not fatal: the next tick tries again.
            }
        }, 1000)
        return () => clearInterval(timer)
    }, [isActive, runId])

    // Reload the history at the start, and whenever the run's status changes.
    useEffect(() => {
        async function load() {
            try {
                setHistory(await listImports())
            } catch (e) {
                setError(e instanceof Error ? e.message : String(e))
            }
        }
        load()
    }, [runStatus])

    async function handleStart() {
        setError(null)
        try {
            if (confirm('Start a new import?')) 
                setRun(await startImport())
        } catch (e) {
            if (e instanceof ApiError && e.status === 409) {
                setError('An import is already queued or running.')
            } else {
                setError(e instanceof Error ? e.message : String(e))
            }
        }
    }

    return (
        <main id="importPage">
            <h1>Imports</h1>

            <div className="start">
                <button onClick={handleStart} disabled={isActive}>
                    {isActive ? 'Importing...' : 'Start import'}
                </button>
            </div>

            {error && <p className="error">{error}</p>}

            {run && (
                <p>
                    Run {run.id}: <strong>{run.status}</strong>. Read {run.read}, inserted {run.inserted}, updated{' '}
                    {run.updated}, unchanged {run.unchanged}, rejected {run.rejected}
                    {run.error && <> (error: {run.error})</>}
                </p>
            )}

            <h2>History</h2>
            <table>
                <thead>
                    <tr>
                        <th>Run</th>
                        <th>Status</th>
                        <th>Inserted</th>
                        <th>Updated</th>
                        <th>Unchanged</th>
                        <th>Rejected</th>
                        <th>Finished</th>
                    </tr>
                </thead>
                <tbody>
                    {history.map((r) => (
                        <tr key={r.id}>
                            <td>{r.id}</td>
                            <td>{r.status}</td>
                            <td>{r.inserted}</td>
                            <td>{r.updated}</td>
                            <td>{r.unchanged}</td>
                            <td>{r.rejected}</td>
                            <td>{r.finishedAt ? new Date(r.finishedAt).toLocaleString('nb-NO') : ''}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </main>
    )
}

export default ImportsPage
