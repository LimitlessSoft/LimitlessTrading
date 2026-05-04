import { useState, useEffect } from 'react'
import {
  Modal,
  ModalOverlay,
  ModalContent,
  ModalHeader,
  ModalBody,
  ModalFooter,
  ModalCloseButton,
  VStack,
  HStack,
  Box,
  Text,
  Badge,
  Divider,
  Button,
  IconButton,
  Input,
  Select,
  Switch,
  Checkbox,
  CheckboxGroup,
  Stack,
  FormControl,
  FormLabel,
  FormHelperText,
  Collapse,
  useToast,
} from '@chakra-ui/react'
import { BellIcon, AddIcon, DeleteIcon, SmallAddIcon } from '@chakra-ui/icons'
import {
  createAlert,
  removeAlert,
  setAlertEnabled,
  listAlerts,
  getNextRun,
  isValidCron,
  subscribeToAlertList,
} from '../modules/alertService'

const ACTION_COLORS = { message: 'blue', sound: 'purple', event: 'orange' }

const CONDITION_TYPES = [
  { value: 'cron', label: 'Cron schedule' },
  { value: 'expression', label: 'Expression' },
]

const OPERATORS = [
  { value: '>', label: '>' },
  { value: '<', label: '<' },
  { value: '>=', label: '>=' },
  { value: '<=', label: '<=' },
  { value: '==', label: '==' },
  { value: '!=', label: '!=' },
  { value: 'contains', label: 'contains' },
  { value: 'not-contains', label: 'not contains' },
]

function blankCondition(type = 'expression') {
  return type === 'cron'
    ? { type: 'cron', expression: '' }
    : { type: 'expression', expression: '', operator: '>', value: '' }
}

const BLANK_FORM = {
  label: '',
  combinator: 'and',
  conditions: [blankCondition('expression')],
  actions: ['sound', 'message'],
  message: '',
  eventName: '',
}

function conditionSummary(c) {
  if (c.type === 'cron') return c.expression || '—'
  return `${c.expression || '?'} ${c.operator} ${c.value}`
}

function scheduleSummary(schedule) {
  if (!schedule) return '—'
  if (schedule.type === 'combined') {
    return schedule.conditions.map(conditionSummary).join(` ${schedule.combinator.toUpperCase()} `)
  }
  return conditionSummary(schedule)
}

function formatNextRun(expression) {
  const next = getNextRun(expression)
  if (!next) return null
  return next.toLocaleString([], { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}

function ConditionRow({ condition, index, onChange, onRemove, showRemove }) {
  const cronValid = condition.type === 'cron' ? isValidCron(condition.expression) : true
  const set = (patch) => onChange(index, { ...condition, ...patch })

  return (
    <Box borderWidth="1px" borderColor="gray.600" borderRadius="md" p={3}>
      <HStack mb={2} justify="space-between">
        <Select
          size="xs"
          w="150px"
          value={condition.type}
          onChange={e => onChange(index, blankCondition(e.target.value))}
        >
          {CONDITION_TYPES.map(ct => (
            <option key={ct.value} value={ct.value}>{ct.label}</option>
          ))}
        </Select>
        {showRemove && (
          <IconButton
            icon={<DeleteIcon />}
            size="xs"
            variant="ghost"
            colorScheme="red"
            aria-label="Remove condition"
            onClick={() => onRemove(index)}
          />
        )}
      </HStack>

      {condition.type === 'cron' && (
        <FormControl isInvalid={condition.expression !== '' && !cronValid}>
          <Input
            size="sm"
            fontFamily="mono"
            placeholder="* * * * *"
            value={condition.expression}
            onChange={e => set({ expression: e.target.value })}
          />
          {cronValid && condition.expression ? (
            <FormHelperText fontSize="xs" color="blue.300">
              Next: {formatNextRun(condition.expression)}
            </FormHelperText>
          ) : condition.expression !== '' ? (
            <FormHelperText fontSize="xs" color="red.400">Invalid expression</FormHelperText>
          ) : (
            <FormHelperText fontSize="xs" color="gray.500">
              min hour day month weekday
            </FormHelperText>
          )}
        </FormControl>
      )}

      {condition.type === 'expression' && (
        <VStack align="stretch" spacing={2}>
          <Input
            size="sm"
            fontFamily="mono"
            placeholder="@{AAPL:last:30}"
            value={condition.expression}
            onChange={e => set({ expression: e.target.value })}
          />
          <HStack>
            <Select
              size="sm"
              w="130px"
              value={condition.operator}
              onChange={e => set({ operator: e.target.value })}
            >
              {OPERATORS.map(op => (
                <option key={op.value} value={op.value}>{op.label}</option>
              ))}
            </Select>
            <Input
              size="sm"
              placeholder="value"
              value={condition.value}
              onChange={e => set({ value: e.target.value })}
            />
          </HStack>
        </VStack>
      )}
    </Box>
  )
}

export default function AlertsPanel({ isOpen, onClose }) {
  const [alerts, setAlerts] = useState(() => listAlerts())
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState(BLANK_FORM)
  const toast = useToast()

  const refresh = () => setAlerts(listAlerts())
  useEffect(() => subscribeToAlertList(refresh), [])

  const handleDelete = (id) => { removeAlert(id); refresh() }

  const setCondition = (index, updated) =>
    setForm(f => ({ ...f, conditions: f.conditions.map((c, i) => i === index ? updated : c) }))

  const removeCondition = (index) =>
    setForm(f => ({ ...f, conditions: f.conditions.filter((_, i) => i !== index) }))

  const addCondition = () =>
    setForm(f => ({ ...f, conditions: [...f.conditions, blankCondition('expression')] }))

  const handleCreate = () => {
    const label = form.label.trim()
    if (!label) {
      toast({ title: 'Label is required', status: 'error', duration: 2000 })
      return
    }
    for (const c of form.conditions) {
      if (c.type === 'cron' && (!c.expression.trim() || !isValidCron(c.expression))) {
        toast({ title: 'Invalid cron expression', status: 'error', duration: 2000 })
        return
      }
      if (c.type === 'expression' && !c.expression.trim()) {
        toast({ title: 'Expression is required', status: 'error', duration: 2000 })
        return
      }
    }
    if (form.actions.length === 0) {
      toast({ title: 'Select at least one action', status: 'error', duration: 2000 })
      return
    }
    if (form.actions.includes('message') && !form.message.trim()) {
      toast({ title: 'Message text is required', status: 'error', duration: 2000 })
      return
    }
    if (form.actions.includes('event') && !form.eventName.trim()) {
      toast({ title: 'Event name is required', status: 'error', duration: 2000 })
      return
    }

    const schedule = form.conditions.length === 1
      ? { ...form.conditions[0] }
      : { type: 'combined', combinator: form.combinator, conditions: form.conditions }

    const message = form.message.trim()
    createAlert({
      label,
      schedule,
      actions: form.actions,
      message: message || undefined,
      getMessage: message ? () => message : undefined,
      eventName: form.eventName.trim() || undefined,
    })

    refresh()
    setForm(BLANK_FORM)
    setShowForm(false)
    toast({ title: 'Alert created', status: 'success', duration: 2000 })
  }

  const handleClose = () => { setShowForm(false); setForm(BLANK_FORM); onClose() }

  return (
    <Modal isOpen={isOpen} onClose={handleClose} size="md">
      <ModalOverlay />
      <ModalContent bg="gray.800">
        <ModalHeader>
          <HStack spacing={2}>
            <BellIcon color="blue.300" />
            <Text>Alerts</Text>
          </HStack>
        </ModalHeader>
        <ModalCloseButton />

        <ModalBody pb={2}>
          {alerts.length === 0 ? (
            <Text color="gray.400" fontSize="sm">No alerts. Create one below.</Text>
          ) : (
            <VStack align="stretch" spacing={0} divider={<Divider borderColor="gray.700" />}>
              {alerts.map(alert => {
                const cronExpr = alert.schedule?.type === 'cron' ? alert.schedule.expression : null
                const next = cronExpr ? formatNextRun(cronExpr) : null
                return (
                  <HStack key={alert.id} py={3} align="flex-start" justify="space-between">
                    <Box flex={1} opacity={alert.enabled === false ? 0.45 : 1}>
                      <Text fontWeight="semibold" fontSize="sm" color="white">
                        {alert.label || alert.id}
                      </Text>
                      <Text fontSize="xs" color="gray.400" mt={0.5} fontFamily="mono">
                        {scheduleSummary(alert.schedule)}
                      </Text>
                      {alert.enabled !== false && next && (
                        <Text fontSize="xs" color="gray.500" mt={0.5}>
                          Next: <Text as="span" color="blue.300">{next}</Text>
                        </Text>
                      )}
                      {alert.eventName && (
                        <Text fontSize="xs" color="gray.500" mt={0.5}>
                          Event: <Text as="span" color="orange.300">{alert.eventName}</Text>
                        </Text>
                      )}
                      <HStack spacing={1} mt={1.5}>
                        {(alert.actions || []).map(action => (
                          <Badge key={action} colorScheme={ACTION_COLORS[action] || 'gray'} fontSize="2xs">
                            {action}
                          </Badge>
                        ))}
                      </HStack>
                    </Box>
                    <HStack spacing={1} mt={0.5}>
                      <Switch
                        size="sm"
                        isChecked={alert.enabled !== false}
                        onChange={e => { setAlertEnabled(alert.id, e.target.checked); refresh() }}
                        colorScheme="blue"
                      />
                      <IconButton
                        icon={<DeleteIcon />}
                        size="xs"
                        variant="ghost"
                        colorScheme="red"
                        aria-label="Delete alert"
                        onClick={() => handleDelete(alert.id)}
                      />
                    </HStack>
                  </HStack>
                )
              })}
            </VStack>
          )}

          <Divider borderColor="gray.700" mt={alerts.length > 0 ? 2 : 0} mb={3} />

          {!showForm ? (
            <Button leftIcon={<AddIcon />} size="sm" variant="ghost" colorScheme="blue" onClick={() => setShowForm(true)}>
              Add Alert
            </Button>
          ) : (
            <Collapse in={showForm}>
              <VStack align="stretch" spacing={3}>
                <Text fontSize="sm" fontWeight="semibold" color="gray.300">New Alert</Text>

                <FormControl>
                  <FormLabel fontSize="xs" color="gray.400">Label</FormLabel>
                  <Input
                    size="sm"
                    placeholder="e.g. Price alert"
                    value={form.label}
                    onChange={e => setForm(f => ({ ...f, label: e.target.value }))}
                  />
                </FormControl>

                {/* Condition list */}
                <Box>
                  <FormLabel fontSize="xs" color="gray.400">Conditions</FormLabel>
                  <VStack align="stretch" spacing={2}>
                    {form.conditions.map((c, i) => (
                      <Box key={i}>
                        <ConditionRow
                          condition={c}
                          index={i}
                          onChange={setCondition}
                          onRemove={removeCondition}
                          showRemove={form.conditions.length > 1}
                        />
                        {i < form.conditions.length - 1 && (
                          <HStack justify="center" my={1}>
                            <Button
                              size="xs"
                              variant="ghost"
                              colorScheme={form.combinator === 'and' ? 'blue' : 'orange'}
                              onClick={() => setForm(f => ({ ...f, combinator: f.combinator === 'and' ? 'or' : 'and' }))}
                              px={3}
                            >
                              {form.combinator.toUpperCase()}
                            </Button>
                          </HStack>
                        )}
                      </Box>
                    ))}
                    <Button
                      leftIcon={<SmallAddIcon />}
                      size="xs"
                      variant="ghost"
                      colorScheme="gray"
                      alignSelf="flex-start"
                      onClick={addCondition}
                    >
                      Add condition
                    </Button>
                  </VStack>
                </Box>

                <FormControl>
                  <FormLabel fontSize="xs" color="gray.400">Actions</FormLabel>
                  <CheckboxGroup
                    value={form.actions}
                    onChange={vals => setForm(f => ({ ...f, actions: vals }))}
                  >
                    <Stack direction="row" spacing={4}>
                      <Checkbox value="sound" size="sm">Sound</Checkbox>
                      <Checkbox value="message" size="sm">Message</Checkbox>
                      <Checkbox value="event" size="sm">Event</Checkbox>
                    </Stack>
                  </CheckboxGroup>
                </FormControl>

                {form.actions.includes('message') && (
                  <FormControl>
                    <FormLabel fontSize="xs" color="gray.400">Message text</FormLabel>
                    <Input
                      size="sm"
                      placeholder="Alert text to display"
                      value={form.message}
                      onChange={e => setForm(f => ({ ...f, message: e.target.value }))}
                    />
                  </FormControl>
                )}

                {form.actions.includes('event') && (
                  <FormControl>
                    <FormLabel fontSize="xs" color="gray.400">Event name</FormLabel>
                    <Input
                      size="sm"
                      placeholder="e.g. market-open"
                      value={form.eventName}
                      onChange={e => setForm(f => ({ ...f, eventName: e.target.value }))}
                    />
                  </FormControl>
                )}
              </VStack>
            </Collapse>
          )}
        </ModalBody>

        {showForm && (
          <ModalFooter gap={2}>
            <Button size="sm" variant="ghost" onClick={() => { setShowForm(false); setForm(BLANK_FORM) }}>Cancel</Button>
            <Button size="sm" colorScheme="blue" onClick={handleCreate}>Create</Button>
          </ModalFooter>
        )}
      </ModalContent>
    </Modal>
  )
}
