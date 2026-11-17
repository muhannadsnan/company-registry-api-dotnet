import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuth } from './AuthContext'

export default function RequireAuth() {
    const { session } = useAuth()
    const location = useLocation()
    if (session) return <Outlet />
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
}
