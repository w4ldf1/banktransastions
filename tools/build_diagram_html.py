from __future__ import annotations

import html
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE_DIR = ROOT / "docs" / "diagrams"
OUT_DIR = ROOT / "docs" / "diagrams-html"

DIAGRAMS = [
    ("class-diagram.mmd", "class.html", "UML Class Diagram"),
    ("dfd.mmd", "dfd.html", "DFD"),
    ("db-schema.mmd", "erd.html", "ERD / DB Schema"),
    ("context-idef0.mmd", "idef0.html", "IDEF0"),
    ("idef3-process.mmd", "idef3.html", "IDEF3"),
    ("sequence-transfer.mmd", "sequence.html", "UML Sequence Diagram"),
    ("use-cases.mmd", "use-case.html", "UML Use Case"),
]

CUSTOM_HTML = {"dfd.html", "idef0.html", "idef3.html"}

HTML_TEMPLATE = """<!doctype html>
<html lang="ru">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{title}</title>
  <style>
    body {{
      margin: 0;
      padding: 28px;
      font-family: Arial, sans-serif;
      color: #1f2937;
      background: #f8fafc;
    }}
    h1 {{
      margin: 0 0 18px;
      font-size: 22px;
      font-weight: 700;
    }}
    .sheet {{
      background: #ffffff;
      border: 1px solid #d8dee8;
      border-radius: 10px;
      padding: 24px;
      overflow: auto;
      box-shadow: 0 12px 30px rgba(15, 23, 42, 0.08);
    }}
  </style>
</head>
<body>
  <h1>{title}</h1>
  <div class="sheet">
    <pre class="mermaid">{diagram}</pre>
  </div>
  <script type="module">
    import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.esm.min.mjs';
    mermaid.initialize({{ startOnLoad: true, securityLevel: 'loose', theme: 'default' }});
  </script>
</body>
</html>
"""


INDEX_TEMPLATE = """<!doctype html>
<html lang="ru">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Диаграммы проекта</title>
  <style>
    body {{
      margin: 0;
      padding: 36px;
      font-family: Arial, sans-serif;
      color: #172033;
      background: #f8fafc;
    }}
    h1 {{ margin-top: 0; }}
    a {{
      display: block;
      width: fit-content;
      margin: 10px 0;
      padding: 10px 14px;
      border: 1px solid #d8dee8;
      border-radius: 8px;
      background: #ffffff;
      color: #1d4ed8;
      text-decoration: none;
    }}
  </style>
</head>
<body>
  <h1>Диаграммы проекта</h1>
  {links}
</body>
</html>
"""


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    links = []

    for source_name, output_name, title in DIAGRAMS:
        if output_name in CUSTOM_HTML:
            links.append(f'<a href="{output_name}">{html.escape(title)}</a>')
            continue

        source = SOURCE_DIR / source_name
        output = OUT_DIR / output_name
        diagram = html.escape(source.read_text(encoding="utf-8"))
        output.write_text(HTML_TEMPLATE.format(title=title, diagram=diagram), encoding="utf-8")
        links.append(f'<a href="{output_name}">{html.escape(title)}</a>')

    (OUT_DIR / "index.html").write_text(
        INDEX_TEMPLATE.format(links="\n  ".join(links)),
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
