"""#45 bounded evaluation: one wholly synthetic submission packet, exported
two ways from the same allowed fields:

  work/out/plain/<packet>/    files/ + manifest-sha256.txt + index.html
  work/out/rocrate/<packet>/  files/ + ro-crate-metadata.json (RO-Crate 1.3)
                                + ro-crate-preview.html

The synthetic "PaperRoute records" in work/source/records.json carry secret
markers in every field the export must leave out (notes, correspondence,
reviewer identities, absolute paths). The source records and files are
hashed before and after to show the export changes nothing.

Standard library only. Run: python make_eval.py
"""

import datetime as dt
import hashlib
import html
import json
import os
import shutil
import struct
import urllib.parse
import zipfile
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(HERE, "work")          # generated; ignored by git
SOURCE = os.path.join(WORK, "source")
OUT = os.path.join(WORK, "out")
EXPORT_DATE = "2026-10-03"
EXPORT_TIME = "2026-10-03T16:00:00Z"
SPEC = "https://w3id.org/ro/crate/1.3"
CONTEXT = "https://w3id.org/ro/crate/1.3/context"


# ---------------------------------------------------------------------------
# Synthetic source files and records (what PaperRoute would hold)
# ---------------------------------------------------------------------------

def tiny_png(rgb):
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
    raw = b"".join(b"\x00" + bytes(rgb) * 4 for _ in range(4))
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 4, 4, 8, 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))


def tiny_pdf(text, author):
    # A minimal one-page PDF whose document information names an author:
    # files are copied byte for byte, so hidden metadata travels with them.
    objects = [
        b"<< /Type /Catalog /Pages 2 0 R >>",
        b"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
        None,
        b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ("<< /Title (%s) /Author (%s) /Producer (synthetic) >>" % (text, author)).encode("latin-1"),
    ]
    stream = ("BT /F1 12 Tf 72 720 Td (%s) Tj ET" % text).encode("latin-1")
    objects[3] = b"<< /Length %d >>\nstream\n" % len(stream) + stream + b"\nendstream"
    out = bytearray(b"%PDF-1.4\n")
    offsets = []
    for number, body in enumerate(objects, start=1):
        offsets.append(len(out))
        out += b"%d 0 obj\n" % number + body + b"\nendobj\n"
    xref = len(out)
    out += b"xref\n0 %d\n0000000000 65535 f \n" % (len(objects) + 1)
    for offset in offsets:
        out += b"%010d 00000 n \n" % offset
    out += b"trailer\n<< /Size %d /Root 1 0 R /Info 6 0 R >>\nstartxref\n%d\n%%%%EOF\n" % (len(objects) + 1, xref)
    return bytes(out)


def tiny_docx(paragraph, creator):
    path = os.path.join(SOURCE, "tmp.docx")
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("[Content_Types].xml",
                   '<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
                   '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
                   '<Default Extension="xml" ContentType="application/xml"/>'
                   '<Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>'
                   '<Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/></Types>')
        z.writestr("_rels/.rels",
                   '<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
                   '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>'
                   '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/></Relationships>')
        z.writestr("word/document.xml",
                   '<?xml version="1.0" encoding="UTF-8"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">'
                   '<w:body><w:p><w:r><w:t>%s</w:t></w:r></w:p></w:body></w:document>' % html.escape(paragraph))
        z.writestr("docProps/core.xml",
                   '<?xml version="1.0" encoding="UTF-8"?><cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" '
                   'xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:creator>%s</dc:creator><cp:lastModifiedBy>%s</cp:lastModifiedBy></cp:coreProperties>'
                   % (html.escape(creator), html.escape(creator)))
    with open(path, "rb") as f:
        data = f.read()
    os.remove(path)
    return data


def make_source():
    if os.path.isdir(SOURCE):
        shutil.rmtree(SOURCE)
    files = os.path.join(SOURCE, "PaperRoute Library", "3f2a", "packets", "9c41")
    os.makedirs(files)

    def put(name, data):
        path = os.path.join(files, name)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as f:
            f.write(data)
        return path

    main = put("main_1a2b3c4d.pdf", tiny_pdf("Example: anchoring effects in clinical risk estimates (revision 2)", "SECRET-PDF-AUTHOR Josiah Carberry"))
    title = put("title_5e6f7a8b.docx", tiny_docx("Title page. Josiah Carberry, Brown University.", "SECRET-DOCX-CREATOR"))
    fig1 = put("fig1_0a0b0c0d.png", tiny_png((200, 40, 40)))
    fig1b = put("fig1_1a1b1c1d.png", tiny_png((40, 40, 200)))
    table = put("table_2a2b2c2d.csv", b"condition,estimate\nanchor high,0.42\nanchor low,0.31\n")
    letter = put("letter_3a3b3c3d.txt", b"Dear Editor, please find our second revision attached. -- the authors\n")
    response = put("response_4a4b4c4d.txt", b"Response to SECRET-REVIEWER-LABEL (Reviewer 2, Dr. Secret Reviewer): we have revised the methods.\n")
    data = put("data_5a5b5c5d.txt", b"Data availability: the synthetic data are in table 1.\n")
    # A file whose fingerprint was recorded and then changed on disk.
    changed = put("supp_6a6b6c6d.txt", b"Supplementary note, edited after the fingerprint was recorded.\n")

    def sha(path):
        with open(path, "rb") as f:
            return hashlib.sha256(f.read()).hexdigest()

    def entry(fid, role, label, path, original, notes="", record=True, stored_sha=None):
        return {
            "Id": fid, "Role": role, "Label": label, "Notes": notes,
            "LocalFilePath": path, "StorageMode": "ManagedCopy", "OriginalFileName": original,
            "Sha256": (stored_sha if stored_sha is not None else (sha(path) if record and path else "")),
            "FileSizeBytes": os.path.getsize(path) if path else None,
        }

    records = {
        "Manuscript": {
            "Id": "3f2a0000-0000-0000-0000-000000000001",
            "Title": "Example: anchoring effects in clinical risk estimates",
            "WorkType": "JournalArticle",
            "Notes": "SECRET-MANUSCRIPT-NOTE about the authors' private plans",
            "ManuscriptUrl": "file:///C:/Users/SECRETUSER/Documents/draft.docx",
            "Metadata": {
                "AbstractText": "A fictional study of anchoring in clinicians' risk estimates, made for testing.",
                "Keywords": ["anchoring", "clinical judgment", "risk estimates"],
                "Doi": "10.5555/example.anchoring",
                "PublicationJournal": "Fictional Journal of Psychology",
                "PublishedDate": "2026-01-01",   # year-only, stored as January 1
                "PreprintDoi": "10.5555/example.preprint",
            },
            "Authors": [
                {"Name": "Josiah Carberry", "Orcid": "0000-0002-1825-0097", "Affiliation": "Brown University", "Notes": "SECRET-AUTHOR-NOTE"},
                {"Name": "Riley Placeholder", "Orcid": "", "Affiliation": "Fictional Institute of Psychology", "Notes": ""},
            ],
            "Versions": [{"Id": "v2", "Label": "Revision 2", "CreatedDate": "2026-08-20", "Notes": "SECRET-VERSION-NOTE"}],
            "Submissions": [{
                "Id": "s1", "JournalName": "Fictional Journal of Psychology", "SubmittedDate": "2026-03-02",
                "ManuscriptNumber": "SECRET-MS-NUMBER FJP-2026-0142", "PortalUrl": "https://portal.example.org/SECRET-PORTAL",
                "Notes": "SECRET-SUBMISSION-NOTE",
                "Correspondence": [{"Subject": "SECRET-CORRESPONDENCE decision letter", "Path": "C:\\Users\\SECRETUSER\\mail.eml"}],
                "ReviewerResponses": [{"ReviewerLabel": "SECRET-REVIEWER-LABEL", "Comment": "SECRET-REVIEWER-COMMENT"}],
            }],
        },
        "Journal": {"Name": "Fictional Journal of Psychology", "Issns": ["0000-0019"], "Notes": "SECRET-JOURNAL-NOTE"},
        "Packet": {
            "Id": "9c410000-0000-0000-0000-000000000001",
            "Label": "Revision 2 to Fictional Journal of Psychology",
            "Notes": "SECRET-PACKET-NOTE",
            "ManuscriptVersionId": "v2", "SubmissionId": "s1", "RevisionRoundNumber": 2,
            "JournalName": "Fictional Journal of Psychology",
            "Files": [
                entry("f1", "Manuscript", "Main text, revision 2", main, "Main text (revision 2).pdf", notes="SECRET-FILE-NOTE"),
                entry("f2", "TitlePage", "Title page", title, "Title page.docx"),
                entry("f3", "Figure", "Figure 1", fig1, "Figure 1.png"),
                entry("f4", "Figure", "Figure 1, recolored", fig1b, "Figure 1.png"),
                entry("f5", "Table", "Table 1", table, "Table #1 (50% sample).csv", record=False),
                entry("f6", "CoverLetter", "Cover letter", letter, "Cover letter.txt"),
                entry("f7", "ResponseToReviewers", "Response to reviewers", response, "Response to reviewers.txt"),
                entry("f8", "DataAvailability", "Data availability statement", data, "Data availability.txt"),
                entry("f9", "Supplement", "Supplementary note", changed, "Supplement.txt", stored_sha="0" * 64),
                {"Id": "f10", "Role": "ReportingChecklist", "Label": "Reporting checklist (entered in the portal)", "Notes": "",
                 "LocalFilePath": "", "StorageMode": "MetadataOnly", "OriginalFileName": "", "Sha256": "", "FileSizeBytes": None},
            ],
        },
    }
    with open(os.path.join(SOURCE, "records.json"), "w", encoding="utf-8") as f:
        json.dump(records, f, indent=2)
    return records


# ---------------------------------------------------------------------------
# Export rules (shared by both formats)
# ---------------------------------------------------------------------------

ROLE_NAMES = {
    "Manuscript": "Main manuscript", "BlindedManuscript": "Anonymized manuscript", "TitlePage": "Title page",
    "CoverLetter": "Cover letter", "Figure": "Figure", "Table": "Table", "Supplement": "Supplementary material",
    "ResponseToReviewers": "Response to reviewers", "DataAvailability": "Data availability statement",
    "ReportingChecklist": "Reporting checklist", "Other": "Other",
}
EXCLUDED_BY_DEFAULT = {"ResponseToReviewers"}          # reviewer identities and comments
WARN_ROLES = {"CoverLetter": "A cover letter can name the editor."}
MEDIA = {".pdf": "application/pdf", ".docx": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
         ".png": "image/png", ".csv": "text/csv", ".txt": "text/plain"}


def plan(records):
    """Which files go, under which names, with what integrity status."""
    used = set()
    chosen, left_out = [], []
    for item in records["Packet"]["Files"]:
        if item["StorageMode"] == "MetadataOnly" or not item["LocalFilePath"]:
            left_out.append((item, "No file: recorded in PaperRoute only"))
            continue
        if item["Role"] in EXCLUDED_BY_DEFAULT:
            left_out.append((item, "Left out by default: may name reviewers"))
            continue
        stem, ext = os.path.splitext(item["OriginalFileName"] or "file")
        name, n = stem + ext, 2
        while name.lower() in used:                 # same name twice: number the later one
            name, n = "%s (%d)%s" % (stem, n, ext), n + 1
        used.add(name.lower())
        with open(item["LocalFilePath"], "rb") as f:     # FileShare.Read in PaperRoute
            data = f.read()
        digest = hashlib.sha256(data).hexdigest()
        stored = (item.get("Sha256") or "").lower()
        status = "Not recorded" if not stored else ("Unchanged" if stored == digest else "Changed since its fingerprint")
        chosen.append({"item": item, "name": name, "data": data, "sha256": digest, "size": len(data), "status": status,
                       "media": MEDIA.get(ext.lower(), "application/octet-stream")})
    return chosen, left_out


def crate_id(name):
    # Relative URI for "files/<name>": percent-encode everything but unreserved
    # characters, so spaces, '#', and '%' can't break the @id.
    return "files/" + urllib.parse.quote(name, safe="()!*'-._~")


def write_files(folder, chosen):
    os.makedirs(os.path.join(folder, "files"))
    for c in chosen:
        with open(os.path.join(folder, "files", c["name"]), "wb") as f:
            f.write(c["data"])


EXCLUSIONS = ("This package was made by PaperRoute from the packet's files and their roles. It leaves out notes, "
              "correspondence, reviewer names and comments, manuscript numbers, portal links, and where files are kept "
              "on the computer. Files are copied unchanged, so anything stored inside them, such as a document's author, "
              "travels with them.")
RIGHTS = ("Rights not stated: PaperRoute records no license for these files, and this package grants none. "
          "The authors' and publishers' usual rights apply.")


def page(title, body):
    return ("<!DOCTYPE html>\n<html lang=\"en\"><head><meta charset=\"utf-8\"><title>%s</title>"
            "<meta name=\"generator\" content=\"PaperRoute Tracker 0.9.0 (synthetic evaluation)\">"
            "<style>body{font-family:Segoe UI,Arial,sans-serif;max-width:60rem;margin:2rem auto;padding:0 1rem;color:#1d2a2b}"
            "table{border-collapse:collapse;width:100%%}th,td{border-bottom:1px solid #cfd8d8;padding:.35rem .5rem;text-align:left;vertical-align:top}"
            "code{font-size:.8em;word-break:break-all}.note{color:#5b6b6c}</style></head><body>%s</body></html>\n"
            % (html.escape(title), body))


def summary_html(records, chosen, left_out, with_crate_links):
    m, p, j = records["Manuscript"], records["Packet"], records["Journal"]
    version = next(v for v in m["Versions"] if v["Id"] == p["ManuscriptVersionId"])
    sub = next(s for s in m["Submissions"] if s["Id"] == p["SubmissionId"])
    e = html.escape
    rows = []
    for c in chosen:
        href = crate_id(c["name"])
        warn = WARN_ROLES.get(c["item"]["Role"], "")
        rows.append("<tr><td><a href=\"%s\">%s</a></td><td>%s%s</td><td>%s bytes</td><td><code>%s</code></td><td>%s</td></tr>"
                    % (e(href), e(c["name"]), e(ROLE_NAMES[c["item"]["Role"]]),
                       (" <span class=\"note\">(%s)</span>" % e(warn)) if warn else "",
                       format(c["size"], ","), c["sha256"], e(c["status"])))
    missing = "".join("<li>%s: %s</li>" % (e(item["Label"]), e(reason)) for item, reason in left_out)
    authors = "; ".join(
        ("<a href=\"https://orcid.org/%s\">%s</a>" % (e(a["Orcid"]), e(a["Name"]))) if a["Orcid"] else e(a["Name"])
        for a in m["Authors"])
    body = (
        "<h1>%s</h1>" % e(p["Label"]) +
        "<p>Submission packet for <b>%s</b>, exported from PaperRoute on %s.</p>" % (e(m["Title"]), EXPORT_DATE) +
        "<table>"
        "<tr><th>Manuscript version</th><td>%s (%s)</td></tr>" % (e(version["Label"]), version["CreatedDate"]) +
        "<tr><th>Sent to</th><td>%s, ISSN %s, revision round %d; first submitted %s; the date this round was sent is not recorded</td></tr>"
        % (e(j["Name"]), e(", ".join(j["Issns"])), p["RevisionRoundNumber"], sub["SubmittedDate"]) +
        "<tr><th>Authors</th><td>%s</td></tr>" % authors +
        "<tr><th>Published as</th><td><a href=\"https://doi.org/%s\">https://doi.org/%s</a> in %s; publication date not exported (only the year is known)</td></tr>"
        % (e(m["Metadata"]["Doi"]), e(m["Metadata"]["Doi"]), e(m["Metadata"]["PublicationJournal"])) +
        "<tr><th>Rights</th><td>%s</td></tr></table>" % e(RIGHTS) +
        "<h2>Files</h2><table><tr><th>File</th><th>Role</th><th>Size</th><th>SHA-256</th><th>Fingerprint</th></tr>%s</table>" % "".join(rows) +
        "<h2>Not included</h2><ul>%s</ul>" % missing +
        "<p class=\"note\">%s</p>" % e(EXCLUSIONS) +
        ("<p class=\"note\">The machine-readable description of this package is <a href=\"ro-crate-metadata.json\">ro-crate-metadata.json</a> "
         "(<a href=\"%s\">RO-Crate 1.3</a>).</p>" % SPEC if with_crate_links else
         "<p class=\"note\">Check the files against <a href=\"manifest-sha256.txt\">manifest-sha256.txt</a>, for example with "
         "<code>Get-FileHash -Algorithm SHA256</code> or <code>sha256sum -c manifest-sha256.txt</code>.</p>")
    )
    return page(p["Label"], body)


# ---------------------------------------------------------------------------
# Format A: plain folder with a SHA-256 manifest and an HTML index
# ---------------------------------------------------------------------------

def export_plain(records, chosen, left_out, folder):
    write_files(folder, chosen)
    with open(os.path.join(folder, "manifest-sha256.txt"), "w", encoding="utf-8", newline="\n") as f:
        for c in chosen:
            f.write("%s  files/%s\n" % (c["sha256"], c["name"]))
    with open(os.path.join(folder, "index.html"), "w", encoding="utf-8", newline="\n") as f:
        f.write(summary_html(records, chosen, left_out, with_crate_links=False))


# ---------------------------------------------------------------------------
# Format B: RO-Crate 1.3
# ---------------------------------------------------------------------------

def one_or_many(values):
    # RO-Crate prefers a single value to a one-item list.
    return values[0] if len(values) == 1 else values


def export_crate(records, chosen, left_out, folder):
    write_files(folder, chosen)
    m, p, j = records["Manuscript"], records["Packet"], records["Journal"]
    version = next(v for v in m["Versions"] if v["Id"] == p["ManuscriptVersionId"])
    sub = next(s for s in m["Submissions"] if s["Id"] == p["SubmissionId"])

    people, orgs = [], {}
    for i, a in enumerate(m["Authors"], start=1):
        org_id = "#org-%d" % (len(orgs) + 1) if a["Affiliation"] not in orgs else orgs[a["Affiliation"]]
        orgs.setdefault(a["Affiliation"], org_id)
        people.append({"@id": ("https://orcid.org/" + a["Orcid"]) if a["Orcid"] else "#author-%d" % i,
                       "@type": "Person", "name": a["Name"], "affiliation": {"@id": orgs[a["Affiliation"]]}})
    organizations = [{"@id": oid, "@type": "Organization", "name": name} for name, oid in orgs.items()]

    files = []
    for c in chosen:
        entity = {
            "@id": crate_id(c["name"]),
            "@type": "File",
            "name": c["name"],
            "description": "%s: %s" % (ROLE_NAMES[c["item"]["Role"]], c["item"]["Label"]),
            "encodingFormat": c["media"],
            "contentSize": str(c["size"]),
            "sha256": c["sha256"],
        }
        if c["item"]["Role"] in ("Manuscript", "BlindedManuscript"):
            entity["version"] = version["Label"]
            entity["dateCreated"] = version["CreatedDate"]
        files.append(entity)
    manuscript_files = [{"@id": f["@id"]} for f, c in zip(files, chosen) if c["item"]["Role"] in ("Manuscript", "BlindedManuscript")]
    published_id = "https://doi.org/" + m["Metadata"]["Doi"]

    graph = [
        {"@id": "ro-crate-metadata.json", "@type": "CreativeWork", "conformsTo": {"@id": SPEC}, "about": {"@id": "./"}},
        {
            "@id": "./",
            "@type": "Dataset",
            "name": p["Label"],
            "description": "The files of a submission packet for \"%s\", as sent to %s in revision round %d. %s"
                           % (m["Title"], p["JournalName"], p["RevisionRoundNumber"], EXCLUSIONS),
            "datePublished": EXPORT_DATE,      # the package's date: the day it was exported
            "license": {"@id": "#rights-not-stated"},
            "mainEntity": {"@id": "#manuscript"},
            "mentions": [{"@id": "#submission-round-2"}, {"@id": "#export"}],
            "hasPart": [{"@id": f["@id"]} for f in files],
        },
        {"@id": "#rights-not-stated", "@type": "CreativeWork", "name": "Rights not stated", "description": RIGHTS},
        {
            # The version in this packet. The published work is a separate
            # entity, so the revision isn't presented as the published article.
            "@id": "#manuscript",
            "@type": "ScholarlyArticle",
            "name": m["Title"],
            "abstract": m["Metadata"]["AbstractText"],
            "keywords": ", ".join(m["Metadata"]["Keywords"]),
            "author": one_or_many([{"@id": person["@id"]} for person in people]),
            "version": version["Label"],
            "encoding": one_or_many(manuscript_files),
            "exampleOfWork": {"@id": published_id},
        },
        {
            # Published there: Metadata.PublicationJournal and a DOI are known.
            # Its date is left out: only the year is known, stored as January 1.
            "@id": published_id,
            "@type": "ScholarlyArticle",
            "name": m["Title"],
            "isPartOf": {"@id": "#journal"},
            "creativeWorkStatus": "Published",
        },
        {"@id": "#journal", "@type": "Periodical", "name": j["Name"], "issn": j["Issns"][0]},
        {
            # No date: PaperRoute records the first submission's date, not
            # when each revision round was sent.
            "@id": "#submission-round-2",
            "@type": "SendAction",
            "name": "Revision round %d sent to %s" % (p["RevisionRoundNumber"], j["Name"]),
            "description": "First submitted on %s. The date revision round %d was sent is not recorded."
                           % (sub["SubmittedDate"], p["RevisionRoundNumber"]),
            "object": {"@id": "#manuscript"},
            "recipient": {"@id": "#journal"},
        },
        {
            "@id": "#export",
            "@type": "CreateAction",
            "name": "Exported from PaperRoute",
            "endTime": EXPORT_TIME,
            "instrument": {"@id": "https://github.com/JUhalt/PaperRoute-Tracker"},
            "result": {"@id": "./"},
        },
        {
            "@id": "https://github.com/JUhalt/PaperRoute-Tracker",
            "@type": "SoftwareApplication",
            "name": "PaperRoute Tracker",
            "url": "https://github.com/JUhalt/PaperRoute-Tracker",
            "version": "0.9.0",
            "license": {"@id": "https://spdx.org/licenses/GPL-3.0-only"},   # the software's license only
        },
        {"@id": "https://spdx.org/licenses/GPL-3.0-only", "@type": "CreativeWork", "name": "GNU General Public License v3.0 only",
         "description": "The license of the PaperRoute software. It does not apply to the files in this package."},
    ] + files + people + organizations

    with open(os.path.join(folder, "ro-crate-metadata.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump({"@context": CONTEXT, "@graph": graph}, f, indent=2, ensure_ascii=False)
        f.write("\n")
    with open(os.path.join(folder, "ro-crate-preview.html"), "w", encoding="utf-8", newline="\n") as f:
        f.write(summary_html(records, chosen, left_out, with_crate_links=True))


# ---------------------------------------------------------------------------

def tree_digest(root):
    digest = {}
    for base, _, names in os.walk(root):
        for name in names:
            path = os.path.join(base, name)
            with open(path, "rb") as f:
                digest[os.path.relpath(path, root)] = hashlib.sha256(f.read()).hexdigest()
    return digest


def main():
    records = make_source()
    before = tree_digest(SOURCE)
    if os.path.isdir(OUT):
        shutil.rmtree(OUT)
    chosen, left_out = plan(records)
    folder = "Revision 2 to Fictional Journal of Psychology"
    export_plain(records, chosen, left_out, os.path.join(OUT, "plain", folder))
    export_crate(records, chosen, left_out, os.path.join(OUT, "rocrate", folder))
    after = tree_digest(SOURCE)
    with open(os.path.join(OUT, "source-unchanged.txt"), "w", encoding="utf-8") as f:
        f.write("Source records and files unchanged: %s (%d files hashed before and after)\n" % (before == after, len(before)))
    print("exported %d files; left out %d; source unchanged: %s" % (len(chosen), len(left_out), before == after))
    for c in chosen:
        print("  %-40s %-30s %s" % (c["name"], c["status"], crate_id(c["name"])))


if __name__ == "__main__":
    main()
