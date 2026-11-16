import { useState } from 'react'
import LoginPage from './auth/LoginPage'
import { clearSession, loadSession } from './auth/session'
import SearchPage from './pages/SearchPage'
import { Route, Routes } from 'react-router'
import NotFoundPage from './pages/NotFoundPage'
import CompanyPage from './pages/CompanyPage'

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

            <Routes>
                <Route path="/" element={<SearchPage />} />
                <Route path="/companies/:orgnr" element={<CompanyPage />} />
                <Route path="*" element={<NotFoundPage />} />
            </Routes>
        </>
    )
}

export default App
