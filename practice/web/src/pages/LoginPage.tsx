import { useState, type SubmitEvent } from 'react'
import { Navigate } from 'react-router'
import { ApiError } from '../api'
import { useAuth } from '../auth/AuthContext'

function LoginPage() {
    const { session, login } = useAuth()
    const [username, setUsername] = useState('')
    const [password, setPassword] = useState('')
    const [busy, setBusy] = useState(false)
    const [error, setError] = useState<string | null>(null)

    if (session) {
        return <Navigate to="/" replace />
    }

    async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
        event.preventDefault()
        setError(null)
        setBusy(true)
        try {
            await login(username, password)
        } catch (e) {
            if (e instanceof ApiError && e.status === 401) {
                setError('Wrong username or password.')
            } else {
                setError('Something went wrong. Try again.')
            }
        } finally {
            setBusy(false)
        }
    }

    return (
        <main id="loginPage">
            <h1>Log in</h1>
            <form onSubmit={handleSubmit}>
                <input
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                    placeholder="Username"
                    autoComplete="username"
                />
                <input
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder="Password"
                    autoComplete="current-password"
                />
                <button type="submit" disabled={busy}>
                    {busy ? 'Logging in...' : 'Log in'}
                </button>
            </form>
            {error && <p className="error">{error}</p>}
        </main>
    )
}

export default LoginPage
