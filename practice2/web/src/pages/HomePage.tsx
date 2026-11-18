import { useAuth } from '../auth/AuthContext'

export default function HomePage() {
    const { session } = useAuth()
    return (
        <div className="py-10 text-center">
            <h2 className="text-3xl font-semibold">Company registry</h2>
            <p className="mt-2 text-gray-400">
                {session ? `Welcome, ${session.username}!` : 'Log in to search companies.'}
            </p>
        </div>
    )
}
