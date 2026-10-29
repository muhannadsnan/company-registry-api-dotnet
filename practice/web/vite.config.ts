import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  return {
    plugins: [react()],
    server: {
        proxy: {
            '/api/imports': {
                target: 'http://localhost:5010',
                headers: { Authorization: `Bearer: ${env.IMPORTER_TOKEN}`}
            },
            '/api': {
                target: 'http://localhost:5010',
                headers: { Authorization: `Bearer ${env.READER_TOKEN}`}
            }
        }
    }
  }
})
