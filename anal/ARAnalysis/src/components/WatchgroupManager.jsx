import { useState, useEffect, forwardRef, useImperativeHandle } from 'react'
import {
  Box,
  Button,
  Modal,
  ModalOverlay,
  ModalContent,
  ModalHeader,
  ModalBody,
  ModalFooter,
  ModalCloseButton,
  VStack,
  HStack,
  Input,
  IconButton,
  Text,
  useDisclosure,
  Badge,
  useToast,
  Wrap,
  WrapItem,
  Tag,
  TagLabel,
  TagCloseButton,
  Divider
} from '@chakra-ui/react'
import { AddIcon, DeleteIcon, EditIcon } from '@chakra-ui/icons'
import {
  getWatchgroups,
  createWatchgroup,
  renameWatchgroup,
  deleteWatchgroup,
  getWatchgroupSymbols,
  addToWatchgroup,
  removeFromWatchgroup,
  getActiveWatchgroup,
  setActiveWatchgroup
} from '../db/database'

const WatchgroupManager = forwardRef(({ onActiveGroupChange, onGroupsChange }, ref) => {
  const { isOpen, onOpen, onClose } = useDisclosure()
  const [groups, setGroups] = useState([])
  const [activeGroupId, setActiveGroupIdState] = useState(null)
  const [selectedGroupId, setSelectedGroupId] = useState(null)
  const [groupSymbols, setGroupSymbols] = useState([])
  const [newGroupName, setNewGroupName] = useState('')
  const [newSymbol, setNewSymbol] = useState('')
  const [editingGroupId, setEditingGroupId] = useState(null)
  const [editingGroupName, setEditingGroupName] = useState('')
  const toast = useToast()

  useImperativeHandle(ref, () => ({
    open: onOpen,
    getGroups: () => groups,
    getActiveGroupId: () => activeGroupId
  }))

  useEffect(() => {
    loadGroups()
  }, [])

  useEffect(() => {
    if (selectedGroupId) {
      loadGroupSymbols(selectedGroupId)
    } else {
      setGroupSymbols([])
    }
  }, [selectedGroupId])

  const loadGroups = async () => {
    const loadedGroups = await getWatchgroups()
    setGroups(loadedGroups)
    onGroupsChange?.(loadedGroups)

    const savedActiveGroup = await getActiveWatchgroup()
    if (savedActiveGroup && loadedGroups.some(g => g.id === savedActiveGroup)) {
      setActiveGroupIdState(savedActiveGroup)
      onActiveGroupChange?.(savedActiveGroup)
    } else if (loadedGroups.length > 0) {
      setActiveGroupIdState(loadedGroups[0].id)
      setActiveWatchgroup(loadedGroups[0].id)
      onActiveGroupChange?.(loadedGroups[0].id)
    } else {
      setActiveGroupIdState(null)
      onActiveGroupChange?.(null)
    }
  }

  const loadGroupSymbols = async (groupId) => {
    const symbols = await getWatchgroupSymbols(groupId)
    setGroupSymbols(symbols)
  }

  const handleCreateGroup = async () => {
    if (!newGroupName.trim()) return
    const id = await createWatchgroup(newGroupName.trim())
    setNewGroupName('')
    await loadGroups()
    setSelectedGroupId(id)
    toast({ title: 'Group created', status: 'success', duration: 2000 })
  }

  const handleSelectActiveGroup = async (groupId) => {
    setActiveGroupIdState(groupId)
    await setActiveWatchgroup(groupId)
    onActiveGroupChange?.(groupId)
  }

  const handleRenameGroup = async () => {
    if (!editingGroupName.trim() || !editingGroupId) return
    await renameWatchgroup(editingGroupId, editingGroupName.trim())
    setEditingGroupId(null)
    setEditingGroupName('')
    await loadGroups()
    toast({ title: 'Group renamed', status: 'success', duration: 2000 })
  }

  const handleDeleteGroup = async (groupId) => {
    await deleteWatchgroup(groupId)
    if (selectedGroupId === groupId) {
      setSelectedGroupId(null)
    }
    if (activeGroupId === groupId) {
      const remaining = groups.filter(g => g.id !== groupId)
      if (remaining.length > 0) {
        setActiveGroupIdState(remaining[0].id)
        await setActiveWatchgroup(remaining[0].id)
        onActiveGroupChange?.(remaining[0].id)
      } else {
        setActiveGroupIdState(null)
        await setActiveWatchgroup(null)
        onActiveGroupChange?.(null)
      }
    }
    await loadGroups()
    toast({ title: 'Group deleted', status: 'info', duration: 2000 })
  }

  const handleAddSymbol = async () => {
    if (!newSymbol.trim() || !selectedGroupId) return
    const symbol = newSymbol.trim().toUpperCase()
    await addToWatchgroup(selectedGroupId, symbol)
    setNewSymbol('')
    await loadGroupSymbols(selectedGroupId)
    onGroupsChange?.(groups) // trigger refresh
    toast({ title: `Added ${symbol}`, status: 'success', duration: 1500 })
  }

  const handleRemoveSymbol = async (symbol) => {
    if (!selectedGroupId) return
    await removeFromWatchgroup(selectedGroupId, symbol)
    await loadGroupSymbols(selectedGroupId)
    onGroupsChange?.(groups) // trigger refresh
  }

  const selectedGroup = groups.find(g => g.id === selectedGroupId)

  return (
    <Modal isOpen={isOpen} onClose={onClose} size="xl">
      <ModalOverlay />
      <ModalContent bg="gray.800" maxW="700px">
        <ModalHeader color="white">Watchgroup Manager</ModalHeader>
        <ModalCloseButton />
        <ModalBody>
          <VStack spacing={4} align="stretch">
            {/* Groups Section */}
            <Box p={3} bg="gray.700" borderRadius="md">
              <Text color="gray.300" fontWeight="bold" mb={3}>Groups</Text>

              <HStack mb={3}>
                <Input
                  placeholder="New group name"
                  value={newGroupName}
                  onChange={(e) => setNewGroupName(e.target.value)}
                  bg="gray.600"
                  size="sm"
                  onKeyDown={(e) => e.key === 'Enter' && handleCreateGroup()}
                />
                <Button size="sm" colorScheme="green" onClick={handleCreateGroup} leftIcon={<AddIcon />}>
                  Create
                </Button>
              </HStack>

              <VStack align="stretch" spacing={2}>
                {groups.map(group => (
                  <HStack
                    key={group.id}
                    justify="space-between"
                    p={2}
                    bg={group.id === selectedGroupId ? 'gray.500' : 'gray.600'}
                    borderRadius="md"
                    cursor="pointer"
                    onClick={() => setSelectedGroupId(group.id)}
                    _hover={{ bg: group.id === selectedGroupId ? 'gray.500' : 'gray.550' }}
                  >
                    {editingGroupId === group.id ? (
                      <HStack flex={1} onClick={(e) => e.stopPropagation()}>
                        <Input
                          value={editingGroupName}
                          onChange={(e) => setEditingGroupName(e.target.value)}
                          size="sm"
                          bg="gray.700"
                          onKeyDown={(e) => e.key === 'Enter' && handleRenameGroup()}
                          autoFocus
                        />
                        <Button size="xs" colorScheme="green" onClick={handleRenameGroup}>Save</Button>
                        <Button size="xs" onClick={() => setEditingGroupId(null)}>Cancel</Button>
                      </HStack>
                    ) : (
                      <>
                        <HStack>
                          <Text color="gray.200">{group.name}</Text>
                          {group.id === activeGroupId && (
                            <Badge colorScheme="yellow" fontSize="xs">Active</Badge>
                          )}
                        </HStack>
                        <HStack spacing={1} onClick={(e) => e.stopPropagation()}>
                          <Button
                            size="xs"
                            colorScheme={group.id === activeGroupId ? 'yellow' : 'gray'}
                            variant={group.id === activeGroupId ? 'solid' : 'outline'}
                            onClick={() => handleSelectActiveGroup(group.id)}
                          >
                            {group.id === activeGroupId ? 'Active' : 'Set Active'}
                          </Button>
                          <IconButton
                            aria-label="Rename"
                            icon={<EditIcon />}
                            size="xs"
                            onClick={() => {
                              setEditingGroupId(group.id)
                              setEditingGroupName(group.name)
                            }}
                          />
                          <IconButton
                            aria-label="Delete"
                            icon={<DeleteIcon />}
                            size="xs"
                            colorScheme="red"
                            onClick={() => handleDeleteGroup(group.id)}
                          />
                        </HStack>
                      </>
                    )}
                  </HStack>
                ))}
                {groups.length === 0 && (
                  <Text color="gray.500" fontSize="sm">Create a group to get started</Text>
                )}
              </VStack>
            </Box>

            <Divider borderColor="gray.600" />

            {/* Symbols Section */}
            <Box p={3} bg="gray.700" borderRadius="md">
              <Text color="gray.300" fontWeight="bold" mb={3}>
                Symbols {selectedGroup && `in "${selectedGroup.name}"`}
              </Text>

              {!selectedGroupId ? (
                <Text color="gray.500" fontSize="sm">Select a group to manage symbols</Text>
              ) : (
                <>
                  <HStack mb={3}>
                    <Input
                      placeholder="Add symbol (e.g. AAPL)"
                      value={newSymbol}
                      onChange={(e) => setNewSymbol(e.target.value.toUpperCase())}
                      bg="gray.600"
                      size="sm"
                      onKeyDown={(e) => e.key === 'Enter' && handleAddSymbol()}
                    />
                    <Button size="sm" colorScheme="blue" onClick={handleAddSymbol} leftIcon={<AddIcon />}>
                      Add
                    </Button>
                  </HStack>

                  {groupSymbols.length === 0 ? (
                    <Text color="gray.500" fontSize="sm">No symbols in this group</Text>
                  ) : (
                    <Wrap spacing={2}>
                      {groupSymbols.map(symbol => (
                        <WrapItem key={symbol}>
                          <Tag size="md" colorScheme="blue" borderRadius="full">
                            <TagLabel>{symbol}</TagLabel>
                            <TagCloseButton onClick={() => handleRemoveSymbol(symbol)} />
                          </Tag>
                        </WrapItem>
                      ))}
                    </Wrap>
                  )}
                </>
              )}
            </Box>
          </VStack>
        </ModalBody>
        <ModalFooter>
          <Button variant="ghost" onClick={onClose}>
            Close
          </Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  )
})

WatchgroupManager.displayName = 'WatchgroupManager'

export default WatchgroupManager
