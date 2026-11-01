import { Navigate, Outlet } from 'react-router'
import { useAuth } from './AuthContext'

function RequireAuth() {
    const { session } = useAuth()
    return session ? <Outlet /> : <Navigate to="/login" replace />
}

export default RequireAuth
