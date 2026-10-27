# BELLATRIX Desktop System Tests Recipes (Java)

> **Note:** These recipes show direct component usage for reference. For production code, always use **Page Objects** (see `bellatrix-desktop-page-objects-and-patterns`).
>
> **Page Object Structure:**
> ```
> pages/{window}/{Window}.java  - Action methods (extends DesktopPage<Map, Asserts>)
> pages/{window}/Map.java       - Component methods (extends PageMap)
> pages/{window}/Asserts.java   - Assertion methods (extends PageAsserts<Map>)
> ```
>
> XPath locators go through the project's `UiaXPathFindStrategy` (`create().by(...)` / `create().allBy(...)`);
> `byXPath` / `allByXPath` are broken in BELLATRIX-Java 1.0 — see `bellatrix-desktop-locators-and-waits`.

---

## Login Flow Recipe

```java
package demo.desktoptests.tests.login;

import demo.desktoptests.common.Constants;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.desktop.components.Button;
import solutions.bellatrix.desktop.components.Label;
import solutions.bellatrix.desktop.components.PasswordField;
import solutions.bellatrix.desktop.components.TextField;
import solutions.bellatrix.desktop.infrastructure.ExecutionApp;
import solutions.bellatrix.desktop.infrastructure.Lifecycle;
import solutions.bellatrix.desktop.infrastructure.junit.DesktopTest;

@ExecutionApp(appPath = Constants.MY_APP_PATH, lifecycle = Lifecycle.RESTART_EVERY_TIME)
public class LoginRecipeTests extends DesktopTest {
    @Test
    @Tag("Smoke")
    public void login_WithValidCredentials_OpensMainWindow() {
        // Arrange
        TextField usernameField = app().create().byAutomationId(TextField.class, "txtUsername");
        PasswordField passwordField = app().create().byAutomationId(PasswordField.class, "txtPassword");
        Button loginButton = app().create().byAutomationId(Button.class, "btnLogin");

        // Act
        usernameField.setText("user@example.com");
        passwordField.setPassword("Password123");
        loginButton.click();

        // Assert
        Label welcomeLabel = app().create().byAutomationId(Label.class, "lblWelcome");
        welcomeLabel.toBeVisible().waitToBe();
        welcomeLabel.validateTextIs("Welcome");
    }
}
```

---

## Form Submission Recipe

```java
@Test
public void registerUser_WithValidData_ShowsSuccessMessage() {
    // Arrange
    TextField nameField = app().create().byAutomationId(TextField.class, "txtName");
    TextField emailField = app().create().byAutomationId(TextField.class, "txtEmail");
    ComboBox countryCombo = app().create().byAutomationId(ComboBox.class, "cmbCountry");
    CheckBox termsCheckbox = app().create().byAutomationId(CheckBox.class, "chkTerms");
    Button submitButton = app().create().byAutomationId(Button.class, "btnSubmit");

    // Act
    nameField.setText("John Doe");
    emailField.setText("john@example.com");
    countryCombo.selectByText("United States");
    termsCheckbox.check();
    submitButton.click();

    // Assert
    Label successMessage = app().create().byAutomationId(Label.class, "lblSuccess");
    successMessage.toHaveContent().waitToBe();
    successMessage.validateTextIs("Registration successful");
}
```

---

## List/Grid Selection Recipe

```java
@Test
public void selectItem_FromList_DisplaysDetails() {
    // Arrange & Act — ListItem has no Java class: map it as Button
    Button item = app().create().by(Button.class, new UiaXPathFindStrategy("//ListItem[@Name='Product A']"));
    item.click();

    // Assert
    Label detailsLabel = app().create().byAutomationId(Label.class, "lblDetails");
    detailsLabel.toBeVisible().waitToBe();
    detailsLabel.validateTextIs("Product A");
}
```

---

## Dialog Confirmation Recipe

```java
@Test
public void deleteItem_WithConfirmation_RemovesItem() {
    // Act - trigger delete
    Button deleteButton = app().create().byAutomationId(Button.class, "btnDelete");
    deleteButton.click();

    // Act - confirm dialog (wait for it, then click)
    Button confirmButton = app().create().byAutomationId(Button.class, "btnConfirm");
    confirmButton.toBeVisible().toBeClickable().waitToBe();
    confirmButton.click();

    // Assert - the dialog is gone and the status updated
    app().waitFor().wait(confirmButton, Wait.to().notExist());
    Label statusLabel = app().create().byAutomationId(Label.class, "lblStatus");
    statusLabel.toHaveContent().waitToBe();
    statusLabel.validateTextIs("Item deleted");
}
```

---

## Menu Navigation Recipe

```java
@Test
public void openSettings_ViaMenu_ShowsSettingsWindow() {
    // Act - navigate menu: File > Settings (MenuItem has no Java class: map it as Button)
    Button fileMenu = app().create().by(Button.class, new UiaXPathFindStrategy("//MenuItem[@Name='File']"));
    fileMenu.click();

    Button settingsItem = app().create().by(Button.class, new UiaXPathFindStrategy("//MenuItem[@Name='Settings']"));
    settingsItem.click();

    // Assert
    Label settingsTitle = app().create().by(Label.class, new UiaXPathFindStrategy("//Text[@Name='Settings']"));
    settingsTitle.toBeVisible().waitToBe();
}
```

---

## Settings Dialog Recipe (Tabs, Combos, Checkboxes)

```java
@Test
public void changeTheme_ToDarkMode_UpdatesAppearance() {
    // Arrange - navigate to appearance tab (TabItem → Button)
    Button appearanceTab = app().create().by(Button.class, new UiaXPathFindStrategy("//TabItem[@Name='Appearance']"));
    appearanceTab.click();

    // Act
    ComboBox themeCombo = app().create().byAutomationId(ComboBox.class, "cmbTheme");
    themeCombo.selectByText("Dark");

    Button saveButton = app().create().byAutomationId(Button.class, "btnSave");
    saveButton.click();

    // Assert
    Label statusLabel = app().create().byAutomationId(Label.class, "lblStatus");
    statusLabel.toHaveContent().waitToBe();
    statusLabel.validateTextIs("Settings saved");
}
```

---

## Data-Driven Test Recipe

Needs `org.junit.jupiter:junit-jupiter-params` in `pom.xml` (it is NOT pulled in by `bellatrix.desktop`). Use it only
for genuine data variations of ONE scenario — **never** to merge several SPECTRA test cases (1 TC = 1 `@Test` method
with its own `@ManualTestCase`).

```java
@ParameterizedTest
@CsvSource({
        "admin, admin123, 'Welcome, Admin'",
        "user, user123, 'Welcome, User'",
        "guest, guest123, 'Welcome, Guest'"
})
public void login_WithDifferentRoles_ShowsCorrectWelcome(String username, String password, String expectedWelcome) {
    // Arrange
    TextField usernameField = app().create().byAutomationId(TextField.class, "txtUsername");
    PasswordField passwordField = app().create().byAutomationId(PasswordField.class, "txtPassword");
    Button loginButton = app().create().byAutomationId(Button.class, "btnLogin");

    // Act
    usernameField.setText(username);
    passwordField.setPassword(password);
    loginButton.click();

    // Assert
    Label welcomeLabel = app().create().byAutomationId(Label.class, "lblWelcome");
    welcomeLabel.toHaveContent().waitToBe();
    welcomeLabel.validateTextIs(expectedWelcome);
}
```

(`org.junit.jupiter.params.ParameterizedTest`, `org.junit.jupiter.params.provider.CsvSource`.)

---

## Page Objects End-to-End Recipe

```java
package demo.desktoptests.tests.endtoend;

import demo.desktoptests.common.Constants;
import demo.desktoptests.pages.loginwindow.LoginWindow;
import demo.desktoptests.pages.mainwindow.MainWindow;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import solutions.bellatrix.desktop.infrastructure.ExecutionApp;
import solutions.bellatrix.desktop.infrastructure.Lifecycle;
import solutions.bellatrix.desktop.infrastructure.junit.DesktopTest;

@ExecutionApp(appPath = Constants.MY_APP_PATH, lifecycle = Lifecycle.RESTART_EVERY_TIME)
public class EndToEndTests extends DesktopTest {
    @Test
    @Tag("Smoke")
    public void completeWorkflow_LoginAndTransfer_ShowsConfirmation() {
        // Arrange
        LoginWindow loginWindow = app().createPage(LoginWindow.class);
        loginWindow.asserts().windowLoaded();

        // Act - Login
        MainWindow mainWindow = loginWindow.loginAndNavigateToMain("testuser", "password123");
        mainWindow.asserts().windowLoaded();

        // Act - Perform transfer
        mainWindow.transferItem("Item2", "targetUser", "securePass");

        // Assert
        mainWindow.asserts().transferSuccessful();
    }
}
```

---

## Common XPath Patterns for Desktop

```text
// By AutomationId — prefer byAutomationId(T.class, "btnOk"); as XPath only inside structural locators
"//Button[@AutomationId='btnOk']"
"//Edit[@AutomationId='txtUsername']"

// By Name — prefer byName(T.class, "OK")
"//Button[@Name='OK']"
"//Edit[@Name='Username']"

// Dynamic with parameter
"//ListItem[@Name='" + itemName + "']"
String.format("//TabItem[@Name='%s']", tabName)
String.format("//TreeItem[@Name='%s']", nodeName)

// Nested (child within parent)
"//ComboBox[@AutomationId='cmbList']//ListItem[@Name='Item1']"
```

All of them are passed as `new UiaXPathFindStrategy(xpath)` to `create().by(...)` / `create().allBy(...)`.

---

## Required Imports

```java
import solutions.bellatrix.desktop.components.*;                 // Button, TextField, PasswordField, Label, ...
import solutions.bellatrix.desktop.pages.DesktopPage;            // page classes (PageMap / PageAsserts for Map / Asserts)
import solutions.bellatrix.desktop.waitstrategies.Wait;          // Wait.to().notExist() ...
import solutions.bellatrix.desktop.infrastructure.ExecutionApp;  // @ExecutionApp
import solutions.bellatrix.desktop.infrastructure.Lifecycle;     // Lifecycle.RESTART_EVERY_TIME ...
import solutions.bellatrix.desktop.infrastructure.junit.DesktopTest; // (or ...infrastructure.testng.DesktopTest)
import org.junit.jupiter.api.Test;                               // @Test, @Tag, Assertions
import demo.desktoptests.common.UiaXPathFindStrategy;            // XPath locators
import demo.desktoptests.pages.loginwindow.LoginWindow;          // your page objects
import demo.desktoptests.models.LoginCredentials;                // your data models (if applicable)
```
