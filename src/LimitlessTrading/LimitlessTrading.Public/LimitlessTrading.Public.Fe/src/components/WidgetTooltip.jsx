import {
  Box,
  Text,
  HStack,
  VStack,
  Popover,
  PopoverTrigger,
  PopoverContent,
  PopoverBody,
  PopoverArrow
} from '@chakra-ui/react'

/**
 * Reusable rich tooltip popover for template widgets.
 *
 * Wrap the widget's trigger element as `children`. Pass pre-resolved strings
 * for title, value, rows, and footer — placeholder resolution stays in the widget.
 *
 * Props:
 *   title    — header left text
 *   value    — header right text (teal accent)
 *   rows     — [{name, detail?, value?}] — detail and value are displayed strings
 *   footer   — optional footer string
 *   placement — Popover placement, default "top"
 *   children — the PopoverTrigger element (must accept a forwarded ref)
 */
function WidgetTooltip({ title, value, rows = [], footer, placement = 'top', children }) {
  return (
    <Popover trigger="hover" placement={placement} isLazy openDelay={300} closeDelay={150}>
      <PopoverTrigger>{children}</PopoverTrigger>
      <PopoverContent
        bg="gray.800"
        borderColor="gray.600"
        width="260px"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <PopoverArrow bg="gray.800" />
        <PopoverBody p={3}>
          <VStack spacing={2} align="stretch">
            <HStack justify="space-between">
              <Text color="white" fontWeight="bold" fontSize="sm">{title}</Text>
              <Text color="teal.300" fontWeight="bold" fontSize="lg">{value}</Text>
            </HStack>

            {rows.map((row, i) => (
              <Box key={i} p={2} bg="gray.700" borderRadius="md" borderLeft="3px solid" borderLeftColor="teal.600">
                <HStack justify="space-between" align="start">
                  <VStack spacing={0} align="start" flex={1} minW={0}>
                    <Text color="gray.300" fontSize="xs" fontWeight="medium" noOfLines={1}>{row.name}</Text>
                    {row.detail && (
                      <Text color="gray.500" fontSize="xs" noOfLines={1}>{row.detail}</Text>
                    )}
                  </VStack>
                  {row.value && (
                    <Text color="teal.300" fontWeight="bold" fontSize="sm" flexShrink={0} ml={2}>
                      {row.value}
                    </Text>
                  )}
                </HStack>
              </Box>
            ))}

            {footer && (
              <Text color="gray.500" fontSize="xs" textAlign="center">{footer}</Text>
            )}
          </VStack>
        </PopoverBody>
      </PopoverContent>
    </Popover>
  )
}

export default WidgetTooltip
