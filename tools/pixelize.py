#!/usr/bin/env python3
"""AI 생성 픽셀아트 후처리: 다운스케일 → 팔레트 양자화 → 마젠타 배경 투명화.

사용법:
    python3 pixelize.py input.png                  # -> input.pixel.png (128x128)
    python3 pixelize.py input.png -s 96 -c 12      # 크기/색수 지정
    python3 pixelize.py raw/*.png -o ../unity-assets/

art-style-guide.md의 파이프라인 구현체. 배경은 마젠타(#FF00FF) 단색으로
생성됐다고 가정하고, 가장자리에서 연결된 마젠타 계열 픽셀만 투명화한다
(캐릭터 내부의 보라/분홍 계열은 보존).
"""
import argparse
import sys
from collections import deque
from pathlib import Path

from PIL import Image

MAGENTA = (255, 0, 255)


def color_dist2(a, b):
    return (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2


def key_background(img, tolerance):
    """가장자리에서 연결된 마젠타 계열 영역만 flood fill로 투명화."""
    img = img.convert("RGBA")
    px = img.load()
    w, h = img.size
    tol2 = tolerance * tolerance
    seen = set()
    queue = deque()
    for x in range(w):
        queue.append((x, 0))
        queue.append((x, h - 1))
    for y in range(h):
        queue.append((0, y))
        queue.append((w - 1, y))
    while queue:
        x, y = queue.popleft()
        if (x, y) in seen or not (0 <= x < w and 0 <= y < h):
            continue
        seen.add((x, y))
        r, g, b, a = px[x, y]
        if a == 0 or color_dist2((r, g, b), MAGENTA) > tol2:
            continue
        px[x, y] = (0, 0, 0, 0)
        queue.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    return img


def pixelize(src, size, colors, tolerance):
    img = Image.open(src).convert("RGB")
    img = img.resize((size, size), Image.NEAREST)
    img = img.quantize(colors=colors, method=Image.MEDIANCUT).convert("RGB")
    return key_background(img, tolerance)


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("inputs", nargs="+", type=Path)
    ap.add_argument("-s", "--size", type=int, default=128, help="출력 한 변 px (기본 128)")
    ap.add_argument("-c", "--colors", type=int, default=12, help="팔레트 색 수 (기본 12)")
    ap.add_argument("-t", "--tolerance", type=int, default=60,
                    help="마젠타 키잉 색 거리 허용치 (기본 60; 캐릭터의 보라 계열이 지워지면 낮출 것)")
    ap.add_argument("-o", "--outdir", type=Path, default=None,
                    help="출력 폴더 (기본: 입력 파일 옆에 .pixel.png)")
    args = ap.parse_args()

    for src in args.inputs:
        if not src.exists():
            print(f"skip (없음): {src}", file=sys.stderr)
            continue
        out = (args.outdir / f"{src.stem}.png") if args.outdir else src.with_suffix(".pixel.png")
        if args.outdir:
            args.outdir.mkdir(parents=True, exist_ok=True)
        result = pixelize(src, args.size, args.colors, args.tolerance)
        result.save(out)
        print(f"{src} -> {out} ({args.size}x{args.size}, {args.colors}색)")


if __name__ == "__main__":
    main()
