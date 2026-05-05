import { FormControl, FormLabel, HStack, NumberInput, NumberInputField, NumberInputStepper, NumberIncrementStepper, NumberDecrementStepper, Select, Text, VStack } from '@chakra-ui/react'

export default function WidgetSizeFields({ width, height, minHeight = 40, dock, onChange }) {
  const fillsBoth = dock === 'fill'
  const fillsHeight = dock === 'fill' || dock === 'fill-height'

  return (
    <FormControl>
      <FormLabel fontSize="sm">Size</FormLabel>
      <VStack spacing={3} align="stretch">
        <HStack spacing={2}>
          <Text fontSize="xs" color="gray.400" whiteSpace="nowrap">Dock</Text>
          <Select
            size="sm"
            value={dock || ''}
            onChange={e => onChange({ width, height, dock: e.target.value || undefined })}
          >
            <option value="">None</option>
            <option value="fill-height">Fill height</option>
            <option value="fill">Fill (width + height)</option>
          </Select>
        </HStack>
        <HStack spacing={4}>
          <HStack spacing={2} flex={1}>
            <Text fontSize="xs" color="gray.400" whiteSpace="nowrap">Width (cols)</Text>
            <NumberInput
              value={width}
              min={1}
              max={24}
              step={1}
              size="sm"
              isDisabled={fillsBoth}
              onChange={(_, val) => !isNaN(val) && onChange({ width: val, height, dock })}
            >
              <NumberInputField />
              <NumberInputStepper>
                <NumberIncrementStepper />
                <NumberDecrementStepper />
              </NumberInputStepper>
            </NumberInput>
          </HStack>
          <HStack spacing={2} flex={1}>
            <Text fontSize="xs" color="gray.400" whiteSpace="nowrap">Height (px)</Text>
            <NumberInput
              value={height}
              min={minHeight}
              step={10}
              size="sm"
              isDisabled={fillsHeight}
              onChange={(_, val) => !isNaN(val) && onChange({ width, height: val, dock })}
            >
              <NumberInputField />
              <NumberInputStepper>
                <NumberIncrementStepper />
                <NumberDecrementStepper />
              </NumberInputStepper>
            </NumberInput>
          </HStack>
        </HStack>
      </VStack>
    </FormControl>
  )
}
