// Tokens are not duplicated here: tailwind.tokens.js is what the designer handed over
// and stays the single copy, so a change there cannot silently disagree with the build.
const tokens = require('./tailwind.tokens.js');

module.exports = {
  darkMode: 'class',
  content: ['./**/*.{razor,html}', '!./bin/**', '!./obj/**', '!./node_modules/**'],
  theme: { extend: tokens },
};
