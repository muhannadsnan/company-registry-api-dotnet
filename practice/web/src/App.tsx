import { Route, Routes } from 'react-router'
import Layout from './Layout'
import RequireAuth from './auth/RequireAuth'

import CompanyPage from './pages/CompanyPage'
import SearchPage from './pages/SearchPage'
import ImportsPage from './pages/ImportsPage'
import LoginPage from './pages/LoginPage'

function App() {
    return (
        <Routes>
            <Route element={<Layout />}>
                <Route path="/login" element={<LoginPage />} />
                <Route element={<RequireAuth />}>
                    <Route path="/" element={<SearchPage />} />
                    <Route path="/companies/:orgnr" element={<CompanyPage />} />
                    <Route path="/import" element={<ImportsPage />} />
                </Route>
                <Route path="*" element={<p>Page not found.</p>} />
            </Route>
        </Routes>
    )
}

export default App
