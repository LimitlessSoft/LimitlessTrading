import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Box,
  Flex,
  Spacer,
  IconButton,
  Menu,
  MenuButton,
  MenuList,
  MenuItem,
  MenuDivider,
  Modal,
  ModalOverlay,
  ModalContent,
  ModalHeader,
  ModalBody,
  ModalFooter,
  ModalCloseButton,
  Button,
  Input,
  FormControl,
  FormLabel,
  Text,
  Divider,
  Code,
  Tooltip,
  useDisclosure,
  useToast
} from '@chakra-ui/react'
import { HamburgerIcon, AddIcon, BellIcon, InfoOutlineIcon, CopyIcon, CloseIcon } from '@chakra-ui/icons'
import { fetchRoutes, createRoute } from '../modules/routesService'
import { useLicense } from '../modules/LicenseContext'
import { useAIChat } from '../modules/AIChatContext'

function AssistantIcon() {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <rect x="4" y="8" width="16" height="11" rx="2" />
      <line x1="12" y1="4" x2="12" y2="8" />
      <circle cx="12" cy="3.2" r="1.2" fill="currentColor" stroke="none" />
      <circle cx="9" cy="13.5" r="1.2" fill="currentColor" stroke="none" />
      <circle cx="15" cy="13.5" r="1.2" fill="currentColor" stroke="none" />
      <line x1="9" y1="16.5" x2="15" y2="16.5" />
    </svg>
  )
}
import { FREE_LIMITS } from '../modules/freeLimits'
import AlertsPanel from './AlertsPanel'
import LicenseModal from './LicenseModal'

function getDeviceInfo() {
  const nav = window.navigator
  const scr = window.screen
  return {
    userAgent: nav.userAgent,
    platform: nav.platform,
    language: nav.language,
    screen: `${scr.width} × ${scr.height} (${window.devicePixelRatio}x DPR)`,
    viewport: `${window.innerWidth} × ${window.innerHeight}`,
    colorDepth: `${scr.colorDepth}-bit`,
    timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
    online: nav.onLine ? 'Yes' : 'No',
    hardwareConcurrency: nav.hardwareConcurrency ?? '—',
  }
}

function PageNavMenu({
  children,
  bg = 'gray.800',
  borderColor = 'gray.700',
  headerControls = [],
  activeDrag = null,
  onRenderWidget,
  headerRef: externalHeaderRef,
  refreshKey
}) {
  const [routes, setRoutes] = useState([])
  const [newName, setNewName] = useState('')
  const [newPath, setNewPath] = useState('')
  const [isCreating, setIsCreating] = useState(false)
  const [appVersion, setAppVersion] = useState(null)
  const { isPro, status: licenseStatus, customerEmail } = useLicense()
  const canAddTemplate = isPro || routes.length < FREE_LIMITS.templates
  const { isOpen, onOpen, onClose } = useDisclosure()
  const { isOpen: isAlertsOpen, onOpen: onAlertsOpen, onClose: onAlertsClose } = useDisclosure()
  const { isOpen: isLicenseOpen, onOpen: onLicenseOpen, onClose: onLicenseClose } = useDisclosure()
  const { isOpen: isAboutOpen, onOpen: onAboutOpen, onClose: onAboutClose } = useDisclosure()
  const navigate = useNavigate()
  const toast = useToast()
  const { open: aiOpen, setOpen: setAiOpen } = useAIChat()
  const internalHeaderRef = useRef(null)
  const headerRef = externalHeaderRef || internalHeaderRef
  const headerControlsRef = useRef(null)

  // Check if something is being dragged over the header
  const isDropTarget = activeDrag && activeDrag.mousePos && headerRef.current && (() => {
    const rect = headerRef.current.getBoundingClientRect()
    const { x, y } = activeDrag.mousePos
    return x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom
  })()

  const loadRoutes = async () => {
    try {
      const data = await fetchRoutes()
      setRoutes(data)
    } catch {
    }
  }

  useEffect(() => {
    loadRoutes()
  }, [refreshKey])

  useEffect(() => {
    fetch('/api/version')
      .then(r => r.json())
      .then(d => setAppVersion(d.version))
      .catch(() => {})
  }, [])

  const handleNavigate = (path) => {
    navigate(path)
  }

  const handleCreateRoute = async () => {
    if (!newName.trim() || !newPath.trim()) {
      toast({
        title: 'Name and path are required',
        status: 'error',
        duration: 3000
      })
      return
    }

    const path = newPath.startsWith('/') ? newPath : `/${newPath}`

    setIsCreating(true)
    try {
      await createRoute(newName.trim(), path)
      await loadRoutes()
      setNewName('')
      setNewPath('')
      onClose()
      toast({
        title: 'Page created',
        status: 'success',
        duration: 2000
      })
    } catch (err) {
      toast({
        title: err.message,
        status: 'error',
        duration: 3000
      })
    } finally {
      setIsCreating(false)
    }
  }

  const handleModalClose = () => {
    setNewName('')
    setNewPath('')
    onClose()
  }

  return (
    <>
      <Box
        ref={headerRef}
        data-header-dropzone="true"
        position="sticky"
        top={0}
        zIndex={1000}
        bg={isDropTarget ? 'blue.900' : bg}
        borderBottom="2px solid"
        borderColor={isDropTarget ? 'blue.400' : borderColor}
        px={4}
        py={2}
        transition="background-color 0.15s, border-color 0.15s"
      >
        <Flex align="center" gap={1}>
          <Menu>
            <MenuButton
              as={IconButton}
              icon={<HamburgerIcon />}
              variant="ghost"
              size="sm"
              aria-label="Navigation menu"
              _hover={{ bg: 'whiteAlpha.200' }}
              display="flex"
              alignItems="center"
              justifyContent="center"
            />
            <MenuList bg="gray.800" borderColor="gray.600">
              <MenuItem
                onClick={() => handleNavigate('/')}
                bg="gray.800"
                _hover={{ bg: 'gray.700' }}
              >
                Home
              </MenuItem>
              <MenuDivider />
              <MenuItem
                icon={<BellIcon />}
                onClick={onAlertsOpen}
                bg="gray.800"
                _hover={{ bg: 'gray.700' }}
              >
                Alerts
              </MenuItem>
              <MenuDivider />
              {routes.map((route) => (
                <MenuItem
                  key={route.id}
                  onClick={() => handleNavigate(route.path)}
                  bg="gray.800"
                  _hover={{ bg: 'gray.700' }}
                >
                  {route.name} ({route.path})
                </MenuItem>
              ))}
              <MenuItem
                icon={<AddIcon />}
                onClick={() => canAddTemplate ? onOpen() : onLicenseOpen()}
                bg="gray.800"
                _hover={{ bg: 'gray.700' }}
              >
                Add New Page
                {!canAddTemplate && ' (Pro)'}
              </MenuItem>
              <MenuDivider />
              <MenuItem
                icon={<InfoOutlineIcon />}
                onClick={onAboutOpen}
                bg="gray.800"
                _hover={{ bg: 'gray.700' }}
              >
                About
              </MenuItem>
            </MenuList>
          </Menu>
          {children}
          <Box w="1px" h="20px" bg="gray.600" mx={1} flexShrink={0} />
          <Tooltip label="Assistant" placement="bottom" hasArrow>
            <IconButton
              icon={<AssistantIcon />}
              variant="ghost"
              size="sm"
              aria-label="Open assistant"
              color={aiOpen ? 'blue.300' : undefined}
              _hover={{ bg: 'whiteAlpha.200' }}
              onClick={() => setAiOpen(v => !v)}
            />
          </Tooltip>
          <Spacer />
          {/* Header controls - right side */}
          {headerControls.length > 0 && onRenderWidget && (() => {
            const dropIndicatorX = isDropTarget && activeDrag?.mousePos && headerControlsRef.current
              ? activeDrag.mousePos.x - headerControlsRef.current.getBoundingClientRect().left
              : null
            return (
              <Flex ref={headerControlsRef} align="center" gap={2} position="relative">
                {dropIndicatorX !== null && (
                  <Box
                    position="absolute"
                    left={`${dropIndicatorX}px`}
                    top="3px"
                    bottom="3px"
                    width="2px"
                    bg="blue.400"
                    borderRadius="full"
                    pointerEvents="none"
                    zIndex={10}
                  />
                )}
                {[...headerControls].sort((a, b) => (a.x || 0) - (b.x || 0)).map(control => onRenderWidget(control))}
              </Flex>
            )
          })()}
        </Flex>
      </Box>

      <AlertsPanel isOpen={isAlertsOpen} onClose={onAlertsClose} />
      <LicenseModal isOpen={isLicenseOpen} onClose={onLicenseClose} />

      <Modal isOpen={isAboutOpen} onClose={onAboutClose} isCentered size="md">
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader as={Flex} align="center">
            <Text flex={1}>About Limitless Trading</Text>
            <Tooltip label="Copy to clipboard" placement="top">
              <IconButton
                icon={<CopyIcon />}
                size="sm"
                variant="ghost"
                colorScheme="gray"
                aria-label="Copy device info"
                onClick={() => {
                  const info = getDeviceInfo()
                  const text = Object.entries(info).map(([k, v]) => `${k}: ${v}`).join('\n')
                  navigator.clipboard.writeText(`Version: ${appVersion ?? '—'}\n${text}`)
                  toast({ title: 'Copied to clipboard', status: 'success', duration: 2000, isClosable: true })
                }}
              />
            </Tooltip>
            <IconButton
              icon={<CloseIcon boxSize={3} />}
              size="sm"
              variant="ghost"
              colorScheme="gray"
              aria-label="Close"
              onClick={onAboutClose}
            />
          </ModalHeader>
          <ModalBody pb={6}>
            <Text color="gray.300" mb={4}>
              Trading analysis toolkit for Interactive Brokers.
            </Text>
            <Text fontSize="sm" color="gray.500" mb={4}>
              Version{' '}
              <Text as="span" color="gray.300">{appVersion ?? '—'}</Text>
            </Text>
            <Divider borderColor="gray.600" mb={4} />
            <Text fontSize="xs" fontWeight="semibold" color="gray.400" textTransform="uppercase" letterSpacing="wider" mb={2}>
              License
            </Text>
            <Box bg="gray.900" borderRadius="md" p={3} fontSize="xs" color="gray.300" mb={2}>
              <Flex gap={2} mb={1}>
                <Text color="gray.500" minW="140px" flexShrink={0}>plan</Text>
                <Code bg="transparent" fontSize="xs" color={licenseStatus === 'loading' ? 'gray.400' : isPro ? 'green.300' : 'gray.200'}>
                  {licenseStatus === 'loading' ? '—' : isPro ? 'Pro' : 'Free'}
                </Code>
              </Flex>
              <Flex gap={2}>
                <Text color="gray.500" minW="140px" flexShrink={0}>email</Text>
                <Code bg="transparent" color="gray.200" fontSize="xs" wordBreak="break-all">
                  {customerEmail ?? '—'}
                </Code>
              </Flex>
            </Box>
            <Text
              fontSize="xs"
              color="blue.400"
              cursor="pointer"
              mb={4}
              onClick={() => { onAboutClose(); onLicenseOpen() }}
            >
              Manage license →
            </Text>
            <Divider borderColor="gray.600" mb={4} />
            <Text fontSize="xs" fontWeight="semibold" color="gray.400" textTransform="uppercase" letterSpacing="wider" mb={2}>
              Device Info
            </Text>
            <Box bg="gray.900" borderRadius="md" p={3} fontSize="xs" color="gray.300">
              {Object.entries(getDeviceInfo()).map(([key, value]) => (
                <Flex key={key} gap={2} mb={1}>
                  <Text color="gray.500" minW="140px" flexShrink={0}>{key}</Text>
                  <Code bg="transparent" color="gray.200" fontSize="xs" wordBreak="break-all">{value}</Code>
                </Flex>
              ))}
            </Box>
          </ModalBody>
        </ModalContent>
      </Modal>

      <Modal isOpen={isOpen} onClose={handleModalClose}>
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Add New Page</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <FormControl mb={4}>
              <FormLabel>Page Name</FormLabel>
              <Input
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
                placeholder="My Page"
              />
            </FormControl>
            <FormControl>
              <FormLabel>Path</FormLabel>
              <Input
                value={newPath}
                onChange={(e) => setNewPath(e.target.value)}
                placeholder="/my-page"
              />
            </FormControl>
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={handleModalClose}>
              Cancel
            </Button>
            <Button
              colorScheme="blue"
              onClick={handleCreateRoute}
              isLoading={isCreating}
            >
              Create
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </>
  )
}

export default PageNavMenu
