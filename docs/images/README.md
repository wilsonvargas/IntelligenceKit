# Screenshot assets

The main `README.md` references the PNGs below. Drop the captured images here with
these exact filenames and the README renders them automatically.

Capture against a server that has some seeded events (run the sample app and press
its buttons a few times so issues, breadcrumbs and a screenshot exist). Use a wide
browser window (~1440px) so the layout breathes, and prefer the **dark theme** for
the hero shots.

| File | What to capture |
| --- | --- |
| `overview.jpg` | The **Overview** page (`/`) — KPI tiles + errors-per-hour chart + exception-share donut + Top issues. This is the hero image; make it look full (seed a few events first). |
| `issues.jpg` | The **Issues** page (`/issues`) — the grouped list showing counts and the ▲/▼ trend column. |
| `event-detail.jpg` | An **event detail** (`/events/{id}`) for an exception that has breadcrumbs, device-state metrics, tags, and ideally the "last screen before the event" screenshot card. |
| `theme-light.jpg` | The Overview in **light** theme (toggle bottom-left of the sidebar). |
| `theme-dark.jpg` | The Overview in **dark** theme (same framing as `theme-light.jpg` for a clean side-by-side). |

Tips
- PNG or JPEG, trimmed to the browser viewport (no OS chrome needed).
- Keep the two theme shots at the same scroll position and window size.
- If a shot would expose real user data, use the sample app's synthetic events.
