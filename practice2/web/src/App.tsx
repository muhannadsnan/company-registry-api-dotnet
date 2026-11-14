import { useEffect, useState } from 'react'
import LoginPage from './auth/LoginPage'
import { clearSession, loadSession } from './auth/session'
import { searchCompanies } from './api'

function App() {
    const [session, setSession] = useState(loadSession) // runs once, on the first render
    const [total, setTotal] = useState<number | null>(null)

    useEffect(() => {
        if (!session) return
        searchCompanies('').then((result) => setTotal(result.totalCount))
    }, [session]) // runs again when session changes (login, logout)

    if (!session) return <LoginPage onLogin={setSession} />

    return (
        <>
            <h1>Practice2</h1>
            <p>
                Logged in as {session.username} ({session.scopes.join(', ')})
            </p>
            <button
                className="logout"
                onClick={() => {
                    clearSession()
                    setSession(null)
                }}
            >
                Log out
            </button>

            <hr />

            <p>{total === null ? 'Loading...' : `${total} companies`}</p>
        </>
    )
}

export default App
