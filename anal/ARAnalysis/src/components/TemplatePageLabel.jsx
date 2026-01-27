import { Box, Text, IconButton } from '@chakra-ui/react'
import { CloseIcon } from '@chakra-ui/icons'

function TemplatePageLabel({ label, onDelete, isEditing }) {
  return (
    <Box
      bg="gray.700"
      px={4}
      py={2}
      borderRadius="md"
      display="flex"
      alignItems="center"
      justifyContent="space-between"
      role="group"
      _hover={{ bg: 'gray.600' }}
    >
      <Text>{label.text}</Text>
      {isEditing && (
        <IconButton
          icon={<CloseIcon />}
          size="xs"
          variant="ghost"
          aria-label="Delete label"
          onClick={() => onDelete(label.id)}
          ml={2}
        />
      )}
    </Box>
  )
}

export default TemplatePageLabel
