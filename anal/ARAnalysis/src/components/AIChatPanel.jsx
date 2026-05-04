import { useState, useRef, useEffect } from 'react'
import {
  Box, VStack, HStack, Text, Input, InputGroup, InputRightElement,
  IconButton, Select, FormControl, FormLabel, Flex, Spinner, Badge,
  useToast
} from '@chakra-ui/react'
import { CloseIcon, SettingsIcon, CheckCircleIcon, WarningIcon } from '@chakra-ui/icons'
import axios from 'axios'

const api = axios.create({ baseURL: '/api', timeout: 60000 })

const PROVIDERS = [
  { id: 'claude_subscription', label: 'Claude Subscription' },
  { id: 'anthropic', label: 'Anthropic API Key' }
]

const MODELS_BY_PROVIDER = {
  claude_subscription: [
    { id: 'claude-opus-4-7', label: 'Claude Opus 4' },
    { id: 'claude-sonnet-4-6', label: 'Claude Sonnet 4' },
    { id: 'claude-haiku-4-5-20251001', label: 'Claude Haiku 4.5' }
  ],
  anthropic: [
    { id: 'claude-opus-4-7', label: 'Claude Opus 4' },
    { id: 'claude-sonnet-4-6', label: 'Claude Sonnet 4' },
    { id: 'claude-haiku-4-5-20251001', label: 'Claude Haiku 4.5' }
  ]
}

function ChatIcon() {
  return (
    <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor">
      <path d="M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm0 14H5.17L4 17.17V4h16v12z" />
    </svg>
  )
}

function SendIcon() {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
      <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z" />
    </svg>
  )
}

function Message({ role, content }) {
  const isUser = role === 'user'
  return (
    <Flex justify={isUser ? 'flex-end' : 'flex-start'} w="100%">
      <Box
        maxW="85%"
        px={3}
        py={2}
        borderRadius="lg"
        bg={isUser ? 'blue.600' : 'gray.700'}
        fontSize="sm"
        lineHeight="1.5"
        whiteSpace="pre-wrap"
        wordBreak="break-word"
      >
        {content}
      </Box>
    </Flex>
  )
}

function SubscriptionStatus() {
  const [status, setStatus] = useState(null)

  useEffect(() => {
    api.get('/ai/subscription-status')
      .then(res => setStatus(res.data))
      .catch(() => setStatus({ valid: false, reason: 'Could not reach server' }))
  }, [])

  if (!status) return <Spinner size="xs" color="gray.400" />

  return (
    <HStack spacing={2}>
      {status.valid
        ? <CheckCircleIcon color="green.400" boxSize={3} />
        : <WarningIcon color="red.400" boxSize={3} />
      }
      <Text fontSize="xs" color={status.valid ? 'green.400' : 'red.400'}>
        {status.valid
          ? `Authenticated (${status.subscriptionType ?? 'subscription'})`
          : status.reason
        }
      </Text>
    </HStack>
  )
}

function SettingsPane({ onClose }) {
  const toast = useToast()
  const [provider, setProvider] = useState('claude_subscription')
  const [model, setModel] = useState('')
  const [apiKey, setApiKey] = useState('')
  const [showKey, setShowKey] = useState(false)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    async function load() {
      try {
        const [provRes, modelRes] = await Promise.all([
          api.get('/user-data/settings/ai.provider'),
          api.get('/user-data/settings/ai.model')
        ])
        const p = provRes.data.value || 'claude_subscription'
        const m = modelRes.data.value || MODELS_BY_PROVIDER[p]?.[0]?.id || ''
        setProvider(p)
        setModel(m)

        // Persist defaults if not yet saved
        const saves = []
        if (!provRes.data.value) saves.push(api.put('/user-data/settings/ai.provider', { value: p }))
        if (!modelRes.data.value) saves.push(api.put('/user-data/settings/ai.model', { value: m }))
        if (saves.length) await Promise.all(saves)

        if (p === 'anthropic') {
          const keyRes = await api.get('/user-data/settings/ai.anthropic.apiKey')
          if (keyRes.data.value) setApiKey(keyRes.data.value)
        }
      } catch {}
    }
    load()
  }, [])

  async function save() {
    setSaving(true)
    try {
      const saves = [
        api.put('/user-data/settings/ai.provider', { value: provider }),
        api.put('/user-data/settings/ai.model', { value: model })
      ]
      if (provider === 'anthropic') {
        saves.push(api.put('/user-data/settings/ai.anthropic.apiKey', { value: apiKey }))
      }
      await Promise.all(saves)
      toast({ title: 'AI settings saved', status: 'success', duration: 2000, isClosable: true })
      onClose()
    } catch {
      toast({ title: 'Failed to save settings', status: 'error', duration: 3000, isClosable: true })
    } finally {
      setSaving(false)
    }
  }

  function onProviderChange(p) {
    setProvider(p)
    setModel(MODELS_BY_PROVIDER[p]?.[0]?.id || '')
    setApiKey('')
  }

  const models = MODELS_BY_PROVIDER[provider] || []

  return (
    <VStack spacing={4} align="stretch" p={4} flex={1} overflowY="auto">
      <FormControl>
        <FormLabel fontSize="xs" color="gray.400" mb={1}>Provider</FormLabel>
        <Select
          size="sm"
          bg="gray.700"
          borderColor="gray.600"
          value={provider}
          onChange={e => onProviderChange(e.target.value)}
        >
          {PROVIDERS.map(p => (
            <option key={p.id} value={p.id}>{p.label}</option>
          ))}
        </Select>
      </FormControl>

      {provider === 'claude_subscription' && (
        <FormControl>
          <FormLabel fontSize="xs" color="gray.400" mb={1}>Status</FormLabel>
          <SubscriptionStatus />
        </FormControl>
      )}

      {provider === 'anthropic' && (
        <FormControl>
          <FormLabel fontSize="xs" color="gray.400" mb={1}>API Key</FormLabel>
          <InputGroup size="sm">
            <Input
              type={showKey ? 'text' : 'password'}
              bg="gray.700"
              borderColor="gray.600"
              value={apiKey}
              onChange={e => setApiKey(e.target.value)}
              placeholder="sk-ant-..."
              pr="4.5rem"
            />
            <InputRightElement width="4.5rem">
              <Box
                as="button"
                fontSize="xs"
                color="gray.400"
                onClick={() => setShowKey(v => !v)}
                _hover={{ color: 'gray.200' }}
              >
                {showKey ? 'Hide' : 'Show'}
              </Box>
            </InputRightElement>
          </InputGroup>
        </FormControl>
      )}

      <FormControl>
        <FormLabel fontSize="xs" color="gray.400" mb={1}>Model</FormLabel>
        <Select
          size="sm"
          bg="gray.700"
          borderColor="gray.600"
          value={model}
          onChange={e => setModel(e.target.value)}
        >
          {models.map(m => (
            <option key={m.id} value={m.id}>{m.label}</option>
          ))}
        </Select>
      </FormControl>

      <Box
        as="button"
        mt={2}
        py={2}
        borderRadius="md"
        bg="blue.600"
        color="white"
        fontSize="sm"
        fontWeight="medium"
        onClick={save}
        disabled={saving}
        opacity={saving ? 0.7 : 1}
        _hover={{ bg: 'blue.500' }}
        _active={{ bg: 'blue.700' }}
        transition="background 0.15s"
      >
        {saving ? 'Saving…' : 'Save'}
      </Box>
    </VStack>
  )
}

export default function AIChatPanel() {
  const [open, setOpen] = useState(false)
  const [view, setView] = useState('chat')
  const [messages, setMessages] = useState([])
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const messagesEndRef = useRef(null)
  const inputRef = useRef(null)
  const toast = useToast()

  useEffect(() => {
    if (open && view === 'chat') inputRef.current?.focus()
  }, [open, view])

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  async function send() {
    const text = input.trim()
    if (!text || loading) return

    const userMsg = { role: 'user', content: text }
    const history = messages.map(m => ({ role: m.role, content: m.content }))
    setMessages(prev => [...prev, userMsg])
    setInput('')
    setLoading(true)

    try {
      const res = await api.post('/ai/chat', { message: text, history })
      setMessages(prev => [...prev, { role: 'assistant', content: res.data.response }])
    } catch (err) {
      const msg = err.response?.data?.error || 'Request failed'
      toast({ title: msg, status: 'error', duration: 4000, isClosable: true })
      setMessages(prev => prev.slice(0, -1))
      setInput(text)
    } finally {
      setLoading(false)
    }
  }

  function onKeyDown(e) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      send()
    }
  }

  return (
    <>
      <Box position="fixed" bottom={6} right={6} zIndex={1000}>
        {!open && (
          <Box
            as="button"
            w={12}
            h={12}
            borderRadius="full"
            bg="blue.600"
            color="white"
            display="flex"
            alignItems="center"
            justifyContent="center"
            boxShadow="lg"
            onClick={() => setOpen(true)}
            _hover={{ bg: 'blue.500' }}
            _active={{ bg: 'blue.700' }}
            transition="background 0.15s"
          >
            <ChatIcon />
          </Box>
        )}

        {open && (
          <Box
            w="360px"
            h="480px"
            bg="gray.800"
            borderRadius="xl"
            boxShadow="2xl"
            border="1px solid"
            borderColor="gray.700"
            display="flex"
            flexDirection="column"
            overflow="hidden"
          >
            <HStack
              px={4}
              py={3}
              bg="gray.900"
              borderBottom="1px solid"
              borderColor="gray.700"
              justify="space-between"
            >
              <Text fontSize="sm" fontWeight="semibold" color="gray.200">
                {view === 'settings' ? 'AI Settings' : 'AI Assistant'}
              </Text>
              <HStack spacing={1}>
                <IconButton
                  icon={<SettingsIcon />}
                  size="xs"
                  variant="ghost"
                  color={view === 'settings' ? 'blue.400' : 'gray.400'}
                  onClick={() => setView(v => v === 'chat' ? 'settings' : 'chat')}
                  aria-label="Settings"
                  _hover={{ color: 'gray.200' }}
                />
                <IconButton
                  icon={<CloseIcon />}
                  size="xs"
                  variant="ghost"
                  color="gray.400"
                  onClick={() => { setOpen(false); setView('chat') }}
                  aria-label="Close"
                  _hover={{ color: 'gray.200' }}
                />
              </HStack>
            </HStack>

            {view === 'settings' ? (
              <SettingsPane onClose={() => setView('chat')} />
            ) : (
              <>
                <VStack
                  flex={1}
                  overflowY="auto"
                  spacing={2}
                  p={3}
                  align="stretch"
                  sx={{
                    '&::-webkit-scrollbar': { width: '4px' },
                    '&::-webkit-scrollbar-thumb': { bg: 'gray.600', borderRadius: '4px' }
                  }}
                >
                  {messages.length === 0 && (
                    <Text color="gray.500" fontSize="sm" textAlign="center" mt={8}>
                      Ask anything about your account or the market.
                    </Text>
                  )}
                  {messages.map((m, i) => (
                    <Message key={i} role={m.role} content={m.content} />
                  ))}
                  {loading && (
                    <Flex justify="flex-start">
                      <Box px={3} py={2} borderRadius="lg" bg="gray.700">
                        <Spinner size="xs" color="gray.400" />
                      </Box>
                    </Flex>
                  )}
                  <div ref={messagesEndRef} />
                </VStack>

                <HStack p={3} borderTop="1px solid" borderColor="gray.700" spacing={2}>
                  <Input
                    ref={inputRef}
                    size="sm"
                    bg="gray.700"
                    borderColor="gray.600"
                    placeholder="Ask a question…"
                    value={input}
                    onChange={e => setInput(e.target.value)}
                    onKeyDown={onKeyDown}
                    borderRadius="md"
                    _focus={{ borderColor: 'blue.500', boxShadow: 'none' }}
                  />
                  <IconButton
                    icon={<SendIcon />}
                    size="sm"
                    colorScheme="blue"
                    onClick={send}
                    isLoading={loading}
                    isDisabled={!input.trim()}
                    aria-label="Send"
                    borderRadius="md"
                  />
                </HStack>
              </>
            )}
          </Box>
        )}
      </Box>
    </>
  )
}
