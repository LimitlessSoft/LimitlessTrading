import { useState, useEffect } from 'react'
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
  Select,
  Input,
  IconButton,
  Text,
  Switch,
  FormControl,
  FormLabel,
  useDisclosure,
  Table,
  Thead,
  Tbody,
  Tr,
  Th,
  Td,
  Badge,
  Divider,
  Menu,
  MenuButton,
  MenuList,
  MenuItem,
  useToast
} from '@chakra-ui/react'
import { AddIcon, DeleteIcon, EditIcon, SettingsIcon, ChevronDownIcon, CopyIcon } from '@chakra-ui/icons'
import {
  getConditionalFormats,
  addConditionalFormat,
  updateConditionalFormat,
  deleteConditionalFormat,
  getFormatGroups,
  addFormatGroup,
  updateFormatGroup,
  deleteFormatGroup,
  getActiveFormatGroup,
  setActiveFormatGroup
} from '../api/userDataService'

const COLUMNS = [
  { value: 'open', label: 'Open' },
  { value: 'high', label: 'High' },
  { value: 'low', label: 'Low' },
  { value: 'close', label: 'Close' },
  { value: 'volume', label: 'Volume' },
  { value: 'change', label: 'Change (Close - Open)' },
  { value: 'changePercent', label: 'Change %' },
  { value: 'gap', label: 'Gap %' },
  { value: 'highVsPrevClose', label: 'High vs Prev Close %' },
  { value: 'lowVsPrevClose', label: 'Low vs Prev Close %' },
  { value: 'first5m', label: 'First 5min %' },
  { value: 'first10m', label: 'First 10min %' },
  { value: 'first15m', label: 'First 15min %' },
  { value: 'first30m', label: 'First 30min %' },
  { value: 'first1h', label: 'First 1hr %' },
  { value: 'vixOpen', label: 'VIX Open' },
  { value: 'vixClose', label: 'VIX Close' },
  { value: 'prevVixOpen', label: 'Prev VIX Open' },
  { value: 'prevVixClose', label: 'Prev VIX Close' }
]

const OPERATORS = [
  { value: '>', label: '>' },
  { value: '<', label: '<' },
  { value: '>=', label: '>=' },
  { value: '<=', label: '<=' },
  { value: '==', label: '=' },
  { value: '!=', label: '!=' },
  { value: 'between', label: 'Between' },
  { value: '>%', label: '> by %' },
  { value: '<%', label: '< by %' },
  { value: '>=%', label: '>= by %' },
  { value: '<=%', label: '<= by %' }
]

const COMPARE_OPTIONS = [
  { value: '', label: 'Value' },
  { value: 'open', label: 'Open' },
  { value: 'high', label: 'High' },
  { value: 'low', label: 'Low' },
  { value: 'close', label: 'Close' },
  { value: 'volume', label: 'Volume' },
  { value: 'gap', label: 'Gap %' },
  { value: 'highVsPrevClose', label: 'High vs Prev Close %' },
  { value: 'lowVsPrevClose', label: 'Low vs Prev Close %' },
  { value: 'first5m', label: 'First 5min %' },
  { value: 'first10m', label: 'First 10min %' },
  { value: 'first15m', label: 'First 15min %' },
  { value: 'first30m', label: 'First 30min %' },
  { value: 'first1h', label: 'First 1hr %' },
  { value: 'vixOpen', label: 'VIX Open' },
  { value: 'vixClose', label: 'VIX Close' },
  { value: 'prevOpen', label: 'Prev Open' },
  { value: 'prevHigh', label: 'Prev High' },
  { value: 'prevLow', label: 'Prev Low' },
  { value: 'prevClose', label: 'Prev Close' },
  { value: 'prevVolume', label: 'Prev Volume' },
  { value: 'prevGap', label: 'Prev Gap %' },
  { value: 'prevHighVsPrevClose', label: 'Prev High vs Prev Close %' },
  { value: 'prevLowVsPrevClose', label: 'Prev Low vs Prev Close %' },
  { value: 'prevFirst5m', label: 'Prev First 5min %' },
  { value: 'prevFirst10m', label: 'Prev First 10min %' },
  { value: 'prevFirst15m', label: 'Prev First 15min %' },
  { value: 'prevFirst30m', label: 'Prev First 30min %' },
  { value: 'prevFirst1h', label: 'Prev First 1hr %' },
  { value: 'prevVixOpen', label: 'Prev VIX Open' },
  { value: 'prevVixClose', label: 'Prev VIX Close' }
]

const PRESET_COLORS = [
  { bg: '#22543d', text: '#68d391', label: 'Green' },
  { bg: '#742a2a', text: '#fc8181', label: 'Red' },
  { bg: '#744210', text: '#f6e05e', label: 'Yellow' },
  { bg: '#2a4365', text: '#63b3ed', label: 'Blue' },
  { bg: '#553c9a', text: '#b794f4', label: 'Purple' },
  { bg: 'transparent', text: '#68d391', label: 'Green Text' },
  { bg: 'transparent', text: '#fc8181', label: 'Red Text' }
]

const DEFAULT_CONDITION = {
  column: 'close',
  operator: '>',
  value: 0,
  value2: 0,
  compareToColumn: ''
}

const DEFAULT_RULE = {
  name: '',
  conditions: [{ ...DEFAULT_CONDITION }],
  enabled: true,
  format: {
    bgColor: '#22543d',
    textColor: '#68d391',
    bold: false
  }
}

function ConditionalFormatManager({ onRulesChange, clearRef }) {
  const { isOpen, onOpen, onClose } = useDisclosure()
  const [rules, setRules] = useState([])
  const [groups, setGroups] = useState([])
  const [activeGroupId, setActiveGroupId] = useState(null)
  const [editingRule, setEditingRule] = useState(null)
  const [isEditing, setIsEditing] = useState(false)
  const [newGroupName, setNewGroupName] = useState('')
  const [editingGroupId, setEditingGroupId] = useState(null)
  const [editingGroupName, setEditingGroupName] = useState('')
  const toast = useToast()

  useEffect(() => {
    loadGroups()
  }, [])

  useEffect(() => {
    loadRules()
  }, [activeGroupId])

  // Expose clear function to parent via ref
  useEffect(() => {
    if (clearRef) {
      clearRef.current = {
        clearSelection: () => {
          setActiveGroupId(null)
          setActiveFormatGroup(null)
          setRules([])
          onRulesChange?.([])
        }
      }
    }
  }, [clearRef, onRulesChange])

  const loadGroups = async () => {
    const loadedGroups = await getFormatGroups()
    setGroups(loadedGroups)

    const savedActiveGroup = await getActiveFormatGroup()
    if (savedActiveGroup && loadedGroups.some(g => g.id === savedActiveGroup)) {
      setActiveGroupId(savedActiveGroup)
    } else {
      // Default to None (no formatting)
      setActiveGroupId(null)
      onRulesChange?.([])
    }
  }

  const loadRules = async () => {
    if (!activeGroupId) {
      setRules([])
      onRulesChange?.([])
      return
    }

    const loaded = await getConditionalFormats(activeGroupId)
    // Migrate old rules that don't have conditions array
    const migrated = loaded.map(rule => {
      if (!rule.conditions) {
        return {
          ...rule,
          conditions: [{
            column: rule.column || 'close',
            operator: rule.operator || '>',
            value: rule.value || 0,
            value2: rule.value2 || 0,
            compareToColumn: rule.compareToColumn || ''
          }]
        }
      }
      return rule
    })
    setRules(migrated)
    onRulesChange?.(migrated)
  }

  const handleCreateGroup = async () => {
    if (!newGroupName.trim()) return
    const id = await addFormatGroup(newGroupName.trim())
    setNewGroupName('')
    await loadGroups()
    setActiveGroupId(id)
    await setActiveFormatGroup(id)
    toast({ title: 'Group created', status: 'success', duration: 2000 })
  }

  const handleSelectGroup = async (groupId) => {
    setActiveGroupId(groupId)
    await setActiveFormatGroup(groupId)
  }

  const handleRenameGroup = async () => {
    if (!editingGroupName.trim() || !editingGroupId) return
    await updateFormatGroup(editingGroupId, { name: editingGroupName.trim() })
    setEditingGroupId(null)
    setEditingGroupName('')
    await loadGroups()
    toast({ title: 'Group renamed', status: 'success', duration: 2000 })
  }

  const handleDeleteGroup = async (groupId) => {
    await deleteFormatGroup(groupId)
    await loadGroups()
    toast({ title: 'Group deleted', status: 'info', duration: 2000 })
  }

  const handleDuplicateGroup = async (group) => {
    // Create new group with "Copy of" prefix
    const newGroupId = await addFormatGroup(`Copy of ${group.name}`)

    // Get all rules from the original group
    const originalRules = await getConditionalFormats(group.id)

    // Copy each rule to the new group
    for (const rule of originalRules) {
      const { id, groupId, ...ruleData } = rule
      await addConditionalFormat({ ...ruleData, groupId: newGroupId })
    }

    await loadGroups()
    setActiveGroupId(newGroupId)
    await setActiveFormatGroup(newGroupId)
    toast({ title: 'Group duplicated', status: 'success', duration: 2000 })
  }

  const handleAddRule = () => {
    if (!activeGroupId) {
      toast({ title: 'Create a group first', status: 'warning', duration: 2000 })
      return
    }
    setEditingRule({ ...DEFAULT_RULE, conditions: [{ ...DEFAULT_CONDITION }], groupId: activeGroupId })
    setIsEditing(false)
    onOpen()
  }

  const handleEditRule = (rule) => {
    const ruleWithConditions = {
      ...rule,
      conditions: rule.conditions || [{
        column: rule.column || 'close',
        operator: rule.operator || '>',
        value: rule.value || 0,
        value2: rule.value2 || 0,
        compareToColumn: rule.compareToColumn || ''
      }]
    }
    setEditingRule(ruleWithConditions)
    setIsEditing(true)
    onOpen()
  }

  const handleDeleteRule = async (id) => {
    await deleteConditionalFormat(id)
    loadRules()
  }

  const handleToggleRule = async (rule) => {
    await updateConditionalFormat(rule.id, { enabled: !rule.enabled })
    loadRules()
  }

  const handleSaveRule = async () => {
    if (!editingRule.name) {
      editingRule.name = `Rule ${rules.length + 1}`
    }

    if (isEditing) {
      await updateConditionalFormat(editingRule.id, editingRule)
    } else {
      await addConditionalFormat({ ...editingRule, groupId: activeGroupId })
    }

    setEditingRule(null)
    loadRules()
  }

  const handlePresetColor = (preset) => {
    setEditingRule({
      ...editingRule,
      format: {
        ...editingRule.format,
        bgColor: preset.bg,
        textColor: preset.text
      }
    })
  }

  const handleAddCondition = () => {
    setEditingRule({
      ...editingRule,
      conditions: [...editingRule.conditions, { ...DEFAULT_CONDITION }]
    })
  }

  const handleRemoveCondition = (index) => {
    if (editingRule.conditions.length <= 1) return
    setEditingRule({
      ...editingRule,
      conditions: editingRule.conditions.filter((_, i) => i !== index)
    })
  }

  const handleConditionChange = (index, field, value) => {
    const newConditions = [...editingRule.conditions]
    newConditions[index] = { ...newConditions[index], [field]: value }
    setEditingRule({ ...editingRule, conditions: newConditions })
  }

  const getColumnLabel = (col) => COLUMNS.find(c => c.value === col)?.label || col
  const getOperatorLabel = (op) => OPERATORS.find(o => o.value === op)?.label || op
  const getCompareLabel = (col) => COMPARE_OPTIONS.find(c => c.value === col)?.label || col

  const formatConditionText = (cond) => {
    const col = getColumnLabel(cond.column)
    const op = getOperatorLabel(cond.operator)
    let compare
    if (cond.compareToColumn) {
      compare = getCompareLabel(cond.compareToColumn)
      if (cond.operator?.endsWith('%')) {
        compare += ` ${cond.value}%`
      }
    } else if (cond.operator === 'between') {
      compare = `${cond.value} - ${cond.value2}`
    } else {
      compare = cond.value
    }
    return `${col} ${op} ${compare}`
  }

  const activeGroup = groups.find(g => g.id === activeGroupId)

  return (
    <>
      <HStack spacing={2}>
        <Menu>
          <MenuButton
            as={Button}
            size="sm"
            variant="outline"
            colorScheme={activeGroupId ? "purple" : "gray"}
            rightIcon={<ChevronDownIcon />}
          >
            {activeGroup?.name || 'None'}
          </MenuButton>
          <MenuList bg="gray.700">
            <MenuItem
              bg={activeGroupId === null ? 'gray.600' : 'transparent'}
              _hover={{ bg: 'gray.600' }}
              onClick={() => handleSelectGroup(null)}
              fontStyle="italic"
            >
              None (no formatting)
            </MenuItem>
            {groups.length > 0 && <Box borderBottom="1px solid" borderColor="gray.600" my={1} />}
            {groups.map(group => (
              <MenuItem
                key={group.id}
                bg={group.id === activeGroupId ? 'gray.600' : 'transparent'}
                _hover={{ bg: 'gray.600' }}
                onClick={() => handleSelectGroup(group.id)}
              >
                {group.name}
              </MenuItem>
            ))}
            {groups.length === 0 && (
              <MenuItem isDisabled>No groups yet</MenuItem>
            )}
          </MenuList>
        </Menu>

        <IconButton
          aria-label="Conditional Formatting"
          icon={<SettingsIcon />}
          size="sm"
          variant="outline"
          colorScheme="purple"
          onClick={onOpen}
        />
      </HStack>

      <Modal isOpen={isOpen} onClose={onClose} size="xl">
        <ModalOverlay />
        <ModalContent bg="gray.800" maxW="750px">
          <ModalHeader color="white">
            {editingRule ? (isEditing ? 'Edit Rule' : 'New Rule') : 'Conditional Formatting'}
          </ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            {editingRule ? (
              <VStack spacing={4} align="stretch">
                <FormControl>
                  <FormLabel color="gray.300">Rule Name</FormLabel>
                  <Input
                    value={editingRule.name}
                    onChange={(e) => setEditingRule({ ...editingRule, name: e.target.value })}
                    placeholder="e.g., Green when up"
                    bg="gray.700"
                  />
                </FormControl>

                <Box>
                  <HStack justify="space-between" mb={2}>
                    <Text color="gray.300" fontWeight="bold">Conditions (AND)</Text>
                    <Button size="xs" leftIcon={<AddIcon />} onClick={handleAddCondition}>
                      Add Condition
                    </Button>
                  </HStack>

                  <VStack spacing={3} align="stretch">
                    {editingRule.conditions.map((cond, index) => (
                      <Box key={index} p={3} bg="gray.700" borderRadius="md">
                        {index > 0 && (
                          <Badge colorScheme="blue" mb={2}>AND</Badge>
                        )}
                        <HStack spacing={2} wrap="wrap">
                          <Select
                            value={cond.column}
                            onChange={(e) => handleConditionChange(index, 'column', e.target.value)}
                            bg="gray.600"
                            size="sm"
                            w="130px"
                          >
                            {COLUMNS.map(col => (
                              <option key={col.value} value={col.value}>{col.label}</option>
                            ))}
                          </Select>

                          <Select
                            value={cond.operator}
                            onChange={(e) => handleConditionChange(index, 'operator', e.target.value)}
                            bg="gray.600"
                            size="sm"
                            w="100px"
                          >
                            {OPERATORS.map(op => (
                              <option key={op.value} value={op.value}>{op.label}</option>
                            ))}
                          </Select>

                          <Select
                            value={cond.compareToColumn}
                            onChange={(e) => handleConditionChange(index, 'compareToColumn', e.target.value)}
                            bg="gray.600"
                            size="sm"
                            w="130px"
                          >
                            {COMPARE_OPTIONS.map(opt => (
                              <option key={opt.value} value={opt.value}>{opt.label}</option>
                            ))}
                          </Select>

                          {!cond.compareToColumn && !cond.operator?.endsWith('%') && (
                            <Input
                              type="number"
                              value={cond.value}
                              onChange={(e) => handleConditionChange(index, 'value', parseFloat(e.target.value) || 0)}
                              bg="gray.600"
                              size="sm"
                              w="80px"
                            />
                          )}

                          {cond.operator === 'between' && !cond.compareToColumn && (
                            <>
                              <Text color="gray.400" fontSize="sm">and</Text>
                              <Input
                                type="number"
                                value={cond.value2}
                                onChange={(e) => handleConditionChange(index, 'value2', parseFloat(e.target.value) || 0)}
                                bg="gray.600"
                                size="sm"
                                w="80px"
                              />
                            </>
                          )}

                          {cond.operator?.endsWith('%') && cond.compareToColumn && (
                            <HStack spacing={1}>
                              <Input
                                type="number"
                                value={cond.value}
                                onChange={(e) => handleConditionChange(index, 'value', parseFloat(e.target.value) || 0)}
                                bg="gray.600"
                                size="sm"
                                w="70px"
                                placeholder="%"
                              />
                              <Text color="gray.400" fontSize="sm">%</Text>
                            </HStack>
                          )}

                          {editingRule.conditions.length > 1 && (
                            <IconButton
                              aria-label="Remove condition"
                              icon={<DeleteIcon />}
                              size="xs"
                              colorScheme="red"
                              variant="ghost"
                              onClick={() => handleRemoveCondition(index)}
                            />
                          )}
                        </HStack>
                      </Box>
                    ))}
                  </VStack>
                </Box>

                <Divider borderColor="gray.600" />

                <FormControl>
                  <FormLabel color="gray.300">Format Presets</FormLabel>
                  <HStack wrap="wrap" spacing={2}>
                    {PRESET_COLORS.map((preset, i) => (
                      <Button
                        key={i}
                        size="sm"
                        bg={preset.bg === 'transparent' ? 'gray.700' : preset.bg}
                        color={preset.text}
                        onClick={() => handlePresetColor(preset)}
                        _hover={{ opacity: 0.8 }}
                      >
                        {preset.label}
                      </Button>
                    ))}
                  </HStack>
                </FormControl>

                <HStack>
                  <FormControl flex={1}>
                    <FormLabel color="gray.300">Background Color</FormLabel>
                    <Input
                      type="color"
                      value={editingRule.format.bgColor === 'transparent' ? '#1a202c' : editingRule.format.bgColor}
                      onChange={(e) => setEditingRule({
                        ...editingRule,
                        format: { ...editingRule.format, bgColor: e.target.value }
                      })}
                      bg="gray.700"
                      h="40px"
                    />
                  </FormControl>

                  <FormControl flex={1}>
                    <FormLabel color="gray.300">Text Color</FormLabel>
                    <Input
                      type="color"
                      value={editingRule.format.textColor}
                      onChange={(e) => setEditingRule({
                        ...editingRule,
                        format: { ...editingRule.format, textColor: e.target.value }
                      })}
                      bg="gray.700"
                      h="40px"
                    />
                  </FormControl>

                  <FormControl flex={1}>
                    <FormLabel color="gray.300">Bold</FormLabel>
                    <Switch
                      isChecked={editingRule.format.bold}
                      onChange={(e) => setEditingRule({
                        ...editingRule,
                        format: { ...editingRule.format, bold: e.target.checked }
                      })}
                      colorScheme="blue"
                      size="lg"
                      mt={2}
                    />
                  </FormControl>
                </HStack>

                <Box p={3} bg="gray.700" borderRadius="md">
                  <Text color="gray.400" mb={2}>Preview:</Text>
                  <Box
                    display="inline-block"
                    px={3}
                    py={1}
                    bg={editingRule.format.bgColor}
                    color={editingRule.format.textColor}
                    fontWeight={editingRule.format.bold ? 'bold' : 'normal'}
                    borderRadius="sm"
                  >
                    123.45
                  </Box>
                </Box>
              </VStack>
            ) : (
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
                    <Button size="sm" colorScheme="green" onClick={handleCreateGroup}>
                      Create
                    </Button>
                  </HStack>

                  <VStack align="stretch" spacing={2}>
                    {groups.map(group => (
                      <HStack key={group.id} justify="space-between" p={2} bg={group.id === activeGroupId ? 'gray.500' : 'gray.600'} borderRadius="md">
                        {editingGroupId === group.id ? (
                          <HStack flex={1}>
                            <Input
                              value={editingGroupName}
                              onChange={(e) => setEditingGroupName(e.target.value)}
                              size="sm"
                              bg="gray.700"
                              onKeyDown={(e) => e.key === 'Enter' && handleRenameGroup()}
                            />
                            <Button size="xs" colorScheme="green" onClick={handleRenameGroup}>Save</Button>
                            <Button size="xs" onClick={() => setEditingGroupId(null)}>Cancel</Button>
                          </HStack>
                        ) : (
                          <>
                            <Text
                              color="gray.200"
                              cursor="pointer"
                              onClick={() => handleSelectGroup(group.id)}
                              flex={1}
                            >
                              {group.name}
                              {group.id === activeGroupId && <Badge ml={2} colorScheme="green">Active</Badge>}
                            </Text>
                            <HStack spacing={1}>
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
                                aria-label="Duplicate"
                                icon={<CopyIcon />}
                                size="xs"
                                colorScheme="blue"
                                onClick={() => handleDuplicateGroup(group)}
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

                {/* Rules Section */}
                <HStack justify="space-between">
                  <Text color="gray.300" fontWeight="bold">
                    Rules {activeGroup && `(${activeGroup.name})`}
                  </Text>
                  <Button
                    leftIcon={<AddIcon />}
                    colorScheme="blue"
                    size="sm"
                    onClick={handleAddRule}
                    isDisabled={!activeGroupId}
                  >
                    Add Rule
                  </Button>
                </HStack>

                {!activeGroupId ? (
                  <Text color="gray.500" textAlign="center" py={4}>
                    Select or create a group first
                  </Text>
                ) : rules.length === 0 ? (
                  <Text color="gray.500" textAlign="center" py={4}>
                    No rules in this group
                  </Text>
                ) : (
                  <Table size="sm">
                    <Thead>
                      <Tr>
                        <Th color="gray.400">On</Th>
                        <Th color="gray.400">Name</Th>
                        <Th color="gray.400">Conditions</Th>
                        <Th color="gray.400">Format</Th>
                        <Th color="gray.400">Actions</Th>
                      </Tr>
                    </Thead>
                    <Tbody>
                      {rules.map(rule => (
                        <Tr key={rule.id}>
                          <Td>
                            <Switch
                              isChecked={rule.enabled}
                              onChange={() => handleToggleRule(rule)}
                              colorScheme="green"
                              size="sm"
                            />
                          </Td>
                          <Td color="gray.300">{rule.name}</Td>
                          <Td>
                            <VStack align="start" spacing={0}>
                              {(rule.conditions || [{ column: rule.column, operator: rule.operator, value: rule.value, value2: rule.value2, compareToColumn: rule.compareToColumn }]).map((cond, i) => (
                                <Text key={i} color="gray.400" fontSize="xs">
                                  {i > 0 && <Badge colorScheme="blue" size="xs" mr={1}>AND</Badge>}
                                  {formatConditionText(cond)}
                                </Text>
                              ))}
                            </VStack>
                          </Td>
                          <Td>
                            <Badge
                              bg={rule.format.bgColor}
                              color={rule.format.textColor}
                              fontWeight={rule.format.bold ? 'bold' : 'normal'}
                            >
                              Sample
                            </Badge>
                          </Td>
                          <Td>
                            <HStack spacing={1}>
                              <IconButton
                                aria-label="Edit"
                                icon={<EditIcon />}
                                size="xs"
                                onClick={() => handleEditRule(rule)}
                              />
                              <IconButton
                                aria-label="Delete"
                                icon={<DeleteIcon />}
                                size="xs"
                                colorScheme="red"
                                onClick={() => handleDeleteRule(rule.id)}
                              />
                            </HStack>
                          </Td>
                        </Tr>
                      ))}
                    </Tbody>
                  </Table>
                )}
              </VStack>
            )}
          </ModalBody>
          <ModalFooter>
            {editingRule ? (
              <>
                <Button variant="ghost" mr={3} onClick={() => setEditingRule(null)}>
                  Back
                </Button>
                <Button colorScheme="blue" onClick={handleSaveRule}>
                  Save Rule
                </Button>
              </>
            ) : (
              <Button variant="ghost" onClick={onClose}>
                Close
              </Button>
            )}
          </ModalFooter>
        </ModalContent>
      </Modal>
    </>
  )
}

export default ConditionalFormatManager
