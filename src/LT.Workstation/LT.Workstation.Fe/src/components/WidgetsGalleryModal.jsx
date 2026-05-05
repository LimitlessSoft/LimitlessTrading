import { useState, useEffect } from 'react'
import {
  Modal, ModalOverlay, ModalContent, ModalHeader, ModalBody, ModalFooter,
  ModalCloseButton, Button, Text, Box, SimpleGrid, Input, Spinner, Center,
  Badge, HStack,
} from '@chakra-ui/react'

function WidgetsGalleryModal({ isOpen, onClose, onImport }) {
  const [widgets, setWidgets] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState(null)

  useEffect(() => {
    if (!isOpen) return
    setSelected(null)
    setSearch('')
    setError(null)
    setLoading(true)
    fetch('http://localhost:8744/api/gallery')
      .then(r => r.json())
      .then(data => { setWidgets(data); setLoading(false) })
      .catch(() => { setError('Could not load gallery'); setLoading(false) })
  }, [isOpen])

  const filtered = widgets.filter(w =>
    !search.trim() ||
    w.name.toLowerCase().includes(search.toLowerCase()) ||
    w.description?.toLowerCase().includes(search.toLowerCase()) ||
    w.widgetType?.toLowerCase().includes(search.toLowerCase())
  )

  const handleImport = () => {
    if (!selected) return
    onImport?.(selected.config)
    setSelected(null)
    onClose()
  }

  const handleClose = () => {
    setSelected(null)
    onClose()
  }

  return (
    <Modal isOpen={isOpen} onClose={handleClose} size="xl" scrollBehavior="inside">
      <ModalOverlay />
      <ModalContent bg="gray.800" maxH="80vh">
        <ModalHeader>Widgets Gallery</ModalHeader>
        <ModalCloseButton />
        <ModalBody>
          <Input
            placeholder="Search widgets…"
            value={search}
            onChange={e => setSearch(e.target.value)}
            mb={4}
            size="sm"
          />

          {loading && (
            <Center py={10}><Spinner /></Center>
          )}

          {error && (
            <Box p={6} textAlign="center" borderWidth="1px" borderColor="gray.600" borderRadius="md" borderStyle="dashed">
              <Text color="gray.400" fontSize="sm">{error}</Text>
            </Box>
          )}

          {!loading && !error && filtered.length === 0 && (
            <Box p={6} textAlign="center" borderWidth="1px" borderColor="gray.600" borderRadius="md" borderStyle="dashed">
              <Text color="gray.400" fontSize="sm">
                {widgets.length === 0 ? 'No widgets in the gallery yet. Configure a widget and share it!' : 'No widgets match your search.'}
              </Text>
            </Box>
          )}

          {!loading && !error && filtered.length > 0 && (
            <SimpleGrid columns={2} spacing={3}>
              {filtered.map(w => (
                <Box
                  key={w.id}
                  p={4}
                  borderWidth="1px"
                  borderRadius="md"
                  cursor="pointer"
                  borderColor={selected?.id === w.id ? 'blue.400' : 'gray.600'}
                  bg={selected?.id === w.id ? 'blue.900' : 'transparent'}
                  _hover={{ borderColor: selected?.id === w.id ? 'blue.400' : 'gray.400' }}
                  onClick={() => setSelected(w)}
                >
                  <HStack mb={1} justify="space-between">
                    <Text fontWeight="semibold" fontSize="sm" noOfLines={1}>{w.name}</Text>
                    {w.widgetType && (
                      <Badge colorScheme="blue" fontSize="9px">{w.widgetType}</Badge>
                    )}
                  </HStack>
                  {w.description && (
                    <Text fontSize="xs" color="gray.400" noOfLines={2} lineHeight="short">{w.description}</Text>
                  )}
                  <Text fontSize="10px" color="gray.500" mt={1}>
                    {w.authorId !== 'anonymous' ? w.authorId : 'Anonymous'}
                  </Text>
                </Box>
              ))}
            </SimpleGrid>
          )}
        </ModalBody>
        <ModalFooter>
          <Button variant="ghost" mr="auto" onClick={handleClose}>Cancel</Button>
          <Button colorScheme="blue" isDisabled={!selected} onClick={handleImport}>
            Import{selected ? ` "${selected.name}"` : ''}
          </Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  )
}

export default WidgetsGalleryModal
