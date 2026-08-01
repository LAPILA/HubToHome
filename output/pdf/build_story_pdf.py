from __future__ import annotations

import html
import re
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    BaseDocTemplate,
    Frame,
    HRFlowable,
    ListFlowable,
    ListItem,
    NextPageTemplate,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)
from reportlab.platypus.tableofcontents import TableOfContents


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / "hub-to-home-story-synthesis.md"
OUTPUT = ROOT / "hub-to-home-story-synthesis.pdf"

NAVY = colors.HexColor("#162434")
INK = colors.HexColor("#26323D")
MUTED = colors.HexColor("#64717D")
TEAL = colors.HexColor("#3C7A78")
PALE = colors.HexColor("#EEF3F2")
GOLD = colors.HexColor("#C89B4A")
WHITE = colors.white


pdfmetrics.registerFont(TTFont("Malgun", r"C:\Windows\Fonts\malgun.ttf"))
pdfmetrics.registerFont(TTFont("Malgun-Bold", r"C:\Windows\Fonts\malgunbd.ttf"))
pdfmetrics.registerFontFamily(
    "Malgun",
    normal="Malgun",
    bold="Malgun-Bold",
    italic="Malgun",
    boldItalic="Malgun-Bold",
)


def inline_markup(text: str) -> str:
    escaped = html.escape(text)
    escaped = re.sub(r"`([^`]+)`", r'<font name="Malgun-Bold" color="#3C7A78">\1</font>', escaped)
    escaped = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", escaped)
    escaped = re.sub(
        r"(https?://[^\s<]+)",
        r'<link href="\1" color="#2D6F9F"><u>\1</u></link>',
        escaped,
    )
    return escaped


class StoryDocTemplate(BaseDocTemplate):
    def afterFlowable(self, flowable):
        if not isinstance(flowable, Paragraph):
            return
        style_name = flowable.style.name
        if style_name not in {"StoryHeading1", "StoryHeading2"}:
            return
        level = 0 if style_name == "StoryHeading1" else 1
        text = flowable.getPlainText()
        key = f"h-{level}-{self.page}-{abs(hash(text))}"
        self.canv.bookmarkPage(key)
        self.canv.addOutlineEntry(text, key, level=level, closed=False)
        self.notify("TOCEntry", (level, text, self.page, key))


def header_footer(canvas, doc):
    canvas.saveState()
    width, height = A4
    canvas.setStrokeColor(colors.HexColor("#D7DEDE"))
    canvas.setLineWidth(0.5)
    canvas.line(18 * mm, height - 14 * mm, width - 18 * mm, height - 14 * mm)
    canvas.setFont("Malgun", 8)
    canvas.setFillColor(MUTED)
    canvas.drawString(18 * mm, height - 10.5 * mm, "HUB TO HOME - 비교 작품 분석 및 통합 시놉시스")
    canvas.drawRightString(width - 18 * mm, 10 * mm, str(doc.page))
    canvas.restoreState()


def first_page(canvas, doc):
    canvas.saveState()
    width, height = A4
    canvas.setFillColor(NAVY)
    canvas.rect(0, height - 66 * mm, width, 66 * mm, stroke=0, fill=1)
    canvas.setFillColor(TEAL)
    canvas.rect(0, 0, 8 * mm, height, stroke=0, fill=1)
    canvas.setFillColor(GOLD)
    canvas.rect(8 * mm, height - 69 * mm, width - 8 * mm, 3 * mm, stroke=0, fill=1)
    canvas.restoreState()


styles = getSampleStyleSheet()
styles.add(
    ParagraphStyle(
        name="CoverTitle",
        fontName="Malgun-Bold",
        fontSize=28,
        leading=35,
        textColor=WHITE,
        alignment=TA_LEFT,
        spaceAfter=8 * mm,
    )
)
styles.add(
    ParagraphStyle(
        name="CoverSubtitle",
        fontName="Malgun",
        fontSize=14,
        leading=21,
        textColor=colors.HexColor("#DCE8E7"),
        spaceAfter=24 * mm,
    )
)
styles.add(
    ParagraphStyle(
        name="CoverMeta",
        fontName="Malgun",
        fontSize=10,
        leading=17,
        textColor=INK,
        spaceAfter=2 * mm,
    )
)
styles.add(
    ParagraphStyle(
        name="StoryHeading1",
        fontName="Malgun-Bold",
        fontSize=19,
        leading=25,
        textColor=NAVY,
        spaceBefore=5 * mm,
        spaceAfter=4 * mm,
        keepWithNext=True,
    )
)
styles.add(
    ParagraphStyle(
        name="StoryHeading2",
        fontName="Malgun-Bold",
        fontSize=14,
        leading=20,
        textColor=TEAL,
        spaceBefore=4 * mm,
        spaceAfter=2.5 * mm,
        keepWithNext=True,
    )
)
styles.add(
    ParagraphStyle(
        name="StoryHeading3",
        fontName="Malgun-Bold",
        fontSize=11.2,
        leading=16,
        textColor=INK,
        spaceBefore=3.5 * mm,
        spaceAfter=1.5 * mm,
        keepWithNext=True,
    )
)
styles.add(
    ParagraphStyle(
        name="BodyKo",
        fontName="Malgun",
        fontSize=9.4,
        leading=16.2,
        textColor=INK,
        alignment=TA_LEFT,
        wordWrap="CJK",
        spaceAfter=3 * mm,
        allowWidows=0,
        allowOrphans=0,
    )
)
styles.add(
    ParagraphStyle(
        name="BulletKo",
        parent=styles["BodyKo"],
        leftIndent=5 * mm,
        firstLineIndent=0,
        spaceAfter=1.3 * mm,
    )
)
styles.add(
    ParagraphStyle(
        name="QuoteKo",
        fontName="Malgun-Bold",
        fontSize=11,
        leading=18,
        textColor=NAVY,
        leftIndent=10 * mm,
        rightIndent=10 * mm,
        borderColor=TEAL,
        borderWidth=1.5,
        borderPadding=(4 * mm, 5 * mm, 4 * mm, 5 * mm),
        backColor=PALE,
        spaceBefore=3 * mm,
        spaceAfter=5 * mm,
        wordWrap="CJK",
    )
)
styles.add(
    ParagraphStyle(
        name="TOCTitle",
        fontName="Malgun-Bold",
        fontSize=20,
        leading=26,
        textColor=NAVY,
        spaceAfter=7 * mm,
    )
)
styles.add(
    ParagraphStyle(
        name="TOCLevel0",
        fontName="Malgun-Bold",
        fontSize=10.5,
        leading=17,
        leftIndent=0,
        firstLineIndent=0,
        textColor=INK,
        spaceBefore=1.5 * mm,
    )
)
styles.add(
    ParagraphStyle(
        name="TOCLevel1",
        fontName="Malgun",
        fontSize=9,
        leading=14,
        leftIndent=8 * mm,
        firstLineIndent=0,
        textColor=MUTED,
    )
)


def parse_blocks(lines: list[str]):
    blocks: list = []
    paragraph: list[str] = []
    bullets: list[str] = []
    numbers: list[str] = []
    quote: list[str] = []

    def flush_paragraph():
        nonlocal paragraph
        if paragraph:
            blocks.append(Paragraph(inline_markup(" ".join(paragraph)), styles["BodyKo"]))
            paragraph = []

    def flush_bullets():
        nonlocal bullets
        if bullets:
            items = [
                ListItem(Paragraph(inline_markup(item), styles["BulletKo"]), leftIndent=0)
                for item in bullets
            ]
            blocks.append(
                ListFlowable(
                    items,
                    bulletType="bullet",
                    start="circle",
                    leftIndent=6 * mm,
                    bulletFontName="Malgun",
                    bulletFontSize=7,
                    bulletColor=TEAL,
                    spaceAfter=3 * mm,
                )
            )
            bullets = []

    def flush_numbers():
        nonlocal numbers
        if numbers:
            items = [
                ListItem(Paragraph(inline_markup(item), styles["BulletKo"]), leftIndent=0)
                for item in numbers
            ]
            blocks.append(
                ListFlowable(
                    items,
                    bulletType="1",
                    leftIndent=7 * mm,
                    bulletFontName="Malgun-Bold",
                    bulletFontSize=8,
                    bulletColor=TEAL,
                    spaceAfter=3 * mm,
                )
            )
            numbers = []

    def flush_quote():
        nonlocal quote
        if quote:
            blocks.append(Paragraph(inline_markup(" ".join(quote)), styles["QuoteKo"]))
            quote = []

    def flush_all():
        flush_paragraph()
        flush_bullets()
        flush_numbers()
        flush_quote()

    first_h1_seen = False
    for raw in lines:
        line = raw.rstrip()
        if not line:
            flush_all()
            continue
        if line == "---":
            flush_all()
            blocks.append(
                HRFlowable(
                    width="100%",
                    thickness=0.6,
                    color=colors.HexColor("#D7DEDE"),
                    spaceBefore=2 * mm,
                    spaceAfter=4 * mm,
                )
            )
            continue
        if line.startswith("# "):
            flush_all()
            if first_h1_seen:
                blocks.append(PageBreak())
            first_h1_seen = True
            blocks.append(Paragraph(inline_markup(line[2:]), styles["StoryHeading1"]))
            continue
        if line.startswith("## "):
            flush_all()
            blocks.append(Paragraph(inline_markup(line[3:]), styles["StoryHeading2"]))
            continue
        if line.startswith("### "):
            flush_all()
            blocks.append(Paragraph(inline_markup(line[4:]), styles["StoryHeading3"]))
            continue
        if line.startswith("> "):
            flush_paragraph()
            flush_bullets()
            flush_numbers()
            quote.append(line[2:])
            continue
        if line.startswith("- "):
            flush_paragraph()
            flush_numbers()
            flush_quote()
            bullets.append(line[2:])
            continue
        if re.match(r"^\d+\.\s+", line):
            flush_paragraph()
            flush_bullets()
            flush_quote()
            numbers.append(re.sub(r"^\d+\.\s+", "", line))
            continue
        flush_bullets()
        flush_numbers()
        flush_quote()
        paragraph.append(line)

    flush_all()
    return blocks


def build():
    text = SOURCE.read_text(encoding="utf-8")
    lines = text.splitlines()

    first_rule = lines.index("---")
    cover_lines = lines[:first_rule]
    body_lines = lines[first_rule + 1 :]

    title = cover_lines[0].removeprefix("# ").strip()
    subtitle = cover_lines[1].removeprefix("## ").strip()
    meta = [line for line in cover_lines[2:] if line.strip()]

    page_width, page_height = A4
    normal_frame = Frame(
        18 * mm,
        16 * mm,
        page_width - 36 * mm,
        page_height - 34 * mm,
        leftPadding=0,
        rightPadding=0,
        topPadding=0,
        bottomPadding=0,
    )
    cover_frame = Frame(
        23 * mm,
        24 * mm,
        page_width - 41 * mm,
        page_height - 43 * mm,
        leftPadding=0,
        rightPadding=0,
        topPadding=0,
        bottomPadding=0,
    )

    doc = StoryDocTemplate(
        str(OUTPUT),
        pagesize=A4,
        title="HUB TO HOME - 비교 작품 서사 분석 및 통합 스토리 시놉시스",
        author="Codex / HUB TO HOME",
        subject="게임 시나리오 분석 및 통합 시놉시스",
        leftMargin=18 * mm,
        rightMargin=18 * mm,
        topMargin=18 * mm,
        bottomMargin=16 * mm,
    )
    doc.addPageTemplates(
        [
            PageTemplate(id="Cover", frames=[cover_frame], onPage=first_page),
            PageTemplate(id="Normal", frames=[normal_frame], onPage=header_footer),
        ]
    )

    story = [
        Spacer(1, 12 * mm),
        Paragraph(inline_markup(title), styles["CoverTitle"]),
        Paragraph(inline_markup(subtitle), styles["CoverSubtitle"]),
        Spacer(1, 20 * mm),
    ]
    for entry in meta:
        story.append(Paragraph(inline_markup(entry), styles["CoverMeta"]))

    summary_data = [
        [
            Paragraph("<b>표면 톤</b>", styles["BodyKo"]),
            Paragraph("가볍고 기묘한 철도 여행", styles["BodyKo"]),
        ],
        [
            Paragraph("<b>내부 갈등</b>", styles["BodyKo"]),
            Paragraph("책임, 단절, 공존, 연결의 비용", styles["BodyKo"]),
        ],
        [
            Paragraph("<b>핵심 구조</b>", styles["BodyKo"]),
            Paragraph("지역과 동료가 열차에 축적되어 최종 작전을 가능하게 함", styles["BodyKo"]),
        ],
    ]
    summary_table = Table(summary_data, colWidths=[31 * mm, 117 * mm])
    summary_table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (0, -1), PALE),
                ("TEXTCOLOR", (0, 0), (-1, -1), INK),
                ("FONTNAME", (0, 0), (-1, -1), "Malgun"),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("BOX", (0, 0), (-1, -1), 0.6, colors.HexColor("#C9D4D3")),
                ("INNERGRID", (0, 0), (-1, -1), 0.4, colors.HexColor("#D7DEDE")),
                ("LEFTPADDING", (0, 0), (-1, -1), 7),
                ("RIGHTPADDING", (0, 0), (-1, -1), 7),
                ("TOPPADDING", (0, 0), (-1, -1), 6),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
            ]
        )
    )
    story.extend([Spacer(1, 12 * mm), summary_table, NextPageTemplate("Normal"), PageBreak()])

    toc = TableOfContents()
    toc.levelStyles = [styles["TOCLevel0"], styles["TOCLevel1"]]
    story.extend(
        [
            Paragraph("목차", styles["TOCTitle"]),
            Paragraph(
                "문서의 장·절 제목을 기준으로 구성했습니다. PDF 뷰어의 책갈피에서도 같은 구조를 확인할 수 있습니다.",
                styles["BodyKo"],
            ),
            toc,
            PageBreak(),
        ]
    )
    story.extend(parse_blocks(body_lines))
    doc.multiBuild(story)


if __name__ == "__main__":
    build()
    print(OUTPUT)
