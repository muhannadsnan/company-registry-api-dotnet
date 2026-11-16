import { useState } from 'react'
import LoginPage from './auth/LoginPage'
import { clearSession, loadSession, type Session } from './auth/session'
import SearchPage from './pages/SearchPage'
import { Route, Routes } from 'react-router'
import NotFoundPage from './pages/NotFoundPage'
import CompanyPage from './pages/CompanyPage'
import Layout from './Layout'
import HomePage from './pages/HomePage'

function App() {
    const [session, setSession] = useState<Session | null>(loadSession) // runs once, on the first render

    function handleLogout() {
        clearSession()
        setSession(null)
    }

    return (
        <Routes>
            <Route element={<Layout session={session} onLogout={handleLogout} />}>
                <Route path="/" element={<HomePage session={session} />} />
                <Route path="/search" element={session ? <SearchPage /> : <LoginPage onLogin={setSession} />} />
                <Route path="/companies/:orgnr" element={<CompanyPage loggedIn={session !== null} />} />
                <Route path="*" element={<NotFoundPage />} />
            </Route>
        </Routes>
    )
}

export default App
