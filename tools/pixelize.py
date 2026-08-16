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
    """마젠타 배경 투명화: 가장자리 연결 영역은 flood fill + 폐쇄 영역은 전역 스윕.

    시트(2x2 그리드)에서는 캐릭터 실루엣 안에 갇힌 마젠타 주머니가 생기므로
    flood fill만으로는 잔여물이 남는다. 전역 스윕은 순수 마젠타 근방만 지우므로
    캐릭터의 보라/분홍 계열(거리 tolerance 초과)은 보존된다."""
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
    # 폐쇄 영역 스윕 — 실루엣 내부에 갇힌 마젠타 주머니 제거
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a != 0 and color_dist2((r, g, b), MAGENTA) <= tol2:
                px[x, y] = (0, 0, 0, 0)
    return img


def align_cells(img, mode):
    """2x2 시트의 각 셀에서 실루엣(알파 bbox)을 기준 위치로 정렬한다.

    AI 생성 시트는 셀마다 캐릭터 위치가 어긋나 프레임 재생 시 좌표 지터가 생긴다.
    bottom: 하단 중앙 고정(발 기준 — 캐릭터용), center: 정중앙 고정(FX 버스트용)."""
    w, h = img.size
    cw, ch = w // 2, h // 2
    bottom_margin = max(4, ch // 12)
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for cy in range(2):
        for cx in range(2):
            cell = img.crop((cx * cw, cy * ch, (cx + 1) * cw, (cy + 1) * ch))
            bbox = cell.getchannel("A").getbbox()
            if bbox is None:
                continue
            bw = bbox[2] - bbox[0]
            bh = bbox[3] - bbox[1]
            dx = (cw - bw) // 2 - bbox[0]
            if mode == "bottom":
                dy = (ch - bottom_margin - bbox[3])
            else:  # center
                dy = (ch - bh) // 2 - bbox[1]
            out.paste(cell, (cx * cw + dx, cy * ch + dy), cell)
    return out


def pixelize(src, size, colors, tolerance, align="none"):
    img = Image.open(src).convert("RGB")
    img = img.resize((size, size), Image.NEAREST)
    img = img.quantize(colors=colors, method=Image.MEDIANCUT).convert("RGB")
    img = key_background(img, tolerance)
    if align != "none":
        img = align_cells(img, align)
    return img


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("inputs", nargs="+", type=Path)
    ap.add_argument("-s", "--size", type=int, default=128, help="출력 한 변 px (기본 128)")
    ap.add_argument("-c", "--colors", type=int, default=12, help="팔레트 색 수 (기본 12)")
    ap.add_argument("-t", "--tolerance", type=int, default=60,
                    help="마젠타 키잉 색 거리 허용치 (기본 60; 캐릭터의 보라 계열이 지워지면 낮출 것)")
    ap.add_argument("-o", "--outdir", type=Path, default=None,
                    help="출력 폴더 (기본: 입력 파일 옆에 .pixel.png)")
    ap.add_argument("-a", "--align", choices=["none", "bottom", "center"], default="none",
                    help="2x2 시트 셀별 실루엣 정렬 — bottom: 캐릭터(발 고정), center: FX")
    args = ap.parse_args()

    for src in args.inputs:
        if not src.exists():
            print(f"skip (없음): {src}", file=sys.stderr)
            continue
        out = (args.outdir / f"{src.stem}.png") if args.outdir else src.with_suffix(".pixel.png")
        if args.outdir:
            args.outdir.mkdir(parents=True, exist_ok=True)
        result = pixelize(src, args.size, args.colors, args.tolerance, args.align)
        result.save(out)
        print(f"{src} -> {out} ({args.size}x{args.size}, {args.colors}색)")


if __name__ == "__main__":
    main()
