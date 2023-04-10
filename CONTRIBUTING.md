# Contributing to Bolero

## What to contribute?

Bolero welcomes all types of contributions:

* Bug reports

* Feature proposals

* Bug fixes and feature implementations

Bug reports and feature proposals should be submitted on [the issue tracker](https://github.com/intellifactory/bolero). Code can be submitted as [pull requests](https://github.com/intellifactory/Bolero/pulls). Please post an issue to the tracker and discuss your idea with the community before submitting significant pull requests!

## Working with this repository

In the instructions below, `build` means `./build.sh` on Linux and OSX, and `.\build.cmd` on Windows.

Alternatively, you can restore the dotnet tools with `dotnet tool restore`, and then build with `dotnet run --project .build`.

### How to build

```
build
```

### How to run the test projects

* Run the unit tests:

    ```
    build -t test
    ```

* Debug the unit tests in Visual Studio Code:

    1. Run the unit tests in debug mode:

        ```
        build -t test-debug
        ```

        (Or in VS Code: "Run Test Task" -> "Unit Tests (debug)")

    2. When the output shows the following:

        ```
        Starting test execution, please wait...
        Host debugging is enabled. Please attach debugger to testhost process to continue.
        Process Id: 24780, Name: dotnet
        ```

        Go to the Debug panel, select "Attach" in the dropdown and click ▶️ "Start debugging".

    3. From the popup, select the process with the process id that was printed above.

    4. The tests may still not proceed properly at this point; in this case, click 🔄 "Reconnect" in the debug toolbar.

* Run the test web application on the client in WebAssembly:

    ```
    build -t run-client
    ```

* Run the test web application on the server side using Blazor.Server:

    ```
    build -t run-server
    ```

* Run the test web application for remoting:

    ```
    build -t run-remoting
    ```

## Project structure

The project in this repository are structure as follows.

* `src/`: The Bolero libraries and tools.

    * `Bolero/`: The main client-side library. Includes the HTML element and attribute types, Elmish components, client-side Remoting bits, and routing.

    * `Bolero.Html/`: The client-side library for HTML element builders and attribute functions.

    * `Bolero.Server/`: The main server-side library. Includes the server-side Remoting bits (ASP.NET Core service).

    * `Bolero.Build/`: The build task. Strips sigdata/optdata from F# assemblies, to reduce the served content size, and applies CSS scopes.

    * `Bolero.Templating/`: The Type Provider Design-Time Component for HTML templating.

* `tests`: The test projects and unit test suite.

    * `Unit/`: The automated tests suite.  
        Can be run using `build -t test`.

    * `Unit.Client/`: The client application for the automated tests suite.

    * `Client/`: A test client-side application.  
        Can be run using `build -t run-client`.

    * `Server/`: An ASP.NET Core application that serves `Client` as a server-side Blazor component.  
        Can be run using `build -t run-server`.

    * `Remoting.Client/`: A test client-side application that uses remoting.  
        Cannot be run standalone, as it requires the corresponding server side.

    * `Remoting.Server/`: An ASP.NET Core application that serves `Remoting.Client` as its client side and contains a server implementation for its remoting API.  
        Can be run using `build -t run-remoting`.

## Automated tests

The automated tests are located in the `tests/Unit/` and `tests/Unit.Client` project. They use the following tools:

* [NUnit](https://nunit.org/) as the testing framework.

* [Unquote](https://github.com/SwensenSoftware/unquote) for assertions.

* [FsCheck](https://fscheck.github.io/FsCheck/) for property checking.

* [Selenium](https://docs.seleniumhq.org/) to run and automate a headless browser.

UI tests are run as follows:

* A Bolero application is defined in `tests/Unit.Client`.

    * Test components are defined in `*.fs`, one file per category.

    * Rendered components for each category are added to the root component in `App.fs`.

* The test project proper is `tests/Unit`.

    * The Bolero application and the browser are started during NUnit setup in `Fixture.fs`.

    * Corresponding NUnit tests are defined in the `Tests` folder. They must be defined in the namespace `Bolero.Tests.Web` for NUnit setup to work properly. They can use `NodeFixture` to query elements.

So, in summary, a UI test category `Foo` consists of:

* A Bolero component defined in `tests/Unit.Client/Foo.fs` and added to `tests/Unit.Client/App.fs`;

* NUnit tests defined in `tests/Unit/Tests/Foo.fs`, using `NodeFixture` to query and interact with the component.
