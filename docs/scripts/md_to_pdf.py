"""Convert price-configurator-back-office-strategy.md to PDF."""
from pathlib import Path

import markdown
from xhtml2pdf import pisa

ROOT = Path(__file__).resolve().parents[1]
MD_PATH = ROOT / "price-configurator-back-office-strategy.md"
PDF_PATH = MD_PATH.with_suffix(".pdf")

# Characters that often fail in default PDF fonts (box-drawing, arrows, bullets).
_UNICODE_REPLACEMENTS = str.maketrans(
    {
        "\u250c": "+",
        "\u2510": "+",
        "\u2514": "+",
        "\u2518": "+",
        "\u2500": "-",
        "\u2502": "|",
        "\u251c": "+",
        "\u2524": "+",
        "\u252c": "+",
        "\u2534": "+",
        "\u253c": "+",
        "\u25bc": "v",
        "\u25b2": "^",
        "\u2192": "->",
        "\u2190": "<-",
        "\u2022": "-",
        "\u2014": "-",
        "\u2013": "-",
    }
)

CSS = """
body { font-family: Helvetica, Arial, sans-serif; font-size: 11pt; line-height: 1.4; margin: 2cm; }
h1 { font-size: 20pt; }
h2 { font-size: 16pt; margin-top: 1.2em; page-break-after: avoid; }
h3 { font-size: 13pt; page-break-after: avoid; }
table { border-collapse: collapse; width: 100%; margin: 1em 0; font-size: 10pt; }
th, td { border: 1px solid #ccc; padding: 6px; text-align: left; vertical-align: top; }
code { font-family: Courier, Courier New, monospace; font-size: 9pt; background: #f5f5f5; }
pre { font-family: Courier, Courier New, monospace; font-size: 9pt; background: #f5f5f5;
      padding: 8px; white-space: pre-wrap; word-wrap: break-word; }
hr { margin: 1.5em 0; }
ol li { margin-bottom: 0.35em; }
"""


def _pdf_safe_text(text: str) -> str:
    return text.translate(_UNICODE_REPLACEMENTS)


def main() -> None:
    text = _pdf_safe_text(MD_PATH.read_text(encoding="utf-8"))
    body = markdown.markdown(text, extensions=["tables", "fenced_code", "nl2br"])
    html = f"""<!DOCTYPE html>
<html><head><meta charset="utf-8"/><style>{CSS}</style></head>
<body>{body}</body></html>"""
    with PDF_PATH.open("wb") as out:
        status = pisa.CreatePDF(html, dest=out, encoding="utf-8")
    if status.err:
        raise SystemExit(f"PDF generation failed: {status.err}")
    print(f"Wrote {PDF_PATH}")


if __name__ == "__main__":
    main()
