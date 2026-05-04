/** @type {import('tailwindcss').Config} */
export default {
  content: ['./src/**/*.{astro,html,js,jsx,md,mdx,ts,tsx}'],
  theme: {
    extend: {
      colors: {
        'ls-bg': '#080808',
        'ls-blue': '#0057ff',
        'ls-blue-dim': 'rgba(0,87,255,0.08)',
        'ls-border': 'rgba(255,255,255,0.06)',
        'ls-text': 'rgba(255,255,255,0.55)',
        'ls-text-dim': 'rgba(255,255,255,0.25)',
      },
      fontFamily: {
        sans: ['"Helvetica Neue"', 'Helvetica', 'Arial', 'sans-serif'],
      },
      letterSpacing: {
        widest2: '0.2em',
        widest3: '0.3em',
      },
    },
  },
  plugins: [],
};
