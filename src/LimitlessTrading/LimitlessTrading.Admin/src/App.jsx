import { Routes, Route, Navigate } from 'react-router-dom'
import { isLoggedIn } from './api'
import LoginPage from './pages/LoginPage'
import WhatsNewPage from './pages/WhatsNewPage'

function PrivateRoute({ children }) {
  return isLoggedIn() ? children : <Navigate to="/login" replace />
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/"
        element={
          <PrivateRoute>
            <WhatsNewPage />
          </PrivateRoute>
        }
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
