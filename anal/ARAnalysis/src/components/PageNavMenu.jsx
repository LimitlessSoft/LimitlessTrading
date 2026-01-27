import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Box,
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
  useDisclosure,
  useToast
} from '@chakra-ui/react'
import { HamburgerIcon, AddIcon } from '@chakra-ui/icons'
import { fetchRoutes, createRoute } from '../modules/routesService'

function PageNavMenu() {
  const [routes, setRoutes] = useState([])
  const [newName, setNewName] = useState('')
  const [newPath, setNewPath] = useState('')
  const [isCreating, setIsCreating] = useState(false)
  const { isOpen, onOpen, onClose } = useDisclosure()
  const navigate = useNavigate()
  const toast = useToast()

  const loadRoutes = async () => {
    try {
      const data = await fetchRoutes()
      setRoutes(data)
    } catch (err) {
      console.error('Failed to load routes:', err)
    }
  }

  useEffect(() => {
    loadRoutes()
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
      <Box display="inline-block">
        <Menu>
          <MenuButton
            as={IconButton}
            icon={<HamburgerIcon />}
            variant="ghost"
            size="sm"
            aria-label="Navigation menu"
            _hover={{ bg: 'gray.700' }}
          />
          <MenuList bg="gray.800" borderColor="gray.600">
            <MenuItem
              onClick={() => handleNavigate('/')}
              bg="gray.800"
              _hover={{ bg: 'gray.700' }}
            >
              Main Page (/)
            </MenuItem>
            {routes.length > 0 && <MenuDivider />}
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
            <MenuDivider />
            <MenuItem
              icon={<AddIcon />}
              onClick={onOpen}
              bg="gray.800"
              _hover={{ bg: 'gray.700' }}
            >
              Add New Page
            </MenuItem>
          </MenuList>
        </Menu>
      </Box>

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
