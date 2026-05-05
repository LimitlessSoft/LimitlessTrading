import { useState } from 'react'
import {
  Modal, ModalOverlay, ModalContent, ModalHeader, ModalBody, ModalFooter,
  ModalCloseButton, Button, FormControl, FormLabel, Input, Textarea, useToast,
} from '@chakra-ui/react'

function ShareToGalleryModal({ isOpen, onClose, widget }) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const toast = useToast()

  const handleClose = () => {
    setName('')
    setDescription('')
    onClose()
  }

  const handleShare = async () => {
    if (!name.trim() || !widget) return
    setIsSaving(true)
    try {
      const res = await fetch('http://localhost:8744/api/gallery', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: name.trim(),
          description: description.trim(),
          widgetType: widget.type ?? '',
          config: widget,
        }),
      })
      if (!res.ok) {
        const err = await res.json().catch(() => ({}))
        throw new Error(err.error || `HTTP ${res.status}`)
      }
      toast({ title: 'Shared to gallery', status: 'success', duration: 2000 })
      handleClose()
    } catch (err) {
      toast({ title: 'Failed to share', description: err.message, status: 'error', duration: 3000 })
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <Modal isOpen={isOpen} onClose={handleClose}>
      <ModalOverlay />
      <ModalContent bg="gray.800">
        <ModalHeader>Share to Gallery</ModalHeader>
        <ModalCloseButton />
        <ModalBody>
          <FormControl mb={4} isRequired>
            <FormLabel fontSize="sm">Name</FormLabel>
            <Input
              placeholder="e.g. SPY RSI label with color coding"
              value={name}
              onChange={e => setName(e.target.value)}
              onKeyDown={e => { if (e.key === 'Enter' && name.trim()) handleShare() }}
            />
          </FormControl>
          <FormControl>
            <FormLabel fontSize="sm">Description</FormLabel>
            <Textarea
              placeholder="What does this widget do?"
              value={description}
              onChange={e => setDescription(e.target.value)}
              rows={3}
            />
          </FormControl>
        </ModalBody>
        <ModalFooter>
          <Button variant="ghost" mr="auto" onClick={handleClose}>Cancel</Button>
          <Button
            colorScheme="blue"
            isDisabled={!name.trim()}
            isLoading={isSaving}
            onClick={handleShare}
          >
            Share
          </Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  )
}

export default ShareToGalleryModal
