export function testRule(resolved, rule) {
  // Backward compat: old rules used contains: true/false
  const op = rule.operator ?? (rule.contains ? 'contains' : 'equals')
  const ruleValue = rule.value || ''

  if (op === 'contains') return resolved.includes(ruleValue)
  if (op === 'not-contains') return !resolved.includes(ruleValue)
  if (op === 'equals') return resolved === ruleValue

  // Numeric comparison operators
  const num = parseFloat(resolved)
  const threshold = parseFloat(ruleValue)
  if (isNaN(num) || isNaN(threshold)) return false
  switch (op) {
    case '>':  return num > threshold
    case '<':  return num < threshold
    case '>=': return num >= threshold
    case '<=': return num <= threshold
    case '!=': return num !== threshold
    default:   return false
  }
}

export function applyConditionalStyles(baseStyle, conditionalStyles, resolveText) {
  if (!conditionalStyles?.length) return baseStyle
  for (const rule of conditionalStyles) {
    if (!rule.placeholder) continue
    const resolved = resolveText(rule.placeholder)
    if (testRule(resolved, rule)) {
      const SKIP = new Set(['id', 'placeholder', 'operator', 'contains', 'value'])
      const overrides = Object.fromEntries(
        Object.entries(rule).filter(([k, v]) => !SKIP.has(k) && v != null)
      )
      return { ...baseStyle, ...overrides }
    }
  }
  return baseStyle
}

export function newConditionalRule() {
  return {
    id: crypto.randomUUID(),
    placeholder: '',
    operator: 'contains',
    value: '',
    bgColor: null,
    textColor: null,
    borderColor: null,
    borderWidth: null
  }
}
