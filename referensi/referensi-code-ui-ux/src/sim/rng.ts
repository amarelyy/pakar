// Deterministic randomness. Everything in the simulation derives from string seeds,
// so any zone at any time is reproducible in every environment.

export function hash32(str: string): number {
  let h = 2166136261 >>> 0
  for (let i = 0; i < str.length; i++) {
    h ^= str.charCodeAt(i)
    h = Math.imul(h, 16777619) >>> 0
  }
  // final avalanche
  h ^= h >>> 16
  h = Math.imul(h, 0x85ebca6b) >>> 0
  h ^= h >>> 13
  h = Math.imul(h, 0xc2b2ae35) >>> 0
  h ^= h >>> 16
  return h >>> 0
}

export type Rng = () => number

export function mulberry32(seed: number): Rng {
  let a = seed >>> 0
  return () => {
    a = (a + 0x6d2b79f5) >>> 0
    let t = a
    t = Math.imul(t ^ (t >>> 15), t | 1)
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61)
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

export function seeded(...parts: (string | number)[]): Rng {
  return mulberry32(hash32(parts.join('|')))
}

/** One uniform in [0,1) for a key, without keeping a generator around. */
export function unit(...parts: (string | number)[]): number {
  return hash32(parts.join('|')) / 4294967296
}

/** Standard normal via Box–Muller. */
export function gaussian(rng: Rng): number {
  let u = 0
  let v = 0
  while (u === 0) u = rng()
  while (v === 0) v = rng()
  return Math.sqrt(-2 * Math.log(u)) * Math.cos(2 * Math.PI * v)
}

export const clamp = (x: number, lo: number, hi: number) => Math.min(hi, Math.max(lo, x))
export const lerp = (a: number, b: number, s: number) => a + (b - a) * s
export const smoothstep = (s: number) => s * s * (3 - 2 * s)
