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
import { BellIcon, AddIcon, DeleteIcon } from '@chakra-ui/icons'
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

function formatNextRun(expression) {
  const next = getNextRun(expression)
  if (!next) return null
  return next.toLocaleString([], {
    month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
  })
}

const CONDITION_TYPES = [
  { value: 'cron', label: 'Cron schedule' },
]

const BLANK_FORM = {
  label: '',
  conditionType: 'cron',
  expression: '',
  actions: ['sound', 'message'],
  message: '',
  eventName: '',
}

export default function AlertsPanel({ isOpen, onClose }) {
  const [alerts, setAlerts] = useState(() => listAlerts())
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState(BLANK_FORM)
  const toast = useToast()

  const refresh = () => setAlerts(listAlerts())

  useEffect(() => subscribeToAlertList(refresh), [])

  const cronValid = isValidCron(form.expression)

  const handleDelete = (id) => {
    removeAlert(id)
    refresh()
  }

  const handleCreate = () => {
    const label = form.label.trim()
    if (!label) {
      toast({ title: 'Label is required', status: 'error', duration: 2000 })
      return
    }
    if (form.conditionType === 'cron' && (!form.expression.trim() || !cronValid)) {
      toast({ title: 'Enter a valid cron expression', status: 'error', duration: 2000 })
      return
    }
    if (form.actions.length === 0) {
      toast({ title: 'Select at least one action', status: 'error', duration: 2000 })
      return
    }
    if (form.actions.includes('message') && !form.message.trim()) {
      toast({ title: 'Message text is required when Message action is selected', status: 'error', duration: 2000 })
      return
    }
    if (form.actions.includes('event') && !form.eventName.trim()) {
      toast({ title: 'Event name is required when Event action is selected', status: 'error', duration: 2000 })
      return
    }

    const message = form.message.trim()
    const condition = form.conditionType === 'cron'
      ? { type: 'cron', expression: form.expression.trim() }
      : { type: form.conditionType }
    createAlert({
      label,
      schedule: condition,
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

  const handleClose = () => {
    setShowForm(false)
    setForm(BLANK_FORM)
    onClose()
  }

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
                const next = alert.schedule?.expression
                  ? formatNextRun(alert.schedule.expression)
                  : null
                return (
                  <HStack key={alert.id} py={3} align="flex-start" justify="space-between">
                    <Box flex={1} opacity={alert.enabled === false ? 0.45 : 1}>
                      <Text fontWeight="semibold" fontSize="sm" color="white">
                        {alert.label || alert.id}
                      </Text>
                      <Text fontSize="xs" color="gray.400" mt={0.5} fontFamily="mono">
                        {alert.schedule?.expression || '—'}
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
            <Button
              leftIcon={<AddIcon />}
              size="sm"
              variant="ghost"
              colorScheme="blue"
              onClick={() => setShowForm(true)}
            >
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
                    placeholder="e.g. Hourly reminder"
                    value={form.label}
                    onChange={e => setForm(f => ({ ...f, label: e.target.value }))}
                  />
                </FormControl>

                <FormControl>
                  <FormLabel fontSize="xs" color="gray.400">Condition</FormLabel>
                  <Select
                    size="sm"
                    value={form.conditionType}
                    onChange={e => setForm(f => ({ ...f, conditionType: e.target.value }))}
                  >
                    {CONDITION_TYPES.map(ct => (
                      <option key={ct.value} value={ct.value}>{ct.label}</option>
                    ))}
                  </Select>
                </FormControl>

                {form.conditionType === 'cron' && (
                  <FormControl isInvalid={form.expression !== '' && !cronValid}>
                    <FormLabel fontSize="xs" color="gray.400">Cron expression</FormLabel>
                    <Input
                      size="sm"
                      fontFamily="mono"
                      placeholder="* * * * *"
                      value={form.expression}
                      onChange={e => setForm(f => ({ ...f, expression: e.target.value }))}
                    />
                    {cronValid && form.expression ? (
                      <FormHelperText fontSize="xs" color="blue.300">
                        Next: {formatNextRun(form.expression)}
                      </FormHelperText>
                    ) : form.expression !== '' ? (
                      <FormHelperText fontSize="xs" color="red.400">Invalid expression</FormHelperText>
                    ) : (
                      <FormHelperText fontSize="xs" color="gray.500">
                        min hour day month weekday — e.g. <Text as="span" fontFamily="mono">*/16 * * * *</Text>
                      </FormHelperText>
                    )}
                  </FormControl>
                )}

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
            <Button size="sm" variant="ghost" onClick={() => { setShowForm(false); setForm(BLANK_FORM) }}>
              Cancel
            </Button>
            <Button size="sm" colorScheme="blue" onClick={handleCreate}>
              Create
            </Button>
          </ModalFooter>
        )}
      </ModalContent>
    </Modal>
  )
}
