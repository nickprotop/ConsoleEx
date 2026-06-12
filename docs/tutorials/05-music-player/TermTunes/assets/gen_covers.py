#!/usr/bin/env python3
"""Generate simple album-cover PNGs (accent gradient + initials) for TermTunes."""
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "covers")
os.makedirs(OUT, exist_ok=True)

# (filename, accent rgb, initials)
COVERS = [
    ("midnight.png",  (255, 80, 160), "MD"),
    ("neon.png",      (80, 200, 255), "NR"),
    ("cassette.png",  (255, 180, 60), "CD"),
    ("afterglow.png", (130, 255, 150), "AG"),
    ("velvet.png",    (190, 130, 255), "VS"),
]

SIZE = 240

def lerp(a, b, t): return tuple(int(a[i] + (b[i]-a[i])*t) for i in range(3))

for name, accent, initials in COVERS:
    img = Image.new("RGB", (SIZE, SIZE))
    d = ImageDraw.Draw(img)
    top = accent
    bottom = tuple(max(0, int(c*0.18)) for c in accent)
    for y in range(SIZE):
        d.line([(0, y), (SIZE, y)], fill=lerp(top, bottom, y / SIZE))
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 96)
    except Exception:
        font = ImageFont.load_default()
    bbox = d.textbbox((0, 0), initials, font=font)
    tw, th = bbox[2]-bbox[0], bbox[3]-bbox[1]
    d.text(((SIZE-tw)/2 - bbox[0], (SIZE-th)/2 - bbox[1]), initials, fill=(255, 255, 255), font=font)
    img.save(os.path.join(OUT, name))
    print("wrote", name)
