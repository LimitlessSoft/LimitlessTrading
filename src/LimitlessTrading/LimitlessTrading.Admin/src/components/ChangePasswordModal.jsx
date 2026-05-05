import { useState } from 'react'
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
  Text
} from '@chakra-ui/react'
import { changePassword } from '../api'

export default function ChangePasswordModal({ isOpen, onClose }) {
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  function reset() {
    setCurrent('')
    setNext('')
    setConfirm('')
    setError('')
  }

  async function handleSubmit() {
    if (next !== confirm) { setError('Passwords do not match'); return }
    if (next.length < 8) { setError('Password must be at least 8 characters'); return }
    setSaving(true)
    setError('')
    try {
      await changePassword(current, next)
      reset()
      onClose()
    } catch (e) {
      setError(e.message)
    } finally {
      setSaving(false)
    }
  }

  function handleClose() {
    reset()
    onClose()
  }

  return (
    <Modal isOpen={isOpen} onClose={handleClose} isCentered>
      <ModalOverlay />
      <ModalContent bg="gray.800" color="white">
        <ModalHeader>Change Password</ModalHeader>
        <ModalCloseButton />
        <ModalBody display="flex" flexDirection="column" gap={4}>
          <FormControl>
            <FormLabel fontSize="sm" color="gray.400">Current password</FormLabel>
            <Input
              type="password"
              value={current}
              onChange={e => setCurrent(e.target.value)}
              bg="gray.700"
              borderColor="gray.600"
            />
          </FormControl>
          <FormControl>
            <FormLabel fontSize="sm" color="gray.400">New password</FormLabel>
            <Input
              type="password"
              value={next}
              onChange={e => setNext(e.target.value)}
              bg="gray.700"
              borderColor="gray.600"
            />
          </FormControl>
          <FormControl>
            <FormLabel fontSize="sm" color="gray.400">Confirm new password</FormLabel>
            <Input
              type="password"
              value={confirm}
              onChange={e => setConfirm(e.target.value)}
              bg="gray.700"
              borderColor="gray.600"
              onKeyDown={e => e.key === 'Enter' && handleSubmit()}
            />
          </FormControl>
          {error && <Text color="red.400" fontSize="sm">{error}</Text>}
        </ModalBody>
        <ModalFooter gap={2}>
          <Button variant="ghost" size="sm" color="gray.400" onClick={handleClose}>Cancel</Button>
          <Button colorScheme="blue" size="sm" isLoading={saving} onClick={handleSubmit}>
            Update password
          </Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  )
}
