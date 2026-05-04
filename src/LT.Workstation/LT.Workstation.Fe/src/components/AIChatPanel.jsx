import { useState, useRef, useEffect } from 'react'
import {
  Box, VStack, HStack, Text, Input, InputGroup, InputRightElement,
  IconButton, Select, FormControl, FormLabel, Flex, Spinner, Button,
  useToast, useDisclosure, Divider
} from '@chakra-ui/react'
import { CheckCircleIcon, WarningIcon, SettingsIcon, AddIcon } from '@chakra-ui/icons'
import axios from 'axios'
import { createAlert, removeAlert, listAlerts, subscribeToAlertList } from '../modules/alertService'
import LicenseModal from './LicenseModal'
import { useLicense } from '../modules/LicenseContext'
import WhisperSetupModal from './WhisperSetupModal'
import { useWidgetCallback } from '../modules/WidgetContext'

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

function makeThread(n) {
  return { id: crypto.randomUUID(), name: `Chat ${n}`, messages: [], limitReached: false }
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

function CopyIcon() {
  return (
    <svg viewBox="0 0 24 24" width="14" height="14" fill="currentColor">
      <path d="M16 1H4C2.9 1 2 1.9 2 3v14h2V3h12V1zm3 4H8C6.9 5 6 5.9 6 7v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z" />
    </svg>
  )
}

function CheckIcon() {
  return (
    <svg viewBox="0 0 24 24" width="14" height="14" fill="currentColor">
      <path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z" />
    </svg>
  )
}

function MicIcon() {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
      <path d="M12 14c1.66 0 3-1.34 3-3V5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3zm-1-9c0-.55.45-1 1-1s1 .45 1 1v6c0 .55-.45 1-1 1s-1-.45-1-1V5zm6 6c0 2.76-2.24 5-5 5s-5-2.24-5-5H5c0 3.53 2.61 6.43 6 6.92V21h2v-3.08c3.39-.49 6-3.39 6-6.92h-2z"/>
    </svg>
  )
}

function StopIcon() {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
      <path d="M6 6h12v12H6z"/>
    </svg>
  )
}

function writeString(view, offset, str) {
  for (let i = 0; i < str.length; i++) view.setUint8(offset + i, str.charCodeAt(i))
}

function encodeWav(samples, sampleRate) {
  const buffer = new ArrayBuffer(44 + samples.length * 2)
  const view = new DataView(buffer)
  writeString(view, 0, 'RIFF')
  view.setUint32(4, 36 + samples.length * 2, true)
  writeString(view, 8, 'WAVE')
  writeString(view, 12, 'fmt ')
  view.setUint32(16, 16, true)
  view.setUint16(20, 1, true)
  view.setUint16(22, 1, true)
  view.setUint32(24, sampleRate, true)
  view.setUint32(28, sampleRate * 2, true)
  view.setUint16(32, 2, true)
  view.setUint16(34, 16, true)
  writeString(view, 36, 'data')
  view.setUint32(40, samples.length * 2, true)
  for (let i = 0; i < samples.length; i++) {
    const s = Math.max(-1, Math.min(1, samples[i]))
    view.setInt16(44 + i * 2, s < 0 ? s * 0x8000 : s * 0x7FFF, true)
  }
  return new Blob([buffer], { type: 'audio/wav' })
}

const LOADING_STEPS = {
  alert: [
    { text: 'Analyzing your request…', ms: 700 },
    { text: 'Fetching live market data…', ms: 1800 },
    { text: 'Setting up alert…', ms: 2500 },
    { text: 'Finalizing…', ms: Infinity },
  ],
  delete_alert: [
    { text: 'Analyzing your request…', ms: 700 },
    { text: 'Removing alert…', ms: Infinity },
  ],
  quote: [
    { text: 'Analyzing your request…', ms: 700 },
    { text: 'Fetching live market data…', ms: 2000 },
    { text: 'Processing…', ms: Infinity },
  ],
  account: [
    { text: 'Analyzing your request…', ms: 700 },
    { text: 'Fetching account data…', ms: 2000 },
    { text: 'Processing…', ms: Infinity },
  ],
  default: [
    { text: 'Analyzing your request…', ms: 900 },
    { text: 'Processing…', ms: Infinity },
  ],
}

function detectStepType(message) {
  if (/delete|remove|cancel|clear.*alert/i.test(message)) return 'delete_alert'
  if (/alert|notify|notif|when.*(above|below|goes|reaches|hits|over|under)/i.test(message)) return 'alert'
  if (/account|balance|portfolio|equity|pnl|p&l|margin|liquidat|buying.?power|cash/i.test(message)) return 'account'
  if (/quote|price|last|bid|ask|change|volume|chart|\b[A-Z]{2,5}\b/.test(message)) return 'quote'
  return 'default'
}

function LoadingStatus({ message }) {
  const steps = LOADING_STEPS[detectStepType(message)]
  const [idx, setIdx] = useState(0)

  useEffect(() => {
    setIdx(0)
    const timers = []
    let elapsed = 0
    for (let i = 0; i < steps.length - 1; i++) {
      elapsed += steps[i].ms
      const next = i + 1
      timers.push(setTimeout(() => setIdx(next), elapsed))
    }
    return () => timers.forEach(clearTimeout)
  }, [message])

  return (
    <Flex justify="flex-start" w="100%">
      <Box px={3} py={2} borderRadius="lg" bg="gray.700" fontSize="sm">
        <HStack spacing={2}>
          <Spinner size="xs" color="blue.400" />
          <Text color="gray.300">{steps[idx].text}</Text>
        </HStack>
      </Box>
    </Flex>
  )
}

function Message({ role, content, onUpgrade }) {
  const isUser = role === 'user'
  if (role === 'limit') {
    return (
      <Flex justify="flex-start" w="100%">
        <Box px={3} py={2} borderRadius="lg" bg="orange.900" borderWidth={1} borderColor="orange.600" fontSize="sm">
          <Text color="orange.200" mb={2}>{content}</Text>
          <Button size="xs" colorScheme="orange" onClick={onUpgrade}>Upgrade to Pro</Button>
        </Box>
      </Flex>
    )
  }
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

function SettingsPane({ onClose, onOpenVoiceSetup }) {
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

      <Divider borderColor="gray.700" />

      <FormControl>
        <FormLabel fontSize="xs" color="gray.400" mb={1}>Voice Input</FormLabel>
        <Box
          as="button"
          w="100%"
          py={2}
          borderRadius="md"
          bg="gray.700"
          color="gray.200"
          fontSize="sm"
          borderWidth={1}
          borderColor="gray.600"
          onClick={onOpenVoiceSetup}
          _hover={{ bg: 'gray.600', borderColor: 'gray.500' }}
          transition="all 0.15s"
        >
          Configure Whisper / Voice Input
        </Box>
      </FormControl>
    </VStack>
  )
}

export default function AIChatPanel() {
  const [open, setOpen] = useState(false)
  const [view, setView] = useState('chat')
  const [threads, setThreads] = useState(() => {
    try {
      const saved = localStorage.getItem('ai-chat-threads')
      if (saved) return JSON.parse(saved)
    } catch {}
    return [makeThread(1)]
  })
  const [activeThreadId, setActiveThreadId] = useState(() => {
    try { return localStorage.getItem('ai-chat-active') || null } catch { return null }
  })
  const [nextChatNum, setNextChatNum] = useState(() => {
    try { return parseInt(localStorage.getItem('ai-chat-counter')) || 2 } catch { return 2 }
  })
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const [pendingMessage, setPendingMessage] = useState('')
  const [copied, setCopied] = useState(false)
  const [recording, setRecording] = useState(false)
  const [transcribing, setTranscribing] = useState(false)
  const [alerts, setAlerts] = useState(() => listAlerts())
  const messagesEndRef = useRef(null)
  const inputRef = useRef(null)
  const mediaRecorderRef = useRef(null)
  const toast = useToast()
  const { isOpen: isLicenseOpen, onOpen: onLicenseOpen, onClose: onLicenseClose } = useDisclosure()
  const { isOpen: isWhisperOpen, onOpen: onWhisperOpen, onClose: onWhisperClose } = useDisclosure()

  const { isPro } = useLicense()
  const { onCreateWidget, getPageContext } = useWidgetCallback()

  const activeThread = threads.find(t => t.id === activeThreadId) ?? threads[0]

  useEffect(() => {
    if (isPro) {
      setThreads(prev => prev.map(t => ({
        ...t,
        limitReached: false,
        messages: t.messages.filter(m => m.role !== 'limit')
      })))
    }
  }, [isPro])

  useEffect(() => {
    localStorage.setItem('ai-chat-threads', JSON.stringify(threads))
  }, [threads])

  useEffect(() => {
    if (activeThreadId) localStorage.setItem('ai-chat-active', activeThreadId)
  }, [activeThreadId])

  useEffect(() => {
    localStorage.setItem('ai-chat-counter', String(nextChatNum))
  }, [nextChatNum])

  useEffect(() => {
    return subscribeToAlertList(() => setAlerts(listAlerts()))
  }, [])

  useEffect(() => {
    if (open && view === 'chat') inputRef.current?.focus()
  }, [open, view, activeThreadId])

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [activeThread.messages])

  function addThread() {
    const t = makeThread(nextChatNum)
    setNextChatNum(n => n + 1)
    setThreads(prev => [...prev, t])
    setActiveThreadId(t.id)
  }

  function closeThread(id) {
    const thread = threads.find(t => t.id === id)
    if (!thread || threads.length === 1) return
    if (
      thread.messages.length > 0 &&
      !window.confirm(`Close "${thread.name}"? This will delete the conversation.`)
    ) return
    const idx = threads.findIndex(t => t.id === id)
    const next = threads.filter(t => t.id !== id)
    setThreads(next)
    if (activeThread.id === id) {
      setActiveThreadId(next[Math.max(0, idx - 1)].id)
    }
  }

  async function send() {
    const text = input.trim()
    if (!text || loading) return

    const threadId = activeThread.id
    const history = activeThread.messages
      .filter(m => m.role === 'user' || m.role === 'assistant')
      .map(m => ({ role: m.role, content: m.content }))

    setThreads(prev => prev.map(t =>
      t.id === threadId ? { ...t, messages: [...t.messages, { role: 'user', content: text }] } : t
    ))
    setInput('')
    setLoading(true)
    setPendingMessage(text)

    try {
      const pageContext = getPageContext()
      const res = await api.post('/ai/chat', { message: text, history, alerts, pageContext })
      setThreads(prev => prev.map(t =>
        t.id === threadId
          ? { ...t, messages: [...t.messages, { role: 'assistant', content: res.data.response }] }
          : t
      ))
      for (const action of res.data.actions || []) {
        if (action.type === 'create_alert') {
          createAlert(action.config)
          toast({ title: `Alert created: ${action.config.label}`, status: 'success', duration: 3000, isClosable: true })
        } else if (action.type === 'delete_alert') {
          const current = listAlerts()
          const target = action.id
            ? current.find(a => a.id === action.id)
            : current.find(a => a.label?.toLowerCase() === action.label?.toLowerCase())
          if (target) {
            removeAlert(target.id)
            toast({ title: `Alert removed: ${target.label}`, status: 'info', duration: 3000, isClosable: true })
          }
        } else if (action.type === 'create_widget') {
          onCreateWidget(action.widgetDef)
        }
      }
    } catch (err) {
      if (err.response?.data?.error === 'daily_limit_reached') {
        setThreads(prev => prev.map(t =>
          t.id === threadId
            ? { ...t, limitReached: true, messages: [...t.messages, { role: 'limit', content: err.response.data.message }] }
            : t
        ))
      } else {
        const msg = err.response?.data?.error || 'Request failed'
        toast({ title: msg, status: 'error', duration: 4000, isClosable: true })
        setThreads(prev => prev.map(t =>
          t.id === threadId ? { ...t, messages: t.messages.slice(0, -1) } : t
        ))
        setInput(text)
      }
    } finally {
      setLoading(false)
    }
  }

  async function copyConversation() {
    const text = activeThread.messages
      .filter(m => m.role === 'user' || m.role === 'assistant')
      .map(m => `[${m.role === 'user' ? 'User' : 'Assistant'}]\n${m.content}`)
      .join('\n\n')
    await navigator.clipboard.writeText(text)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  function onKeyDown(e) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      send()
    }
  }

  async function startRecording() {
    let stream
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true })
    } catch (err) {
      toast({ title: 'Microphone access denied', description: err.message, status: 'error', duration: 4000, isClosable: true })
      return
    }

    const chunks = []
    const recorder = new MediaRecorder(stream)
    mediaRecorderRef.current = recorder

    recorder.ondataavailable = e => { if (e.data.size > 0) chunks.push(e.data) }

    recorder.onstop = async () => {
      stream.getTracks().forEach(t => t.stop())
      setRecording(false)
      setTranscribing(true)
      try {
        const blob = new Blob(chunks, { type: recorder.mimeType })
        const arrayBuffer = await blob.arrayBuffer()
        const audioCtx = new AudioContext()
        const audioBuffer = await audioCtx.decodeAudioData(arrayBuffer)
        audioCtx.close()

        const sampleRate = 16000
        const offlineCtx = new OfflineAudioContext(1, Math.ceil(audioBuffer.duration * sampleRate), sampleRate)
        const source = offlineCtx.createBufferSource()
        source.buffer = audioBuffer
        source.connect(offlineCtx.destination)
        source.start()
        const resampled = await offlineCtx.startRendering()

        const wavBlob = encodeWav(resampled.getChannelData(0), sampleRate)
        const formData = new FormData()
        formData.append('audio', wavBlob, 'audio.wav')

        const res = await api.post('/ai/transcribe', formData, {
          headers: { 'Content-Type': 'multipart/form-data' },
          timeout: 60000,
        })
        const transcript = res.data.text
        if (transcript) setInput(prev => prev ? `${prev} ${transcript}` : transcript)
      } catch (err) {
        const msg = err.response?.data?.error || err.message
        toast({ title: 'Transcription failed', description: msg, status: 'error', duration: 4000, isClosable: true })
      } finally {
        setTranscribing(false)
        inputRef.current?.focus()
      }
    }

    recorder.start()
    setRecording(true)
  }

  function toggleRecording() {
    if (recording) {
      mediaRecorderRef.current?.stop()
    } else {
      startRecording()
    }
  }

  return (
    <>
      <LicenseModal isOpen={isLicenseOpen} onClose={onLicenseClose} />
      <WhisperSetupModal isOpen={isWhisperOpen} onClose={onWhisperClose} />

      {!open && (
        <Box
          position="fixed"
          left={0}
          top="calc(48px + (100vh - 48px) / 2)"
          transform="translateY(-50%)"
          zIndex={1100}
        >
          <Box
            as="button"
            bg="gray.800"
            color="gray.300"
            borderRight="1px solid"
            borderTop="1px solid"
            borderBottom="1px solid"
            borderColor="gray.700"
            borderRightRadius="md"
            py={4}
            px={2}
            display="flex"
            flexDirection="column"
            alignItems="center"
            gap={2}
            boxShadow="md"
            onClick={() => setOpen(true)}
            _hover={{ bg: 'gray.700', color: 'white' }}
            transition="all 0.15s"
          >
            <ChatIcon />
            <Text
              fontSize="10px"
              fontWeight="semibold"
              letterSpacing="0.08em"
              color="inherit"
              sx={{ writingMode: 'vertical-rl', textOrientation: 'mixed', transform: 'rotate(180deg)' }}
            >
              AI
            </Text>
          </Box>
        </Box>
      )}

      {open && (
        <Box
          position="fixed"
          left={0}
          top="48px"
          h="calc(100vh - 48px)"
          w="300px"
          bg="gray.800"
          borderRight="1px solid"
          borderColor="gray.600"
          boxShadow="2xl"
          display="flex"
          flexDirection="row"
          overflow="hidden"
          zIndex={1100}
        >
          <Box display="flex" flexDirection="column" flex={1} overflow="hidden">
          {/* Header */}
          <Flex
            px={4}
            py={3}
            bg="gray.900"
            borderBottom="1px solid"
            borderColor="gray.700"
            align="center"
            justify="space-between"
            flexShrink={0}
          >
            <Text fontSize="sm" fontWeight="bold" color="white" letterSpacing="0.02em">
              {view === 'settings' ? 'AI Settings' : 'AI Assistant'}
            </Text>
            <HStack spacing={1}>
              {activeThread.messages.length > 0 && view === 'chat' && (
                <IconButton
                  icon={copied ? <CheckIcon /> : <CopyIcon />}
                  size="sm"
                  variant="ghost"
                  color={copied ? 'green.400' : 'gray.400'}
                  onClick={copyConversation}
                  aria-label="Copy conversation"
                  _hover={{ color: 'white', bg: 'gray.700' }}
                />
              )}
              <IconButton
                icon={<SettingsIcon />}
                size="sm"
                variant="ghost"
                color={view === 'settings' ? 'blue.400' : 'gray.400'}
                bg={view === 'settings' ? 'blue.900' : 'transparent'}
                onClick={() => setView(v => v === 'chat' ? 'settings' : 'chat')}
                aria-label="Settings"
                _hover={{ color: 'white', bg: 'gray.700' }}
              />
            </HStack>
          </Flex>

          {view === 'settings' ? (
            <SettingsPane onClose={() => setView('chat')} onOpenVoiceSetup={onWhisperOpen} />
          ) : (
            <>
              {/* Thread tabs */}
              <HStack
                px={2}
                py={1.5}
                spacing={1}
                bg="gray.850"
                borderBottom="1px solid"
                borderColor="gray.700"
                overflowX="auto"
                flexShrink={0}
                sx={{
                  '&::-webkit-scrollbar': { height: '2px' },
                  '&::-webkit-scrollbar-thumb': { bg: 'gray.600' },
                  background: 'var(--chakra-colors-gray-900)'
                }}
              >
                {threads.map(t => (
                  <HStack
                    key={t.id}
                    spacing={1}
                    px={2}
                    py={1}
                    borderRadius="md"
                    bg={t.id === activeThread.id ? 'gray.700' : 'transparent'}
                    border="1px solid"
                    borderColor={t.id === activeThread.id ? 'gray.600' : 'transparent'}
                    cursor="pointer"
                    flexShrink={0}
                    maxW="110px"
                    onClick={() => setActiveThreadId(t.id)}
                    _hover={{ bg: t.id === activeThread.id ? 'gray.700' : 'gray.800' }}
                  >
                    <Text
                      fontSize="xs"
                      color={t.id === activeThread.id ? 'gray.100' : 'gray.400'}
                      overflow="hidden"
                      textOverflow="ellipsis"
                      whiteSpace="nowrap"
                      flex={1}
                      minW={0}
                    >
                      {t.name}
                    </Text>
                    {threads.length > 1 && (
                      <Box
                        as="button"
                        color="gray.500"
                        fontSize="10px"
                        lineHeight={1}
                        flexShrink={0}
                        _hover={{ color: 'gray.100' }}
                        onClick={e => { e.stopPropagation(); closeThread(t.id) }}
                      >
                        ✕
                      </Box>
                    )}
                  </HStack>
                ))}
                <IconButton
                  icon={<AddIcon />}
                  size="xs"
                  variant="ghost"
                  color="gray.400"
                  flexShrink={0}
                  onClick={addThread}
                  aria-label="New chat"
                  _hover={{ color: 'white', bg: 'gray.700' }}
                />
              </HStack>

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
                {activeThread.messages.length === 0 && (
                  <Text color="gray.500" fontSize="sm" textAlign="center" mt={8}>
                    Ask anything about your account or the market.
                  </Text>
                )}
                {activeThread.messages.map((m, i) => (
                  <Message key={i} role={m.role} content={m.content} onUpgrade={onLicenseOpen} />
                ))}
                {loading && <LoadingStatus message={pendingMessage} />}
                <div ref={messagesEndRef} />
              </VStack>

              <VStack p={3} borderTop="1px solid" borderColor="gray.700" spacing={1.5} flexShrink={0} align="stretch">
                {(recording || transcribing) && (
                  <HStack spacing={1.5}>
                    <Spinner size="xs" color="red.400" speed="0.8s" />
                    <Text fontSize="xs" color="red.400">{transcribing ? 'Transcribing…' : 'Recording…'}</Text>
                  </HStack>
                )}
                <HStack spacing={2}>
                  <Input
                    ref={inputRef}
                    size="sm"
                    bg="gray.700"
                    borderColor="gray.600"
                    placeholder={activeThread.limitReached ? 'Daily limit reached — upgrade to continue' : 'Ask a question…'}
                    value={input}
                    onChange={e => setInput(e.target.value)}
                    onKeyDown={onKeyDown}
                    borderRadius="md"
                    isDisabled={activeThread.limitReached}
                    _focus={{ borderColor: 'blue.500', boxShadow: 'none' }}
                  />
                  <IconButton
                    icon={recording ? <StopIcon /> : transcribing ? <Spinner size="xs" /> : <MicIcon />}
                    size="sm"
                    variant="ghost"
                    color={recording ? 'red.400' : 'gray.400'}
                    bg={recording ? 'red.900' : 'transparent'}
                    onClick={toggleRecording}
                    isDisabled={activeThread.limitReached || loading || transcribing}
                    aria-label={recording ? 'Stop recording' : 'Voice input'}
                    borderRadius="md"
                    _hover={{ color: recording ? 'red.300' : 'white', bg: recording ? 'red.800' : 'gray.700' }}
                  />
                  <IconButton
                    icon={<SendIcon />}
                    size="sm"
                    colorScheme="blue"
                    onClick={send}
                    isLoading={loading}
                    isDisabled={!input.trim() || activeThread.limitReached}
                    aria-label="Send"
                    borderRadius="md"
                  />
                </HStack>
              </VStack>
            </>
          )}
          </Box>

          {/* Collapse handle on the right edge */}
          <Box
            as="button"
            w="16px"
            h="100%"
            bg="gray.800"
            borderLeft="1px solid"
            borderColor="gray.700"
            display="flex"
            alignItems="center"
            justifyContent="center"
            flexShrink={0}
            color="gray.500"
            onClick={() => { setOpen(false); setView('chat') }}
            _hover={{ bg: 'gray.700', color: 'gray.300' }}
            transition="all 0.15s"
            title="Collapse panel"
            sx={{ cursor: 'pointer' }}
          >
            <svg viewBox="0 0 8 24" width="8" height="24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round">
              <polyline points="6,4 2,12 6,20" />
            </svg>
          </Box>
        </Box>
      )}
    </>
  )
}
