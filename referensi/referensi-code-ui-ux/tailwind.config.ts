import type { Config } from 'tailwindcss'

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        ground: 'var(--ground)',
        card: 'var(--card)',
        hairline: 'var(--hairline)',
        ink: 'var(--ink)',
        muted: 'var(--muted)',
        muted2: 'var(--muted-2)',
        accent: { DEFAULT: 'var(--accent)', soft: 'var(--accent-soft)', ink: 'var(--accent-ink)' },
        ok: { DEFAULT: 'var(--ok)', soft: 'var(--ok-soft)', ink: 'var(--ok-ink)' },
        warn: { DEFAULT: 'var(--warn)', soft: 'var(--warn-soft)', ink: 'var(--warn-ink)' },
        bad: { DEFAULT: 'var(--bad)', soft: 'var(--bad-soft)', ink: 'var(--bad-ink)' },
        closed: { DEFAULT: 'var(--closed)', soft: 'var(--closed-soft)' },
        nodata: { DEFAULT: 'var(--nodata)', soft: 'var(--nodata-soft)' },
      },
      borderRadius: { card: '8px', chip: '6px' },
      fontFamily: { sans: ['"IBM Plex Sans"', 'system-ui', '-apple-system', '"Segoe UI"', 'Roboto', 'sans-serif'] },
      fontSize: {
        xs: ['12px', '16px'],
        sm: ['14px', '20px'],
        base: ['16px', '24px'],
        lg: ['20px', '28px'],
        xl: ['28px', '32px'],
        '2xl': ['40px', '44px'],
      },
      boxShadow: { sheet: '0 -8px 24px rgba(20, 24, 31, 0.10)', drawer: '-8px 0 24px rgba(20, 24, 31, 0.10)' },
    },
  },
  plugins: [],
} satisfies Config
