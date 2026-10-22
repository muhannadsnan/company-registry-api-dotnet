import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Route, Routes } from 'react-router'
import './index.css'
import SearchPage from './page/SearchPage.tsx'
import CompanyPage from './page/CompanyPage.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
        <Routes>
            <Route path="/" element={<SearchPage />} />
            <Route path="/companies/:orgnr" element={<CompanyPage />} />
        </Routes>
    </BrowserRouter>
  </StrictMode>,
)
