import { Select, InputGroup, InputLeftAddon } from '@chakra-ui/react'

function IntervalPicker({ value, onChange, intervals }) {
  const handleChange = (e) => {
    onChange(e.target.value)
  }

  return (
    <InputGroup size="md" w="280px">
      <InputLeftAddon
        bg="gray.700"
        color="gray.300"
        borderColor="gray.600"
        borderRightRadius={0}
      >
        Interval
      </InputLeftAddon>
      <Select
        value={value}
        onChange={handleChange}
        bg="gray.800"
        borderColor="gray.600"
        borderLeftRadius={0}
        _hover={{ borderColor: 'gray.500' }}
      >
        {intervals.map(int => (
          <option key={int.value} value={int.value}>
            {int.label}
          </option>
        ))}
      </Select>
    </InputGroup>
  )
}

export default IntervalPicker
