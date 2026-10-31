import { Route, Routes } from 'react-router'
import Layout from './Layout'
import CompanyPage from './pages/CompanyPage'
import SearchPage from './pages/SearchPage'
import ImportsPage from './pages/ImportsPage'

function App() {
    return (
        <Routes>
            <Route element={<Layout />}>
                <Route path="/" element={<SearchPage />} />
                <Route path="/companies/:orgnr" element={<CompanyPage />} />
                <Route path="/import" element={<ImportsPage />} />
                <Route path="*" element={<p>Page not found.</p>} />
            </Route>
        </Routes>
    )
}

export default App
