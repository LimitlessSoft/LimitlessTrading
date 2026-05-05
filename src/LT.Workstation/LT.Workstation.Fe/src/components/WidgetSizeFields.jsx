import { FormControl, FormLabel, HStack, NumberInput, NumberInputField, NumberInputStepper, NumberIncrementStepper, NumberDecrementStepper, Text } from '@chakra-ui/react'

export default function WidgetSizeFields({ width, height, minHeight = 40, onChange }) {
  return (
    <FormControl>
      <FormLabel fontSize="sm">Size</FormLabel>
      <HStack spacing={4}>
        <HStack spacing={2} flex={1}>
          <Text fontSize="xs" color="gray.400" whiteSpace="nowrap">Width (cols)</Text>
          <NumberInput
            value={width}
            min={1}
            max={24}
            step={1}
            size="sm"
            onChange={(_, val) => !isNaN(val) && onChange({ width: val, height })}
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
            onChange={(_, val) => !isNaN(val) && onChange({ width, height: val })}
          >
            <NumberInputField />
            <NumberInputStepper>
              <NumberIncrementStepper />
              <NumberDecrementStepper />
            </NumberInputStepper>
          </NumberInput>
        </HStack>
      </HStack>
    </FormControl>
  )
}
