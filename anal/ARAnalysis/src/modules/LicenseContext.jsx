import { createContext, useContext, useState, useEffect } from 'react'

const LicenseContext = createContext({
  isPro: false,
  status: 'loading',
  customerEmail: null,
  activate: async () => {},
  deactivate: async () => {}
})

export function LicenseProvider({ children }) {
  const [state, setState] = useState({ isPro: false, status: 'loading', customerEmail: null })

  useEffect(() => {
    fetch('/api/license/status')
      .then(r => r.json())
      .then(data => setState({ isPro: data.valid, status: 'ready', customerEmail: data.customerEmail ?? null }))
      .catch(() => setState({ isPro: false, status: 'error', customerEmail: null }))
  }, [])

  async function activate(key) {
    const response = await fetch('/api/license/activate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ key })
    })
    const data = await response.json()
    if (!response.ok) throw new Error(data.error || 'Activation failed')
    setState({ isPro: data.valid, status: 'ready', customerEmail: data.customerEmail ?? null })
    return data
  }

  async function deactivate() {
    await fetch('/api/license/deactivate', { method: 'POST' })
    setState({ isPro: false, status: 'ready', customerEmail: null })
  }

  return (
    <LicenseContext.Provider value={{ ...state, activate, deactivate }}>
      {children}
    </LicenseContext.Provider>
  )
}

export const useLicense = () => useContext(LicenseContext)
