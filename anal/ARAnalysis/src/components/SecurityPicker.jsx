import { useState, useEffect } from 'react'
import {
  Input,
  Select,
  InputGroup,
  InputLeftAddon,
  HStack,
  IconButton,
  Tooltip
} from '@chakra-ui/react'
import { StarIcon } from '@chakra-ui/icons'
import { getFavorites, addFavorite, removeFavorite, isFavorite } from '../db/database'

function SecurityPicker({ value, onChange, securities }) {
  const [inputValue, setInputValue] = useState(value)
  const [favorites, setFavorites] = useState([])
  const [isCurrentFavorite, setIsCurrentFavorite] = useState(false)

  // Load favorites on mount
  useEffect(() => {
    loadFavorites()
  }, [])

  // Check if current symbol is favorite when value changes
  useEffect(() => {
    if (value) {
      isFavorite(value).then(setIsCurrentFavorite)
    } else {
      setIsCurrentFavorite(false)
    }
  }, [value])

  const loadFavorites = async () => {
    const favs = await getFavorites()
    setFavorites(favs)
  }

  const handleInputChange = (e) => {
    setInputValue(e.target.value.toUpperCase())
  }

  const handleInputKeyDown = (e) => {
    if (e.key === 'Enter' && inputValue.trim()) {
      onChange(inputValue.trim())
    }
  }

  const handleInputBlur = () => {
    if (inputValue.trim() && inputValue.trim() !== value) {
      onChange(inputValue.trim())
    }
  }

  const handleSelectChange = (e) => {
    const symbol = e.target.value
    if (symbol) {
      setInputValue(symbol)
      onChange(symbol)
    }
  }

  const handleToggleFavorite = async () => {
    if (!value) return

    if (isCurrentFavorite) {
      await removeFavorite(value)
      setIsCurrentFavorite(false)
    } else {
      await addFavorite(value)
      setIsCurrentFavorite(true)
    }
    loadFavorites()
  }

  return (
    <HStack spacing={2}>
      <InputGroup size="md" w="200px">
        <InputLeftAddon
          bg="gray.700"
          color="gray.300"
          borderColor="gray.600"
          borderRightRadius={0}
        >
          Symbol
        </InputLeftAddon>
        <Input
          placeholder="e.g. AAPL"
          value={inputValue}
          onChange={handleInputChange}
          onKeyDown={handleInputKeyDown}
          onBlur={handleInputBlur}
          bg="gray.800"
          borderColor="gray.600"
          borderLeftRadius={0}
          _hover={{ borderColor: 'gray.500' }}
          _focus={{ borderColor: 'blue.400', boxShadow: 'none' }}
        />
      </InputGroup>

      <Tooltip label={isCurrentFavorite ? 'Remove from favorites' : 'Add to favorites'}>
        <IconButton
          aria-label="Toggle favorite"
          icon={<StarIcon />}
          size="md"
          variant="outline"
          colorScheme={isCurrentFavorite ? 'yellow' : 'gray'}
          color={isCurrentFavorite ? 'yellow.400' : 'gray.500'}
          borderColor={isCurrentFavorite ? 'yellow.400' : 'gray.600'}
          onClick={handleToggleFavorite}
          isDisabled={!value}
          _hover={{
            borderColor: isCurrentFavorite ? 'yellow.300' : 'gray.500',
            color: isCurrentFavorite ? 'yellow.300' : 'gray.400'
          }}
        />
      </Tooltip>

      {favorites.length > 0 && (
        <Select
          placeholder="Favorites"
          size="md"
          w="140px"
          value=""
          onChange={handleSelectChange}
          bg="gray.800"
          borderColor="gray.600"
          _hover={{ borderColor: 'gray.500' }}
        >
          {favorites.map(symbol => (
            <option key={symbol} value={symbol}>
              {symbol}
            </option>
          ))}
        </Select>
      )}

      {securities.length > 0 && (
        <Select
          placeholder="Recent"
          size="md"
          w="120px"
          value=""
          onChange={handleSelectChange}
          bg="gray.800"
          borderColor="gray.600"
          _hover={{ borderColor: 'gray.500' }}
        >
          {securities.map(sec => (
            <option key={sec.symbol} value={sec.symbol}>
              {sec.symbol}
            </option>
          ))}
        </Select>
      )}
    </HStack>
  )
}

export default SecurityPicker
