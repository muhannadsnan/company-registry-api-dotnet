import { useState } from 'react'
import LoginPage from './auth/LoginPage'
import { clearSession, loadSession } from './auth/session'
import SearchPage from './pages/SearchPage'

function App() {
    const [session, setSession] = useState(loadSession) // runs once, on the first render

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

            <SearchPage />
        </>
    )
}

export default App
