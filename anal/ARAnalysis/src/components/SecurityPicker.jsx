import { useState, useEffect, useRef } from 'react'
import {
  Input,
  InputGroup,
  InputLeftAddon,
  InputRightElement,
  HStack,
  IconButton,
  Tooltip,
  Menu,
  MenuButton,
  MenuList,
  MenuItem,
  MenuDivider,
  Button,
  Text,
  Portal
} from '@chakra-ui/react'
import { StarIcon, EditIcon, ChevronDownIcon, TimeIcon } from '@chakra-ui/icons'
import {
  getWatchgroups,
  getWatchgroupSymbols,
  addToWatchgroup,
  removeFromWatchgroup,
  getSymbolWatchgroups,
  getActiveWatchgroup,
  setActiveWatchgroup
} from '../api/userDataService'
import WatchgroupManager from './WatchgroupManager'

function SecurityPicker({ value, onChange, securities }) {
  const [inputValue, setInputValue] = useState(value)
  const [isInActiveGroup, setIsInActiveGroup] = useState(false)
  const [activeGroupId, setActiveGroupId] = useState(null)
  const [activeGroupSymbols, setActiveGroupSymbols] = useState([])
  const [watchgroups, setWatchgroups] = useState([])
  const [refreshKey, setRefreshKey] = useState(0)
  const managerRef = useRef(null)

  // Load watchgroups and active group on mount
  useEffect(() => {
    loadWatchgroups()
  }, [refreshKey])

  const loadWatchgroups = async () => {
    const groups = await getWatchgroups()
    setWatchgroups(groups)

    const savedActiveGroup = await getActiveWatchgroup()
    if (savedActiveGroup && groups.some(g => g.id === savedActiveGroup)) {
      setActiveGroupId(savedActiveGroup)
    } else if (groups.length > 0) {
      setActiveGroupId(groups[0].id)
      await setActiveWatchgroup(groups[0].id)
    } else {
      setActiveGroupId(null)
    }
  }

  // Sync input when value prop changes
  useEffect(() => {
    setInputValue(value)
  }, [value])

  // Check if current symbol is in the active watchgroup
  useEffect(() => {
    if (value && activeGroupId) {
      getSymbolWatchgroups(value).then(groups => {
        setIsInActiveGroup(groups.some(g => g.id === activeGroupId))
      })
    } else {
      setIsInActiveGroup(false)
    }
  }, [value, activeGroupId, refreshKey])

  // Load symbols in the active watchgroup
  useEffect(() => {
    if (activeGroupId) {
      getWatchgroupSymbols(activeGroupId).then(setActiveGroupSymbols)
    } else {
      setActiveGroupSymbols([])
    }
  }, [activeGroupId, refreshKey])

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

  const handleSymbolSelect = (symbol) => {
    setInputValue(symbol)
    onChange(symbol)
  }

  const handleToggleWatchgroup = async () => {
    if (!value || !activeGroupId) return

    if (isInActiveGroup) {
      await removeFromWatchgroup(activeGroupId, value)
      setIsInActiveGroup(false)
      setActiveGroupSymbols(prev => prev.filter(s => s !== value.toUpperCase()))
    } else {
      await addToWatchgroup(activeGroupId, value)
      setIsInActiveGroup(true)
      setActiveGroupSymbols(prev => [...prev, value.toUpperCase()])
    }
  }

  const handleActiveGroupChange = (groupId) => {
    setActiveGroupId(groupId)
  }

  const handleGroupsChange = () => {
    setRefreshKey(k => k + 1)
  }

  const handleOpenManager = () => {
    managerRef.current?.open()
  }

  const activeGroup = watchgroups.find(g => g.id === activeGroupId)

  return (
    <HStack spacing={2}>
      <InputGroup size="md" w="240px">
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
          pr="40px"
          _hover={{ borderColor: 'gray.500' }}
          _focus={{ borderColor: 'blue.400', boxShadow: 'none' }}
        />
        {securities.length > 0 && (
          <InputRightElement>
            <Menu>
              <MenuButton
                as={IconButton}
                icon={<TimeIcon />}
                size="xs"
                variant="ghost"
                color="gray.400"
                _hover={{ color: 'gray.200' }}
                aria-label="Recent symbols"
              />
              <Portal>
                <MenuList bg="gray.700" maxH="300px" overflowY="auto" minW="120px" zIndex={1400}>
                  <Text px={3} py={1} fontSize="xs" color="gray.400">Recent</Text>
                  <MenuDivider borderColor="gray.600" />
                  {securities.map(sec => (
                    <MenuItem
                      key={sec.symbol}
                      bg="transparent"
                      _hover={{ bg: 'gray.600' }}
                      onClick={() => handleSymbolSelect(sec.symbol)}
                    >
                      {sec.symbol}
                    </MenuItem>
                  ))}
                </MenuList>
              </Portal>
            </Menu>
          </InputRightElement>
        )}
      </InputGroup>

      <Tooltip label={
        !activeGroupId
          ? 'Select a watchgroup first'
          : isInActiveGroup
            ? 'Remove from watchgroup'
            : 'Add to watchgroup'
      }>
        <IconButton
          aria-label="Toggle watchgroup"
          icon={<StarIcon />}
          size="md"
          variant="outline"
          colorScheme={isInActiveGroup ? 'yellow' : 'gray'}
          color={isInActiveGroup ? 'yellow.400' : 'gray.500'}
          borderColor={isInActiveGroup ? 'yellow.400' : 'gray.600'}
          onClick={handleToggleWatchgroup}
          isDisabled={!value || !activeGroupId}
          _hover={{
            borderColor: isInActiveGroup ? 'yellow.300' : 'gray.500',
            color: isInActiveGroup ? 'yellow.300' : 'gray.400'
          }}
        />
      </Tooltip>

      <HStack spacing={0}>
        <Menu>
          <MenuButton
            as={Button}
            size="md"
            variant="outline"
            colorScheme={activeGroupId ? 'yellow' : 'gray'}
            rightIcon={<ChevronDownIcon />}
            borderRightRadius={0}
            minW="130px"
          >
            {activeGroup?.name || 'No Group'}
          </MenuButton>
          <MenuList bg="gray.700" minW="160px">
            {watchgroups.length > 0 ? (
              watchgroups.map(group => (
                <MenuItem
                  key={group.id}
                  bg="transparent"
                  fontWeight={group.id === activeGroupId ? 'bold' : 'normal'}
                  color={group.id === activeGroupId ? 'yellow.400' : 'inherit'}
                  _hover={{ bg: 'gray.600' }}
                  onClick={async () => {
                    setActiveGroupId(group.id)
                    await setActiveWatchgroup(group.id)
                  }}
                >
                  {group.name}
                </MenuItem>
              ))
            ) : (
              <MenuItem isDisabled bg="transparent">No watchgroups</MenuItem>
            )}
          </MenuList>
        </Menu>
        <Tooltip label="Manage watchgroups">
          <IconButton
            aria-label="Manage watchgroups"
            icon={<EditIcon />}
            size="md"
            variant="outline"
            colorScheme="yellow"
            borderLeftRadius={0}
            borderLeft="none"
            onClick={handleOpenManager}
          />
        </Tooltip>
      </HStack>

      <WatchgroupManager
        ref={managerRef}
        onActiveGroupChange={handleActiveGroupChange}
        onGroupsChange={handleGroupsChange}
      />
    </HStack>
  )
}

export default SecurityPicker
