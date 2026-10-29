# The `.openNote` file format (v1)

An `.openNote` file is a standard **ZIP archive**, so it can be opened with any unzip tool
and processed by other programs without openNotes.

```
note.openNote
├── manifest.json     metadata
├── content.md        the note body (UTF-8 Markdown, no BOM)
└── assets/           embedded files referenced from content.md
    ├── screenshot.png
    └── diagram.svg
```

## manifest.json

```json
{
  "format": "openNote",
  "version": 1,
  "id": "4f0c8a2e-5d1b-4a7e-9c55-2b8f3e6d1a90",
  "title": "Bevásárlólista",
  "createdAt": "2026-10-13T09:12:44+02:00",
  "updatedAt": "2026-10-13T09:30:02+02:00",
  "isPinned": false,
  "generator": "openNotes 1.0.0",
  "priority": "high",
  "dueDate": "2026-10-20",
  "dueTime": "14:00:00",
  "reminder": true,
  "isDone": false,
  "folderId": "9a6f1c2b-3d4e-4f50-8a1b-2c3d4e5f6a7b",
  "folderName": "Munka"
}
```

| Field | Type | Notes |
|---|---|---|
| `format` | string | Always `"openNote"`. |
| `version` | int | Format version. Readers reject files with a higher version than they know. |
| `id` | GUID | Stable identity. Importing a file whose `id` already exists asks to overwrite or copy. |
| `title` | string | May be empty. |
| `createdAt` / `updatedAt` | ISO 8601 with offset | |
| `isPinned` | bool | Pinned notes are listed first. |
| `generator` | string | Informational. |
| `priority` | `"low"` \| `"medium"` \| `"high"` | Optional. Shown as purple / yellow / red. Missing = none. |
| `dueDate` | `yyyy-MM-dd` | Optional deadline (local date). |
| `dueTime` | `HH:mm:ss` | Optional; without it the note is due at the end of `dueDate`. |
| `reminder` | bool | Optional. Remind daily until done or switched off. |
| `isDone` | bool | Optional. Closed ("lezárt") notes don't remind and are listed under *Lezártak*. |
| `folderId` | GUID | Optional. Folder the note is filed in (see `folders.json` below). |
| `folderName` | string | Optional. Folder name, so an imported note can be filed into a folder of the same name. |

## content.md

CommonMark + GitHub-style extensions (tables, task lists `- [ ]` / `- [x]`, strikethrough `~~x~~`,
autolinks). Images are referenced relative to the archive root:

```markdown
![Képernyőkép](assets/screenshot.png)
```

Raw HTML is **not rendered** by openNotes (security: notes are shared between people).

## assets/

Flat folder. File names are restricted to `A–Z a–z 0–9 _ -` plus a lowercase extension,
so they are safe as paths and URLs. Identical content is stored once.

## Compatibility rules

* New optional manifest fields may be added without bumping `version`; readers ignore unknown fields.
* Changing the meaning of existing fields or the layout bumps `version`.

## folders.json (library only)

The folder list lives next to the notes (`%APPDATA%\openNotes\folders.json`), not inside `.openNote` files:

```json
{
  "version": 1,
  "folders": [
    { "id": "9a6f1c2b-3d4e-4f50-8a1b-2c3d4e5f6a7b", "name": "Munka", "color": "#2F5A8A" }
  ]
}
```

Notes reference folders by `id`, so renaming a folder doesn't touch any note file. If the file is missing or broken,
notes are still loaded – they just appear without folder.
