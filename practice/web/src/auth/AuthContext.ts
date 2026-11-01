import { createContext, useContext } from 'react'
import type { Session } from './session'

export type AuthContextValue = {
    session: Session | null
    login: (username: string, password: string) => Promise<void>
    logout: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth() {
    const value = useContext(AuthContext)
    if (!value) {
        throw new Error('useAuth must be used inside <AuthProvider>')
    }
    return value
}
