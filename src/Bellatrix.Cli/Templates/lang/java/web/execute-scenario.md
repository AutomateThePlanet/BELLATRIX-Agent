<!-- lang:component-mapping -->
## HTML Element → BELLATRIX Component Mapping (Java)

Use the snapshot `Tag` + `Type` (+ `Role`) of each validated element to pick the Java component class for the generated `Map` class. All classes are in `solutions.bellatrix.web.components` (except `Table`); members are camelCase methods.

| HTML element (snapshot `Tag` / `Type`) | BELLATRIX component | Typical actions |
|----------------------------------------|---------------------|-----------------|
| `button`, `input type=submit/button` | `Button` | `click()` |
| `input type=reset` | `Reset` | `click()` |
| `input type=text` (or no type) | `TextInput` | `setText(…)` |
| `input type=password` | `PasswordInput` | `setPassword(…)` |
| `input type=email` | `EmailInput` | `setEmail(…)` |
| `input type=tel` | `PhoneInput` | `setPhone(…)` |
| `input type=search` | `SearchField` | `setSearch(…)` |
| `input type=url` | `UrlField` | `setUrl(…)` |
| `input type=number` | `NumberInput` | `setNumber(double)` |
| `input type=range` | `Range` | `setRange(Number)` |
| `input type=date` | `DateInput` | `setDate(year, month, day)` |
| `input type=time` | `TimeInput` | `setTime(hours, minutes)` |
| `input type=datetime-local` / `month` / `week` | `DateTimeInput` / `MonthInput` / `WeekInput` | `setTime(LocalDateTime)` / `setMonth(year, month)` / `setWeek(year, week)` |
| `input type=color` | `ColorInput` | `setColor(String)` |
| `input type=checkbox` | `CheckBox` | `check()`, `uncheck()` |
| `input type=radio` | `RadioButton` | `click()` |
| `input type=file` | `FileInput` | `upload(path)` |
| `select` | `Select` | `selectByText(…)`, `selectByIndex(…)` |
| `option` | `Option` | — (read `getText()`, `isSelected()`) |
| `textarea` | `TextArea` | `setText(…)` |
| `a` | `Anchor` | `click()`, `getHref()` |
| `img` | `Image` | `click()`, `getSrc()` |
| `label` | `Label` | read `getText()` |
| `h1`–`h6` | `Heading` | read `getText()` |
| `span` | `Span` | read `getText()` |
| `div`, `p`, `li`, `section`, `form`, `td` (text containers) | `Div` | read `getText()` |
| `output` / `progress` | `Output` / `Progress` | read `getText()` / `getValue()` |
| `iframe` | `Frame` | switch into it via `app().browser().switchToFrame(frame)` |
| `table` | `Table` (`solutions.bellatrix.web.components.advanced.table`) | `getRows()`, `getCell(header, row)` |
| clickable `div`/`span`/`li` (role `button`/`tab`/`menuitem`) | `Button` | `click()` — `Div`/`Span`/`Label`/`Heading` have NO `click()` |
| ARIA `role=checkbox` on a non-input | `Button` (or a custom component) | `click()` |

There is no `TextField`, `Password`, `Email`, `Phone`, `Search`, `Url`, `Number`, `InputFile` (the C# names), nor `H1`, `ListItem`, `Paragraph`, or `ComboBox` in BELLATRIX Java web — use the classes above. No component has `clear()`: `setText(…)` clears before typing.

---

