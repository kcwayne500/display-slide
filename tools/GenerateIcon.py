from pathlib import Path

from PIL import Image, ImageDraw


icon = Image.new("RGBA", (128, 128))
draw = ImageDraw.Draw(icon)
draw.rounded_rectangle((6, 9, 122, 119), radius=22, fill="#204d98")
draw.rounded_rectangle((21, 26, 44, 76), radius=4, fill="#ffffff")
draw.rounded_rectangle((50, 34, 107, 76), radius=4, fill="#ffffff")
draw.rounded_rectangle((46, 83, 85, 105), radius=4, fill="#83ccff")

destination = Path(__file__).resolve().parents[1] / "src" / "DisplaySlide.ico"
icon.save(destination, sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128)])
