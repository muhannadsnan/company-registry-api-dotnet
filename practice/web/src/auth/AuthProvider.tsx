import { useEffect, useState, type ReactNode } from 'react'
import { login as apiLogin } from '../api'
import { clearSession, loadSession, saveSession, type Session } from './session'
import { AuthContext } from './AuthContext'

function AuthProvider({ children }: { children: ReactNode }) {
    const [session, setSession] = useState<Session | null>(loadSession)

    async function login(username: string, password: string) {
        const newSession = await apiLogin(username, password)
        saveSession(newSession)
        setSession(newSession)
    }

    function logout() {
        clearSession()
        setSession(null)
    }

    // Log out by itself when the token expires.
    useEffect(() => {
        if (!session) {
            return
        }
        const msLeft = new Date(session.expiresAt).getTime() - Date.now()
        const timer = setTimeout(logout, msLeft)
        return () => clearTimeout(timer)
    }, [session])

    return <AuthContext value={{ session, login, logout }}>{children}</AuthContext>
}

export default AuthProvider
