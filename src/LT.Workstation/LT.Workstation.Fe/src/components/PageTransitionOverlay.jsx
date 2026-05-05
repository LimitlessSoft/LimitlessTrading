import { useState, useEffect } from 'react'
import { useLocation } from 'react-router-dom'
import { Box, Portal } from '@chakra-ui/react'
import { keyframes } from '@emotion/react'

const fadeIn = keyframes`from { opacity: 0 } to { opacity: 1 }`
const fadeOut = keyframes`from { opacity: 1 } to { opacity: 0 }`

const FADE_MS = 200
const MIN_HOLD_MS = 80

export default function PageTransitionOverlay() {
  const location = useLocation()
  const [visible, setVisible] = useState(false)
  const [fading, setFading] = useState(false)

  useEffect(() => {
    setVisible(true)
    setFading(false)

    const start = Date.now()
    let raf1, raf2, timer

    const startFade = () => {
      const remaining = Math.max(0, MIN_HOLD_MS - (Date.now() - start))
      timer = setTimeout(() => {
        setFading(true)
        setTimeout(() => setVisible(false), FADE_MS)
      }, remaining)
    }

    // Double rAF ensures the browser has painted the new page at least once
    raf1 = requestAnimationFrame(() => {
      raf2 = requestAnimationFrame(startFade)
    })

    return () => {
      cancelAnimationFrame(raf1)
      cancelAnimationFrame(raf2)
      clearTimeout(timer)
    }
  }, [location.pathname])

  if (!visible) return null

  return (
    <Portal>
      <Box
        position="fixed"
        inset={0}
        zIndex={9000}
        pointerEvents="none"
        bg="blackAlpha.900"
        animation={`${fading ? fadeOut : fadeIn} ${FADE_MS}ms ease forwards`}
        display="flex"
        alignItems="center"
        justifyContent="center"
      >
        <Box
          w="80px"
          h="80px"
          border="6px solid"
          borderColor="whiteAlpha.200"
          borderTopColor="blue.300"
          borderRadius="full"
          animation="spin 0.8s linear infinite"
          boxShadow="0 0 24px var(--chakra-colors-blue-400)"
          sx={{ '@keyframes spin': { to: { transform: 'rotate(360deg)' } } }}
        />
      </Box>
    </Portal>
  )
}
