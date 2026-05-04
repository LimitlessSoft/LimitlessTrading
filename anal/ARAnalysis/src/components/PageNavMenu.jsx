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
  useDisclosure,
  useToast
} from '@chakra-ui/react'
import { HamburgerIcon, AddIcon, BellIcon, InfoOutlineIcon } from '@chakra-ui/icons'
import { fetchRoutes, createRoute } from '../modules/routesService'
import { useLicense } from '../modules/LicenseContext'
import { FREE_LIMITS } from '../modules/freeLimits'
import AlertsPanel from './AlertsPanel'
import LicenseModal from './LicenseModal'

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
  const { isPro } = useLicense()
  const canAddTemplate = isPro || routes.length < FREE_LIMITS.templates
  const { isOpen, onOpen, onClose } = useDisclosure()
  const { isOpen: isAlertsOpen, onOpen: onAlertsOpen, onClose: onAlertsClose } = useDisclosure()
  const { isOpen: isLicenseOpen, onOpen: onLicenseOpen, onClose: onLicenseClose } = useDisclosure()
  const { isOpen: isAboutOpen, onOpen: onAboutOpen, onClose: onAboutClose } = useDisclosure()
  const navigate = useNavigate()
  const toast = useToast()
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
        <Flex align="center">
          <Menu>
            <MenuButton
              as={IconButton}
              icon={<HamburgerIcon />}
              variant="ghost"
              size="sm"
              aria-label="Navigation menu"
              _hover={{ bg: 'whiteAlpha.200' }}
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

      <Modal isOpen={isAboutOpen} onClose={onAboutClose} isCentered size="sm">
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>About ARAnalysis</ModalHeader>
          <ModalCloseButton />
          <ModalBody pb={6}>
            <Text color="gray.300" mb={4}>
              Trading analysis toolkit for Interactive Brokers.
            </Text>
            <Text fontSize="sm" color="gray.500">
              Version{' '}
              <Text as="span" color="gray.300">{appVersion ?? '—'}</Text>
            </Text>
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
