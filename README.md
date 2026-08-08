# EchoBox Icons

EchoBox Icons is a modern, high-performance Windows icon customization and desktop personalization application built with WinUI 3, .NET 8, and Fluent Design principles. It provides a complete workflow to customize, manage, and batch-apply custom icons across folders, storage drives, file type extensions, desktop shortcuts, and system elements, while integrating desktop background and theme customization.

---

## Overview

EchoBox Icons simplifies Windows shell customization through an intuitive desktop interface. Powered by an asynchronous pipeline and safety protections, the application allows users to personalize their desktop environment safely without risking system corruption.

---

## Features

### Icon Customization Pipeline
* **Folder Icons**: Apply custom icon assets to individual folders or execute recursive batch applications utilizing Windows `desktop.ini` configuration.
* **Drive Personalization**: Customize drive icons across fixed storage drives, removable media, and mapped network drives via Windows Registry integration.
* **File Extension Binding**: Associate custom `.ico` assets with specific file extensions (e.g., `.txt`, `.pdf`, `.cs`, `.jpg`).
* **Shortcut Icon Management**: Modify shortcut icon targets across user and public desktop locations.

### Icon Library & Conversion
* **Multi-Resolution ICO Converter**: Built-in asset pipeline to convert standard image formats into multi-resolution `.ico` files.
* **Category & Profile Storage**: Organize icon collections into categories with conflict detection and batch import capabilities.
* **Profile Export/Import**: Package, export, and transfer themed icon sets across environments.

### Themes & Personalization
* **Desktop Background Control**: Manage Windows desktop backgrounds with support for Picture, Solid Color, and Slideshow configurations.
* **Accent & Color Scheme Integration**: Configure system accent colors and toggle between dark and light themes seamlessly.
* **Streamlined UI**: Native Windows 11 Fluent Design interface with full-width responsive layouts.

### Shell Integration & Performance
* **Windows Explorer Context Menu**: Optional right-click shell extension for rapid icon application directly from File Explorer.
* **Fast File System Scanner**: High-performance multi-threaded directory scanner optimized for large folder hierarchies.
* **Safety Filter Protections**: Automated filters to prevent accidental modification of protected system directories (such as `C:\Windows`, `System32`, and `Program Files`).

---

## Architecture & Project Structure

The solution follows a clean modular architecture separated into distinct layers:

| Component | Target Framework | Responsibilities |
| :--- | :--- | :--- |
| **EchoBox.App** | net8.0-windows10.0 | WinUI 3 presentation layer providing navigation, dialogs, views (`HomePage`, `IconsPage`, `ThemesPage`, `SettingsPage`), and user settings management. |
| **EchoBox.Engine** | net8.0-windows10.0 | Core execution logic including `DesktopIniWriter`, `RegistryIconWriter`, `IcoConverter`, `FastFileSystemScanner`, `IconApplierPipeline`, `WindowsThemeService`, and `ContextMenuRegistrar`. |
| **EchoBox.Core** | net8.0 | Shared domain models, data structures, storage contracts, logging (`AppLogger`), and profile management (`ProfileService`, `IconStorageService`). |
| **EchoBox.ShellExtension** | Native C++ / WinRT | Windows Explorer context menu shell extension integration. |

---

## Getting Started

### Prerequisites

* **Operating System**: Windows 10 version 1809 (Build 17763) or higher, or Windows 11
* **Developer SDK**: [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* **Build Tools**: Visual Studio 2022 (with *.NET Desktop Development* and *Windows App Development* workloads) or VS Code with C# Dev Kit.

### Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/FoxyBimbo/EchoBox-Icons.git
   cd EchoBox-Icons
   ```

2. Restore and build the solution:
   ```bash
   dotnet build EchoBox-Icons.sln -c Debug
   ```

3. Run the desktop application:
   ```bash
   dotnet run --project EchoBox.App/EchoBox.App.csproj
   ```

---

## Technology Stack

* **UI Framework**: WinUI 3 / Windows App SDK (Fluent Design)
* **Runtime**: .NET 8 (C# 12)
* **Shell & System API**: Windows Desktop APIs, Registry Services, Win32 Interop
* **Concurrency**: Async/Await task pipelines and multi-threaded file system scanning

---

## License

This project is licensed under the [MIT License](LICENSE).

---

## Attribution

App Icon by [John Bloor](https://pixabay.com/users/johnbloor-47096611/) from [Pixabay](https://pixabay.com/).

