"""ResolutionAdjust mod icon, in the Drinkable "STAY FRESH" poster style: sage poster with cream edges, a sun,
an ink-outlined vintage monitor whose screen is half pixelated / half sharp with an adjust knob, and the name in
Alfa Slab One letters (RESOLUTION dark / ADJUST light).
Usage: python3 resadj_icon.py <AlfaSlabOne-Regular.ttf> <out.png>"""
import sys, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance, ImageChops

FONT, OUT = sys.argv[1:3]
S = 1024
SAGE, CREAM, INK, SUN = (151, 177, 145), (226, 222, 205), (36, 32, 26), (219, 190, 52)
CASE, CASE_DK, SCREEN, RED = (214, 200, 168), (176, 160, 128), (44, 66, 70), (196, 58, 46)


def vintage(img):
    rgb = img.convert("RGB")
    rgb = ImageEnhance.Color(rgb).enhance(0.85)
    rgb = ImageChops.multiply(rgb, Image.new("RGB", rgb.size, (250, 238, 218)))
    out = rgb.convert("RGBA")
    out.putalpha(img.split()[3])
    return out


def outlined(img, w):
    a = img.split()[3].point(lambda v: 255 if v > 40 else 0)
    big = a.filter(ImageFilter.MaxFilter(w * 2 + 1))
    o = Image.new("RGBA", img.size, INK + (0,))
    o.putalpha(big)
    o.alpha_composite(img)
    return o


def scene(w, h):
    """What's on the screen: night sky, moon, hills and a little ship light."""
    im = Image.new("RGBA", (w, h), SCREEN + (255,))
    d = ImageDraw.Draw(im)
    d.ellipse([w * 0.58, h * 0.12, w * 0.84, h * 0.12 + w * 0.26], fill=CREAM)          # moon
    d.polygon([(0, h * 0.72), (w * 0.25, h * 0.48), (w * 0.47, h * 0.66), (w * 0.7, h * 0.44),
               (w, h * 0.64), (w, h), (0, h)], fill=(98, 120, 92))                         # far hills
    d.polygon([(0, h * 0.86), (w * 0.35, h * 0.70), (w * 0.62, h * 0.82), (w, h * 0.74),
               (w, h), (0, h)], fill=(64, 82, 60))                                         # near hills
    d.ellipse([w * 0.14, h * 0.62, w * 0.20, h * 0.62 + w * 0.06], fill=SUN)              # a light
    return im


def monitor():
    W, H = 760, 560
    m = Image.new("RGBA", (W + 120, H + 160), (0, 0, 0, 0))
    d = ImageDraw.Draw(m)
    ox, oy = 60, 40
    d.rounded_rectangle([ox, oy, ox + W, oy + H], radius=46, fill=CASE)                   # case
    d.rounded_rectangle([ox + 18, oy + H - 70, ox + W - 18, oy + H - 14], radius=20, fill=CASE_DK)
    d.rectangle([ox + W // 2 - 90, oy + H, ox + W // 2 + 90, oy + H + 50], fill=CASE_DK)  # stand
    d.rounded_rectangle([ox + W // 2 - 200, oy + H + 44, ox + W // 2 + 200, oy + H + 84], radius=16, fill=CASE)
    # screen: right half sharp, left half pixelated
    sx0, sy0, sw, sh = ox + 50, oy + 46, W - 230, H - 140
    sharp = scene(sw, sh)
    low = scene(sw // 14, sh // 14).resize((sw, sh), Image.NEAREST)
    screen = sharp.copy()
    screen.paste(low.crop((0, 0, sw // 2, sh)), (0, 0))
    mask = Image.new("L", (sw, sh), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, sw - 1, sh - 1], radius=34, fill=255)
    m.paste(screen, (sx0, sy0), mask)
    d.line([(sx0 + sw // 2, sy0 + 4), (sx0 + sw // 2, sy0 + sh - 4)], fill=CREAM, width=8)
    d.rounded_rectangle([sx0, sy0, sx0 + sw, sy0 + sh], radius=34, outline=INK, width=12)
    # the adjust knob with ticks, and a red power light
    kx, ky, kr = ox + W - 92, oy + 170, 58
    for i in range(7):
        import math
        a = math.radians(140 + i * 43)
        d.line([(kx + math.cos(a) * (kr + 12), ky + math.sin(a) * (kr + 12)),
                (kx + math.cos(a) * (kr + 30), ky + math.sin(a) * (kr + 30))], fill=INK, width=9)
    d.ellipse([kx - kr, ky - kr, kx + kr, ky + kr], fill=CASE_DK, outline=INK, width=10)
    d.line([(kx, ky), (kx + kr * 0.55, ky - kr * 0.75)], fill=INK, width=14)
    d.ellipse([kx - 22, oy + 330 - 22, kx + 22, oy + 330 + 22], fill=RED, outline=INK, width=8)
    for i in range(3):                                                                    # speaker slots
        d.rounded_rectangle([kx - 40, oy + 390 + i * 26, kx + 40, oy + 402 + i * 26], radius=6, fill=INK)
    return outlined(vintage(m), 14)


img = Image.new("RGBA", (S, S), CREAM + (255,))
d = ImageDraw.Draw(img)
d.rectangle([40, 0, S - 41, S], fill=SAGE)                      # the poster, cream edges
d.line([(40, 0), (40, S)], fill=(120, 130, 110), width=3)
d.line([(S - 41, 0), (S - 41, S)], fill=(120, 130, 110), width=3)
d.ellipse([520, 40, 960, 480], fill=SUN)                        # the sun

mon = monitor().rotate(-7, resample=Image.BICUBIC, expand=True)
mon = mon.resize((int(mon.width * 0.80), int(mon.height * 0.80)), Image.LANCZOS)
img.alpha_composite(mon, ((S - mon.width) // 2 + 10, -6))


def word(text, fill, edge, cx, cy, size, maxw=860):
    f = ImageFont.truetype(FONT, size)
    while f.getlength(text) + 40 > maxw:                        # shrink to fit between the cream edges
        size -= 4
        f = ImageFont.truetype(FONT, size)
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    ld.text((cx + 14, cy + 16), text, font=f, fill=INK, anchor="mm", stroke_width=20, stroke_fill=INK)   # shadow
    ld.text((cx, cy), text, font=f, fill=fill, anchor="mm", stroke_width=12, stroke_fill=edge)
    return layer


img.alpha_composite(word("RESOLUTION", INK, CREAM, S // 2, 690, 160))
img.alpha_composite(word("ADJUST", CREAM, INK, S // 2, 860, 240))

rnd = random.Random(7)                                          # print grain
g = Image.new("RGBA", (S, S), (0, 0, 0, 0))
gd = ImageDraw.Draw(g)
for _ in range(260):
    x, y, r = rnd.randrange(S), rnd.randrange(S), rnd.choice([2, 3, 3, 4])
    gd.ellipse([x - r, y - r, x + r, y + r], fill=(255, 255, 240, rnd.randrange(60, 140)))
img.alpha_composite(g)
noise = Image.effect_noise((S, S), 22).convert("L")
n = Image.merge("RGBA", (noise, noise, noise, Image.new("L", (S, S), 26)))
img = Image.alpha_composite(img, n)
img.convert("RGB").save(OUT.replace(".png", "_1024.png"))
img.convert("RGB").resize((256, 256), Image.LANCZOS).save(OUT)
print("ok")
