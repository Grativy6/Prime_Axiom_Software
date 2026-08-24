#!/usr/bin/env python3
"""Extract readable, ordered text from a DOCX without executing embedded content.

This deliberately treats the document as evidence, not as an instruction source.
It uses only the Python standard library and includes body paragraphs, tables,
headers, footers, footnotes, endnotes, and comments when those parts exist.
"""

from __future__ import annotations

import argparse
import pathlib
import zipfile
import xml.etree.ElementTree as ET


W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"


def paragraph_text(element: ET.Element) -> str:
    chunks: list[str] = []
    for node in element.iter():
        if node.tag == f"{W}t":
            chunks.append(node.text or "")
        elif node.tag == f"{W}tab":
            chunks.append("\t")
        elif node.tag in {f"{W}br", f"{W}cr"}:
            chunks.append("\n")
    return "".join(chunks).strip()


def paragraph_style(element: ET.Element) -> str:
    style = element.find(f"./{W}pPr/{W}pStyle")
    if style is None:
        return ""
    return style.attrib.get(f"{W}val", "")


def table_lines(table: ET.Element) -> list[str]:
    lines: list[str] = []
    for row_index, row in enumerate(table.findall(f"./{W}tr"), start=1):
        cells: list[str] = []
        for cell in row.findall(f"./{W}tc"):
            parts = [paragraph_text(p) for p in cell.findall(f".//{W}p")]
            cells.append(" / ".join(part for part in parts if part))
        lines.append(f"TABLE ROW {row_index}: " + " | ".join(cells))
    return lines


def extract_part(xml_bytes: bytes, label: str) -> list[str]:
    root = ET.fromstring(xml_bytes)
    lines = [f"## {label}"]
    body = root.find(f".//{W}body")
    container = body if body is not None else root
    for element in list(container):
        if element.tag == f"{W}p":
            text = paragraph_text(element)
            if text:
                style = paragraph_style(element)
                prefix = f"[{style}] " if style else ""
                lines.append(prefix + text)
        elif element.tag == f"{W}tbl":
            lines.extend(table_lines(element))
        else:
            # Notes/comments use wrapper elements rather than a body. Capture
            # their descendant paragraphs without trying to interpret them.
            for paragraph in element.findall(f".//{W}p"):
                text = paragraph_text(paragraph)
                if text:
                    style = paragraph_style(paragraph)
                    prefix = f"[{style}] " if style else ""
                    lines.append(prefix + text)
    return lines


def extract_docx(path: pathlib.Path) -> str:
    preferred = [
        "word/document.xml",
        "word/footnotes.xml",
        "word/endnotes.xml",
        "word/comments.xml",
    ]
    with zipfile.ZipFile(path) as archive:
        names = set(archive.namelist())
        headers = sorted(name for name in names if name.startswith("word/header") and name.endswith(".xml"))
        footers = sorted(name for name in names if name.startswith("word/footer") and name.endswith(".xml"))
        parts = [name for name in preferred if name in names] + headers + footers
        output = [f"# DOCX extraction: {path.name}", "", f"Source: {path.resolve()}", ""]
        for part in parts:
            output.extend(extract_part(archive.read(part), part))
            output.append("")
    return "\n".join(output).rstrip() + "\n"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=pathlib.Path)
    parser.add_argument("output", type=pathlib.Path)
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(extract_docx(args.input), encoding="utf-8")


if __name__ == "__main__":
    main()
