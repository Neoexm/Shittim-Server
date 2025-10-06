const latinize = require('latinize');

test('strips diacritics', () => {
  expect(latinize('ỆᶍǍᶆṔƚÉ áéíóúýčďěňřšťžů Hello World')).toBe('ExAmPlE aeiouycdenrstzu Hello World');
});

