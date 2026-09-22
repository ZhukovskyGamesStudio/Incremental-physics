# Melok

The game's chalk hand. The font is generated from code, like the rest of the game's art. Each letter is a skeleton
(the path the chalk takes) in `glyphs.py`. `build.py` bends it by hand: slant, a slight wobble, and each letter
tilted and lifted off the baseline a little. It then strokes it with a round pen and writes
`Assets/Resources/Fonts/Melok-Regular.ttf`.

```
pip install fonttools skia-pathops pillow
python check.py     # outlines are whole, and every character the game's code uses is in the font
python build.py     # writes the .ttf into Assets/Resources/Fonts
python render.py    # specimen.png: sample lines and every glyph, drawn by FreeType like in Unity
```

- Coverage: Russian and English (both cases), Greek, digits, ASCII, typographic quotes and dashes, math signs
  (× · − ± ≈ ≤ ≥ √ ∞ ½ ² …), arrows, the game's pictograms (▶ ► ★ ☆ ✓ ✗ ⚗ ⚙) and non-breaking/thin spaces.
- Digits are all the same width, so a changing number does not jiggle.
- Adding a character: register a function with `@g('ч')` that returns its strokes (`line`, `spline`, `arc`, `dot`,
  `fill`), then run `check.py` and `build.py`. `check.py` fails if the game's code uses a character the font lacks.
- The hand's randomness is seeded per character (`HAND['seed']`). Do not change the seed: every letter would get
  different bends from the ones that were approved.
