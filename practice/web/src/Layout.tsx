import { NavLink, Outlet } from 'react-router'
import { useAuth } from './auth/AuthContext'

function Layout() {
    const { session, logout } = useAuth()
    const canImport = session?.scopes.includes('companies.import') ?? false

    return (
        <>
            <header>
                <strong>Company Registry</strong>
                {session && (
                    <nav>
                        <NavLink to="/">Search</NavLink>
                        {canImport && (
                            <>
                                <span> | </span> <NavLink to="/import">Imports</NavLink>
                                <span> | </span>
                            </>
                        )}
                        <button onClick={logout}>Log out</button>
                        <span> {session.username} </span>
                    </nav>
                )}
            </header>

            <Outlet />

            <footer>
                <div>
                    <p>Data from Brønnøysundregistrene</p>
                </div>
            </footer>
        </>
    )
}

export default Layout