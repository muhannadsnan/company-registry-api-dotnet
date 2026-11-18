import { NavLink, Outlet } from 'react-router'
import { useAuth } from './auth/AuthContext'
import { TW } from './tw'

export default function Layout() {
    const { session, logout } = useAuth()
    const canImport = session?.scopes.includes('companies.import') ?? false

    function navClass({ isActive }: { isActive: boolean }) {
        return isActive ? 'font-semibold text-white' : 'text-gray-400 hover:text-white'
    }
    return (
        <>
            <div className="flex min-h-screen flex-col bg-gray-900 text-gray-200">
                <header className="border-b border-gray-700 bg-gray-800">
                    <div className="mx-auto flex max-w-4xl items-center justify-between px-4 py-3">
                        <h1 className="text-xl font-semibold">
                            <NavLink to="/">Practice 2</NavLink>
                        </h1>
                        {session ? (
                            <nav className="flex flex-1 items-center justify-between gap-4 text-sm ml-3">
                                <div className="flex gap-4">
                                    <NavLink to="/" className={navClass}>
                                        Home
                                    </NavLink>
                                    <NavLink to="/search" className={navClass}>
                                        Search
                                    </NavLink>
                                    {canImport && (
                                        <>
                                            <NavLink to="/imports" className={navClass}>
                                                Imports
                                            </NavLink>
                                        </>
                                    )}
                                </div>
                                <div className="flex gap-4 items-center">
                                    <span className="text-gray-500"> Welcome, {session.username}</span>
                                    <span>({session.scopes.join(', ')})</span>
                                    <button onClick={logout} className={TW.btn.secondary}>
                                        Log out
                                    </button>
                                </div>
                            </nav>
                        ) : (
                            <nav className="text-sm">
                                Anonymous user
                                <NavLink to="/login" className={`ml-2 ${TW.btn.secondary}`}>
                                    Log in
                                </NavLink>
                            </nav>
                        )}
                    </div>
                </header>

                <main className="mx-auto w-full max-w-4xl flex-1 px-4 py-6">
                    <Outlet /> {/* the matching child route renders here */}
                </main>

                <footer className="border-t border-gray-700 py-4 text-center text-sm text-gray-500 bg-gray-800">
                    <p>Public data from Brønnøysundregistrene</p>
                </footer>
            </div>
        </>
    )
}
