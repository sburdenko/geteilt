// Geteilt design tokens, "Leder & Leinen": leather and linen, in the spirit of iOS 6.
// Used as theme.extend in tailwind.config.js. Dark theme: the `dark` class; every role has a -dark twin.
module.exports = {
  colors: {
    // Light theme (linen)
    'linen':        '#e3e0d8', // screen background (the linen weave below goes on top)
    'linen-lift':   '#e9e6de', // raised areas (QR screen in landscape)
    'surface':      '#fdfdfb', // card and cell surface (top gradient stop)
    'surface-low':  '#ecece6', // bottom gradient stop of the surface
    'ink':          '#3a352e', // body text
    'ink-muted':    '#8a8375', // muted text, section labels
    'border':       '#b3aea3', // card and field borders
    'border-strong':'#9a9488', // button and segment borders
    'leather':      '#7a4f24', // accent material: bars, edges (middle stop)
    'leather-deep': '#5d3a17', // bottom stop of the leather
    'brass':        '#c3a05c', // brass: plates, secondary accent
    'action':       '#3a7cc2', // action: the call-to-action button (middle stop)
    'debt':         '#7c4a1a', // debt (warm ochre), always with a minus sign
    'credit':       '#2f5f9e', // credit (blue), always with a plus sign
    'success':      '#559a37', // confirmation ("Überwiesen", Rest 0,00 ✓)
    // Dark theme (dark linen)
    'linen-dark':        '#2b2926',
    'surface-dark':      '#3b3934',
    'surface-low-dark':  '#312f2a',
    'ink-dark':          '#e8e6e1',
    'ink-muted-dark':    '#9b968c',
    'border-dark':       '#1c1a16',
    'border-strong-dark':'#17150f',
    'leather-dark':      '#6b431c', // leather a little duller in the dark
    'debt-dark':         '#d8a05e', // amber, readable on dark
    'credit-dark':       '#8fb8e8',
    'success-dark':      '#a2d68a',
  },
  fontSize: {
    // role: [size, line-height]
    'amount-xl': ['54px', '1.05'],  // the amount on the QR screen
    'title':     ['26px', '1.15'],  // recipient name, screen titles
    'nav':       ['17px', '1.2'],   // navigation bar title
    'row':       ['15px', '1.35'],  // names in lists, buttons
    'body':      ['14px', '1.45'],  // field values, amounts in rows
    'label':     ['12px', '1.3'],   // section labels (caps, tracking 0.06em)
    'caption':   ['11px', '1.35'],  // footnotes, "GIROCODE · EPC-QR"
  },
  spacing: {
    // 4px grid; the steps the design actually uses:
    '1.5': '6px', '2': '8px', '2.5': '10px', '3': '12px',
    '3.5': '14px', '4': '16px', '4.5': '18px', '5': '20px',
    '6': '24px', '7': '28px',
  },
  borderRadius: {
    'ctl':  '6px',  // inputs, buttons in the bar
    'btn':  '8px',  // buttons, segmented control
    'card': '9px',  // grouped cards
    'qr':   '10px', // the white mat behind the QR code
  },
  boxShadow: {
    // skeuomorphism: an inner highlight on top, a soft drop below
    'card':     'inset 0 1px 0 #ffffff, 0 1px 2px rgba(0,0,0,.20)',
    'btn':      'inset 0 1px 0 rgba(255,255,255,.50), 0 1px 2px rgba(0,0,0,.30)',
    'btn-dark': 'inset 0 1px 0 rgba(255,255,255,.15), 0 1px 2px rgba(0,0,0,.50)',
    'inset-field': 'inset 0 2px 3px rgba(0,0,0,.12)', // inset input fields
    'leather':  'inset 0 1px 0 rgba(255,220,170,.40), 0 2px 4px rgba(0,0,0,.35)',
  },
  backgroundImage: {
    // materials only through these gradients, never as flat colours
    'leather-h':   'linear-gradient(#96693c, #7a4f24 48%, #6b431c 52%, #5d3a17)',
    'btn-action':  'linear-gradient(#8cc0ee 0%, #5493d3 49%, #3a7cc2 51%, #4f8ecb 100%)',
    'btn-success': 'linear-gradient(#a2d68a 0%, #6cb04c 49%, #559a37 51%, #63a844 100%)',
    'btn-plain':   'linear-gradient(#fdfdfb 0%, #e4e0d6 49%, #d6d2c6 51%, #e0dcd2 100%)',
    'surface-v':   'linear-gradient(#fdfdfb, #ecece6)',
    'brass-plate': 'linear-gradient(#e8cf9a, #c3a05c 50%, #a8843e)',
    // linen weave, laid OVER bg-linen in two layers:
    'linen-weave': 'repeating-linear-gradient(0deg, rgba(255,255,255,.35) 0 1px, transparent 1px 3px), repeating-linear-gradient(90deg, rgba(120,115,105,.12) 0 1px, transparent 1px 3px)',
  },
};
// Text on materials: on light ones text-shadow 0 1px 0 rgba(255,255,255,.7) (embossed),
// on dark or coloured ones 0 -1px 0 rgba(0,0,0,.4–.6) (engraved).
