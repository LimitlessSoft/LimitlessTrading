import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { Box, Flex, Heading, Button, VStack, Select, HStack, Text, Tooltip, Input, InputGroup, InputLeftElement, useDisclosure } from '@chakra-ui/react'
import { AddIcon, LockIcon, SearchIcon } from '@chakra-ui/icons'
import PageNavMenu from '../components/PageNavMenu'
import FeedbackCard from '../components/FeedbackCard'
import NewFeedbackModal from '../components/NewFeedbackModal'
import { useLicense } from '../modules/LicenseContext'

function FeedbackPage() {
  const [items, setItems] = useState([])
  const [category, setCategory] = useState('')
  const [status, setStatus] = useState('open')
  const [sort, setSort] = useState('date')
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const { isOpen, onOpen, onClose } = useDisclosure()
  const navigate = useNavigate()
  const { isPro } = useLicense()

  const load = async () => {
    setLoading(true)
    try {
      const params = new URLSearchParams()
      if (category) params.set('category', category)
      if (status) params.set('status', status)
      if (sort) params.set('sort', sort)
      if (search) params.set('search', search)
      const res = await fetch(`/api/feedback?${params}`)
      const data = await res.json()
      setItems(data)
    } catch {
      setItems([])
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    const t = setTimeout(() => setSearch(searchInput.trim()), 300)
    return () => clearTimeout(t)
  }, [searchInput])

  useEffect(() => { load() }, [category, status, sort, search])

  const handleUpvote = async (id) => {
    try {
      const res = await fetch(`/api/feedback/${id}/upvote`, { method: 'POST' })
      const data = await res.json()
      setItems(prev => prev.map(item => item.id === id ? { ...item, upvotes: data.upvotes, hasUpvoted: data.hasUpvoted } : item))
    } catch {}
  }

  return (
    <Box minH="100vh" bg="gray.900">
      <PageNavMenu />
      <Box maxW="800px" mx="auto" px={6} py={8}>
        <Flex align="center" mb={6}>
          <Heading size="lg" color="gray.100" flex={1}>Feedback</Heading>
          <Tooltip
            label="A Pro license is required to submit feedback. Activate yours from the menu → About → Manage license."
            isDisabled={isPro}
            placement="left"
            hasArrow
          >
            <Button
              leftIcon={isPro ? <AddIcon /> : <LockIcon />}
              colorScheme="blue"
              size="sm"
              onClick={isPro ? onOpen : undefined}
              opacity={isPro ? 1 : 0.5}
              cursor={isPro ? 'pointer' : 'not-allowed'}
            >
              New Feedback
            </Button>
          </Tooltip>
        </Flex>

        <InputGroup mb={4}>
          <InputLeftElement pointerEvents="none">
            <SearchIcon color="gray.500" />
          </InputLeftElement>
          <Input
            value={searchInput}
            onChange={e => setSearchInput(e.target.value)}
            placeholder="Search feedback..."
            bg="gray.800"
            borderColor="gray.600"
            _hover={{ borderColor: 'gray.500' }}
            _focus={{ borderColor: 'blue.500' }}
          />
        </InputGroup>

        <HStack mb={6} spacing={3}>
          <Select size="sm" value={category} onChange={e => setCategory(e.target.value)} bg="gray.800" borderColor="gray.600" w="160px">
            <option value="">All categories</option>
            <option value="feature">Feature</option>
            <option value="bug">Bug</option>
            <option value="improvement">Improvement</option>
          </Select>
          <Select size="sm" value={status} onChange={e => setStatus(e.target.value)} bg="gray.800" borderColor="gray.600" w="160px">
            <option value="">All statuses</option>
            <option value="open">Open</option>
            <option value="in-review">In Review</option>
            <option value="planned">Planned</option>
            <option value="done">Done</option>
            <option value="rejected">Rejected</option>
          </Select>
          <Select size="sm" value={sort} onChange={e => setSort(e.target.value)} bg="gray.800" borderColor="gray.600" w="140px">
            <option value="date">Newest first</option>
            <option value="votes">Most voted</option>
          </Select>
        </HStack>

        {loading ? (
          <Text color="gray.500" textAlign="center" py={12}>Loading...</Text>
        ) : items.length === 0 ? (
          <Text color="gray.500" textAlign="center" py={12}>
            {search ? `No results for "${search}"` : 'No feedback yet. Be the first to submit!'}
          </Text>
        ) : (
          <VStack spacing={3} align="stretch">
            {items.map(item => (
              <FeedbackCard
                key={item.id}
                item={item}
                onClick={() => navigate(`/feedback/${item.id}`)}
                onUpvote={handleUpvote}
                isPro={isPro}
              />
            ))}
          </VStack>
        )}
      </Box>

      <NewFeedbackModal isOpen={isOpen} onClose={onClose} onCreated={item => setItems(prev => [item, ...prev])} />
    </Box>
  )
}

export default FeedbackPage
