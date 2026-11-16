import { NavLink, Outlet } from 'react-router'
import { type Session } from './auth/session'

type Props = {
    session: Session | null
    onLogout: () => void
}

export default function Layout({ session, onLogout }: Props) {
    return (
        <>
            <header>
                <h1>Practice 2</h1>
                {session ? (
                    <nav className="d-flex justify-content-around">
                        <div>
                            <NavLink to="/">Home</NavLink> | <NavLink to="/search">Search</NavLink>
                        </div>
                        <div>
                            <span> Welcome, {session.username}</span>
                            <span>({session.scopes.join(', ')})</span>
                            <button className="logout" onClick={onLogout}>
                                Log out
                            </button>
                        </div>
                    </nav>
                ) : (
                    <nav>
                        Anonymous user, <NavLink to="/search">Log in</NavLink>
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
