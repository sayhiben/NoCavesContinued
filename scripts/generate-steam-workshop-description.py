#!/usr/bin/env python3
"""Generate a Steam Workshop BBCode description from README.md."""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

DEFAULT_EXCLUDED_SECTIONS = {
    "Manual Install",
    "GitHub And Development",
    "Development Prerequisites",
    "Build",
    "Development Test",
    "Steam Workshop Publishing",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Convert README Markdown into Steam Workshop BBCode."
    )
    parser.add_argument("input", type=Path, help="Input Markdown file.")
    parser.add_argument("output", type=Path, help="Output Steam BBCode file.")
    parser.add_argument(
        "--include-dev-sections",
        action="store_true",
        help="Convert the full README instead of excluding GitHub/development sections.",
    )
    return parser.parse_args()


def normalize_heading(text: str) -> str:
    return re.sub(r"\s+", " ", text.strip().strip("#").strip())


def steam_facing_markdown(markdown: str, include_dev_sections: bool) -> str:
    if include_dev_sections:
        return markdown

    output_lines: list[str] = []
    skip_current_section = False

    for line in markdown.splitlines():
        if line.startswith("## "):
            heading = normalize_heading(line)
            skip_current_section = heading in DEFAULT_EXCLUDED_SECTIONS

        if not skip_current_section:
            output_lines.append(line)

    return "\n".join(output_lines).strip() + "\n"


def convert_markdown_to_steam_bbcode(markdown: str) -> str:
    try:
        from md2steam import markdown_to_steam_bbcode
    except ImportError as exception:
        raise SystemExit(
            "md2steam is required. Install it with: python3 -m pip install md2steam==1.0.1"
        ) from exception

    return markdown_to_steam_bbcode(markdown).strip() + "\n"


def main() -> int:
    args = parse_args()

    markdown = args.input.read_text(encoding="utf-8")
    selected_markdown = steam_facing_markdown(markdown, args.include_dev_sections)
    steam_bbcode = convert_markdown_to_steam_bbcode(selected_markdown)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(steam_bbcode, encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
