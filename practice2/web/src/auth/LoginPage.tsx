import { useState, type SubmitEvent } from 'react'
import { type Session } from './session'
import { useAuth } from './AuthContext'
import { Navigate, useLocation } from 'react-router'

export default function LoginPage() {
    const { session, login } = useAuth()
    const from = useLocation().state?.from ?? '/search'

    const [username, setUsername] = useState('')
    const [password, setPassword] = useState('')
    const [error, setError] = useState<string | null>(null)
    const [busy, setBusy] = useState(false)

    if (session) return <Navigate to={from} replace /> // after all hooks

    async function handleSubmit(e: SubmitEvent<HTMLFormElement>) {
        e.preventDefault()
        setBusy(true)
        setError(null)

        try {
            const response = await fetch('/api/auth/login', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ username, password }),
            })
            if (response.status === 401) {
                setError('Wrong username or password')
                return
            }
            if (!response.ok) {
                setError(`Login failed (${response.status})`)
                return
            }
            const newSession: Session = await response.json()

            login(newSession)
        } catch {
            setError('Could not reach the server')
        } finally {
            setBusy(false)
        }
    }

    return (
        <form onSubmit={handleSubmit}>
            <h1>Log in</h1>
            <label>
                Username &nbsp;
                <input value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" />
            </label>
            <br />
            <label>
                Password &nbsp;
                <input
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    autoComplete="current-password"
                />
            </label>
            <button type="submit" disabled={busy}>
                {busy ? 'Logging in…' : 'Log in'}
            </button>
            {error && (
                <p role="alert" className="error">
                    {error}
                </p>
            )}
        </form>
    )
}
