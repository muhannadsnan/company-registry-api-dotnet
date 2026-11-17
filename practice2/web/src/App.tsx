import LoginPage from './auth/LoginPage'
import SearchPage from './pages/SearchPage'
import { Route, Routes } from 'react-router'
import NotFoundPage from './pages/NotFoundPage'
import CompanyPage from './pages/CompanyPage'
import Layout from './Layout'
import HomePage from './pages/HomePage'
import RequireAuth from './auth/RequireAuth'

function App() {
    return (
        <Routes>
            <Route element={<Layout />}>
                <Route path="/login" element={<LoginPage />} />
                <Route path="/" element={<HomePage />} />
                <Route path="/companies/:orgnr" element={<CompanyPage />} />
                <Route element={<RequireAuth />}>
                    <Route path="/search" element={<SearchPage />} />
                </Route>
                <Route path="*" element={<NotFoundPage />} />
            </Route>
        </Routes>
    )
}

export default App
