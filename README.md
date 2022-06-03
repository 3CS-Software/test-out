# About Test-Out
Testout is a distributed test running platform.  It's main goal is to reduce CI processing times for long running integration tests.

# How it works
(TODO: diagram)
A test run is started by an Invoker, which sends the test folder to the server.  The server then distributes that folder to each agent, and invokes tests in parallel on each agent.  Once all the tests have been processed, the results are given to the invoker, which then writes out the test result file in .trx format.

# Installation

## Supported Platforms
Right now it only supports windows platforms for the agents, as it's currently hard coded to use the .net framework version of nUnit.

## Installation for Server/Agent
To install Test-Out, run the following command:
`dotnet tool install --tool-path C:\Tools\dotnet ThreeCS.TestOut.Console`

Note that we've put the tool into a specific folder.  This is so the tool instance is distinct from any user dotnet tools on the pc (the --tool-path option), and we can control versions and permissions for it, as is required for services.

To setup a Test-Out server, run
`sc.exe create "TestoutServer" binpath="C:\Tools\dotnet\testout.exe --as-service --mode Server --server-url https://the-server-hostname:34872"`

To setup a Test-Out agent, run
`sc.exe create "TestoutAgent" binpath="C:\Tools\dotnet\testout.exe --as-service --mode Agent --server-url https://the-server-hostname:34872"`

Be sure to check what users the service is running as, what the startup for the service is, and check firewalls after installation.

When run with the `--as-service` flag, Test-Out will put all it's data in the global program data folder, usually C:\ProgramData\TestOut.  This flag can be used with the service stopped, along with the `--verbose` option to help diagnose issues.

## Installation for Test-Out Client
Run the following command:
`dotnet tool install ThreeCS.TestOut.Console`

Test-Out can now be invoked using the 'testout' command.  Run `testout -?` for detailed usage instructions.
Test-Out can be run in any mode (Run, Agent, Server) interactively.  Data that is used is stored under the users AppData/Local/TestOut folder.

# Running Tests
Once the tool is installed, tests can be run with:
`testout --mode Run --base-path ".\MyProject.Tests\bin\Debug" --test-assembly "MyProject.Tests.dll"`

For single machine testing scenarios, the mode can be both Run/Agent and Server, eg:
`testout --mode Run,Agent,Server --base-path ".\MyProject.Tests\bin\Debug" --test-assembly "MyProject.Tests.dll"`
This will start a console process that hosts a server on the default address (http://localhost:34872), along with an agent for that server and finally starts a runner process.


