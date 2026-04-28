import {
  Box,
  Button,
  HStack,
  VStack,
  Text,
  Input,
  Select,
  Checkbox,
  IconButton,
  Divider
} from '@chakra-ui/react'
import { AddIcon, DeleteIcon } from '@chakra-ui/icons'
import { newConditionalRule } from '../modules/conditionalStyles'

const ALL_FIELDS = [
  { label: 'Background', key: 'bgColor', type: 'color', default: '#4A5568' },
  { label: 'Text', key: 'textColor', type: 'color', default: '#FFFFFF' },
  { label: 'Border color', key: 'borderColor', type: 'color', default: '#718096' },
  { label: 'Border px', key: 'borderWidth', type: 'number', default: 1 },
]

/**
 * Reusable conditional styles editor section for widget settings modals.
 *
 * @param {object[]} rules - Current conditionalStyles array from the widget's style.
 * @param {(rules: object[]) => void} onChange - Called with the updated rules array.
 * @param {string[]} [fields] - Which style keys to expose. Defaults to all four.
 *   Pass ['bgColor','borderColor','borderWidth'] to hide the text color field.
 */
function ConditionalStylesSection({ rules, onChange, fields }) {
  const activeFields = !fields
    ? ALL_FIELDS
    : typeof fields[0] === 'string'
      ? ALL_FIELDS.filter(f => fields.includes(f.key))
      : fields

  const handleAdd = () => onChange([...(rules || []), newConditionalRule()])

  const handleUpdate = (idx, changes) => {
    const updated = [...rules]
    updated[idx] = { ...updated[idx], ...changes }
    onChange(updated)
  }

  const handleDelete = (idx) => onChange(rules.filter((_, i) => i !== idx))

  return (
    <Box width="100%">
      <Divider mb={4} />
      <HStack justify="space-between" mb={2}>
        <Text fontSize="sm" fontWeight="medium">Conditional Styles</Text>
        <Button size="xs" leftIcon={<AddIcon />} variant="ghost" onClick={handleAdd}>
          Add Rule
        </Button>
      </HStack>
      <VStack spacing={2} align="stretch">
        {(rules || []).map((rule, idx) => (
          <Box key={rule.id} p={3} bg="gray.700" borderRadius="md">
            <HStack mb={2} spacing={2}>
              <Text fontSize="xs" color="gray.400" flexShrink={0}>When</Text>
              <Input
                size="xs"
                placeholder="${EXCHANGE:status:10}"
                value={rule.placeholder}
                onChange={(e) => handleUpdate(idx, { placeholder: e.target.value })}
                flex={1}
              />
              <Select
                size="xs"
                value={rule.operator ?? (rule.contains ? 'contains' : 'equals')}
                onChange={(e) => handleUpdate(idx, { operator: e.target.value })}
                width="110px"
                flexShrink={0}
              >
                <option value="contains">contains</option>
                <option value="equals">equals</option>
                <option value=">">{'>'}</option>
                <option value="<">{'<'}</option>
                <option value=">=">{'>='}</option>
                <option value="<=">{'<='}</option>
                <option value="!=">{'!='}</option>
              </Select>
              <Input
                size="xs"
                placeholder="Open"
                value={rule.value}
                onChange={(e) => handleUpdate(idx, { value: e.target.value })}
                flex={1}
              />
              <IconButton
                icon={<DeleteIcon />}
                size="xs"
                variant="ghost"
                colorScheme="red"
                aria-label="Delete rule"
                flexShrink={0}
                onClick={() => handleDelete(idx)}
              />
            </HStack>
            <HStack spacing={4} flexWrap="wrap">
              {activeFields.map(({ label, key, type, default: defaultVal }) => (
                <Box key={key}>
                  <HStack spacing={1} mb="2px">
                    <Checkbox
                      isChecked={rule[key] != null}
                      onChange={(e) => handleUpdate(idx, { [key]: e.target.checked ? defaultVal : null })}
                      size="sm"
                    />
                    <Text fontSize="xs" color="gray.400">{label}</Text>
                  </HStack>
                  <Input
                    type={type}
                    min={type === 'number' ? 0 : undefined}
                    max={type === 'number' ? 10 : undefined}
                    value={rule[key] ?? defaultVal}
                    onChange={(e) => handleUpdate(idx, {
                      [key]: type === 'number' ? (parseInt(e.target.value) || 0) : e.target.value
                    })}
                    height="28px"
                    p={type === 'color' ? '2px' : '4px'}
                    width={type === 'color' ? '44px' : '52px'}
                    isDisabled={rule[key] == null}
                  />
                </Box>
              ))}
            </HStack>
          </Box>
        ))}
      </VStack>
    </Box>
  )
}

export default ConditionalStylesSection
