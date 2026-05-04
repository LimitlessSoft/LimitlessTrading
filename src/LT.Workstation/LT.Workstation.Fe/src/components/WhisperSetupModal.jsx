import { useState, useEffect, useRef } from 'react'
import {
  Modal, ModalOverlay, ModalContent, ModalHeader, ModalBody, ModalFooter,
  ModalCloseButton, VStack, HStack, Text, Input, Box, Button, Badge,
  Progress, Divider, FormControl, FormLabel, FormHelperText, Spinner,
  useToast, Link
} from '@chakra-ui/react'
import { CheckCircleIcon, WarningIcon, ExternalLinkIcon } from '@chakra-ui/icons'
import axios from 'axios'

const api = axios.create({ baseURL: '/api', timeout: 30000 })

export default function WhisperSetupModal({ isOpen, onClose }) {
  const toast = useToast()
  const [status, setStatus] = useState(null)
  const [loading, setLoading] = useState(false)
  const [binaryPath, setBinaryPath] = useState('')
  const [modelsDir, setModelsDir] = useState('')
  const [activeModel, setActiveModel] = useState('')
  const [downloading, setDownloading] = useState(null)
  const [saving, setSaving] = useState(false)
  const eventSourceRef = useRef(null)

  useEffect(() => {
    if (isOpen) loadStatus()
  }, [isOpen])

  async function loadStatus() {
    setLoading(true)
    try {
      const res = await api.get('/ai/whisper/status')
      setStatus(res.data)
      setBinaryPath(res.data.customBinaryPath || '')
      setModelsDir(res.data.customModelsDir || '')
      setActiveModel(res.data.activeModel || res.data.localModels[0] || '')
    } catch {
      setStatus({ binaryPath: null, modelsDir: null, localModels: [], availableModels: [] })
    } finally {
      setLoading(false)
    }
  }

  function startDownload(name) {
    if (downloading) return
    setDownloading({ name, progress: 0 })

    const es = new EventSource(`/api/ai/whisper/download-model?name=${encodeURIComponent(name)}`)
    eventSourceRef.current = es

    es.onmessage = (e) => {
      const data = JSON.parse(e.data)
      if (data.type === 'progress') {
        setDownloading(d => d ? { ...d, progress: data.progress } : null)
      } else if (data.type === 'done') {
        es.close()
        setDownloading(null)
        loadStatus()
        toast({ title: `Model "${name}" downloaded`, status: 'success', duration: 3000, isClosable: true })
      } else if (data.type === 'error') {
        es.close()
        setDownloading(null)
        toast({ title: 'Download failed', description: data.message, status: 'error', duration: 5000, isClosable: true })
      }
    }

    es.onerror = () => {
      es.close()
      setDownloading(null)
      toast({ title: 'Download connection lost', status: 'error', duration: 4000, isClosable: true })
    }
  }

  function cancelDownload() {
    eventSourceRef.current?.close()
    setDownloading(null)
  }

  async function save() {
    setSaving(true)
    try {
      await Promise.all([
        api.put('/user-data/settings/ai.whisper.binaryPath', { value: binaryPath }),
        api.put('/user-data/settings/ai.whisper.modelsDir', { value: modelsDir }),
        api.put('/user-data/settings/ai.whisper.model', { value: activeModel }),
      ])
      toast({ title: 'Voice input settings saved', status: 'success', duration: 2000, isClosable: true })
      onClose()
    } catch {
      toast({ title: 'Failed to save settings', status: 'error', duration: 3000, isClosable: true })
    } finally {
      setSaving(false)
    }
  }

  function handleClose() {
    if (downloading) cancelDownload()
    onClose()
  }

  const availableModels = status?.availableModels || []
  const localModels = status?.localModels || []

  return (
    <Modal isOpen={isOpen} onClose={handleClose} isCentered size="lg">
      <ModalOverlay />
      <ModalContent bg="gray.800" maxH="85vh" display="flex" flexDirection="column">
        <ModalHeader fontSize="md" color="white">Voice Input Setup</ModalHeader>
        <ModalCloseButton />

        <ModalBody overflowY="auto" flex={1}>
          {loading ? (
            <HStack justify="center" py={8}>
              <Spinner color="blue.400" />
              <Text color="gray.400">Detecting whisper-cli…</Text>
            </HStack>
          ) : (
            <VStack spacing={5} align="stretch">

              {/* Binary status */}
              <Box>
                <Text fontSize="xs" fontWeight="semibold" color="gray.400" textTransform="uppercase" letterSpacing="0.08em" mb={2}>
                  whisper-cli Binary
                </Text>
                {status?.binaryPath ? (
                  <HStack spacing={2} p={2} bg="green.900" borderRadius="md" borderWidth={1} borderColor="green.700">
                    <CheckCircleIcon color="green.400" boxSize={3} flexShrink={0} />
                    <Text fontSize="xs" color="green.300" fontFamily="mono" wordBreak="break-all">{status.binaryPath}</Text>
                  </HStack>
                ) : (
                  <VStack align="stretch" spacing={2}>
                    <HStack spacing={2} p={2} bg="orange.900" borderRadius="md" borderWidth={1} borderColor="orange.700">
                      <WarningIcon color="orange.400" boxSize={3} flexShrink={0} />
                      <Text fontSize="xs" color="orange.300">
                        whisper-cli not found.{' '}
                        <Link href="https://github.com/ggerganov/whisper.cpp" isExternal color="blue.300">
                          Build from source <ExternalLinkIcon mx="1px" boxSize={3} />
                        </Link>
                      </Text>
                    </HStack>
                  </VStack>
                )}
                <FormControl mt={2}>
                  <FormLabel fontSize="xs" color="gray.400" mb={1}>Custom binary path (optional)</FormLabel>
                  <Input
                    size="sm"
                    bg="gray.700"
                    borderColor="gray.600"
                    placeholder="/path/to/whisper-cli"
                    value={binaryPath}
                    onChange={e => setBinaryPath(e.target.value)}
                    fontFamily="mono"
                    fontSize="xs"
                    _focus={{ borderColor: 'blue.500', boxShadow: 'none' }}
                  />
                </FormControl>
              </Box>

              <Divider borderColor="gray.700" />

              {/* Models dir */}
              <FormControl>
                <FormLabel fontSize="xs" fontWeight="semibold" color="gray.400" textTransform="uppercase" letterSpacing="0.08em" mb={1}>
                  Models Directory
                </FormLabel>
                <Input
                  size="sm"
                  bg="gray.700"
                  borderColor="gray.600"
                  placeholder={status?.modelsDir || '~/.cache/whisper'}
                  value={modelsDir}
                  onChange={e => setModelsDir(e.target.value)}
                  fontFamily="mono"
                  fontSize="xs"
                  _focus={{ borderColor: 'blue.500', boxShadow: 'none' }}
                />
                <FormHelperText fontSize="xs" color="gray.500">
                  Leave blank to use auto-detected: {status?.modelsDir || '~/.cache/whisper'}
                </FormHelperText>
              </FormControl>

              <Divider borderColor="gray.700" />

              {/* Models list */}
              <Box>
                <Text fontSize="xs" fontWeight="semibold" color="gray.400" textTransform="uppercase" letterSpacing="0.08em" mb={2}>
                  Models
                </Text>
                <VStack spacing={1} align="stretch">
                  {availableModels.map(model => {
                    const isLocal = localModels.includes(model.name)
                    const isDownloading = downloading?.name === model.name
                    const isSelected = activeModel === model.name

                    return (
                      <Box
                        key={model.name}
                        p={2}
                        borderRadius="md"
                        bg={isSelected ? 'blue.900' : 'gray.750'}
                        borderWidth={1}
                        borderColor={isSelected ? 'blue.600' : 'gray.700'}
                        cursor={isLocal ? 'pointer' : 'default'}
                        onClick={() => isLocal && setActiveModel(model.name)}
                        _hover={isLocal ? { borderColor: isSelected ? 'blue.500' : 'gray.500' } : {}}
                        transition="border-color 0.15s"
                        sx={{ background: isSelected ? 'var(--chakra-colors-blue-900)' : 'rgba(255,255,255,0.03)' }}
                      >
                        <HStack justify="space-between" mb={isDownloading ? 1 : 0}>
                          <HStack spacing={2}>
                            {isLocal && (
                              <Box
                                w="8px" h="8px" borderRadius="full" flexShrink={0}
                                bg={isSelected ? 'blue.400' : 'gray.600'}
                                borderWidth={isSelected ? 0 : '1px'}
                                borderColor="gray.500"
                              />
                            )}
                            <VStack align="start" spacing={0}>
                              <Text fontSize="sm" color={isLocal ? 'gray.100' : 'gray.400'} fontWeight={isSelected ? 'medium' : 'normal'}>
                                {model.label}
                              </Text>
                              <Text fontSize="xs" color="gray.500" fontFamily="mono">{model.name}</Text>
                            </VStack>
                          </HStack>
                          <HStack spacing={2}>
                            <Text fontSize="xs" color="gray.500">{model.sizeMb >= 1000 ? `${(model.sizeMb / 1000).toFixed(1)} GB` : `${model.sizeMb} MB`}</Text>
                            {isLocal ? (
                              <Badge colorScheme="green" variant="subtle" fontSize="10px">Downloaded</Badge>
                            ) : isDownloading ? (
                              <Button size="xs" colorScheme="red" variant="ghost" onClick={e => { e.stopPropagation(); cancelDownload() }}>
                                Cancel
                              </Button>
                            ) : (
                              <Button
                                size="xs"
                                colorScheme="blue"
                                variant="outline"
                                isDisabled={!!downloading}
                                onClick={e => { e.stopPropagation(); startDownload(model.name) }}
                              >
                                Download
                              </Button>
                            )}
                          </HStack>
                        </HStack>
                        {isDownloading && (
                          <Progress
                            value={downloading.progress}
                            size="xs"
                            colorScheme="blue"
                            borderRadius="full"
                            mt={1}
                            hasStripe
                            isAnimated
                          />
                        )}
                      </Box>
                    )
                  })}
                </VStack>
              </Box>

            </VStack>
          )}
        </ModalBody>

        <ModalFooter borderTop="1px solid" borderColor="gray.700">
          <HStack w="100%" justify="space-between">
            <Text fontSize="xs" color="gray.500">
              {localModels.length > 0
                ? `Active: ${activeModel || 'none selected'}`
                : 'No models downloaded yet'}
            </Text>
            <HStack spacing={2}>
              <Button variant="ghost" size="sm" onClick={handleClose}>Cancel</Button>
              <Button
                size="sm"
                colorScheme="blue"
                onClick={save}
                isLoading={saving}
                isDisabled={loading}
              >
                Save
              </Button>
            </HStack>
          </HStack>
        </ModalFooter>
      </ModalContent>
    </Modal>
  )
}
