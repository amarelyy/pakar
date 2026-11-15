import { useEffect } from 'react'
import { useMap } from 'react-leaflet'
import type { StatusLevel } from '@/domain/types'

const COLORS: Record<StatusLevel, string> = {
  available: '#0f7a4a',
  limited: '#e39b00',
  almostFull: '#c42b2b',
  full: '#c42b2b',
  closed: '#6b7280',
  noData: '#9aa3ae',
}

export const hatchId = (s: StatusLevel) => `hatch-${s}`

/** Injects the 45° hatch patterns into Leaflet's SVG overlay so predicted polygons can fill with them. */
export function HatchDefs() {
  const map = useMap()
  useEffect(() => {
    const inject = () => {
      const svg = map.getPane('overlayPane')?.querySelector('svg')
      if (!svg || svg.querySelector('defs[data-pakar]')) return
      const ns = 'http://www.w3.org/2000/svg'
      const defs = document.createElementNS(ns, 'defs')
      defs.setAttribute('data-pakar', '1')
      for (const [status, color] of Object.entries(COLORS) as [StatusLevel, string][]) {
        const p = document.createElementNS(ns, 'pattern')
        p.setAttribute('id', hatchId(status))
        p.setAttribute('patternUnits', 'userSpaceOnUse')
        p.setAttribute('width', '8')
        p.setAttribute('height', '8')
        p.setAttribute('patternTransform', 'rotate(45)')
        const bg = document.createElementNS(ns, 'rect')
        bg.setAttribute('width', '8')
        bg.setAttribute('height', '8')
        bg.setAttribute('fill', color)
        bg.setAttribute('fill-opacity', '0.12')
        const bar = document.createElementNS(ns, 'rect')
        bar.setAttribute('width', '3')
        bar.setAttribute('height', '8')
        bar.setAttribute('fill', color)
        bar.setAttribute('fill-opacity', '0.55')
        p.appendChild(bg)
        p.appendChild(bar)
        defs.appendChild(p)
      }
      svg.insertBefore(defs, svg.firstChild)
    }
    inject()
    map.on('layeradd', inject)
    return () => {
      map.off('layeradd', inject)
    }
  }, [map])
  return null
}
