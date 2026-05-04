import {
  Modal,
  ModalOverlay,
  ModalContent,
  ModalHeader,
  ModalBody,
  ModalFooter,
  ModalCloseButton,
  Button,
  Text,
  Box
} from '@chakra-ui/react'

function WidgetsGalleryModal({ isOpen, onClose }) {
  return (
    <Modal isOpen={isOpen} onClose={onClose} size="xl">
      <ModalOverlay />
      <ModalContent bg="gray.800">
        <ModalHeader>Widgets Gallery</ModalHeader>
        <ModalCloseButton />
        <ModalBody>
          <Box
            p={8}
            textAlign="center"
            borderWidth="1px"
            borderColor="gray.600"
            borderRadius="md"
            borderStyle="dashed"
          >
            <Text color="gray.400" fontSize="sm">
              TODO — implement some server which will return template controls here
            </Text>
          </Box>
        </ModalBody>
        <ModalFooter>
          <Button variant="ghost" onClick={onClose}>
            Close
          </Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  )
}

export default WidgetsGalleryModal
