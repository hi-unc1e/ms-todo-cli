<p align="center">
    <img 
        src="./assets/logo.png"
        width="200"
        height="200"
    />
</p>

# Todo CLI

A cross-platform command-line interface to interact with Microsoft To Do, built using .NET 8.

## Build Status

| Platform | Status |
| ------ | ------------ |
| CI | [![CI build status](https://dev.azure.com/mtseckin/todo-cli/_apis/build/status/CI)](https://dev.azure.com/mtseckin/todo-cli/_build/latest?definitionId=1) |
| CD | [![Windows (x64) build status](https://dev.azure.com/mtseckin/todo-cli/_apis/build/status/CD)](https://dev.azure.com/mtseckin/todo-cli/_build/latest?definitionId=5)

## Getting Started

### Install

If you just want to give it a spin, head over to [releases](https://github.com/mehmetseckin/todo-cli/releases/). Download a release and extract to somewhere in your `PATH`, and run `todo --help` to get started.

### Build

```
# Clone the repository
git clone https://github.com/mehmetseckin/todo-cli.git

# Navigate into the source code folder
cd .\todo-cli\src

# Build the project
dotnet build
```

### Run

The application will automatically prompt you to sign in with your Microsoft account, and ask for your consent to access your data when needed.

```
# Run using dotnet run
dotnet run -p .\Todo.CLI -- --help

# Run from build output
.\src\publish\todo.exe --help
```

### Publish (self-contained .exe)

```
# Windows x64
dotnet publish src\Todo.CLI -c Release -r win-x64 -o .\src\publish

# macOS (Intel)
dotnet publish src\Todo.CLI -c Release -r osx-x64 -o .\src\publish

# macOS (Apple Silicon)
dotnet publish src\Todo.CLI -c Release -r osx-arm64 -o .\src\publish

# Linux x64
dotnet publish src\Todo.CLI -c Release -r linux-x64 -o .\src\publish
```

## Agent feedback subsystem (v0.5.0)

Send a rich decision request to the human and detect their answer — no external
services, only native To Do structures (see `docs/agent-design.md`).

```bash
# 1) Send: HTML body + checklist options + reminder (client notifies the human)
todo agent send "[Agent] Choose the deploy plan" \
  --html-file decision.html \
  --step "✅ Plan A: keep as-is" --step "✅ Plan B: add relay" \
  --important --due-date 2026-10-19 --remind 20:00

# 2) The human answers by checking a step or editing the body in any To Do app.

# 3) Read the feedback back
todo agent poll   --task-id <id>            # single check (cron-friendly)
todo agent watch  --task-id <id> --json     # adaptive loop: 5min/1h -> hourly/24h -> daily
```

Poll exit codes: `0` feedback detected (diff printed), `1` none yet, `2` error/task deleted.
The baseline is seeded at send time, so immediate human answers are never missed.

## Usage

### Commands

| Command | Description |
|---------|-------------|
| `todo add item <subject>` | Add a new to-do item |
| `todo add list <name>` | Add a new to-do list |
| `todo list [options]` | List items |
| `todo complete <id>` | Mark an item as completed |
| `todo remove <id>` | Remove an item |

### Options

| Option | Description |
|--------|-------------|
| `--list <name>` | Target a specific list |
| `--star` | Mark the item as important |
| `--due-date <date>` | Set a due date (`yyyy-MM-dd` or `MM-dd`) |
| `--remind <time>` | Set a reminder (`HH:mm`, or `yyyy-MM-dd HH:mm`). The To Do app notifies at that time |
| `--note <text>` | Add a note (body) to the item |

### Examples

```
# Add an item with a due date
todo add item "Buy groceries" --due-date 2026-05-20

# Add an item with MM-dd format (current year is used)
todo add item "Doctor appointment" --due-date 06-15

# Add a starred item to a specific list
todo add item "Important report" --star --list Work
todo add item "Submit tax form" --due-date 2026-05-20 --remind 09:00 --note "Bring last year's copy"
```

> **Notes**
> - Due dates and reminders are sent with your machine's IANA time zone (e.g. `Asia/Shanghai`), falling back to `TZ` or `Asia/Shanghai`.
> - Not supported by the Microsoft Graph To Do API (thus by this CLI): adding to **My Day**, **recurrence**, and **file attachments**.

# List all tasks
todo list

# Mark a task as completed
todo complete <task-id>

# Remove a task
todo remove <task-id>
```

### Output Example

```
Work (3):
- Buy groceries - NotStarted (05-20)
✓ Completed task - Completed (2026-05-10)
- Important report - NotStarted
```

## Contributing

Interested? You are awesome. Feel free to fork, do your thing and send a PR! Everything is appreciated.

## Code of Conduct

Be nice to people, give constructive feedback, and have fun!

## Stack

This project is built using the following nuggets of awesomeness, and many more. Many thanks to the folks who are working on and maintaining these products.

- [.NET 8](https://github.com/dotnet/core)
- [System.CommandLine](https://github.com/dotnet/command-line-api)
- [Microsoft Graph Beta SDK](https://github.com/microsoftgraph/msgraph-beta-sdk-dotnet)
- [Inquirer.cs](https://github.com/hayer/Inquirer.cs)
