import { useEffect, useState } from 'react'
import {
  Button,
  FormControl,
  FormLabel,
  Input,
  Modal,
  ModalBody,
  ModalCloseButton,
  ModalContent,
  ModalFooter,
  ModalHeader,
  ModalOverlay,
  Text,
  Textarea,
  VStack
} from '@chakra-ui/react'
import { createEntry, updateEntry } from '../api'

const EMPTY = { version: '', title: '', publishedAt: '', tags: '', body: '' }

function toFormValues(entry) {
  if (!entry) return EMPTY
  const date = entry.publishedAt ? entry.publishedAt.slice(0, 10) : ''
  return {
    version: entry.version ?? '',
    title: entry.title ?? '',
    publishedAt: date,
    tags: (entry.tags ?? []).join(', '),
    body: entry.body ?? ''
  }
}

export default function WhatsNewModal({ isOpen, onClose, onSaved, entry }) {
  const [form, setForm] = useState(EMPTY)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (isOpen) {
      setForm(toFormValues(entry))
      setError('')
    }
  }, [isOpen, entry])

  function set(field, value) {
    setForm(prev => ({ ...prev, [field]: value }))
  }

  async function handleSave() {
    if (!form.version.trim() || !form.title.trim() || !form.publishedAt) {
      setError('Version, Title, and Published At are required.')
      return
    }
    setError('')
    setSaving(true)
    try {
      const payload = {
        version: form.version.trim(),
        title: form.title.trim(),
        body: form.body.trim(),
        publishedAt: new Date(form.publishedAt).toISOString(),
        tags: form.tags
          .split(',')
          .map(t => t.trim())
          .filter(Boolean)
      }
      if (entry) {
        await updateEntry(entry.id, payload)
      } else {
        await createEntry(payload)
      }
      onSaved()
    } catch (e) {
      setError(e.message ?? 'Save failed')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal isOpen={isOpen} onClose={onClose} size="lg">
      <ModalOverlay />
      <ModalContent bg="gray.800">
        <ModalHeader color="white">{entry ? 'Edit Entry' : 'New Entry'}</ModalHeader>
        <ModalCloseButton color="gray.400" />
        <ModalBody>
          <VStack spacing={4}>
            <FormControl>
              <FormLabel color="gray.300">Version</FormLabel>
              <Input
                value={form.version}
                onChange={e => set('version', e.target.value)}
                bg="gray.700"
                border="none"
                color="white"
                placeholder="0.2.0"
              />
            </FormControl>
            <FormControl>
              <FormLabel color="gray.300">Title</FormLabel>
              <Input
                value={form.title}
                onChange={e => set('title', e.target.value)}
                bg="gray.700"
                border="none"
                color="white"
                placeholder="Release title"
              />
            </FormControl>
            <FormControl>
              <FormLabel color="gray.300">Published At</FormLabel>
              <Input
                type="date"
                value={form.publishedAt}
                onChange={e => set('publishedAt', e.target.value)}
                bg="gray.700"
                border="none"
                color="white"
              />
            </FormControl>
            <FormControl>
              <FormLabel color="gray.300">Tags (comma-separated)</FormLabel>
              <Input
                value={form.tags}
                onChange={e => set('tags', e.target.value)}
                bg="gray.700"
                border="none"
                color="white"
                placeholder="workstation, widgets"
              />
            </FormControl>
            <FormControl>
              <FormLabel color="gray.300">Body</FormLabel>
              <Textarea
                value={form.body}
                onChange={e => set('body', e.target.value)}
                bg="gray.700"
                border="none"
                color="white"
                rows={8}
                placeholder="Release notes content..."
              />
            </FormControl>
            {error && (
              <Text color="red.400" fontSize="sm" alignSelf="flex-start">
                {error}
              </Text>
            )}
          </VStack>
        </ModalBody>
        <ModalFooter>
          <Button variant="ghost" colorScheme="gray" color="gray.400" mr={3} onClick={onClose}>
            Cancel
          </Button>
          <Button colorScheme="blue" isLoading={saving} onClick={handleSave}>
            Save
          </Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  )
}
