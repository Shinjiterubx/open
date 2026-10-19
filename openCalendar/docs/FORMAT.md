# The `.opencalendar` file format (v1)

A `.opencalendar` file is a standard **ZIP archive** holding one or more calendars. The same format is used
for the app's own storage (one file per calendar in `%APPDATA%\openCalendar\Calendars`) and for export / import.

```
work.opencalendar
├── manifest.json
└── calendars/
    └── 3f6c…e1.json      one file per calendar
```

## manifest.json

```json
{
  "format": "openCalendar",
  "version": 1,
  "generator": "openCalendar 1.0.0",
  "exportedAt": "2026-10-13T10:00:00+02:00",
  "calendars": [ { "id": "3f6c…e1", "name": "Munka", "eventCount": 12 } ]
}
```

Readers reject files whose `version` is higher than they understand.

## calendars/{id}.json

```json
{
  "id": "3f6c8a2e-…",
  "name": "Munka",
  "color": "#3E8FC9",
  "isVisible": true,
  "createdAt": "2026-10-01T08:00:00+02:00",
  "updatedAt": "2026-10-13T09:30:00+02:00",
  "events": [
    {
      "id": "9b1d…",
      "title": "Sprint review",
      "location": "Tárgyaló 2",
      "description": "Demo + retro",
      "start": "2026-10-16T14:00:00",
      "end": "2026-10-16T15:00:00",
      "isAllDay": false,
      "recurrence": {
        "frequency": "weekly",
        "interval": 2,
        "daysOfWeek": ["friday"],
        "endKind": "afterCount",
        "count": 10
      },
      "exceptionDates": ["2026-12-25"],
      "reminderMinutes": [15, 1440],
      "note": { "noteId": "4f0c…", "title": "Review jegyzet" },
      "createdAt": "…",
      "updatedAt": "…"
    }
  ]
}
```

| Field | Notes |
|---|---|
| `start` / `end` | Local wall-clock time without offset. For all-day events only the date part counts and `end` is the **last day (inclusive)**. |
| `recurrence` | Optional. `frequency`: `daily` \| `weekly` \| `monthly` \| `yearly`; `interval` ≥ 1; `daysOfWeek` (weekly; empty = weekday of `start`); `endKind`: `never` \| `onDate` (`until`, inclusive date) \| `afterCount` (`count`). Monthly on a day a month doesn't have (e.g. 31) skips that month. |
| `exceptionDates` | Occurrences removed from a series ("delete only this one"). |
| `reminderMinutes` | Minutes before each occurrence (0 = at start). All-day events remind relative to 09:00 of the day. |
| `note` | Optional link to an [openNotes](https://github.com/) note (`noteId` = the note's manifest id). |

Unknown fields are ignored, so newer optional fields can be added without breaking older readers.

## openNotes integration

openCalendar reads (never writes) the openNotes library: every `.openNote` file's `manifest.json`
(`dueDate`, `dueTime`, `priority`, `reminder`, `isDone`). The folder is published by openNotes in
`HKCU\Software\openNotes` (`NotesDirectory`, `ExePath`) and can be overridden in the settings.
Linked notes are opened with `openNotes.exe --open-note <id>`.
