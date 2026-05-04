import TemplatePageLabel from './TemplatePageLabel'
import TemplatePagePanel from './TemplatePagePanel'
import TemplatePageBadge from './TemplatePageBadge'
import TemplatePageChart from './TemplatePageChart'
import TemplatePageGauge from './TemplatePageGauge'
import TemplatePageGraph from './TemplatePageGraph'

// Single place to add a new widget type:
// 1. Add an entry here
// 2. Create the TemplatePage[Name].jsx component
export const WIDGET_REGISTRY = {
  label: { displayName: 'Label',  width: 4,  height: 60,  Component: TemplatePageLabel, needsText: true  },
  panel: { displayName: 'Panel',  width: 20, height: 120, Component: TemplatePagePanel, children: []     },
  badge: { displayName: 'Badge',  width: 2,  height: 40,  Component: TemplatePageBadge                  },
  chart: { displayName: 'Chart',  width: 10, height: 220, Component: TemplatePageChart, symbol: '', interval: '5m' },
  gauge: { displayName: 'Gauge',  width: 6,  height: 160, Component: TemplatePageGauge,
    min: 0, max: 100,
    segments: [
      { from: 0,  to: 25,  color: '#48BB78' },
      { from: 25, to: 50,  color: '#ECC94B' },
      { from: 50, to: 75,  color: '#ED8936' },
      { from: 75, to: 100, color: '#F56565' },
    ]
  },
  graph: { displayName: 'Graph', width: 10, height: 220, Component: TemplatePageGraph, series: [] },
}

export function createWidget(type, text) {
  const def = WIDGET_REGISTRY[type] || WIDGET_REGISTRY.label
  const { displayName, Component, needsText, ...initial } = def
  return {
    id: crypto.randomUUID(),
    type,
    x: 0,
    y: 0,
    createdAt: Date.now(),
    ...(needsText ? { text: text || '' } : {}),
    ...initial,
  }
}

export function renderWidget(widget, props) {
  const def = WIDGET_REGISTRY[widget.type]
  const Component = def?.Component ?? TemplatePageLabel
  const { key, ...rest } = props
  return <Component key={key} {...rest} />
}
