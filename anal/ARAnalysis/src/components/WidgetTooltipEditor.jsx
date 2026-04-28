import {
  Box,
  Text,
  IconButton,
  Button,
  FormControl,
  FormLabel,
  Input,
  HStack,
  VStack
} from '@chakra-ui/react'
import { AddIcon, DeleteIcon } from '@chakra-ui/icons'

/**
 * Reusable tooltip configuration editor for widget settings modals.
 *
 * Props:
 *   rows      — [{name, detail, value}]
 *   footer    — string
 *   onChange  — ({rows, footer}) => void
 *
 * All detail/value fields support @{SYMBOL:property:seconds} placeholders.
 */
function WidgetTooltipEditor({ rows = [], footer = '', onChange }) {
  const updateRows = (next) => onChange({ rows: next, footer })
  const updateFooter = (next) => onChange({ rows, footer: next })

  const addRow = () => updateRows([...rows, { name: '', detail: '', value: '' }])

  const removeRow = (i) => updateRows(rows.filter((_, idx) => idx !== i))

  const updateRow = (i, field, val) => {
    const next = [...rows]
    next[i] = { ...next[i], [field]: val }
    updateRows(next)
  }

  return (
    <Box>
      <HStack justify="space-between" mb={2}>
        <FormLabel fontSize="sm" mb={0}>Tooltip Rows</FormLabel>
        <Button size="xs" leftIcon={<AddIcon />} onClick={addRow}>Add</Button>
      </HStack>
      <Text fontSize="xs" color="gray.500" mb={2}>
        Detail and value fields support @{'{SYMBOL:property:seconds}'} placeholders.
      </Text>

      <VStack spacing={2} align="stretch" mb={4}>
        {rows.map((row, i) => (
          <Box key={i} p={2} bg="gray.700" borderRadius="md">
            <VStack spacing={2}>
              <HStack spacing={2} width="100%">
                <Input
                  size="sm"
                  placeholder="Name"
                  value={row.name}
                  onChange={(e) => updateRow(i, 'name', e.target.value)}
                  flex={1}
                />
                <IconButton
                  icon={<DeleteIcon />}
                  size="xs"
                  variant="ghost"
                  colorScheme="red"
                  onClick={() => removeRow(i)}
                  aria-label="Remove row"
                />
              </HStack>
              <Input
                size="sm"
                placeholder="Detail  e.g. VIX: @{VIX:last:5}"
                value={row.detail}
                onChange={(e) => updateRow(i, 'detail', e.target.value)}
                fontFamily="monospace"
              />
              <Input
                size="sm"
                placeholder="Value  e.g. @{VIX:last:5}"
                value={row.value}
                onChange={(e) => updateRow(i, 'value', e.target.value)}
                fontFamily="monospace"
              />
            </VStack>
          </Box>
        ))}
      </VStack>

      <FormControl>
        <FormLabel fontSize="sm">Tooltip Footer</FormLabel>
        <Input
          value={footer}
          onChange={(e) => updateFooter(e.target.value)}
          placeholder="Optional footer — supports @{} placeholders"
          fontFamily="monospace"
          fontSize="sm"
        />
      </FormControl>
    </Box>
  )
}

export default WidgetTooltipEditor
