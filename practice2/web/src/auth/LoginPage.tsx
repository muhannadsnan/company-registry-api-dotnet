import { useState, type SubmitEvent } from 'react'
import { type Session } from './session'
import { useAuth } from './AuthContext'
import { Navigate, useLocation } from 'react-router'
import { TW } from '../tw'

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
        <form
            onSubmit={handleSubmit}
            className="mx-auto mt-10 flex max-w-sm flex-col gap-4 rounded-lg border border-gray-700 bg-gray-800 p-6"
        >
            <h1 className="text-2xl font-semibold">Log in</h1>
            <label className="flex flex-col gap-1 text-sm">
                Username
                <input
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                    autoComplete="username"
                    className={TW.input}
                />
            </label>
            <label className="flex flex-col gap-1 text-sm">
                Password
                <input
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    autoComplete="current-password"
                    className={TW.input}
                />
            </label>
            <button type="submit" disabled={busy} className={TW.btn.primary}>
                {busy ? 'Logging in…' : 'Log in'}
            </button>
            {error && (
                <p role="alert" className={`text-sm ${TW.error}`}>
                    {error}
                </p>
            )}
        </form>
    )
}
