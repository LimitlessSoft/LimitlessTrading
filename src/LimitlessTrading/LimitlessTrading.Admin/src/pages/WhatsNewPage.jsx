import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Box,
  Button,
  Flex,
  Heading,
  HStack,
  IconButton,
  Spinner,
  Table,
  Tbody,
  Td,
  Text,
  Th,
  Thead,
  Tr
} from '@chakra-ui/react'
import { DeleteIcon, EditIcon } from '@chakra-ui/icons'
import { deleteEntry, fetchEntries, logout } from '../api'
import WhatsNewModal from '../components/WhatsNewModal'
import ChangePasswordModal from '../components/ChangePasswordModal'

export default function WhatsNewPage() {
  const [entries, setEntries] = useState([])
  const [loading, setLoading] = useState(true)
  const [modalOpen, setModalOpen] = useState(false)
  const [editEntry, setEditEntry] = useState(null)
  const [pwModalOpen, setPwModalOpen] = useState(false)
  const navigate = useNavigate()

  async function load() {
    setLoading(true)
    try {
      const data = await fetchEntries()
      setEntries(data)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  function handleNew() {
    setEditEntry(null)
    setModalOpen(true)
  }

  function handleEdit(entry) {
    setEditEntry(entry)
    setModalOpen(true)
  }

  async function handleDelete(id) {
    if (!window.confirm('Delete this entry?')) return
    await deleteEntry(id)
    await load()
  }

  function handleLogout() {
    logout()
    navigate('/login')
  }

  function handleSaved() {
    setModalOpen(false)
    load()
  }

  return (
    <Box minH="100vh" bg="gray.900" p={6}>
      <Flex mb={6} align="center" justify="space-between">
        <Heading size="md" color="white">What's New</Heading>
        <HStack>
          <Button colorScheme="blue" size="sm" onClick={handleNew}>
            New Entry
          </Button>
          <Button variant="ghost" size="sm" color="gray.400" onClick={() => setPwModalOpen(true)}>
            Change Password
          </Button>
          <Button variant="ghost" colorScheme="gray" size="sm" color="gray.400" onClick={handleLogout}>
            Logout
          </Button>
        </HStack>
      </Flex>

      {loading ? (
        <Flex justify="center" mt={10}>
          <Spinner color="blue.400" />
        </Flex>
      ) : entries.length === 0 ? (
        <Text color="gray.500">No entries yet.</Text>
      ) : (
        <Box overflowX="auto">
          <Table variant="simple" size="sm">
            <Thead>
              <Tr>
                <Th color="gray.400">Version</Th>
                <Th color="gray.400">Title</Th>
                <Th color="gray.400">Published At</Th>
                <Th color="gray.400">Tags</Th>
                <Th color="gray.400"></Th>
              </Tr>
            </Thead>
            <Tbody>
              {entries.map(e => (
                <Tr key={e.id}>
                  <Td color="white">{e.version}</Td>
                  <Td color="white">{e.title}</Td>
                  <Td color="gray.300">{new Date(e.publishedAt).toLocaleDateString()}</Td>
                  <Td color="gray.400">{(e.tags ?? []).join(', ')}</Td>
                  <Td>
                    <HStack spacing={1}>
                      <IconButton
                        icon={<EditIcon />}
                        size="xs"
                        variant="ghost"
                        colorScheme="blue"
                        aria-label="Edit"
                        onClick={() => handleEdit(e)}
                      />
                      <IconButton
                        icon={<DeleteIcon />}
                        size="xs"
                        variant="ghost"
                        colorScheme="red"
                        aria-label="Delete"
                        onClick={() => handleDelete(e.id)}
                      />
                    </HStack>
                  </Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        </Box>
      )}

      <WhatsNewModal
        isOpen={modalOpen}
        onClose={() => setModalOpen(false)}
        onSaved={handleSaved}
        entry={editEntry}
      />
      <ChangePasswordModal isOpen={pwModalOpen} onClose={() => setPwModalOpen(false)} />
    </Box>
  )
}
