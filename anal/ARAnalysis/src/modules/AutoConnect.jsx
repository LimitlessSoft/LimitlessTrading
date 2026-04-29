import { useEffect, useRef } from 'react'
import { connectToIBKR, getConnectionStatus, getLastPort } from './dataService'

function AutoConnect() {
  const attempted = useRef(false)

  useEffect(() => {
    if (attempted.current) return
    attempted.current = true

    const run = async () => {
      try {
        const status = await getConnectionStatus()
        if (status.connected) return
        const port = (await getLastPort()) || 7496
        await connectToIBKR(port)
      } catch {
        // Silently ignore — pages show their own disconnected state
      }
    }

    run()
  }, [])

  return null
}

export default AutoConnect
