import { useState } from 'react'
import {
  Modal, ModalOverlay, ModalContent, ModalHeader, ModalBody, ModalFooter,
  ModalCloseButton, Button, Input, FormControl, FormLabel, Text, VStack,
  HStack, Box, Badge, Divider, useToast
} from '@chakra-ui/react'
import { CheckCircleIcon, StarIcon } from '@chakra-ui/icons'
import { useLicense } from '../modules/LicenseContext'

const PRO_FEATURES = [
  'Unlimited template pages',
  'Unlimited widgets per page',
  'Unlimited AI chat messages',
  'Priority support',
]

function LicenseModal({ isOpen, onClose }) {
  const { isPro, customerEmail, activate, deactivate } = useLicense()
  const [key, setKey] = useState('')
  const [isActivating, setIsActivating] = useState(false)
  const [isDeactivating, setIsDeactivating] = useState(false)
  const toast = useToast()

  const handleActivate = async () => {
    if (!key.trim()) return
    setIsActivating(true)
    try {
      const result = await activate(key.trim())
      if (result.valid) {
        toast({ title: 'License activated', status: 'success', duration: 3000 })
        setKey('')
        onClose()
      } else {
        toast({ title: 'Invalid license key', description: 'Check your key and try again.', status: 'error', duration: 4000 })
      }
    } catch (err) {
      toast({ title: 'Activation failed', description: err.message, status: 'error', duration: 5000 })
    } finally {
      setIsActivating(false)
    }
  }

  const handleDeactivate = async () => {
    setIsDeactivating(true)
    try {
      await deactivate()
      toast({ title: 'License removed', status: 'info', duration: 3000 })
    } finally {
      setIsDeactivating(false)
    }
  }

  return (
    <Modal isOpen={isOpen} onClose={onClose} isCentered>
      <ModalOverlay />
      <ModalContent bg="gray.800">
        <ModalHeader>
          <HStack>
            <StarIcon color="yellow.400" />
            <Text>Limitless Trading Pro</Text>
          </HStack>
        </ModalHeader>
        <ModalCloseButton />
        <ModalBody>
          <VStack align="stretch" spacing={4}>
            {isPro ? (
              <Box p={3} bg="green.900" borderRadius="md" borderWidth={1} borderColor="green.600">
                <HStack>
                  <CheckCircleIcon color="green.400" />
                  <Text color="green.300" fontWeight="medium">Pro license active</Text>
                </HStack>
                {customerEmail && (
                  <Text fontSize="sm" color="gray.400" mt={1}>{customerEmail}</Text>
                )}
              </Box>
            ) : (
              <>
                <Text color="gray.300" fontSize="sm">
                  You've reached the free limit of 3 template pages. Upgrade to Pro to create unlimited pages.
                </Text>
                <VStack align="start" spacing={1}>
                  {PRO_FEATURES.map(f => (
                    <HStack key={f} spacing={2}>
                      <CheckCircleIcon color="blue.400" boxSize={3} />
                      <Text fontSize="sm" color="gray.200">{f}</Text>
                    </HStack>
                  ))}
                </VStack>
                <Button
                  as="a"
                  href="https://buy.polar.sh/polar_cl_rEtdEQBXZuLMv16oGihKb43BEY3BDbeS9ECNM348UL3"
                  target="_blank"
                  rel="noopener noreferrer"
                  colorScheme="blue"
                  size="sm"
                >
                  Get Pro License
                </Button>
                <Divider borderColor="gray.600" />
              </>
            )}

            <FormControl>
              <FormLabel fontSize="sm">{isPro ? 'Replace license key' : 'Already have a key?'}</FormLabel>
              <HStack>
                <Input
                  value={key}
                  onChange={e => setKey(e.target.value)}
                  placeholder="XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX"
                  size="sm"
                  fontFamily="mono"
                  onKeyDown={e => e.key === 'Enter' && handleActivate()}
                />
                <Button
                  size="sm"
                  colorScheme="blue"
                  onClick={handleActivate}
                  isLoading={isActivating}
                  isDisabled={!key.trim()}
                >
                  Activate
                </Button>
              </HStack>
            </FormControl>

            {isPro && (
              <Button
                size="sm"
                variant="ghost"
                colorScheme="red"
                onClick={handleDeactivate}
                isLoading={isDeactivating}
              >
                Remove license
              </Button>
            )}
          </VStack>
        </ModalBody>
        <ModalFooter>
          <Button variant="ghost" onClick={onClose}>Close</Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  )
}

export default LicenseModal
