# BlazorTerm

An interactive, iTerm-inspired personal portfolio built with Blazor, .NET 10, and C# 14. The interface uses a Powerlevel10k-style prompt to present professional experience, projects, open-source contributions, and contact information as terminal commands.

## Features

- Interactive Server rendering with persisted circuit state
- Command history, aliases, typo suggestions, and tab completion
- Powerlevel10k-inspired prompt and responsive iTerm-style interface
- Project case studies with terminal architecture diagrams
- GitHub projects and verified open-source contributions
- Terminal-formatted resume, career timeline, and technology stack
- Custom reconnect and circuit-resume experience
- Mobile layout and reduced-motion support

## Commands

Run `help` in the terminal to discover the full command set. Useful starting points include:

```text
neofetch
man tom
resume
stack
timeline
projects
project service-bus-explorer
contributions
github
contact
```

## Run Locally

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then run:

```shell
dotnet run
```

Open the URL shown in the terminal. The project uses Blazor Interactive Server rendering, so WebSocket support is required when hosting it behind a proxy.

## Container

Commits to `main` publish a container image to GitHub Container Registry. Pull and run the latest image with:

```shell
docker run --rm -p 8080:8080 ghcr.io/tombiddulph/blazorterm:latest
```

Pull requests build the image without publishing it. Version tags matching `v*` also produce matching container tags.

## Configuration

Personal content, project details, and external links are defined in `TerminalContent.cs`. Terminal behavior is implemented in `Components/Pages/Home.razor`, with browser-side keyboard handling in `wwwroot/terminal.js`.

## Inspiration

The terminal interaction concept was inspired by [Clark DuVall's jsterm](https://github.com/clarkduvall/jsterm). This project is an original Blazor and C# implementation with its own interface and command system.
