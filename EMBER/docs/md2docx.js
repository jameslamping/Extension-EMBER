// Minimal Markdown -> DOCX converter for the LANDIS-II user guide layout.
// Supports: YAML front matter (title, subtitle, author list, date), # ## ### headings,
// paragraphs with **bold** *italic* `code`, "* " bullets, "1. " numbered items,
// fenced code blocks, and a TOC after the title page.
const fs = require("fs");
const {
  Document, Packer, Paragraph, TextRun, HeadingLevel, TableOfContents, PageBreak,
  AlignmentType, LevelFormat, ShadingType, BorderStyle, PageNumber, Footer, Header,
} = require("docx");

const [, , inPath, outPath] = process.argv;
const src = fs.readFileSync(inPath, "utf8");

// ---- front matter -----------------------------------------------------------------
let body = src, meta = { title: "", subtitle: "", author: [], date: "" };
const fm = src.match(/^---\n([\s\S]*?)\n---\n/);
if (fm) {
  body = src.slice(fm[0].length);
  const lines = fm[1].split("\n");
  let key = null;
  for (const l of lines) {
    const m = l.match(/^(\w+):\s*(.*)$/);
    if (m) { key = m[1]; const v = m[2].trim().replace(/^"|"$/g, ""); if (v) meta[key] = v; else meta[key] = []; }
    else if (key && l.trim().startsWith("- ")) meta[key].push(l.trim().slice(2).replace(/^"|"$/g, ""));
  }
}

const FONT = "Calibri", MONO = "Consolas";

// inline formatting -> TextRuns
function runs(text, base = {}) {
  const out = [];
  const re = /(\*\*[^*]+\*\*|\*[^*]+\*|`[^`]+`)/g;
  let last = 0, m;
  while ((m = re.exec(text)) !== null) {
    if (m.index > last) out.push(new TextRun({ text: text.slice(last, m.index), font: FONT, size: 22, ...base }));
    const t = m[0];
    if (t.startsWith("**")) out.push(new TextRun({ text: t.slice(2, -2), bold: true, font: FONT, size: 22, ...base }));
    else if (t.startsWith("`")) out.push(new TextRun({ text: t.slice(1, -1), font: MONO, size: 20, ...base }));
    else out.push(new TextRun({ text: t.slice(1, -1), italics: true, font: FONT, size: 22, ...base }));
    last = m.index + t.length;
  }
  if (last < text.length) out.push(new TextRun({ text: text.slice(last), font: FONT, size: 22, ...base }));
  return out;
}

const children = [];
// ---- title page --------------------------------------------------------------------
children.push(new Paragraph({ spacing: { before: 2400, after: 240 }, alignment: AlignmentType.CENTER,
  children: [new TextRun({ text: meta.title, bold: true, size: 48, font: FONT })] }));
if (meta.subtitle) children.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 600 },
  children: [new TextRun({ text: meta.subtitle, size: 28, font: FONT, italics: true })] }));
for (const a of meta.author) children.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 120 },
  children: [new TextRun({ text: a, size: 26, font: FONT })] }));
children.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 600 },
  children: [new TextRun({ text: meta.date, size: 22, font: FONT })] }));
children.push(new Paragraph({ children: [new PageBreak()] }));
children.push(new Paragraph({ children: [new TextRun({ text: "Table of Contents", bold: true, size: 28, font: FONT })], spacing: { after: 200 } }));
children.push(new TableOfContents("Contents", { hyperlink: true, headingStyleRange: "1-3" }));
children.push(new Paragraph({ children: [new PageBreak()] }));

// ---- body --------------------------------------------------------------------------
const lines = body.split("\n");
let i = 0, para = [], h1 = 0, h2 = 0, h3 = 0, listSeq = 0;
function flushPara() {
  if (para.length) { children.push(new Paragraph({ children: runs(para.join(" ")), spacing: { after: 160 } })); para = []; }
}
while (i < lines.length) {
  const line = lines[i];
  if (line.startsWith("```")) {
    flushPara();
    const code = [];
    i++;
    while (i < lines.length && !lines[i].startsWith("```")) { code.push(lines[i]); i++; }
    i++;
    for (const c of code) {
      children.push(new Paragraph({
        children: [new TextRun({ text: c.length ? c : " ", font: MONO, size: 18 })],
        shading: { type: ShadingType.CLEAR, fill: "F2F2F2", color: "auto" },
        spacing: { after: 0, line: 240 }, indent: { left: 360 },
      }));
    }
    children.push(new Paragraph({ spacing: { after: 160 } }));
    continue;
  }
  let m;
  if ((m = line.match(/^(#{1,3})\s+(.*)$/))) {
    flushPara();
    const level = m[1].length;
    let label = m[2];
    if (level === 1) { h1++; h2 = 0; h3 = 0; label = `${h1}. ${label}`; if (h1 > 1) children.push(new Paragraph({ children: [new PageBreak()] })); }
    else if (level === 2) { h2++; h3 = 0; label = `${h1}.${h2}. ${label}`; }
    else { h3++; label = `${h1}.${h2}.${h3}. ${label}`; }
    children.push(new Paragraph({
      heading: level === 1 ? HeadingLevel.HEADING_1 : level === 2 ? HeadingLevel.HEADING_2 : HeadingLevel.HEADING_3,
      children: [new TextRun({ text: label, font: FONT })],
      spacing: { before: level === 1 ? 360 : 240, after: 120 },
    }));
    i++; continue;
  }
  if ((m = line.match(/^\*\s+(.*)$/))) {
    flushPara();
    children.push(new Paragraph({ children: runs(m[1]), numbering: { reference: "bullets", level: 0 }, spacing: { after: 80 } }));
    i++; continue;
  }
  if ((m = line.match(/^(\d+)\.\s+(.*)$/))) {
    flushPara();
    if (m[1] === "1") listSeq++;
    children.push(new Paragraph({ children: runs(m[2]), numbering: { reference: "numbers" + listSeq, level: 0 }, spacing: { after: 80 } }));
    i++; continue;
  }
  if (line.trim() === "") { flushPara(); i++; continue; }
  para.push(line.trim());
  i++;
}
flushPara();

const numbering = { config: [
  { reference: "bullets", levels: [{ level: 0, format: LevelFormat.BULLET, text: "•", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] },
] };
for (let k = 1; k <= listSeq; k++)
  numbering.config.push({ reference: "numbers" + k, levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] });

const doc = new Document({
  creator: meta.author.join(", "),
  title: meta.title,
  numbering,
  styles: {
    default: { document: { run: { font: FONT, size: 22 } } },
    paragraphStyles: [
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true, run: { size: 32, bold: true, color: "1F3864", font: FONT }, paragraph: { spacing: { before: 360, after: 160 }, outlineLevel: 0 } },
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true, run: { size: 26, bold: true, color: "2E5395", font: FONT }, paragraph: { spacing: { before: 240, after: 100 }, outlineLevel: 1 } },
      { id: "Heading3", name: "Heading 3", basedOn: "Normal", next: "Normal", quickFormat: true, run: { size: 23, bold: true, italics: true, color: "2E5395", font: FONT }, paragraph: { spacing: { before: 200, after: 80 }, outlineLevel: 2 } },
    ],
  },
  features: { updateFields: true },
  sections: [{
    properties: { page: { size: { width: 12240, height: 15840 }, margin: { top: 1440, bottom: 1440, left: 1440, right: 1440 } } },
    headers: { default: new Header({ children: [new Paragraph({ alignment: AlignmentType.RIGHT, children: [new TextRun({ text: meta.title, size: 18, font: FONT, color: "666666" })] })] }) },
    footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ children: [PageNumber.CURRENT], size: 18, font: FONT })] })] }) },
    children,
  }],
});

Packer.toBuffer(doc).then(buf => { fs.writeFileSync(outPath, buf); console.log("wrote", outPath, buf.length, "bytes"); });
