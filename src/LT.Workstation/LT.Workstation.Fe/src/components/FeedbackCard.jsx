import { Box, Flex, Text, Badge, IconButton, Tooltip } from '@chakra-ui/react'
import { ChevronUpIcon, LockIcon } from '@chakra-ui/icons'

function PinIcon() {
  return (
    <svg viewBox="0 0 24 24" width="11" height="11" fill="currentColor">
      <path d="M16 3a1 1 0 0 1 .7 1.7L15 6.4l.6 4.6H18a1 1 0 0 1 .7 1.7l-5 5a1 1 0 0 1-1.7-.7v-2.5L8.4 16l-.7.7a1 1 0 0 1-1.4-1.4l.7-.7 1.4-3.6H6a1 1 0 0 1-.7-1.7l5-5A1 1 0 0 1 11 4h4.6L15 3.3A1 1 0 0 1 16 3z" />
    </svg>
  )
}

const CATEGORY_COLORS = { feature: 'blue', bug: 'red', improvement: 'purple' }
const STATUS_COLORS = { open: 'gray', 'in-review': 'yellow', planned: 'cyan', done: 'green', rejected: 'red' }

function FeedbackCard({ item, onClick, onUpvote, isPro }) {
  return (
    <Box
      bg="gray.800"
      border="1px solid"
      borderColor="gray.700"
      borderRadius="md"
      p={4}
      cursor="pointer"
      _hover={{ borderColor: 'gray.500' }}
      onClick={onClick}
      transition="border-color 0.15s"
    >
      <Flex align="flex-start" gap={3}>
        <Tooltip
          label="A Pro license is required to upvote"
          isDisabled={isPro}
          placement="top"
          hasArrow
        >
          <Flex
            direction="column"
            align="center"
            minW="40px"
            onClick={e => { e.stopPropagation(); if (isPro) onUpvote(item.id) }}
            cursor={isPro ? 'pointer' : 'not-allowed'}
            opacity={isPro ? 1 : 0.5}
          >
            <IconButton
              icon={isPro ? <ChevronUpIcon /> : <LockIcon boxSize={3} />}
              size="xs"
              variant="ghost"
              colorScheme={item.hasUpvoted ? 'blue' : 'gray'}
              aria-label="Upvote"
              isDisabled={!isPro}
              _hover={{ bg: isPro ? 'whiteAlpha.200' : 'transparent' }}
              _disabled={{ opacity: 1 }}
            />
            <Text fontSize="xs" color={item.hasUpvoted ? 'blue.300' : 'gray.400'} fontWeight="semibold">{item.upvotes}</Text>
          </Flex>
        </Tooltip>
        <Box flex={1}>
          <Flex gap={2} mb={1} flexWrap="wrap">
            <Badge colorScheme={CATEGORY_COLORS[item.category] ?? 'gray'} fontSize="xs">{item.category}</Badge>
            <Badge colorScheme={STATUS_COLORS[item.status] ?? 'gray'} variant="outline" fontSize="xs">{item.status}</Badge>
          </Flex>
          <Flex align="center" gap={2} mb={1}>
            <Text color="gray.100" fontWeight="semibold">{item.title}</Text>
            {item.isPinned && (
              <Flex align="center" gap={1} color="yellow.400">
                <PinIcon />
                <Text fontSize="xs" fontWeight="semibold">Pinned</Text>
              </Flex>
            )}
          </Flex>
          <Text color="gray.400" fontSize="sm" noOfLines={2}>{item.description}</Text>
          <Text color="gray.600" fontSize="xs" mt={2}>{new Date(item.createdAt).toLocaleDateString()}</Text>
        </Box>
      </Flex>
    </Box>
  )
}

export default FeedbackCard
