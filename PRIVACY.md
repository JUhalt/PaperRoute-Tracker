# Privacy policy

PaperRoute Tracker is a desktop app for Windows. It needs no account, and your library stays on your computer: the manuscript records, settings and backups under `%LocalAppData%\PaperRoute\`, and the copies of your files under `Documents\PaperRoute Library\`. The project does not run a server and receives nothing from the app.

## What PaperRoute never sends

- No usage data, analytics, telemetry or advertising identifiers.
- No crash or error reports. When something goes wrong, PaperRoute writes a report to a `logs` folder on your computer; it stays there unless you choose to share it.
- Nothing about your manuscripts, files, notes or contacts, except what an online feature you start shows you before it sends it.

## The one request PaperRoute makes by itself

When PaperRoute starts, it checks GitHub for a newer release. The check reads the list of PaperRoute releases and sends nothing about you or your library. **Settings > Preferences > Updates** turns it off, and **Work offline** stops every online feature.

## Online features you start

Each of these runs only when you choose it, shows what it will send first where it sends anything of yours, and stops under **Work offline**. **Settings > Preferences > Online services** lists them with the hosts each one contacts.

| Feature | Contacts | Sends | Their privacy policy |
| --- | --- | --- | --- |
| Update check | GitHub | Nothing about you or your library; it reads the list of PaperRoute releases. | [GitHub Privacy Statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement) |
| DOI lookup | Crossref | A DOI. | [Crossref privacy](https://www.crossref.org/operations-and-sustainability/privacy/) |
| Publication check | Crossref, ORCID | The DOIs and titles of the manuscripts you check, and your ORCID iD if you include it. | [Crossref privacy](https://www.crossref.org/operations-and-sustainability/privacy/); [ORCID Privacy Policy](https://info.orcid.org/privacy-policy/) |
| ORCID import | ORCID | An ORCID iD you enter. | [ORCID Privacy Policy](https://info.orcid.org/privacy-policy/) |
| Journal facts | DOAJ, OpenAlex | A journal's ISSNs, or a name you type to find a journal and the id of the one you pick. | [DOAJ privacy notice](https://doaj.org/privacy/); [OpenAlex privacy](https://openalex.org/privacy) |
| Find journals | OpenAlex | The keywords you review, a start date, and the ids of the journals found. | [OpenAlex privacy](https://openalex.org/privacy) |
| Your citations | ORCID, OpenAlex | Your ORCID iD to ORCID, and the DOIs of your works to OpenAlex; your iD to OpenAlex only if you choose. | [ORCID Privacy Policy](https://info.orcid.org/privacy-policy/); [OpenAlex privacy](https://openalex.org/privacy) |
| AI assistant: Claude | Anthropic | Only what an AI assistant window shows you before sending: a decision letter you paste; a journal's author instructions you paste, with the article type you enter; one reviewer comment with its reviewer label and your planned action; or a manuscript's title, abstract, keywords, type of work, and target journal with its Journal Library facts. Test Connection in Preferences sends only your key, if you added one. | With your own key you are Anthropic's customer: its [Commercial Terms](https://www.anthropic.com/legal/commercial-terms) and [Data Processing Addendum](https://www.anthropic.com/legal/data-processing-addendum) apply |
| AI assistant: another server or a model on this computer | The address you set | The same, sent only to that address. | The server's operator |

The [User Guide](docs/USER_GUIDE.md#what-paperroute-sends-and-when) gives the exact hosts and wording for each feature.

When PaperRoute contacts a service, that service also sees what any internet request reveals, such as your IP address, and its own privacy policy applies to what it receives.

## Keys you add

Keys for OpenAlex or the AI assistant are encrypted for your Windows account and are never stored in PaperRoute's settings, backups, exports or diagnostic reports. Each key is sent only to the service it belongs to. PaperRoute ships with no keys of its own.

## Changes and contact

Changes to this policy are noted in the release notes of the version that makes them. Questions and privacy concerns can be raised as described in the [security policy](.github/SECURITY.md).
