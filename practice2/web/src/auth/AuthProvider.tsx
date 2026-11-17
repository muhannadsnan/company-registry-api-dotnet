import { useState, type ReactNode } from 'react'
import { clearSession, loadSession, saveSession, type Session } from './session'
import { AuthContext } from './AuthContext'

export default function AuthProvider({ children }: { children: ReactNode }) {
    const [session, setSession] = useState(loadSession)

    function login(newSession: Session) {
        saveSession(newSession)
        setSession(newSession)
    }

    function logout() {
        clearSession()
        setSession(null)
    }

    return <AuthContext value={{ session, login, logout }}>{children}</AuthContext>
}
