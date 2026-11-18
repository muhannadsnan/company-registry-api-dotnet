import { NavLink, Outlet } from 'react-router'
import { useAuth } from './auth/AuthContext'

export default function Layout() {
    const { session, logout } = useAuth()
    const canImport = session?.scopes.includes('companies.import') ?? false

    return (
        <>
            <header>
                <h1>Practice 2</h1>
                {session ? (
                    <nav className="d-flex justify-content-around">
                        <div>
                            <NavLink to="/">Home</NavLink>&nbsp; | &nbsp;<NavLink to="/search">Search</NavLink>
                            {canImport && (
                                <>
                                    &nbsp; | &nbsp;<NavLink to="/imports">Imports</NavLink>
                                </>
                            )}
                        </div>
                        <div>
                            <span> Welcome, {session.username}</span>
                            <span>({session.scopes.join(', ')})</span>
                            <button className="logout" onClick={logout}>
                                Log out
                            </button>
                        </div>
                    </nav>
                ) : (
                    <nav>
                        Anonymous user, <NavLink to="/login">Log in</NavLink>
                    </nav>
                )}
            </header>

            <main>
                <Outlet /> {/* the matching child route renders here */}
            </main>

            <footer>
                <p>Public data from Brønnøysundregistrene</p>
            </footer>
        </>
    )
}
