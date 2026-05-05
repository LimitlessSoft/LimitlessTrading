import { useState } from 'react'
import {
  Modal, ModalOverlay, ModalContent, ModalHeader, ModalBody, ModalFooter, ModalCloseButton,
  FormControl, FormLabel, Input, Textarea, Select, Button, useToast
} from '@chakra-ui/react'

function NewFeedbackModal({ isOpen, onClose, onCreated }) {
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [category, setCategory] = useState('feature')
  const [loading, setLoading] = useState(false)
  const toast = useToast()

  const handleSubmit = async () => {
    if (!title.trim() || !description.trim()) {
      toast({ title: 'Title and description are required', status: 'error', duration: 3000 })
      return
    }
    setLoading(true)
    try {
      const res = await fetch('/api/feedback', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ title, description, category })
      })
      if (!res.ok) {
        const err = await res.json()
        throw new Error(err.error || 'Submission failed')
      }
      const item = await res.json()
      toast({ title: 'Feedback submitted', status: 'success', duration: 2000 })
      setTitle(''); setDescription(''); setCategory('feature')
      onClose()
      onCreated(item)
    } catch (err) {
      toast({ title: err.message, status: 'error', duration: 3000 })
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal isOpen={isOpen} onClose={onClose} isCentered>
      <ModalOverlay />
      <ModalContent bg="gray.800">
        <ModalHeader color="gray.100">New Feedback</ModalHeader>
        <ModalCloseButton />
        <ModalBody>
          <FormControl mb={4}>
            <FormLabel color="gray.300">Category</FormLabel>
            <Select value={category} onChange={e => setCategory(e.target.value)} bg="gray.700" borderColor="gray.600">
              <option value="feature">Feature Request</option>
              <option value="bug">Bug Report</option>
              <option value="improvement">Improvement</option>
            </Select>
          </FormControl>
          <FormControl mb={4}>
            <FormLabel color="gray.300">Title</FormLabel>
            <Input value={title} onChange={e => setTitle(e.target.value)} placeholder="Brief summary" bg="gray.700" borderColor="gray.600" />
          </FormControl>
          <FormControl>
            <FormLabel color="gray.300">Description</FormLabel>
            <Textarea value={description} onChange={e => setDescription(e.target.value)} placeholder="Detailed description..." bg="gray.700" borderColor="gray.600" rows={5} />
          </FormControl>
        </ModalBody>
        <ModalFooter>
          <Button variant="ghost" mr={3} onClick={onClose}>Cancel</Button>
          <Button colorScheme="blue" onClick={handleSubmit} isLoading={loading}>Submit</Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  )
}

export default NewFeedbackModal
