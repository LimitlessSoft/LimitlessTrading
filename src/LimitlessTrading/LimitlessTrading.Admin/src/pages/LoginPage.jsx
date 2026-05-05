import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Box,
  Button,
  FormControl,
  FormLabel,
  Heading,
  Input,
  Text,
  VStack
} from '@chakra-ui/react'
import { login } from '../api'

export default function LoginPage() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const navigate = useNavigate()

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      await login(email, password)
      navigate('/')
    } catch {
      setError('Invalid credentials')
    } finally {
      setLoading(false)
    }
  }

  return (
    <Box minH="100vh" bg="gray.900" display="flex" alignItems="center" justifyContent="center">
      <Box bg="gray.800" p={8} rounded="lg" w="full" maxW="400px">
        <Heading size="md" mb={6} color="white">
          LT Admin
        </Heading>
        <form onSubmit={handleSubmit}>
          <VStack spacing={4}>
            <FormControl>
              <FormLabel color="gray.300">Email</FormLabel>
              <Input
                type="email"
                value={email}
                onChange={e => setEmail(e.target.value)}
                bg="gray.700"
                border="none"
                color="white"
                _placeholder={{ color: 'gray.500' }}
                placeholder="admin@example.com"
                required
              />
            </FormControl>
            <FormControl>
              <FormLabel color="gray.300">Password</FormLabel>
              <Input
                type="password"
                value={password}
                onChange={e => setPassword(e.target.value)}
                bg="gray.700"
                border="none"
                color="white"
                required
              />
            </FormControl>
            {error && (
              <Text color="red.400" fontSize="sm">
                {error}
              </Text>
            )}
            <Button
              type="submit"
              colorScheme="blue"
              w="full"
              isLoading={loading}
            >
              Sign in
            </Button>
          </VStack>
        </form>
      </Box>
    </Box>
  )
}
