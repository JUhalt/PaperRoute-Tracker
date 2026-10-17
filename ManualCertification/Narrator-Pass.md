# Narrator pass

The scripted screen-reader check for a release (#139), run once during certification and recorded on the certification issue (for v0.9.2, #147). The automated tests already prove names, Esc, live regions, default actions, and the shortcut list; this pass checks what Narrator actually says.

## Before you start

- [ ] The installed candidate, on the standing certification fixture (never your own library), at 100% or 125%, in Light.
- [ ] Narrator on (**Ctrl+Win+Enter**), with its default voice and verbosity. The Narrator key is **Caps Lock** or **Insert**.
- [ ] Note the date, the PaperRoute version, the Windows build, and the Narrator version (Narrator settings).

## For every page and window below

- [ ] **Tab** through every control once. Each one Narrator reaches says its name and its role; no field is read as just "edit", "list", or "combo box", and none takes the name of a hint, a count, or a link.
- [ ] A button that shows a symbol (**⋯**, **«**, **»**) is read by what it does.
- [ ] A **?** help button is read as "Help: " and what it helps with (a field's label, or the button or badge beside it), and **Narrator+Enter** on it shows its help.
- [ ] Record what Narrator said for anything that was wrong, word for word.

## The six pages

- [ ] **Board** (**Ctrl+1**): a card's title opens with **Enter**; a Needs Attention chip is read as a button, and **Narrator+Enter** applies it. Then press **Ctrl+F**: Narrator lands on the search box by name.
- [ ] **Library** (**Ctrl+2**): the table and its filter box are named; **Enter** on a row opens it. On Authors & Affiliations, both lists are named.
- [ ] **Journals** (**Ctrl+3**): the journal list is read as "Journals", not as its count.
- [ ] **Deadlines** (**Ctrl+4**): **Up** and **Down** move between items and each is read; a filter chip is pressed with **Narrator+Enter**.
- [ ] **Insights** (**Ctrl+5**): each table is named; in the route chart, **Up** and **Down** read each route.
- [ ] **Import & Export** (**Ctrl+6**): every button is read by what it does.

## The five most-used windows

- [ ] **Add Manuscript**: each field by its label; **Esc** closes it without adding.
- [ ] **Add Submission**: "Journal manuscript ID" and its "Help: Journal manuscript ID"; the Submitted and Follow-up date pickers by name; **Esc** closes it.
- [ ] **Add Decision**: the Decision date and Revision deadline pickers by name; **Esc** closes it.
- [ ] **Route View**: **Tab** reads "Open in Manuscript: <waypoint>, <date>" for each waypoint in route order; **Enter** opens that waypoint in the manuscript page.
- [ ] **Settings > Preferences**: each number box and list by its label; **Esc** closes it without saving.

## Announcements

- [ ] On a manuscript page, change a field and press **Ctrl+S**: Narrator reads "Saved locally" from the status line without moving focus.
- [ ] In **Check for Publications**, check one fixture manuscript (this sends its title to Crossref): the progress line is read as it changes; **Esc** stops the check and the window stays open.

## Record

Post on the certification issue: the date, the versions noted above, each checked item, and for anything that failed, the window, the control, and what Narrator said.
