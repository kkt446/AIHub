"""Generate AIHub app icon (ico + png) with Pillow."""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_ICO = os.path.join(ROOT, "src", "AIHub.App", "Assets", "AIHub.ico")
OUT_PNG = os.path.join(ROOT, "assets", "logo.png")
FONT = r"C:\Windows\Fonts\segoeuib.ttf"
S = 1024
C1 = (43, 92, 255)
C2 = (138, 61, 255)


def rounded_gradient(size, radius_ratio=0.22):
    base = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    grad = Image.new("RGBA", (size, size))
    px = grad.load()
    for y in range(size):
        for x in range(size):
            t = (x / size * 0.55 + y / size * 0.45)
            px[x, y] = (
                int(C1[0] + (C2[0] - C1[0]) * t),
                int(C1[1] + (C2[1] - C1[1]) * t),
                int(C1[2] + (C2[2] - C1[2]) * t),
                255,
            )
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        (0, 0, size - 1, size - 1), radius=int(size * radius_ratio), fill=255
    )
    base.paste(grad, (0, 0), mask)
    return base


def draw_logo(size):
    img = rounded_gradient(size)
    d = ImageDraw.Draw(img)
    font = ImageFont.truetype(FONT, int(size * 0.46))
    text = "AI"
    box = d.textbbox((0, 0), text, font=font)
    tw, th = box[2] - box[0], box[3] - box[1]
    d.text(
        ((size - tw) / 2 - box[0], (size - th) / 2 - box[1] - size * 0.06),
        text,
        font=font,
        fill=(255, 255, 255, 255),
    )
    # three connected nodes = three clients sharing one hub
    r = size * 0.055
    cy = size * 0.76
    xs = [size * 0.36, size * 0.5, size * 0.64]
    d.line([(xs[0], cy), (xs[2], cy)], fill=(255, 255, 255, 160), width=int(size * 0.028))
    for i, x in enumerate(xs):
        fill = (255, 255, 255, 255) if i != 1 else (110, 255, 170, 255)
        d.ellipse((x - r, cy - r, x + r, cy + r), fill=fill)
    return img


def main():
    os.makedirs(os.path.dirname(OUT_ICO), exist_ok=True)
    os.makedirs(os.path.dirname(OUT_PNG), exist_ok=True)
    master = draw_logo(S)
    master.resize((512, 512), Image.LANCZOS).save(OUT_PNG)
    master.save(
        OUT_ICO,
        format="ICO",
        sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
    )
    print("ico:", OUT_ICO, os.path.getsize(OUT_ICO))
    print("png:", OUT_PNG, os.path.getsize(OUT_PNG))


if __name__ == "__main__":
    main()
