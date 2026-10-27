<!-- lang:component-mapping -->
## HTML Element → BELLATRIX Component Mapping

Use the snapshot `Tag` + `Type` (+ `Role`) of each validated element to pick the C# component type for the generated Map file.

| HTML element (snapshot `Tag` / `Type`) | BELLATRIX component | Typical actions |
|----------------------------------------|---------------------|-----------------|
| `button`, `input type=submit/button` | `Button` | `Click()` |
| `input type=reset` | `Reset` | `Click()` |
| `input type=text` (or no type) | `TextField` | `SetText(…)` |
| `input type=password` | `Password` | `SetPassword(…)` |
| `input type=email` | `Email` | `SetEmail(…)` |
| `input type=tel` | `Phone` | `SetPhone(…)` |
| `input type=search` | `Search` | `SetSearch(…)` |
| `input type=url` | `Url` | `SetUrl(…)` |
| `input type=number` | `Number` | `SetNumber(double)` |
| `input type=range` | `Range` | `SetRange(int)` |
| `input type=date` | `Date` | `SetDate(year, month, day)` |
| `input type=time` | `Time` | `SetTime(hours, minutes)` |
| `input type=datetime-local` / `month` / `week` | `DateTimeLocal` / `Month` / `Week` | — |
| `input type=color` | `Color` | — |
| `input type=checkbox` | `CheckBox` | `Check()`, `Uncheck()` |
| `input type=radio` | `RadioButton` | `Click()` |
| `input type=file` | `InputFile` | `Upload(path)` |
| `select` | `Select` | `SelectByText(…)`, `SelectByIndex(…)` |
| `option` | `Option` | — (read `InnerText`, `IsSelected`) |
| `textarea` | `TextArea` | `SetText(…)` |
| `a` | `Anchor` | `Click()`, `Href` |
| `img` | `Image` | `Click()`, `Src` |
| `label` | `Label` | read `InnerText` |
| `h1`–`h6` | `Heading` | read `InnerText` |
| `span` | `Span` | read `InnerText` |
| `div`, `p`, `li`, `section`, `form`, `td` (text containers) | `Div` | read `InnerText` |
| `iframe` | `Frame` | switch into it via `App.Browser.SwitchToFrame(frame)` |
| `table` | `Table` | rows/cells API |
| clickable `div`/`span`/`li` (role `button`/`tab`/`menuitem`) | `Button` | `Click()` — `Div`/`Span`/`Label` have NO `Click()` |
| ARIA `role=checkbox` on a non-input | `Button` (or a custom component) | `Click()` |

There is no `H1`, `ListItem`, `Paragraph`, or `ComboBox` component in BELLATRIX Web — use the types above.

---

