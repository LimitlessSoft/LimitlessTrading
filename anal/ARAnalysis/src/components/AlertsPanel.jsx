import { useState } from 'react'
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
  Checkbox,
  CheckboxGroup,
  Stack,
  FormControl,
  FormLabel,
  Collapse,
  useToast,
} from '@chakra-ui/react'
import { BellIcon, AddIcon, DeleteIcon } from '@chakra-ui/icons'
import { createAlert, removeAlert, listAlerts } from '../modules/alertService'

const ACTION_COLORS = { message: 'blue', sound: 'purple', event: 'orange' }

function scheduleDescription(schedule) {
  if (!schedule) return '—'
  if (schedule.type === 'interval') {
    if (schedule.alignToHour) return 'Every hour, on the hour'
    const s = schedule.ms / 1000
    if (s < 60) return `Every ${s}s`
    const m = s / 60
    if (Number.isInteger(m) && m < 60) return `Every ${m} min`
    const h = m / 60
    return `Every ${h % 1 === 0 ? h : h.toFixed(1)}h`
  }
  return schedule.type
}

const BLANK_FORM = {
  label: '',
  scheduleType: 'on-the-hour',
  intervalValue: '30',
  intervalUnit: 'minutes',
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

    let schedule
    if (form.scheduleType === 'on-the-hour') {
      schedule = { type: 'interval', alignToHour: true }
    } else {
      const val = parseInt(form.intervalValue, 10)
      if (!val || val <= 0) {
        toast({ title: 'Interval must be a positive number', status: 'error', duration: 2000 })
        return
      }
      const ms = form.intervalUnit === 'minutes' ? val * 60_000 : val * 3_600_000
      schedule = { type: 'interval', ms }
    }

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
          {/* Alert list */}
          {alerts.length === 0 ? (
            <Text color="gray.400" fontSize="sm">No alerts. Create one below.</Text>
          ) : (
            <VStack align="stretch" spacing={0} divider={<Divider borderColor="gray.700" />}>
              {alerts.map(alert => (
                <HStack key={alert.id} py={3} align="flex-start" justify="space-between">
                  <Box>
                    <Text fontWeight="semibold" fontSize="sm" color="white">
                      {alert.label || alert.id}
                    </Text>
                    <Text fontSize="xs" color="gray.400" mt={0.5}>
                      {scheduleDescription(alert.schedule)}
                    </Text>
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
                  <IconButton
                    icon={<DeleteIcon />}
                    size="xs"
                    variant="ghost"
                    colorScheme="red"
                    aria-label="Delete alert"
                    onClick={() => handleDelete(alert.id)}
                    mt={0.5}
                  />
                </HStack>
              ))}
            </VStack>
          )}

          <Divider borderColor="gray.700" mt={alerts.length > 0 ? 2 : 0} mb={3} />

          {/* Create form toggle */}
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
                  <FormLabel fontSize="xs" color="gray.400">Schedule</FormLabel>
                  <Select
                    size="sm"
                    value={form.scheduleType}
                    onChange={e => setForm(f => ({ ...f, scheduleType: e.target.value }))}
                  >
                    <option value="on-the-hour">Every hour, on the hour</option>
                    <option value="interval">Every N minutes / hours</option>
                  </Select>
                </FormControl>

                {form.scheduleType === 'interval' && (
                  <HStack>
                    <Input
                      size="sm"
                      type="number"
                      min={1}
                      value={form.intervalValue}
                      onChange={e => setForm(f => ({ ...f, intervalValue: e.target.value }))}
                      w="80px"
                    />
                    <Select
                      size="sm"
                      value={form.intervalUnit}
                      onChange={e => setForm(f => ({ ...f, intervalUnit: e.target.value }))}
                    >
                      <option value="minutes">Minutes</option>
                      <option value="hours">Hours</option>
                    </Select>
                  </HStack>
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
