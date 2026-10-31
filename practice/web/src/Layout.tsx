import { NavLink, Outlet } from 'react-router'

function Layout() {
    return (
        <>
            <header>
                <strong>Company Registry</strong>
                <nav>
                    <NavLink to="/">Search</NavLink> | <NavLink to="/import">Imports</NavLink>
                </nav>
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