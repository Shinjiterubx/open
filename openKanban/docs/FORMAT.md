# The `.openKanban` file format (v1)

A `.openKanban` file is a **ZIP archive** holding one complete workspace. The app stores every workspace this way
(`%APPDATA%\openKanban\Workspaces\{id}.openKanban`), and export / import use the same file.

```
munka.openKanban
├── manifest.json     format, version, generator, workspace id / name, counts
└── workspace.json    the workspace → boards → columns → cards
```

## manifest.json

```json
{ "format": "openKanban", "version": 1, "generator": "openKanban 1.0.0",
  "exportedAt": "2026-10-14T10:00:00+02:00", "workspaceId": "…", "workspaceName": "Munka",
  "boardCount": 2, "cardCount": 31 }
```

## workspace.json (abridged)

```json
{
  "id": "…", "name": "Munka", "description": "Céges feladatok", "color": "#2F5A8A",
  "boards": [{
    "id": "…", "name": "Sprint 42", "background": "#EAF1F8",
    "labels": [ { "id": "L1", "name": "Sürgős", "color": "#E0434F" } ],
    "columns": [{
      "id": "…", "name": "Folyamatban", "color": "#3E8FC9",
      "cardLimit": 5,                 // WIP limit, omitted = unlimited
      "marksCardsDone": false,        // true: cards moved here become done
      "cards": [{
        "id": "…", "title": "Riport", "description": "…", "coverColor": "#3E8FC9",
        "labelIds": ["L1"],
        "checklist": [ { "id": "…", "text": "Adatok", "isDone": true } ],
        "dueDate": "2026-10-20", "dueTime": "09:30:00", "isDone": false,
        "note": { "noteId": "…", "title": "Riport jegyzet" },
        "createdAt": "…", "updatedAt": "…"
      }]
    }]
  }]
}
```

Readers ignore unknown fields; new optional fields don't bump `version`.

## Integration (all optional)

| | |
|---|---|
| **openCalendar** | openKanban writes `%APPDATA%\openKanban\deadlines.json` (every card with a due date, flattened) and publishes its path in `HKCU\Software\openKanban\DeadlinesFile`. openCalendar shows these cards; clicking one runs `openKanban.exe --open-card <id>`. A card's "Megnyitás naptárban" button runs `openCalendar.exe --goto yyyy-MM-dd`. |
| **openNotes** | Cards can link a note (`note.noteId`); the list of notes is read from the openNotes library, and the note opens with `openNotes.exe --open-note <id>`. |
| **openLauncher** | Reads `HKCU\Software\openKanban\ExePath` / the uninstall entry to start, install or remove the app. |
