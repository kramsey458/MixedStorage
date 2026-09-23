"""Makes the cabinet's material textures for the MixedStorage site. Procedural: no source images, no generative model.
Run from this folder with Python 3, numpy and Pillow:  python make_textures.py
Every texture tiles (walnut and brushed brass horizontally and vertically, kraft both ways); the holder frame is a
nine-slice for CSS border-image and the pull is a single image."""
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

rng = np.random.default_rng(1200)  # fixed seed: the same files every run


def periodic_noise(h, w, octaves, rng):
    """Smooth noise that tiles: a sum of random sinusoids with whole periods across the image."""
    y, x = np.mgrid[0:h, 0:w]
    out = np.zeros((h, w))
    for fy, fx, amp in octaves:
        for _ in range(4):
            ky, kx = rng.integers(1, fy + 1), rng.integers(0, fx + 1)
            ph = rng.uniform(0, 2 * np.pi)
            out += amp * np.sin(2 * np.pi * (ky * y / h + kx * x / w) + ph)
    return out / max(1e-6, np.abs(out).max())


def save(arr, name, quality=82):
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).save(name, quality=quality, method=6)


# walnut: long grain running across the drawer front, with darker late-wood lines and fine pores
h, w = 180, 720
warp = periodic_noise(h, w, [(3, 2, 1.0), (6, 3, .4)], rng) * 9
y = np.mgrid[0:h, 0:w][0].astype(float)
rings = np.sin(2 * np.pi * (y + warp) / 13.0) * .5 + .5
fine = np.sin(2 * np.pi * (y + warp * 1.7) / 3.1) * .5 + .5
pores = rng.random((h, w)) ** 18
tone = periodic_noise(h, w, [(2, 1, 1.0)], rng) * .5 + .5
light, dark = np.array([112, 76, 51]), np.array([66, 41, 26])
t = (.55 * rings ** 3 + .25 * fine + .2 * tone)[..., None]
walnut = dark + (light - dark) * (1 - t) - pores[..., None] * 38
save(walnut, "walnut.webp")

# kraft: pale brown card with paper tooth and a few long fibres
h = w = 200
base = np.array([216, 194, 155], float)
tooth = rng.normal(0, 1, (h, w))
tooth = np.array(Image.fromarray(((tooth * 20) + 128).clip(0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(.6))).astype(float) - 128
blot = periodic_noise(h, w, [(2, 2, 1.0), (4, 4, .5)], rng) * 6
kraft = base + (tooth * .55 + blot)[..., None] * np.array([1, .95, .8])
img = Image.fromarray(kraft.clip(0, 255).astype(np.uint8))
d = ImageDraw.Draw(img)
for _ in range(14):
    x0, y0 = rng.uniform(0, w), rng.uniform(0, h)
    ang, ln = rng.uniform(0, np.pi), rng.uniform(8, 26)
    x1, y1 = x0 + np.cos(ang) * ln, y0 + np.sin(ang) * ln
    for ox in (-w, 0, w):
        for oy in (-h, 0, h):
            d.line([(x0 + ox, y0 + oy), (x1 + ox, y1 + oy)], fill=(196, 172, 132), width=1)
img.save("kraft.webp", quality=84, method=6)

# brushed brass: fine horizontal streaks over a warm metal, for the Download plate and label frames
h, w = 64, 512
noise = rng.normal(0, 1, (h, w * 2))
kernel = np.ones(160) / 160
streak = np.array([np.convolve(r, kernel, mode="same") for r in noise])[:, w // 2:w // 2 + w]
streak = streak / np.abs(streak).max()
fine = rng.normal(0, 1, (h, 1)) * .5  # each brushed line a touch lighter or darker, all the way across
yy = np.linspace(0, 1, h)[:, None]
sheen = np.exp(-((yy - .32) / .12) ** 2)  # one soft highlight band, as on polished metal
brass_lo, brass_hi = np.array([150, 110, 28]), np.array([250, 222, 140])
tt = (.42 + streak * .08 + fine * .06 + sheen * .45 - yy * .12).clip(0, 1)[..., None]
brass = brass_lo + (brass_hi - brass_lo) * tt
save(brass, "brass.webp", 86)


def brass_fill(size, hi=1.0):
    """A bevelled brass rectangle: lit from above, darker at the bottom edge."""
    W, H = size
    yy = np.linspace(0, 1, H)[:, None] * np.ones((1, W))
    tex = np.array(Image.open("brass.webp").convert("RGB").resize((W, H))).astype(float)
    shade = (1.12 - .38 * yy) * hi
    return (tex * shade[..., None]).clip(0, 255)


# the label holder: a nine-slice brass frame with a rivet at each end (border-image-slice: 16)
W, H, B = 112, 56, 16
frame = np.zeros((H, W, 4))
frame[..., :3] = brass_fill((W, H))
frame[..., 3] = 255
frame[B - 4:H - B + 4, B - 4:W - B + 4, 3] = 0  # the window the card shows through
im = Image.fromarray(frame.astype(np.uint8), "RGBA")
d = ImageDraw.Draw(im)
d.rounded_rectangle([0, 0, W - 1, H - 1], radius=5, outline=(110, 82, 30, 255), width=2)
d.rectangle([B - 5, B - 5, W - B + 4, H - B + 4], outline=(120, 90, 34, 255), width=1)
for cx in (7, W - 8):
    cy = H // 2
    d.ellipse([cx - 4, cy - 4, cx + 4, cy + 4], fill=(150, 112, 44, 255), outline=(96, 70, 24, 255))
    d.ellipse([cx - 2, cy - 3, cx + 1, cy], fill=(240, 214, 150, 255))
mask = Image.new("L", (W, H), 0)
ImageDraw.Draw(mask).rounded_rectangle([0, 0, W - 1, H - 1], radius=5, fill=255)
a = np.array(im); a[..., 3] = np.minimum(a[..., 3], np.array(mask))
Image.fromarray(a, "RGBA").save("holder.png", optimize=True)

# the cup pull: a half-round brass cup screwed to the drawer face
W, H = 96, 40
pull = np.zeros((H, W, 4))
pull[..., :3] = brass_fill((W, H), .95)
yy, xx = np.mgrid[0:H, 0:W]
inside = ((xx - W / 2) / (W / 2 - 2)) ** 2 + ((yy - 4) / (H - 8)) ** 2 <= 1
pull[..., 3] = np.where(inside & (yy >= 4), 255, 0)
depth = ((yy - 4) / (H - 8)).clip(0, 1)
pull[..., :3] *= (1.1 - .55 * depth)[..., None]
plate = (yy < 9) & (np.abs(xx - W / 2) < W / 2 - 1)
pull[plate, :3] = brass_fill((W, H))[plate] * 1.05
pull[plate, 3] = 255
im = Image.fromarray(pull.clip(0, 255).astype(np.uint8), "RGBA")
d = ImageDraw.Draw(im)
for cx in (9, W - 10):
    d.ellipse([cx - 3, 1, cx + 3, 7], fill=(120, 88, 32, 255))
    d.line([cx - 2, 4, cx + 2, 4], fill=(240, 214, 150, 255))
im.save("pull.png", optimize=True)
print("made walnut.webp kraft.webp brass.webp holder.png pull.png")
