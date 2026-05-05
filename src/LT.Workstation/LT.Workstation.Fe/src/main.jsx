import './modules/diagnostics'
import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { ChakraProvider } from '@chakra-ui/react'
import HomePage from './pages/HomePage'
import TemplatePage from './pages/TemplatePage'
import PlaceholderDocsPage from './pages/PlaceholderDocsPage'
import FeedbackPage from './pages/FeedbackPage'
import FeedbackDetailPage from './pages/FeedbackDetailPage'
import WidgetsGalleryPage from './pages/WidgetsGalleryPage'
import AlertOverlay from './components/AlertOverlay'
import PageTransitionOverlay from './components/PageTransitionOverlay'
import AlertExpressionWatcher from './components/AlertExpressionWatcher'
import AutoConnect from './modules/AutoConnect'
import AIChatPanel from './components/AIChatPanel'
import { LicenseProvider } from './modules/LicenseContext'
import { WidgetProvider } from './modules/WidgetContext'
import { AIChatContext } from './modules/AIChatContext'
import theme from './theme'

function AIChatProvider({ children }) {
  const [open, setOpen] = React.useState(false)
  return (
    <AIChatContext.Provider value={{ open, setOpen }}>
      {children}
    </AIChatContext.Provider>
  )
}

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <ChakraProvider theme={theme}>
      <LicenseProvider>
        <WidgetProvider>
          <BrowserRouter>
            <AIChatProvider>
              <AutoConnect />
              <AlertOverlay />
              <PageTransitionOverlay />
              <AlertExpressionWatcher />
              <AIChatPanel />
              <Routes>
                <Route path="/" element={<HomePage />} />
                <Route path="/docs/placeholders" element={<PlaceholderDocsPage />} />
                <Route path="/feedback" element={<FeedbackPage />} />
                <Route path="/feedback/:id" element={<FeedbackDetailPage />} />
                <Route path="/gallery" element={<WidgetsGalleryPage />} />
                <Route path="*" element={<TemplatePage />} />
              </Routes>
            </AIChatProvider>
          </BrowserRouter>
        </WidgetProvider>
      </LicenseProvider>
    </ChakraProvider>
  </React.StrictMode>
)
