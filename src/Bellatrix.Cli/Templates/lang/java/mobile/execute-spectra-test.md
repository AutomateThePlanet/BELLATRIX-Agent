<!-- lang:app-path -->
Optional: `--device-name "<device>"`, `--appium-url <url>` (default `http://127.0.0.1:4723`). Take the app path
from the Java test project: the `appPath` of the test class's `@ExecutionApp` (usually a `Constants` field), else
`defaultAppPath` in `src/main/resources/testFrameworkSettings.<env>.json` — section `androidSettings` (Android) or
`iOSSettings` (iOS); `<env>` is the `environment=` value in `src/main/resources/application.properties`. Expand
`${…}` placeholders (Java system properties such as `${user.home}`) to a real path before passing `--app` — or
ask the user. The device name / Appium URL there are `defaultDeviceName` / `serviceUrl`. Use the same `--session`
name on every later call.
