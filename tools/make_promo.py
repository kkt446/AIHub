"""Render the AIHub promo video (1920x1080, 30 fps) from real UI screenshots.

Outputs assets/promo.mp4. Requires Pillow, numpy and imageio-ffmpeg (or ffmpeg on PATH).
"""
import math
import os
import subprocess
import sys
import wave

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHOTS = os.path.join(ROOT, "tools", "promo", "shots")
OUT_DIR = os.path.join(ROOT, "tools", "promo", "out")
OUT_MP4 = os.path.join(ROOT, "assets", "promo.mp4")
FONT_BOLD = r"C:\Windows\Fonts\msyhbd.ttc"
FONT_REG = r"C:\Windows\Fonts\msyh.ttc"

W, H, FPS = 1920, 1080, 30
TOTAL = 44.0

ACCENT = (110, 255, 170)
ACCENT2 = (122, 162, 255)


def ease_out(t):
    t = max(0.0, min(1.0, t))
    return 1 - (1 - t) ** 3


def ease_in_out(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def smooth(t):
    return ease_in_out(t)


def font(size, bold=True):
    return ImageFont.truetype(FONT_BOLD if bold else FONT_REG, size, index=0)


def rounded_mask(size, radius):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius=radius, fill=255)
    return m


def fit(img, w):
    h = round(w * img.height / img.width)
    return img.resize((w, h), Image.LANCZOS)


def vertical_gradient(size, top, bottom):
    w, h = size
    g = Image.new("RGB", (1, h))
    px = g.load()
    for y in range(h):
        t = y / max(1, h - 1)
        px[0, y] = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3))
    return g.resize((w, h))


def blurred_backdrop(img):
    """Blurred, darkened screenshot used as the scene backdrop."""
    cover = img.copy()
    scale = max(W / cover.width, H / cover.height) * 1.25
    cover = cover.resize((round(cover.width * scale), round(cover.height * scale)), Image.LANCZOS)
    left = (cover.width - W) // 2
    top = (cover.height - H) // 2
    cover = cover.crop((left, top, left + W, top + H)).filter(ImageFilter.GaussianBlur(46))
    overlay = Image.new("RGB", (W, H), (14, 16, 34))
    cover = Image.blend(cover, overlay, 0.66)
    # purple wash
    wash = vertical_gradient((W, H), (24, 28, 74), (10, 10, 26))
    return Image.blend(cover, wash, 0.45)


def draw_shadow(canvas, box, radius, alpha=150, spread=40, offset=26):
    x0, y0, x1, y1 = box
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.rounded_rectangle((x0, y0 + offset, x1, y1 + offset), radius=radius, fill=(0, 0, 0, alpha))
    layer = layer.filter(ImageFilter.GaussianBlur(spread))
    canvas.alpha_composite(layer)


def caption(canvas, text, alpha=255, y=1002, size=44, color=(255, 255, 255)):
    if alpha <= 0 or not text:
        return
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    f = font(size)
    tw = d.textlength(text, font=f)
    x = (W - tw) / 2
    pad = 30
    bar = (x - pad, y - 22, x + tw + pad, y + size + 22)
    d.rounded_rectangle(bar, radius=18, fill=(10, 12, 26, int(150 * alpha / 255)))
    # accent dot
    cy = (bar[1] + bar[3]) / 2
    d.ellipse((bar[0] + 16, cy - 7, bar[0] + 30, cy + 7), fill=ACCENT + (alpha,))
    d.text((x, y), text, font=f, fill=color + (alpha,))
    canvas.alpha_composite(layer)


def brand_mark(canvas, alpha=220):
    """Small AIHub lockup in the top-left corner."""
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    logo = LOGO.resize((54, 54), Image.LANCZOS)
    layer.alpha_composite(logo, (72, 60))
    d.text((140, 66), "AIHub", font=font(34), fill=(255, 255, 255, alpha))
    canvas.alpha_composite(layer)


def shot_card(shot, width, y_center=None, radius=26, shadow=True, canvas=None):
    """Compose a screenshot as a floating rounded card, returned as RGBA."""
    card = fit(shot, width)
    mask = rounded_mask(card.size, radius)
    x = (W - card.width) // 2
    y = (H - card.height) // 2 if y_center is None else y_center
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    if shadow and canvas is not None:
        draw_shadow(canvas, (x, y, x + card.width, y + card.height), radius)
    layer.paste(card.convert("RGBA"), (x, y), mask)
    return layer


def scene_intro(t, dur):
    canvas = BASE_INTRO.copy().convert("RGBA")
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    p = ease_out(t / 1.2)
    w = int(210 * (0.86 + 0.14 * p))
    logo = LOGO.resize((w, w), Image.LANCZOS)
    a = int(255 * min(1.0, t / 0.8))
    if a < 255:
        r, g, b, al = logo.split()
        logo.putalpha(al.point(lambda v: int(v * a / 255)))
    layer.alpha_composite(logo, ((W - w) // 2, 268 + int(18 * (1 - p))))

    def fade(start, dur_=0.7, full=255):
        return int(full * max(0.0, min(1.0, (t - start) / dur_)))

    a1 = fade(0.6, 0.8)
    if a1:
        d.text(((W - d.textlength("AIHub", font=font(118))) / 2, 496), "AIHub",
               font=font(118), fill=(255, 255, 255, a1))
    a2 = fade(1.05, 0.8)
    if a2:
        tag = "让三个 AI 客户端，共用同一个工作区"
        d.text(((W - d.textlength(tag, font=font(46, bold=False))) / 2, 652), tag,
               font=font(46, bold=False), fill=(228, 233, 252, a2))
    pills = ["Qoder", "Qoder CN", "DeepSeek Harness"]
    fpill = font(28, bold=False)
    widths = [d.textlength(s, font=fpill) + 48 for s in pills]
    total = sum(widths) + 20 * (len(pills) - 1)
    x = (W - total) / 2
    for i, (s_, pw) in enumerate(zip(pills, widths)):
        a3 = fade(1.5 + i * 0.25, 0.6, 232)
        if a3:
            d.rounded_rectangle((x, 748, x + pw, 748 + 56), radius=28,
                                outline=(150, 170, 255, a3), width=2,
                                fill=(255, 255, 255, int(a3 * 0.10)))
            d.text((x + 24, 762), s_, font=fpill, fill=(235, 240, 255, a3))
        x += pw + 20
    canvas.alpha_composite(layer)
    return canvas


def scene_shot(shot, text, zoom_from, zoom_to, pan=None):
    bg = BACKDROPS[id(shot)].convert("RGBA")
    base_w = 1400
    local = dict(card_alpha=255)

    def render(t, dur):
        canvas = bg.copy()
        p = smooth(min(1.0, t / max(0.1, dur - 0.2)))
        w = int(base_w * (zoom_from + (zoom_to - zoom_from) * p))
        y = 72 - int(8 * p)
        xoff = 0
        if pan:
            xoff = int(pan[0] + (pan[1] - pan[0]) * p)
        layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        card = fit(shot, w)
        mask = rounded_mask(card.size, 26)
        x = (W - card.width) // 2 + xoff
        draw_shadow(canvas, (x, y, x + card.width, y + card.height), 26)
        layer.paste(card.convert("RGBA"), (x, y), mask)
        canvas.alpha_composite(layer)
        # caption fade in / out
        ca = 255 * min(1.0, t / 0.6) * min(1.0, max(0.0, (dur - t) / 0.6))
        caption(canvas, text, int(ca))
        return canvas

    return render


def scene_points(t, dur):
    canvas = BASE_POINTS.copy().convert("RGBA")
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    title = "为什么是 AIHub"
    p0 = ease_out(t / 0.8)
    d.text(((W - d.textlength(title, font=font(56))) / 2, 150 - int(12 * (1 - p0))), title,
           font=font(56), fill=(255, 255, 255, int(255 * p0)))
    items = [
        ("01", "Qoder Hooks 自动同步", "SessionStart / Stop 自动导入并注入交接"),
        ("02", "dsh 零配置接力", "读取 AGENTS.local.md，无需插件"),
        ("03", "纯本地 · 无云端", "数据都在工作区 .aihub 目录，不联网"),
    ]
    for i, (num, head, sub) in enumerate(items):
        p = ease_out(max(0.0, (t - 0.5 - i * 0.55) / 0.7))
        if p <= 0:
            continue
        y = 330 + i * 190 + int(26 * (1 - p))
        box = (300, y, W - 300, y + 150)
        d.rounded_rectangle(box, radius=24, fill=(255, 255, 255, int(26 * p)),
                            outline=(255, 255, 255, int(46 * p)), width=2)
        d.rounded_rectangle((300, y, 316, y + 150), radius=8, fill=ACCENT + (int(220 * p),))
        d.text((352, y + 34), num, font=font(38), fill=ACCENT2 + (int(255 * p),))
        d.text((432, y + 26), head, font=font(42), fill=(255, 255, 255, int(255 * p)))
        d.text((432, y + 84), sub, font=font(28, bold=False), fill=(214, 220, 245, int(230 * p)))
    canvas.alpha_composite(layer)
    ca = 255 * min(1.0, t / 0.6) * min(1.0, max(0.0, (dur - t) / 0.6))
    caption(canvas, "本地运行 · 开源 · MIT", int(ca))
    return canvas


def scene_outro(t, dur):
    canvas = BASE_INTRO.copy().convert("RGBA")
    d = ImageDraw.Draw(canvas)
    p = ease_out(t / 0.9)
    w = int(150 * (0.9 + 0.1 * p))
    logo = LOGO.resize((w, w), Image.LANCZOS)
    canvas.alpha_composite(logo, ((W - w) // 2, 236))
    a = int(255 * min(1.0, (t - 0.3) / 0.7))
    if a > 0:
        f = font(84)
        t1 = "AIHub"
        d.text(((W - d.textlength(t1, font=f)) / 2, 420), t1, font=f, fill=(255, 255, 255, a))
        f2 = font(40, bold=False)
        t2 = "github.com/kkt446/AIHub"
        d.text(((W - d.textlength(t2, font=f2)) / 2, 548), t2, font=f2, fill=ACCENT2 + (a,))
        f3 = font(30, bold=False)
        t3 = "Windows 10 / 11  x64   ·   无需管理员   ·   自带 .NET 8 与 Windows App SDK"
        d.text(((W - d.textlength(t3, font=f3)) / 2, 630), t3, font=f3, fill=(206, 214, 240, int(a * 0.9)))
        f4 = font(28, bold=False)
        t4 = "v0.1.0  ·  MIT License"
        d.text(((W - d.textlength(t4, font=f4)) / 2, 700), t4, font=f4, fill=(170, 180, 210, int(a * 0.8)))
    return canvas


def build_scenes():
    intro = dict(start=0.0, dur=5.0, render=scene_intro)
    s1 = dict(start=5.0, dur=9.0, render=scene_shot(SHOT1, "自动导入 Qoder / Qoder CN / DeepSeek Harness 的全部会话",
                                                  1.05, 0.99))
    s2 = dict(start=14.0, dur=9.0, render=scene_shot(SHOT2, "对话、附图、工具调用 —— 一处归档，随时接力",
                                                   1.0, 1.06, pan=(-26, 18)))
    s3 = dict(start=23.0, dur=9.0, render=scene_shot(SHOT3, "一键生成交接，新会话自动注入上下文",
                                                   1.0, 1.04))
    pts = dict(start=32.0, dur=6.0, render=scene_points)
    outro = dict(start=38.0, dur=TOTAL - 38.0, render=scene_outro)
    return [intro, s1, s2, s3, pts, outro]


def frame_at(scenes, t):
    active = [s for s in scenes if s["start"] <= t < s["start"] + s["dur"]]
    if not active:
        return scenes[-1]["render"](scenes[-1]["dur"] - 0.01, scenes[-1]["dur"]).convert("RGB")
    cur = active[-1]
    img = cur["render"](t - cur["start"], cur["dur"])
    # crossfade with the previous scene during the first 0.6s
    idx = scenes.index(cur)
    ft = t - cur["start"]
    if idx > 0 and ft < 0.6:
        prev = scenes[idx - 1]
        pimg = prev["render"](prev["dur"] - 0.01, prev["dur"])
        img = Image.blend(pimg.convert("RGBA"), img, smooth(ft / 0.6))
    return img.convert("RGB")


def make_audio(path, seconds):
    sr = 44100
    n = int(sr * seconds)
    t = np.arange(n) / sr
    chords = [
        (220.00, 261.63, 329.63),
        (174.61, 220.00, 261.63),
        (130.81, 164.81, 196.00),
        (196.00, 246.94, 293.66),
    ]
    seg = seconds / len(chords)

    pad = np.zeros(n)
    for i, ch in enumerate(chords):
        start, end = i * seg, min(seconds, (i + 1) * seg)
        m = (t >= start) & (t < end)
        local = t[m] - start
        env = np.minimum(1.0, local / 1.2) * np.minimum(1.0, np.clip((seg - local) / 1.2, 0, 1))
        tone = np.zeros(m.sum())
        for f in ch:
            for det in (-0.5, 0.0, 0.5):
                tone += np.sin(2 * np.pi * (f + det) * local) / 3
        pad[m] += tone * env
    pad *= 0.16

    beat = 60 / 100
    bass = np.zeros(n)
    for i, ch in enumerate(chords):
        for k in np.arange(i * seg, min(seconds, (i + 1) * seg) - 0.001, beat * 2):
            m = (t >= k) & (t < k + beat * 1.9)
            local = t[m] - k
            bass[m] += np.sin(2 * np.pi * ch[0] / 2 * local) * np.exp(-2.5 * local)
    bass *= 0.22

    arp = np.zeros(n)
    pattern = [0, 1, 2, 1, 2, 1, 0, 2]
    idx = 0
    for k in np.arange(0.0, seconds, beat / 2):
        ch = chords[min(3, int(k // seg))]
        f = ch[pattern[idx % len(pattern)]] * 2
        m = (t >= k) & (t < k + beat * 0.9)
        local = t[m] - k
        arp[m] += np.sin(2 * np.pi * f * local) * np.exp(-7 * local)
        idx += 1
    gate = np.clip((t - 4.0) / 2.0, 0, 1) * np.clip((seconds - 6.0 - t) / 3.0, 0, 1)
    arp *= 0.10 * gate

    kick = np.zeros(n)
    for k in np.arange(5.0, seconds - 2.0, beat):
        m = (t >= k) & (t < k + 0.35)
        local = t[m] - k
        kick[m] += np.sin(2 * np.pi * (58 - 18 * local) * local) * np.exp(-12 * local)
    kick *= 0.30

    rng = np.random.default_rng(7)
    hats = np.zeros(n)
    for k in np.arange(5.0, seconds - 2.0, beat / 2):
        m = (t >= k) & (t < k + 0.08)
        local = t[m] - k
        hats[m] += rng.standard_normal(m.sum()) * np.exp(-50 * local)
    hats *= 0.035

    mix = pad + bass + arp + kick + hats
    fade = np.minimum(1.0, t / 1.2) * np.minimum(1.0, np.clip((seconds - t) / 3.0, 0, 1))
    mix *= fade
    mix = np.tanh(mix * 1.3) / np.tanh(1.3)
    mix = mix / max(1e-6, np.max(np.abs(mix))) * 0.42
    stereo = np.stack([mix, np.roll(mix, 64)], axis=1)
    data = (stereo * 32767).astype(np.int16).tobytes()
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(data)


def ffmpeg_exe():
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except Exception:
        return "ffmpeg"


def main():
    global LOGO, SHOT1, SHOT2, SHOT3, BACKDROPS, BASE_INTRO, BASE_POINTS
    os.makedirs(OUT_DIR, exist_ok=True)
    LOGO = Image.open(os.path.join(ROOT, "assets", "logo.png")).convert("RGBA")
    SHOT1 = Image.open(os.path.join(SHOTS, "s1.png")).convert("RGB")
    SHOT2 = Image.open(os.path.join(SHOTS, "s2.png")).convert("RGB")
    SHOT3 = Image.open(os.path.join(SHOTS, "s3.png")).convert("RGB")
    BACKDROPS = {id(SHOT1): blurred_backdrop(SHOT1), id(SHOT2): blurred_backdrop(SHOT2), id(SHOT3): blurred_backdrop(SHOT3)}
    BASE_INTRO = vertical_gradient((W, H), (30, 34, 86), (10, 10, 24))
    # soft glow blobs for the intro/outro background
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse((-280, -320, 700, 620), fill=(80, 90, 255, 70))
    gd.ellipse((1250, 640, 2200, 1500), fill=(150, 70, 255, 60))
    glow = glow.filter(ImageFilter.GaussianBlur(120))
    BASE_INTRO = BASE_INTRO.convert("RGBA")
    BASE_INTRO.alpha_composite(glow)
    BASE_POINTS = vertical_gradient((W, H), (20, 22, 58), (9, 9, 22)).convert("RGBA")
    BASE_POINTS.alpha_composite(glow)

    scenes = build_scenes()
    frames = int(TOTAL * FPS)
    fdir = os.path.join(OUT_DIR, "frames")
    os.makedirs(fdir, exist_ok=True)
    for i in range(frames):
        tsec = i / FPS
        img = frame_at(scenes, tsec)
        img.save(os.path.join(fdir, f"{i:05d}.png"), compress_level=1)
        if i % 120 == 0:
            print(f"frame {i}/{frames}", flush=True)

    wav = os.path.join(OUT_DIR, "promo.wav")
    make_audio(wav, TOTAL)

    cmd = [
        ffmpeg_exe(), "-y", "-framerate", str(FPS), "-i", os.path.join(fdir, "%05d.png"),
        "-i", wav, "-c:v", "libx264", "-preset", "medium", "-crf", "18",
        "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "160k", "-shortest",
        "-movflags", "+faststart", OUT_MP4,
    ]
    print(" ".join(cmd))
    subprocess.run(cmd, check=True)
    print("video:", OUT_MP4, os.path.getsize(OUT_MP4))


if __name__ == "__main__":
    sys.exit(main())
