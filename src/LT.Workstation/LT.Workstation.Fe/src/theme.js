import { extendTheme } from '@chakra-ui/react'

const theme = extendTheme({
  config: {
    initialColorMode: 'dark',
    useSystemColorMode: false
  },
  styles: {
    global: {
      body: {
        bg: 'gray.900',
        color: 'gray.100'
      }
    }
  },
  components: {
    Table: {
      variants: {
        striped: {
          tbody: {
            tr: {
              '&:nth-of-type(odd)': {
                bg: 'gray.800'
              }
            }
          }
        }
      }
    }
  }
})

export default theme
