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

  const fetchStatus = async (attempt = 0) => {
    try {
      const r = await fetch('/api/license/status')
      const data = await r.json()
      setState({ isPro: data.valid, status: 'ready', customerEmail: data.customerEmail ?? null })
    } catch {
      if (attempt < 5) {
        setTimeout(() => fetchStatus(attempt + 1), 1500 * (attempt + 1))
      } else {
        setState({ isPro: false, status: 'error', customerEmail: null })
      }
    }
  }

  useEffect(() => { fetchStatus() }, [])

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
    <LicenseContext.Provider value={{ ...state, activate, deactivate, refetch: fetchStatus }}>
      {children}
    </LicenseContext.Provider>
  )
}

export const useLicense = () => useContext(LicenseContext)
