# PaperRoute Manual Certification Fixtures

This directory contains disposable import fixtures used for repeatable F5/manual certification.

## v0.4 release records

- [Release closure and published-asset verification](v0.4-Release-Closure.md)
- [Historical development checkpoints](v0.4-Development-Checkpoint.md)
- [Disposable workflow demo](V04Demo/README.md)
- [Candidate packaging and installation procedure](v0.4-Release-Packaging.md)

## Safety

Certification workbooks are **additive imports**.

They create new PaperRoute manuscripts with new internal identifiers. They do not overwrite existing manuscripts or Routes.

Importing the same workbook more than once will create another copy of the certification manuscripts, so delete the prior `ZZZ-CERT-*` records before re-importing when you want a clean certification run.

## Naming

Fixtures use conspicuous titles:

```text
ZZZ-CERT-<release>-<case>
```

They are intentionally easy to find, sort, and delete after certification.

## v0.3 Version History / Route fixture

`v0.3-VersionHistory-Certification.xlsx` contains six manuscripts:

1. `ZZZ-CERT-v0.3-01 Empty Draft`
   - Draft manuscript with no submissions.
   - Use for empty Version History, metadata-only versions, and basic Route navigation.

2. `ZZZ-CERT-v0.3-02 Active Submission`
   - Submitted to a journal with no editorial decision.
   - Use for submission-linked version testing.

3. `ZZZ-CERT-v0.3-03 Major Revision`
   - Active Major Revision with a deadline and reviewer correspondence.
   - Use for decision-linked versions, revision rounds, and reviewer-workflow groundwork.

4. `ZZZ-CERT-v0.3-04 Rejection and Reroute`
   - Rejected by Journal A, then submitted to Journal B.
   - Use for reroute visualization.

5. `ZZZ-CERT-v0.3-05 Withdrawn`
   - Latest submission was withdrawn.
   - Expected current manuscript state is Draft.

6. `ZZZ-CERT-v0.3-06 Same-Day Revision`
   - Submission and Major Revision decision share a calendar date.
   - Use for deterministic same-day chronology.

The standard workbook format currently imports manuscripts, submissions, decisions, and correspondence. Version records are then created manually during the Version History certification steps so the user-facing workflow itself is exercised.
