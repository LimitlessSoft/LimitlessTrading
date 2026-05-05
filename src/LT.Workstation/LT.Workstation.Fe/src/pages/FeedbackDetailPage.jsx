import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { Box, Flex, Heading, Text, Badge, Button, Select, Tooltip, IconButton } from '@chakra-ui/react'
import { ChevronUpIcon, ArrowBackIcon, LockIcon } from '@chakra-ui/icons'
import PageNavMenu from '../components/PageNavMenu'
import FeedbackCommentThread from '../components/FeedbackCommentThread'
import { useLicense } from '../modules/LicenseContext'

function PinIcon() {
  return (
    <svg viewBox="0 0 24 24" width="12" height="12" fill="currentColor">
      <path d="M16 3a1 1 0 0 1 .7 1.7L15 6.4l.6 4.6H18a1 1 0 0 1 .7 1.7l-5 5a1 1 0 0 1-1.7-.7v-2.5L8.4 16l-.7.7a1 1 0 0 1-1.4-1.4l.7-.7 1.4-3.6H6a1 1 0 0 1-.7-1.7l5-5A1 1 0 0 1 11 4h4.6L15 3.3A1 1 0 0 1 16 3z" />
    </svg>
  )
}

const CATEGORY_COLORS = { feature: 'blue', bug: 'red', improvement: 'purple' }
const STATUS_COLORS = { open: 'gray', 'in-review': 'yellow', planned: 'cyan', done: 'green', rejected: 'red' }
const STATUSES = ['open', 'in-review', 'planned', 'done', 'rejected']

function FeedbackDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { isPro, customerEmail } = useLicense()
  const [item, setItem] = useState(null)
  const [comments, setComments] = useState([])
  const [loading, setLoading] = useState(true)

  const isAdmin = customerEmail === 'aristiccitsira@gmail.com'

  useEffect(() => {
    fetch(`/api/feedback/${id}`)
      .then(r => r.json())
      .then(data => {
        setItem(data)
        setComments(data.comments ?? [])
      })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [id])

  const handleUpvote = async () => {
    try {
      const res = await fetch(`/api/feedback/${id}/upvote`, { method: 'POST' })
      const data = await res.json()
      setItem(prev => ({ ...prev, upvotes: data.upvotes, hasUpvoted: data.hasUpvoted }))
    } catch {}
  }

  const handleTogglePin = async () => {
    try {
      const res = await fetch(`/api/feedback/${id}/pin`, { method: 'POST' })
      const data = await res.json()
      setItem(prev => ({ ...prev, isPinned: data.isPinned }))
    } catch {}
  }

  const handleStatusChange = async (newStatus) => {
    try {
      const res = await fetch(`/api/feedback/${id}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status: newStatus })
      })
      const data = await res.json()
      setItem(prev => ({ ...prev, status: data.status }))
    } catch {}
  }

  const handleNewComment = (comment) => {
    setComments(prev => {
      if (comment.parentCommentId) {
        return prev.map(c => c.id === comment.parentCommentId
          ? { ...c, replies: [...(c.replies ?? []), comment] }
          : c)
      }
      return [...prev, { ...comment, replies: [] }]
    })
  }

  if (loading) return (
    <Box minH="100vh" bg="gray.900"><PageNavMenu /><Text color="gray.500" textAlign="center" py={12}>Loading...</Text></Box>
  )

  if (!item) return (
    <Box minH="100vh" bg="gray.900"><PageNavMenu /><Text color="gray.500" textAlign="center" py={12}>Not found.</Text></Box>
  )

  return (
    <Box minH="100vh" bg="gray.900">
      <PageNavMenu />
      <Box maxW="800px" mx="auto" px={6} py={8}>
        <Button leftIcon={<ArrowBackIcon />} variant="ghost" colorScheme="gray" size="sm" mb={6} onClick={() => navigate('/feedback')}>
          Back to Feedback
        </Button>

        <Box bg="gray.800" border="1px solid" borderColor="gray.700" borderRadius="md" p={6} mb={6}>
          <Flex gap={2} mb={3} flexWrap="wrap" align="center">
            <Badge colorScheme={CATEGORY_COLORS[item.category] ?? 'gray'}>{item.category}</Badge>
            <Badge colorScheme={STATUS_COLORS[item.status] ?? 'gray'} variant="outline">{item.status}</Badge>
            {item.isPinned && <Badge colorScheme="yellow" variant="subtle">Pinned</Badge>}
            {isAdmin && (
              <>
                <Select size="xs" value={item.status} onChange={e => handleStatusChange(e.target.value)} bg="gray.700" borderColor="gray.600" w="140px" ml={2}>
                  {STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
                </Select>
                <Tooltip label={item.isPinned ? 'Unpin' : 'Pin to top'} placement="top" hasArrow>
                  <IconButton
                    icon={<PinIcon />}
                    size="xs"
                    variant={item.isPinned ? 'solid' : 'outline'}
                    colorScheme="yellow"
                    aria-label={item.isPinned ? 'Unpin' : 'Pin'}
                    onClick={handleTogglePin}
                  />
                </Tooltip>
              </>
            )}
          </Flex>

          <Heading size="md" color="gray.100" mb={3}>{item.title}</Heading>
          <Text color="gray.300" whiteSpace="pre-wrap" mb={4}>{item.description}</Text>

          <Flex align="center" gap={3}>
            <Tooltip
              label="A Pro license is required to upvote. Activate yours from the menu → About → Manage license."
              isDisabled={isPro}
              placement="top"
              hasArrow
            >
              <Flex
                align="center"
                gap={1}
                cursor={isPro ? 'pointer' : 'not-allowed'}
                opacity={isPro ? 1 : 0.5}
                onClick={isPro ? handleUpvote : undefined}
              >
                {isPro
                  ? <ChevronUpIcon color={item.hasUpvoted ? 'blue.300' : 'gray.500'} boxSize={5} />
                  : <LockIcon color="gray.500" boxSize={3.5} />
                }
                <Text fontSize="sm" color={item.hasUpvoted ? 'blue.300' : 'gray.400'} fontWeight="semibold">{item.upvotes} votes</Text>
              </Flex>
            </Tooltip>
            <Text fontSize="xs" color="gray.600">{item.authorId} · {new Date(item.createdAt).toLocaleDateString()}</Text>
          </Flex>
        </Box>

        <Heading size="sm" color="gray.300" mb={4}>{comments.length} comment{comments.length !== 1 ? 's' : ''}</Heading>
        <FeedbackCommentThread feedbackId={id} comments={comments} onCommentsChange={handleNewComment} isPro={isPro} />
      </Box>
    </Box>
  )
}

export default FeedbackDetailPage
