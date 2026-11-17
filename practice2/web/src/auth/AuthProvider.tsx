import { useEffect, useState, type ReactNode } from 'react'
import { clearSession, loadSession, saveSession, type Session } from './session'
import { AuthContext } from './AuthContext'

export default function AuthProvider({ children }: { children: ReactNode }) {
    const [session, setSession] = useState(loadSession)

    // 1. Log out by itself when the token expires.
    useEffect(() => {
        if (!session) return
        const msLeft = new Date(session.expiresAt).getTime() - Date.now()
        const timer = setTimeout(() => {
            clearSession()
            setSession(null)
        }, msLeft)
        return () => clearTimeout(timer) // a new login or logout cancels the old timer
    }, [session])

    // 2. Log out when apiFetch reports a 401.
    useEffect(() => {
        // sent in addEventListener to be called later and not set session value right away
        function handleExpired() {
            setSession(null)
        }
        window.addEventListener('auth:expired', handleExpired)
        return () => window.removeEventListener('auth:expired', handleExpired)
    }, [])

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
