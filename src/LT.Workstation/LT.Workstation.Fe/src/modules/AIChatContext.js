import { createContext, useContext } from 'react'

export const AIChatContext = createContext({ open: false, setOpen: () => {} })
export const useAIChat = () => useContext(AIChatContext)
