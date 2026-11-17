import { useAuth } from '../auth/AuthContext'

export default function HomePage() {
    const { session } = useAuth()
    return (
        <>
            <h1>HOME PAGE</h1>

            {session !== null ? <h2>Welcome, {session.username}!</h2> : <h2>To have access to SEARCH, click login</h2>}
        </>
    )
}
