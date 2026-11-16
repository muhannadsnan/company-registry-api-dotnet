import { useState } from 'react'
import LoginPage from './auth/LoginPage'
import { clearSession, loadSession } from './auth/session'
import SearchPage from './pages/SearchPage'
import { Link, Route, Routes } from 'react-router'
import NotFoundPage from './pages/NotFoundPage'
import CompanyPage from './pages/CompanyPage'

function App() {
    const [session, setSession] = useState(loadSession) // runs once, on the first render

    return (
        <>
            <h1>Practice2</h1>
            {session ? (
                <>
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
                </>
            ) : (
                <p>
                    Anonymous user. <Link to="/">Login here</Link>
                </p>
            )}

            <hr />

            <Routes>
                <Route path="/" element={session ? <SearchPage /> : <LoginPage onLogin={setSession} />} />
                <Route path="/companies/:orgnr" element={<CompanyPage loggedIn={session !== null} />} />
                <Route path="*" element={<NotFoundPage />} />
            </Routes>
        </>
    )
}

export default App
