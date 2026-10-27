<!-- lang:component-mapping -->
## HTML Element → BELLATRIX Component Mapping (TypeScript)

Use the snapshot `Tag` + `Type` (+ `Role`) of each validated element to pick the TypeScript component class (all from `'@bellatrix/web/components'`) for the generated Map class.

| HTML element (snapshot `Tag` / `Type`) | BELLATRIX component | Typical actions (all `await`ed) |
|----------------------------------------|---------------------|-----------------|
| `button`, `input type=submit/button` | `Button` | `click()` |
| `input type=reset` | `Reset` | `click()` |
| `input type=text` (or no type) | `TextField` | `setText(…)`; clear with `setText('')` |
| `input type=password` | `PasswordField` | `setPassword(…)` |
| `input type=email` | `EmailField` | `setEmail(…)` |
| `input type=tel` | `PhoneField` | `setPhone(…)` |
| `input type=search` | `SearchField` | `setSearch(…)` |
| `input type=url` | `UrlField` | `setUrl(…)` |
| `input type=number` | `NumberInput` | `setNumber(n)` |
| `input type=range` | `RangeInput` | `setValue(n)` |
| `input type=date` | `DateInput` | `setDate(new Date(…))` |
| `input type=time` | `TimeInput` | `setTime(hours, minutes)` |
| `input type=datetime-local` / `month` / `week` | `DateTimeInput` / `MonthInput` / `WeekInput` | `setTime(Date)` / `setMonth(y, m)` / `setWeek(y, w)` |
| `input type=color` | `ColorInput` | `setColor(…)` |
| `input type=checkbox` | `CheckBox` | `check()`, `uncheck()` (NO `click()`) |
| `input type=radio` | `RadioButton` | `click()` |
| `input type=file` | `FileInput` | `upload(path)` |
| `select` | `Select` | `selectByText(…)`, `selectByValue(…)`, `selectByIndex(…)` |
| `option` | `Option` | `select()`; read `getInnerText()`, `isSelected()` |
| `textarea` | `TextArea` | `setText(…)` |
| `a` | `Anchor` | `click()`, `getHref()` |
| `img` | `Image` | read `getSrc()`, `getAlt()` (NO `click()` — create a clickable image as `Button`) |
| `label` | `Label` | read `getInnerText()` |
| `h1`–`h6` | `Heading` | read `getInnerText()` |
| `span` | `Span` | read `getInnerText()` |
| `div`, `p`, `li`, `section`, `form`, `td` (text containers) | `Div` | read `getInnerText()` |
| `iframe` | `Frame` | read `getName()` only — elements INSIDE an iframe cannot be automated in BELLATRIX TypeScript |
| `table` | — (no `Table`) | `allByXpath` rows + row-scoped XPath for cells |
| clickable `div`/`span`/`li` (role `button`/`tab`/`menuitem`/`option`) | `Button` | `click()` — `Div`/`Span`/`Label`/`Heading` have NO `click()` |
| ARIA `role=checkbox` on a non-input | `Button` (or a custom component) | `click()` |

There is no `H1`, `ListItem`, `Paragraph`, `ComboBox`, or `Table` component in BELLATRIX TypeScript — use the types above.

**Record these TS-specific obstacles in Phase 5** — the generated code cannot handle them the C# way:
- **iframe content** — not reachable (no frame switching). Report the step to the user as not automatable instead of inventing a frame API.
- **New tab / window** — no tab switching. Record the link's `href`; the generated code navigates to it in the same tab.
- **Native `alert`/`confirm`** — `this.app.dialog` works, but the dialog must be handled right after the triggering click (auto-dismissed after 5 s with Playwright).
- **Hidden native `<select>` behind a custom dropdown** — not actionable (finding waits for visibility); record the visible trigger and option instead.
- **Keyboard-only actions (Enter, Tab, chords) and drag-and-drop** — no API; record whether a form submit / click achieves the same result.

---

