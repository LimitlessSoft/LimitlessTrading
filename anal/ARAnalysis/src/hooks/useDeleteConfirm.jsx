import { useRef, useState } from 'react'
import {
  AlertDialog,
  AlertDialogBody,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogContent,
  AlertDialogOverlay,
  Button,
  useDisclosure,
} from '@chakra-ui/react'

export function useDeleteConfirm() {
  const { isOpen, onOpen, onClose } = useDisclosure()
  const cancelRef = useRef()
  const [pendingId, setPendingId] = useState(null)
  const [pendingCallback, setPendingCallback] = useState(null)

  const requestDelete = (id, callback) => {
    setPendingId(id)
    setPendingCallback(() => callback)
    onOpen()
  }

  const handleConfirm = () => {
    if (pendingCallback) pendingCallback(pendingId)
    onClose()
    setPendingId(null)
    setPendingCallback(null)
  }

  const handleCancel = () => {
    onClose()
    setPendingId(null)
    setPendingCallback(null)
  }

  const confirmDeleteDialog = (
    <AlertDialog isOpen={isOpen} leastDestructiveRef={cancelRef} onClose={handleCancel} isCentered>
      <AlertDialogOverlay>
        <AlertDialogContent bg="gray.800" borderColor="gray.600" borderWidth="1px">
          <AlertDialogHeader fontSize="lg" fontWeight="bold" color="white">
            Delete Widget
          </AlertDialogHeader>
          <AlertDialogBody color="gray.300">
            Are you sure? This cannot be undone.
          </AlertDialogBody>
          <AlertDialogFooter>
            <Button ref={cancelRef} onClick={handleCancel} variant="ghost" colorScheme="gray">
              Cancel
            </Button>
            <Button colorScheme="red" onClick={handleConfirm} ml={3}>
              Delete
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialogOverlay>
    </AlertDialog>
  )

  return { requestDelete, confirmDeleteDialog }
}
