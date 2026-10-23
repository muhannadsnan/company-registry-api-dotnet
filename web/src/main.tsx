import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Route, Routes } from 'react-router'
import './index.css'
import SearchPage from './page/SearchPage.tsx'
import CompanyPage from './page/CompanyPage.tsx'
import HomePage from './page/HomePage.tsx'
import AboutPage from './page/AboutPage.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
        <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/companies" element={<SearchPage />} />
            <Route path="/companies/:orgnr" element={<CompanyPage />} />
            <Route path="/about" element={<AboutPage />} />
        </Routes>
    </BrowserRouter>
  </StrictMode>,
)
