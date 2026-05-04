export const LINK_GROUPS = {
  1: { color: '#4299E1', name: 'Blue' },
  2: { color: '#48BB78', name: 'Green' },
  3: { color: '#ECC94B', name: 'Yellow' },
  4: { color: '#FC8181', name: 'Red' },
  5: { color: '#B794F4', name: 'Purple' },
}

export function getLinkGroupColor(groupId) {
  return LINK_GROUPS[groupId]?.color ?? null
}
