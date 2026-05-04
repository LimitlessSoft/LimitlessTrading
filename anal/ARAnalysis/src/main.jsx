import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { ChakraProvider } from '@chakra-ui/react'
import App from './App'
import TemplatePage from './pages/TemplatePage'
import PlaceholderDocsPage from './pages/PlaceholderDocsPage'
import AlertOverlay from './components/AlertOverlay'
import AlertExpressionWatcher from './components/AlertExpressionWatcher'
import AutoConnect from './modules/AutoConnect'
import theme from './theme'

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <ChakraProvider theme={theme}>
      <BrowserRouter>
        <AutoConnect />
        <AlertOverlay />
        <AlertExpressionWatcher />
        <Routes>
          <Route path="/" element={<App />} />
          <Route path="/docs/placeholders" element={<PlaceholderDocsPage />} />
          <Route path="*" element={<TemplatePage />} />
        </Routes>
      </BrowserRouter>
    </ChakraProvider>
  </React.StrictMode>
)
