# Narrator pass

The scripted screen-reader check for a release (#139), run once during certification and recorded on the certification issue (for v0.9.2, #147). The automated tests already prove names, Esc, live regions, default actions, focus events, and the shortcut list; this pass checks what Narrator actually says. Each item names the issue or pull request that asked for it.

## Before you start

- [ ] The installed build, on the standing certification fixture (never your own library), at 100% or 125%, in Light.
- [ ] Windows' **Animation effects** on (**Settings > Accessibility > Visual effects**), so page switches fade (#142).
- [ ] Narrator on (**Ctrl+Win+Enter**), with its default voice and verbosity. The Narrator key is **Caps Lock** or **Insert**.
- [ ] Note the date, the PaperRoute version, the Windows build, and the Narrator version (Narrator settings).
- [ ] Run this after the collector's last report: the status-line items below add, move and delete a manuscript of their own, named `ZZZ-CERT-v0.9.2-Narrator`, and the collector rejects a library with any other title.

## For every page and window below

- [ ] **Tab** through every control once. Each one Narrator reaches says its name and its role; no field is read as just "edit", "list", or "combo box", and none takes the name of a hint, a count, or a link (#137).
- [ ] A button that shows a symbol (**⋯**, **«**, **»**) is read by what it does.
- [ ] A **?** help button is read as "Help: " and what it helps with (a field's label, or the button or badge beside it), and **Narrator+Enter** on it shows its help.
- [ ] Record what Narrator said for anything that was wrong, word for word.

## The six pages

- [ ] **Board** (**Ctrl+1**): **Tab** reaches each card in turn: its title, read by name, then its **⋯** button, then **View route**. **Enter** or **Space** on a title opens that manuscript (#192). A Needs Attention chip is read as a button, and **Narrator+Enter** applies it. Then press **Ctrl+F**: Narrator lands on the search box by name.
- [ ] **Opening a page from the Board** (#213, PR #220): **Enter** on a card's title opens the manuscript, and Narrator reads the page's **Overview** tab, not the Board's search box. **Alt+Left** returns to the Board and Narrator reads that card's title. From the search box, **Ctrl+2** reads the Library's filter box, and **Alt+Left** reads the search box again. On each way back, Narrator says nothing of **Collapse navigation**: focus passes through that button for a moment as the page is left (`KeyboardAccessTests` pins the two focus changes). If Narrator announces it, record its words on #213; the fix is to reorder the switch.
- [ ] **Page fades** (#142, PR #220): with Animation effects on, switch between the pages with **Ctrl+1** to **Ctrl+6** and **Alt+Left**. Narrator reads only the page arrived at and its focused control. Nothing new is announced during a fade: no unnamed window or pane, and the keyboard never lands on the fade.
- [ ] **Library** (**Ctrl+2**): the table and its filter box are named; **Enter** on a row opens it. On Authors & Affiliations, both lists are named.
- [ ] **Journals** (**Ctrl+3**): the journal list is read as "Journals", not as its count.
- [ ] **Deadlines** (**Ctrl+4**), a drawn list (PR #195): each heading and each item is read in order; **Up** and **Down** move between items and each is read; a filter chip is pressed with **Narrator+Enter**; **Narrator+Enter** on a row's button and on the **Done** line's **Show** presses it.
- [ ] **Insights** (**Ctrl+5**): each table is named. In the route chart (#193, PR #238), **Tab** onto it, then **Up** and **Down**: each route is read once, by its title, as the selection lands on it, and nothing is read past either end; **Enter** opens the selected route, and a click on a route is read once, as that route. The chart raises focus events only: record whether Narrator also wants it to say the route is selected. A route has no default action, so Narrator's own "do default action" on it does nothing: record whether that is a problem.
- [ ] **Import & Export** (**Ctrl+6**), drawn around its buttons (PR #195): every button is read by what it does, and Narrator's scan mode reads each heading, then each button and its description, in order.

## The manuscript page

- [ ] **Journal shortlist**, a drawn list (PR #215), on `ZZZ-CERT-v0.9.2-04 Draft with a shortlist`: on the Overview, **Tab** reaches each shortlisted journal's **Edit...** and **⋯** in turn, read as "Edit <journal>" and "More actions for <journal>". Narrator's scan mode reads each journal's name, status, details, facts and evidence in order. **Enter** or **Space** on **⋯**, or the context-menu key, opens that journal's menu. After **Make Target Journal** on another journal, Narrator hears the change.

## The five most-used windows

- [ ] **Add Manuscript**: each field by its label; **Esc** closes it without adding.
- [ ] **Add Submission**: "Journal manuscript ID" and its "Help: Journal manuscript ID"; the Submitted and Follow-up date pickers by name; **Esc** closes it.
- [ ] **Add Decision**: the Decision date and Revision deadline pickers by name; **Esc** closes it.
- [ ] **Route View** (#136): **Tab** reads "Open in Manuscript: <waypoint>, <date>" for each waypoint in route order; **Enter** opens that waypoint in the manuscript page.
- [ ] **Settings > Preferences**: each number box and list by its label; **Esc** closes it without saving.

## Announcements

- [ ] On a manuscript page, change a field and press **Ctrl+S**: Narrator reads "Saved locally" from the status line without moving focus (#139).
- [ ] **What just happened** (#225, PR #236), each read from the status line without moving focus: add a manuscript named `ZZZ-CERT-v0.9.2-Narrator` ('Added "ZZZ-CERT-v0.9.2-Narrator".'); move it to the File Drawer ('Moved "…" to the File Drawer.'); restore it ('Restored "…" to the Pipeline.'); delete it, typing DELETE ('Deleted "…".').
- [ ] In **Check for Publications**, check one fixture manuscript (this sends its title to Crossref): the progress line is read as it changes; **Esc** stops the check and the window stays open.

## Record

Post on the certification issue: the date, the versions noted above, each checked item, and for anything that failed, the window, the control, and what Narrator said. Post the **Collapse navigation** answer on #213 as well, and the route chart's two answers (selection, default action) where #238 can find them, on #147.
