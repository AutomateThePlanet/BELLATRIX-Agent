<!-- lang:component-mapping -->
## ControlType → BELLATRIX Component Mapping (Java)

| WinAppDriver ControlType | BELLATRIX Component | XPath Tag |
|--------------------------|--------------------|-----------|
| Button | `Button` | `//Button` |
| Edit | `TextField` | `//Edit` |
| Edit (IsPassword) | `PasswordField` | `//Edit` |
| Document | `TextArea` | `//Document` |
| ComboBox | `ComboBox` | `//ComboBox` |
| CheckBox | `CheckBox` | `//CheckBox` |
| RadioButton | `RadioButton` | `//RadioButton` |
| Text | `Label` | `//Text` |
| Hyperlink | `Anchor` | `//Hyperlink` |
| List | `ListBox` | `//List` |
| ListItem | `Button` (no `ListItem` class) | `//ListItem` |
| Tab | `Tabs` | `//Tab` |
| TabItem | `Button` (no `TabItem` class) | `//TabItem` |
| Menu / MenuItem | `Menu` / `Button` (no `MenuItem` class) | `//Menu` / `//MenuItem` |
| DatePicker | `DateInput` | varies |
| TimePicker | `TimeInput` | varies |
| Calendar | `DesktopComponent` (day cells: `Button`) | varies |
| Tree | `DesktopComponent` | `//Tree` |
| TreeItem | `Button` | `//TreeItem` |
| Expander (Group) | `Expander` | varies |
| ProgressBar | `Progress` | `//ProgressBar` |
| Image | `Image` | `//Image` |
| Window / Pane | `DesktopComponent` | `//Window` / `//Pane` |

All classes are in `solutions.bellatrix.desktop.components`. In Java code, XPath locators go through the project's
`UiaXPathFindStrategy` (`create().by(T.class, new UiaXPathFindStrategy("…"))`), never `byXPath` — see
`bellatrix-desktop-locators-and-waits`.

---

