import { useState } from 'react'
import { Box, Flex, Text, Textarea, Button, Badge, useToast } from '@chakra-ui/react'
import { LockIcon } from '@chakra-ui/icons'

function Comment({ comment, depth = 0, onReply, isPro }) {
  const [replying, setReplying] = useState(false)
  const [body, setBody] = useState('')
  const [loading, setLoading] = useState(false)
  const toast = useToast()

  const handleReply = async () => {
    if (!body.trim()) return
    setLoading(true)
    try {
      await onReply(comment.id, body.trim())
      setBody('')
      setReplying(false)
    } catch (err) {
      toast({ title: err.message, status: 'error', duration: 3000 })
    } finally {
      setLoading(false)
    }
  }

  return (
    <Box ml={depth > 0 ? 6 : 0} mt={3}>
      <Box
        bg={comment.isOfficialResponse ? 'blue.900' : 'gray.800'}
        border="1px solid"
        borderColor={comment.isOfficialResponse ? 'blue.600' : 'gray.700'}
        borderRadius="md"
        p={3}
      >
        <Flex align="center" gap={2} mb={1}>
          <Text fontSize="xs" color="gray.400">{comment.authorId}</Text>
          {comment.isOfficialResponse && <Badge colorScheme="blue" fontSize="xs">Official Response</Badge>}
          <Text fontSize="xs" color="gray.600">{new Date(comment.createdAt).toLocaleDateString()}</Text>
        </Flex>
        <Text color="gray.200" fontSize="sm" whiteSpace="pre-wrap">{comment.body}</Text>
        {depth === 0 && isPro && (
          <Button size="xs" variant="ghost" colorScheme="gray" mt={2} onClick={() => setReplying(v => !v)}>
            Reply
          </Button>
        )}
        {depth === 0 && !isPro && (
          <Text fontSize="xs" color="gray.600" mt={2} display="flex" alignItems="center" gap={1}>
            <LockIcon boxSize={2.5} /> Pro license required to reply
          </Text>
        )}
      </Box>

      {replying && (
        <Box mt={2} ml={6}>
          <Textarea value={body} onChange={e => setBody(e.target.value)} placeholder="Write a reply..." bg="gray.700" borderColor="gray.600" size="sm" rows={3} />
          <Flex gap={2} mt={2}>
            <Button size="xs" colorScheme="blue" onClick={handleReply} isLoading={loading}>Post</Button>
            <Button size="xs" variant="ghost" onClick={() => setReplying(false)}>Cancel</Button>
          </Flex>
        </Box>
      )}

      {comment.replies?.map(reply => (
        <Comment key={reply.id} comment={reply} depth={depth + 1} onReply={onReply} isPro={isPro} />
      ))}
    </Box>
  )
}

function FeedbackCommentThread({ feedbackId, comments, onCommentsChange, isPro }) {
  const [body, setBody] = useState('')
  const [loading, setLoading] = useState(false)
  const toast = useToast()

  const postComment = async (parentCommentId, commentBody) => {
    const res = await fetch(`/api/feedback/${feedbackId}/comments`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ parentCommentId: parentCommentId ?? null, body: commentBody })
    })
    if (!res.ok) {
      const err = await res.json()
      throw new Error(err.error || 'Failed to post comment')
    }
    const newComment = await res.json()
    onCommentsChange(newComment)
  }

  const handleTopLevelPost = async () => {
    if (!body.trim()) return
    setLoading(true)
    try {
      await postComment(null, body.trim())
      setBody('')
    } catch (err) {
      toast({ title: err.message, status: 'error', duration: 3000 })
    } finally {
      setLoading(false)
    }
  }

  return (
    <Box>
      {comments.map(comment => (
        <Comment key={comment.id} comment={comment} depth={0} onReply={(parentId, text) => postComment(parentId, text)} isPro={isPro} />
      ))}
      <Box mt={4}>
        {isPro ? (
          <>
            <Textarea value={body} onChange={e => setBody(e.target.value)} placeholder="Add a comment..." bg="gray.700" borderColor="gray.600" size="sm" rows={3} />
            <Button mt={2} size="sm" colorScheme="blue" onClick={handleTopLevelPost} isLoading={loading}>Post Comment</Button>
          </>
        ) : (
          <Flex align="center" gap={2} bg="gray.800" border="1px solid" borderColor="gray.700" borderRadius="md" px={4} py={3}>
            <LockIcon color="gray.500" />
            <Text fontSize="sm" color="gray.400">
              A Pro license is required to post comments. Activate yours from the menu → About → Manage license.
            </Text>
          </Flex>
        )}
      </Box>
    </Box>
  )
}

export default FeedbackCommentThread
