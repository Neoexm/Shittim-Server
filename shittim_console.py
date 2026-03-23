import json
import os
import queue
import shutil
import sqlite3
import subprocess
import sys
import tempfile
import textwrap
import threading
import time
import tkinter as tk
import urllib.request
import webbrowser
import zipfile
from dataclasses import dataclass
from datetime import datetime, timedelta
from pathlib import Path
from tkinter import filedialog, messagebox, scrolledtext, ttk


APP_TITLE = "Shittim Server"
APP_SUBTITLE = "Launcher and management console"


@dataclass
class RuntimeProcess:
    name: str
    process: subprocess.Popen | None = None
    state: str = "stopped"
    detail: str = "Idle"


class ShittimConsole:
    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title(APP_TITLE)
        self.root.geometry("1420x920")
        self.root.minsize(1240, 820)

        self.base_dir = Path(__file__).resolve().parent
        self.launch_dir = Path(sys.executable).resolve().parent if getattr(sys, "frozen", False) else self.base_dir
        self.appdata_dir = Path(os.environ.get("APPDATA", str(Path.home() / "AppData" / "Roaming"))) / "Shittim Server"
        self.appdata_dir.mkdir(parents=True, exist_ok=True)
        self.server_dir = self.appdata_dir / "Shittim-Server"
        self.server_project = self.server_dir / "Shittim-Server.csproj"
        self.server_runtime_dir = self.appdata_dir / "server-runtime"
        self.server_runtime_executable = self.server_runtime_dir / "Shittim-Server.exe"
        self.source_database_path = self.server_dir / "shittim.sqlite3"
        self.runtime_database_path = self.server_runtime_dir / "shittim.sqlite3"
        self.database_path = self.source_database_path
        self.mitm_script_dir = self.appdata_dir / "Scripts" / "redirect_server_mitmproxy"
        self.mitm_script_path = self.mitm_script_dir / "redirect_server.py"
        self.bundled_runtime_dir = self.launch_dir / "server-runtime"
        self.bundled_source_dir = self.launch_dir / "Shittim-Server"
        self.bundled_scripts_dir = self.launch_dir / "Scripts"
        self.config_dir = self.appdata_dir / ".shittim-console"
        self.config_dir.mkdir(exist_ok=True)
        self.logs_dir = self.config_dir / "logs"
        self.logs_dir.mkdir(exist_ok=True)
        self.current_log_path = self.logs_dir / "current-session.log"
        self.settings_path = self.config_dir / "console_settings.json"
        self.gacha_config_path = self.config_dir / "gacha_config.json"
        self.banner_config_path = self.config_dir / "banner_config.json"
        self.update_state_path = self.config_dir / "update_state.json"

        self.status_palette = {
            "running": {"dot": "●", "label": "Running", "color": "#2fbf71"},
            "starting": {"dot": "●", "label": "Starting", "color": "#f5a524"},
            "stopped": {"dot": "●", "label": "Stopped", "color": "#8f98a8"},
            "failed": {"dot": "●", "label": "Failed", "color": "#e25555"},
            "ready": {"dot": "●", "label": "Ready", "color": "#2fbf71"},
            "warning": {"dot": "●", "label": "Attention", "color": "#f5a524"},
            "missing": {"dot": "●", "label": "Missing", "color": "#e25555"},
            "unknown": {"dot": "●", "label": "Unknown", "color": "#8f98a8"},
        }

        self.environment = {
            "dotnet": {"status": "unknown", "detail": "Checking .NET SDK"},
            "python": {"status": "unknown", "detail": "Checking Python runtime"},
            "mitmweb": {"status": "unknown", "detail": "Checking mitmproxy tools"},
            "certificate_file": {"status": "unknown", "detail": "Checking mitm certificate files"},
            "certificate_installed": {"status": "unknown", "detail": "Checking Local Machine trust store"},
            "database": {"status": "unknown", "detail": "Checking game database"},
            "server_project": {"status": "unknown", "detail": "Checking server project"},
            "mitm_script": {"status": "unknown", "detail": "Checking redirect script"},
        }
        self.runtime = {
            "server": RuntimeProcess("Game Server", None, "stopped", "Server is not running"),
            "mitm": RuntimeProcess("MITM Proxy", None, "stopped", "Proxy is not running"),
        }
        self.log_queue: queue.Queue[tuple[str, str]] = queue.Queue()
        self.mail_items: list[dict] = []
        self.accounts_cache: list[tuple[int, str]] = []
        self.current_view = None
        self.nav_buttons: dict[str, ttk.Button] = {}
        self.views: dict[str, ttk.Frame] = {}
        self.selected_account_server_id: int | None = None
        self.monitor_after_id = None
        self.refresh_in_progress = False
        self.install_in_progress = False
        self.install_prompt_shown = False

        self.settings = self.load_settings()
        self.apply_directory_settings()
        self.update_state = self.load_update_state()
        self.update_status = {"state": "unknown", "detail": "", "release_tag": None, "asset_url": None, "release_name": None}
        self._prepare_log_file()
        self.refresh_runtime_paths()
        self._apply_window_chrome()
        self._configure_theme()
        self._build_layout()
        self.show_view("overview")
        self.refresh_environment_async(initial=True)
        self.load_accounts()
        self.poll_logs()
        self.monitor_processes()
        self.check_for_updates_async(initial=True)
        self.schedule_update_check()
        self.root.protocol("WM_DELETE_WINDOW", self.on_close)

    def load_settings(self):
        defaults = {
            "theme_mode": "dark",
            "server_url": "http://localhost:5000",
            "mitm_url": "http://127.0.0.1:8081",
            "server_port": 5000,
            "mitm_listen_port": 8080,
            "mitm_web_port": 8081,
            "mitm_mode": "local:BlueArchive.exe",
            "dotnet_arguments": "run --project Shittim-Server.csproj",
            "mitm_arguments": "--no-http2 -s redirect_server.py --set termlog_verbosity=warn --mode local:BlueArchive.exe",
            "auto_refresh_seconds": 20,
            "install_dir": "",
            "logs_dir": "",
        }
        if self.settings_path.exists():
            try:
                loaded = json.loads(self.settings_path.read_text(encoding="utf-8"))
                defaults.update(loaded)
            except Exception:
                pass
        return defaults

    def save_settings(self):
        self.settings_path.write_text(json.dumps(self.settings, indent=2), encoding="utf-8")

    def apply_directory_settings(self):
        install_dir = self.settings.get("install_dir", "").strip()
        if install_dir:
            self.install_dir = Path(install_dir)
        else:
            self.install_dir = self.appdata_dir
        self.install_dir.mkdir(parents=True, exist_ok=True)

        self.server_dir = self.install_dir / "Shittim-Server"
        self.server_project = self.server_dir / "Shittim-Server.csproj"
        self.server_runtime_dir = self.install_dir / "server-runtime"
        self.server_runtime_executable = self.server_runtime_dir / "Shittim-Server.exe"
        self.source_database_path = self.server_dir / "shittim.sqlite3"
        self.runtime_database_path = self.server_runtime_dir / "shittim.sqlite3"
        self.database_path = self.source_database_path
        self.mitm_script_dir = self.install_dir / "Scripts" / "redirect_server_mitmproxy"
        self.mitm_script_path = self.mitm_script_dir / "redirect_server.py"

        logs_dir = self.settings.get("logs_dir", "").strip()
        if logs_dir:
            self.logs_dir = Path(logs_dir)
        else:
            self.logs_dir = self.config_dir / "logs"
        self.logs_dir.mkdir(parents=True, exist_ok=True)
        self.current_log_path = self.logs_dir / "current-session.log"

    def load_update_state(self):
        defaults = {"last_seen_release": None, "last_checked": None, "asset_url": None, "release_name": None}
        if self.update_state_path.exists():
            try:
                loaded = json.loads(self.update_state_path.read_text(encoding="utf-8"))
                defaults.update(loaded)
            except Exception:
                pass
        return defaults

    def save_update_state(self):
        self.update_state_path.write_text(json.dumps(self.update_state, indent=2), encoding="utf-8")

    def check_for_updates_async(self, initial=False):
        self.update_status = {"state": "checking", "detail": "Checking latest release", "release_tag": None, "asset_url": None, "release_name": None}
        self.update_update_views()

        def worker():
            status = self.collect_update_status()
            self.root.after(0, lambda: self.apply_update_status(status, initial))

        threading.Thread(target=worker, daemon=True).start()

    def schedule_update_check(self):
        interval_ms = 30 * 60 * 1000  # 30 minutes
        self.update_check_after_id = self.root.after(interval_ms, self._periodic_update_check)

    def _periodic_update_check(self):
        self.check_for_updates_async(initial=False)
        self.schedule_update_check()

    def collect_update_status(self):
        url = "https://api.github.com/repos/Neoexm/Shittim-Server/releases/latest"
        request = urllib.request.Request(url, headers={"User-Agent": "Shittim-Server"})
        try:
            with urllib.request.urlopen(request, timeout=15) as response:
                payload = json.loads(response.read().decode("utf-8"))
            release_tag = payload.get("tag_name")
            release_name = payload.get("name") or release_tag
            assets = payload.get("assets", [])
            asset_url = None
            for asset in assets:
                name = asset.get("name", "")
                if name.lower().endswith(".zip"):
                    asset_url = asset.get("browser_download_url")
                    break
            if not release_tag:
                return {"state": "failed", "detail": "No published release tag was found", "release_tag": None, "asset_url": None, "release_name": None}

            last_seen = self.update_state.get("last_seen_release")
            if not last_seen:
                state = "ready"
                detail = f"Tracking latest release {release_tag}."
            elif release_tag != last_seen:
                state = "available"
                detail = f"New release available: {release_tag}"
            else:
                state = "current"
                detail = f"You are on the latest release: {release_tag}"

            return {"state": state, "detail": detail, "release_tag": release_tag, "asset_url": asset_url, "release_name": release_name}
        except Exception as exc:
            return {"state": "failed", "detail": f"Update check failed: {exc}", "release_tag": None, "asset_url": None, "release_name": None}

    def apply_update_status(self, status, initial=False):
        self.update_status = status
        self.update_state["last_checked"] = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        if status.get("asset_url"):
            self.update_state["asset_url"] = status["asset_url"]
        if status.get("release_name"):
            self.update_state["release_name"] = status["release_name"]
        if initial and status.get("release_tag") and not self.update_state.get("last_seen_release"):
            self.update_state["last_seen_release"] = status["release_tag"]
            self.save_update_state()
            self.update_status = {
                "state": "current",
                "detail": f"Tracking latest release {status['release_tag']}",
                "release_tag": status["release_tag"],
                "asset_url": status.get("asset_url"),
                "release_name": status.get("release_name"),
            }
        else:
            self.save_update_state()
        self.update_update_views()

    def update_update_views(self):
        state = self.update_status.get("state", "unknown") if hasattr(self, "update_status") else "unknown"
        detail = self.update_status.get("detail", "") if hasattr(self, "update_status") else ""
        label_map = {
            "checking": "Checking",
            "available": "Update available",
            "current": "Up to date",
            "ready": "Tracking current",
            "failed": "Check failed",
            "unknown": "Unknown",
        }
        summary = label_map.get(state, "Unknown")
        color_map = {
            "checking": self.colors.get("muted", "#9aa6b8"),
            "available": self.colors.get("warning", "#f5a524"),
            "current": self.colors.get("success", "#2fbf71"),
            "ready": self.colors.get("success", "#2fbf71"),
            "failed": self.colors.get("danger", "#e25555"),
            "unknown": self.colors.get("muted", "#9aa6b8"),
        }
        if hasattr(self, "update_value_label"):
            self.update_value_label.configure(text=summary)
        if hasattr(self, "update_detail_label"):
            self.update_detail_label.configure(text=detail)
        if hasattr(self, "update_status_label"):
            self.update_status_label.configure(text=detail or summary, foreground=color_map.get(state, self.colors.get("text", "#edf2fb")))
        if hasattr(self, "summary_cards") and "updates" in self.summary_cards:
            self.summary_cards["updates"][0].configure(text=summary)
            self.summary_cards["updates"][1].configure(text=detail)

    def install_latest_update(self):
        if self.install_in_progress:
            messagebox.showinfo(APP_TITLE, "Project installation is already running")
            return
        self.install_release_async()

    def install_release_async(self):
        if self.install_in_progress:
            return
        asset_url = self.update_status.get("asset_url")
        release_tag = self.update_status.get("release_tag")
        if not asset_url or not release_tag:
            messagebox.showerror(APP_TITLE, "No downloadable release asset is currently available")
            return
        self.install_in_progress = True
        self.append_log("Updater", f"Installing release {release_tag}")
        threading.Thread(target=self._install_release_worker, args=(asset_url, release_tag), daemon=True).start()

    def _install_release_worker(self, asset_url, release_tag):
        temp_dir = None
        try:
            temp_dir = Path(tempfile.mkdtemp(prefix="shittim-release-"))
            archive_path = temp_dir / "release.zip"
            extract_dir = temp_dir / "extract"
            self.append_log("Updater", f"Downloading release asset from {asset_url}")
            urllib.request.urlretrieve(asset_url, archive_path)
            extract_dir.mkdir(parents=True, exist_ok=True)
            with zipfile.ZipFile(archive_path, "r") as zip_ref:
                zip_ref.extractall(extract_dir)
            self.install_extracted_payload(extract_dir)
            self.update_state["last_seen_release"] = release_tag
            self.update_state["asset_url"] = asset_url
            self.save_update_state()
            self.update_status = {
                "state": "current",
                "detail": f"Installed latest release {release_tag}",
                "release_tag": release_tag,
                "asset_url": asset_url,
                "release_name": self.update_status.get("release_name"),
            }
            self.root.after(0, self.update_update_views)
            self.root.after(0, lambda: messagebox.showinfo(APP_TITLE, f"Release {release_tag} installed successfully"))
        except Exception as exc:
            self.append_log("Updater", f"Release installation failed: {exc}")
            self.root.after(0, lambda: messagebox.showerror(APP_TITLE, f"Failed to install release: {exc}"))
        finally:
            self.install_in_progress = False
            if temp_dir and temp_dir.exists():
                shutil.rmtree(temp_dir, ignore_errors=True)
            self.refresh_runtime_paths()
            self.root.after(0, self.refresh_environment_async)

    def refresh_runtime_paths(self):
        self.database_path = self.runtime_database_path if self.server_runtime_executable.exists() else self.source_database_path

    def has_published_server(self):
        return self.server_runtime_executable.exists()

    def has_server_source(self):
        return self.server_project.exists()

    def has_local_release_payload(self):
        return self.bundled_runtime_dir.exists() or self.bundled_source_dir.exists() or self.bundled_scripts_dir.exists()

    def server_installation_available(self):
        return self.has_published_server() or self.has_server_source()

    def get_server_launch_target(self):
        if self.has_published_server():
            return [str(self.server_runtime_executable)], self.server_runtime_dir, "Using prepublished server runtime"
        args = ["dotnet"] + self.settings.get("dotnet_arguments", "run --project Shittim-Server.csproj").split()
        return args, self.server_dir, "Using source-based dotnet launch"

    def _apply_window_chrome(self):
        self.root.configure(bg="#10141d")

    def _configure_theme(self):
        style = ttk.Style(self.root)
        try:
            style.theme_use("clam")
        except tk.TclError:
            pass

        self.colors = {
            "bg": "#10141d",
            "surface": "#161c27",
            "surface_alt": "#1b2230",
            "surface_soft": "#232c3b",
            "border": "#2f394a",
            "text": "#edf2fb",
            "muted": "#9aa6b8",
            "accent": "#4f8cff",
            "accent_pressed": "#3e73d9",
            "success": "#2fbf71",
            "warning": "#f5a524",
            "danger": "#e25555",
        }

        style.configure("App.TFrame", background=self.colors["bg"])
        style.configure("Surface.TFrame", background=self.colors["surface"])
        style.configure("Soft.TFrame", background=self.colors["surface_alt"])
        style.configure("Card.TLabelframe", background=self.colors["surface"], bordercolor=self.colors["border"], relief="solid")
        style.configure("Card.TLabelframe.Label", background=self.colors["surface"], foreground=self.colors["text"], font=("Segoe UI Semibold", 11))
        style.configure("Title.TLabel", background=self.colors["bg"], foreground=self.colors["text"], font=("Segoe UI Semibold", 23))
        style.configure("Subtitle.TLabel", background=self.colors["bg"], foreground=self.colors["muted"], font=("Segoe UI", 10))
        style.configure("SectionTitle.TLabel", background=self.colors["surface"], foreground=self.colors["text"], font=("Segoe UI Semibold", 13))
        style.configure("CardTitle.TLabel", background=self.colors["surface"], foreground=self.colors["text"], font=("Segoe UI Semibold", 12))
        style.configure("Body.TLabel", background=self.colors["surface"], foreground=self.colors["text"], font=("Segoe UI", 10))
        style.configure("Muted.TLabel", background=self.colors["surface"], foreground=self.colors["muted"], font=("Segoe UI", 9))
        style.configure("Value.TLabel", background=self.colors["surface"], foreground=self.colors["text"], font=("Segoe UI Semibold", 16))

        style.configure(
            "Nav.TButton",
            background=self.colors["surface_alt"],
            foreground=self.colors["text"],
            borderwidth=0,
            focusthickness=0,
            font=("Segoe UI Semibold", 10),
            padding=(16, 11),
            anchor="w",
        )
        style.map("Nav.TButton", background=[("active", self.colors["surface_soft"]), ("pressed", self.colors["surface_soft"])] )

        style.configure(
            "NavSelected.TButton",
            background=self.colors["accent"],
            foreground="#ffffff",
            borderwidth=0,
            focusthickness=0,
            font=("Segoe UI Semibold", 10),
            padding=(16, 11),
            anchor="w",
        )
        style.map("NavSelected.TButton", background=[("active", self.colors["accent_pressed"]), ("pressed", self.colors["accent_pressed"])] )

        style.configure(
            "Primary.TButton",
            background=self.colors["accent"],
            foreground="#ffffff",
            borderwidth=0,
            focusthickness=0,
            font=("Segoe UI Semibold", 10),
            padding=(14, 9),
        )
        style.map("Primary.TButton", background=[("active", self.colors["accent_pressed"]), ("pressed", self.colors["accent_pressed"])] )

        style.configure(
            "Secondary.TButton",
            background=self.colors["surface_soft"],
            foreground=self.colors["text"],
            borderwidth=0,
            focusthickness=0,
            font=("Segoe UI", 10),
            padding=(12, 9),
        )
        style.map("Secondary.TButton", background=[("active", self.colors["border"]), ("pressed", self.colors["border"])] )

        style.configure(
            "Danger.TButton",
            background=self.colors["danger"],
            foreground="#ffffff",
            borderwidth=0,
            focusthickness=0,
            font=("Segoe UI Semibold", 10),
            padding=(12, 9),
        )
        style.map("Danger.TButton", background=[("active", "#c84747"), ("pressed", "#c84747")])

        style.configure("TEntry", fieldbackground="#0f141e", foreground=self.colors["text"], bordercolor=self.colors["border"], insertcolor=self.colors["text"], padding=8)
        style.configure("TCombobox", fieldbackground="#0f141e", foreground=self.colors["text"], bordercolor=self.colors["border"], padding=6)
        style.map("TCombobox", fieldbackground=[("readonly", "#0f141e")], foreground=[("readonly", self.colors["text"])])
        style.configure("Treeview", background="#0f141e", fieldbackground="#0f141e", foreground=self.colors["text"], bordercolor=self.colors["border"], rowheight=30)
        style.configure("Treeview.Heading", background=self.colors["surface_soft"], foreground=self.colors["text"], font=("Segoe UI Semibold", 10), relief="flat")
        style.map("Treeview", background=[("selected", self.colors["accent"])] , foreground=[("selected", "#ffffff")])
        style.map("Treeview.Heading", background=[("active", self.colors["surface_soft"]), ("pressed", self.colors["surface_soft"])], foreground=[("active", self.colors["text"]), ("pressed", self.colors["text"])])
        style.configure("TNotebook", background=self.colors["surface"])
        style.configure("TNotebook.Tab", background=self.colors["surface_soft"], foreground=self.colors["muted"], padding=(14, 8), font=("Segoe UI Semibold", 10))
        style.map("TNotebook.Tab", background=[("selected", self.colors["surface"]), ("active", self.colors["surface_soft"])], foreground=[("selected", self.colors["text"]), ("active", self.colors["text"])])

    def _build_layout(self):
        self.root.grid_columnconfigure(1, weight=1)
        self.root.grid_rowconfigure(0, weight=1)

        sidebar = ttk.Frame(self.root, style="Soft.TFrame", padding=(18, 18, 18, 18))
        sidebar.grid(row=0, column=0, sticky="nsw")
        sidebar.grid_columnconfigure(0, weight=1)

        ttk.Label(sidebar, text=APP_TITLE, style="SectionTitle.TLabel").grid(row=0, column=0, sticky="w", pady=(6, 2))
        ttk.Label(sidebar, text=APP_SUBTITLE, style="Muted.TLabel").grid(row=1, column=0, sticky="w", pady=(0, 18))

        nav_items = [
            ("overview", "Overview"),
            ("operations", "Operations"),
            ("accounts", "Accounts"),
            ("gacha", "Gacha"),
            ("mail", "Mail"),
            ("items", "Items"),
            ("characters", "Characters"),
            ("management", "Management"),
        ]
        for index, (key, label) in enumerate(nav_items, start=2):
            button = ttk.Button(sidebar, text=label, style="Nav.TButton", command=lambda k=key: self.show_view(k))
            button.grid(row=index, column=0, sticky="ew", pady=4)
            self.nav_buttons[key] = button

        ttk.Button(sidebar, text="Refresh status", style="Secondary.TButton", command=self.refresh_environment_async).grid(row=21, column=0, sticky="ew", pady=(24, 4))
        ttk.Button(sidebar, text="Launch server", style="Primary.TButton", command=self.start_environment).grid(row=22, column=0, sticky="ew", pady=4)
        ttk.Button(sidebar, text="Stop services", style="Danger.TButton", command=self.stop_environment).grid(row=23, column=0, sticky="ew", pady=4)
        sidebar.grid_rowconfigure(24, weight=1)

        content = ttk.Frame(self.root, style="App.TFrame", padding=(24, 20, 24, 24))
        content.grid(row=0, column=1, sticky="nsew")
        content.grid_columnconfigure(0, weight=1)
        content.grid_rowconfigure(1, weight=1)

        header = ttk.Frame(content, style="App.TFrame")
        header.grid(row=0, column=0, sticky="ew", pady=(0, 18))
        header.grid_columnconfigure(0, weight=1)

        title_block = ttk.Frame(header, style="App.TFrame")
        title_block.grid(row=0, column=0, sticky="w")
        ttk.Label(title_block, text=APP_TITLE, style="Title.TLabel").grid(row=0, column=0, sticky="w")
        ttk.Label(title_block, text="Install, configure, launch, monitor, and maintain the complete Shittim server environment from one place.", style="Subtitle.TLabel").grid(row=1, column=0, sticky="w", pady=(4, 0))

        self.header_runtime_label = tk.Label(header, text="Stopped", bg=self.colors["surface_soft"], fg=self.colors["text"], font=("Segoe UI Semibold", 10), padx=14, pady=8)
        self.header_runtime_label.grid(row=0, column=1, rowspan=2, sticky="e")

        self.page_container = ttk.Frame(content, style="App.TFrame")
        self.page_container.grid(row=1, column=0, sticky="nsew")
        self.page_container.grid_columnconfigure(0, weight=1)
        self.page_container.grid_rowconfigure(0, weight=1)

        self.views["overview"] = self.build_overview_view(self.page_container)
        self.views["operations"] = self.build_operations_view(self.page_container)
        self.views["accounts"] = self.build_accounts_view(self.page_container)
        self.views["gacha"] = self.build_gacha_view(self.page_container)
        self.views["mail"] = self.build_mail_view(self.page_container)
        self.views["items"] = self.build_items_view(self.page_container)
        self.views["characters"] = self.build_characters_view(self.page_container)
        self.views["management"] = self.build_management_view(self.page_container)

        for frame in self.views.values():
            frame.grid(row=0, column=0, sticky="nsew")

    def show_view(self, key: str):
        self.current_view = key
        for view_key, frame in self.views.items():
            frame.grid_remove()
            self.nav_buttons[view_key].configure(style="Nav.TButton")
        self.views[key].grid()
        self.nav_buttons[key].configure(style="NavSelected.TButton")

    def build_page_shell(self, parent, title, description):
        outer = ttk.Frame(parent, style="App.TFrame")
        outer.grid_columnconfigure(0, weight=1)
        outer.grid_rowconfigure(1, weight=1)

        hero = ttk.Frame(outer, style="Surface.TFrame", padding=(18, 16, 18, 16))
        hero.grid(row=0, column=0, sticky="ew", pady=(0, 16))
        hero.grid_columnconfigure(0, weight=1)
        ttk.Label(hero, text=title, style="SectionTitle.TLabel").grid(row=0, column=0, sticky="w")
        ttk.Label(hero, text=description, style="Muted.TLabel").grid(row=1, column=0, sticky="w", pady=(4, 0))

        body = ttk.Frame(outer, style="App.TFrame")
        body.grid(row=1, column=0, sticky="nsew")
        body.grid_columnconfigure(0, weight=1)
        body.grid_rowconfigure(0, weight=1)
        return outer, body

    def build_overview_view(self, parent):
        page, body = self.build_page_shell(parent, "Operational overview", "Central readiness and service status for the local environment.")
        body.grid_columnconfigure(0, weight=3)
        body.grid_columnconfigure(1, weight=2)
        body.grid_rowconfigure(1, weight=1)

        cards = ttk.Frame(body, style="App.TFrame")
        cards.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 16))
        for index in range(5):
            cards.grid_columnconfigure(index, weight=1)

        self.summary_cards = {}
        card_specs = [
            ("readiness", "Readiness", "Evaluating"),
            ("server", "Game Server", "Stopped"),
            ("mitm", "MITM Proxy", "Stopped"),
            ("accounts", "Accounts", "0 loaded"),
            ("updates", "Updates", "Checking"),
        ]
        for idx, (key, title, value) in enumerate(card_specs):
            card = ttk.Frame(cards, style="Surface.TFrame", padding=(18, 16, 18, 16))
            card.grid(row=0, column=idx, sticky="nsew", padx=(0 if idx == 0 else 8, 0))
            ttk.Label(card, text=title, style="Muted.TLabel").grid(row=0, column=0, sticky="w")
            value_label = ttk.Label(card, text=value, style="Value.TLabel")
            value_label.grid(row=1, column=0, sticky="w", pady=(8, 6))
            detail_label = ttk.Label(card, text="", style="Muted.TLabel")
            detail_label.grid(row=2, column=0, sticky="w")
            self.summary_cards[key] = (value_label, detail_label)

        readiness_card = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        readiness_card.grid(row=1, column=0, sticky="nsew", padx=(0, 10))
        readiness_card.grid_columnconfigure(0, weight=1)
        readiness_card.grid_rowconfigure(1, weight=1)
        header_row = ttk.Frame(readiness_card, style="Surface.TFrame")
        header_row.grid(row=0, column=0, sticky="ew")
        header_row.grid_columnconfigure(0, weight=1)
        ttk.Label(header_row, text="Readiness details", style="CardTitle.TLabel").grid(row=0, column=0, sticky="w")
        self.readiness_state_label = tk.Label(header_row, text="Checking", bg=self.colors["surface"], fg=self.colors["muted"], font=("Segoe UI Semibold", 10))
        self.readiness_state_label.grid(row=0, column=1, sticky="e")
        self.readiness_tree = ttk.Treeview(readiness_card, columns=("component", "status", "detail"), show="headings", height=10)
        self.readiness_tree.heading("component", text="Component")
        self.readiness_tree.heading("status", text="Status")
        self.readiness_tree.heading("detail", text="Detail")
        self.readiness_tree.column("component", width=170, anchor="w")
        self.readiness_tree.column("status", width=120, anchor="w")
        self.readiness_tree.column("detail", width=480, anchor="w")
        self.readiness_tree.grid(row=1, column=0, sticky="nsew", pady=(12, 0))

        next_steps_card = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        next_steps_card.grid(row=1, column=1, sticky="nsew")
        next_steps_card.grid_columnconfigure(0, weight=1)
        next_steps_card.grid_rowconfigure(1, weight=1)
        ttk.Label(next_steps_card, text="Next actions", style="CardTitle.TLabel").grid(row=0, column=0, sticky="w")
        self.next_actions_text = scrolledtext.ScrolledText(next_steps_card, wrap=tk.WORD, height=16, bg="#0f141e", fg=self.colors["text"], insertbackground=self.colors["text"], relief="flat", font=("Segoe UI", 10))
        self.next_actions_text.grid(row=1, column=0, sticky="nsew", pady=(12, 0))
        self.next_actions_text.configure(state="disabled")
        return page

    def build_operations_view(self, parent):
        page, body = self.build_page_shell(parent, "Launch and monitor", "Launch the game server and MITM proxy together, then track their live status.")
        body.grid_columnconfigure(0, weight=3)
        body.grid_columnconfigure(1, weight=2)
        body.grid_rowconfigure(1, weight=1)

        launch_bar = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        launch_bar.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 16))
        ttk.Label(launch_bar, text="Server controls", style="CardTitle.TLabel").pack(anchor="w")
        ttk.Label(launch_bar, text="Launch server starts the local game server and the MITM proxy required for traffic redirection. Stop services closes both.", style="Muted.TLabel").pack(anchor="w", pady=(4, 14))

        actions = ttk.Frame(launch_bar, style="Surface.TFrame")
        actions.pack(fill="x")
        ttk.Button(actions, text="Launch server", style="Primary.TButton", command=self.start_environment).pack(side="left")
        ttk.Button(actions, text="Stop services", style="Danger.TButton", command=self.stop_environment).pack(side="left", padx=10)
        ttk.Button(actions, text="Refresh status", style="Secondary.TButton", command=self.refresh_environment_async).pack(side="left")

        self.runtime_status_label = ttk.Label(launch_bar, text="Stopped", style="Body.TLabel")
        self.runtime_status_label.pack(anchor="w", pady=(14, 0))

        services_card = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        services_card.grid(row=1, column=0, sticky="nsew", padx=(0, 10))
        services_card.grid_columnconfigure(0, weight=1)
        services_card.grid_rowconfigure(1, weight=1)
        ttk.Label(services_card, text="Service state", style="CardTitle.TLabel").grid(row=0, column=0, sticky="w")
        self.service_tree = ttk.Treeview(services_card, columns=("service", "state", "detail"), show="headings", height=8)
        self.service_tree.heading("service", text="Service")
        self.service_tree.heading("state", text="State")
        self.service_tree.heading("detail", text="Detail")
        self.service_tree.column("service", width=160)
        self.service_tree.column("state", width=120)
        self.service_tree.column("detail", width=500)
        self.service_tree.grid(row=1, column=0, sticky="nsew", pady=(12, 0))

        logs_card = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        logs_card.grid(row=1, column=1, sticky="nsew")
        logs_card.grid_columnconfigure(0, weight=1)
        logs_card.grid_rowconfigure(1, weight=1)
        ttk.Label(logs_card, text="Live activity", style="CardTitle.TLabel").grid(row=0, column=0, sticky="w")
        self.log_text = scrolledtext.ScrolledText(logs_card, wrap=tk.WORD, height=18, bg="#0f141e", fg=self.colors["text"], insertbackground=self.colors["text"], relief="flat", font=("Consolas", 9))
        self.log_text.grid(row=1, column=0, sticky="nsew", pady=(12, 0))
        self.log_text.configure(state="disabled")
        return page

    def build_accounts_view(self, parent):
        page, body = self.build_page_shell(parent, "Account workspace", "Edit account state directly from the game database.")
        body.grid_columnconfigure(0, weight=1)
        body.grid_rowconfigure(1, weight=1)

        top = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        top.grid(row=0, column=0, sticky="ew", pady=(0, 16))
        ttk.Label(top, text="Target account", style="CardTitle.TLabel").grid(row=0, column=0, sticky="w")
        ttk.Label(top, text="The database must be present for account tooling to become active.", style="Muted.TLabel").grid(row=1, column=0, sticky="w", pady=(4, 12))
        ttk.Label(top, text="Account", style="Body.TLabel").grid(row=2, column=0, sticky="w")
        self.account_combo = ttk.Combobox(top, width=48, state="readonly")
        self.account_combo.grid(row=2, column=1, sticky="w", padx=(12, 12))
        self.account_combo.bind("<<ComboboxSelected>>", self.load_account_data)
        ttk.Button(top, text="Refresh accounts", style="Secondary.TButton", command=self.load_accounts).grid(row=2, column=2, sticky="w")

        form = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        form.grid(row=1, column=0, sticky="nsew")
        for col in range(4):
            form.grid_columnconfigure(col, weight=1)

        fields = [
            ("Nickname", "nickname_entry"),
            ("Level", "level_entry"),
            ("Experience", "exp_entry"),
            ("Comment", "comment_entry"),
            ("Free gems", "gem_free_entry"),
            ("Paid gems", "gem_paid_entry"),
            ("Gold", "gold_entry"),
            ("AP", "ap_entry"),
            ("Arena ticket", "arena_entry"),
            ("Raid ticket", "raid_entry"),
        ]
        for idx, (label, attr) in enumerate(fields):
            row = idx // 2
            col = (idx % 2) * 2
            ttk.Label(form, text=label, style="Body.TLabel").grid(row=row, column=col, sticky="w", padx=(0, 8), pady=8)
            entry = ttk.Entry(form, width=32)
            entry.grid(row=row, column=col + 1, sticky="ew", pady=8)
            setattr(self, attr, entry)

        actions = ttk.Frame(form, style="Surface.TFrame")
        actions.grid(row=6, column=0, columnspan=4, sticky="w", pady=(18, 0))
        ttk.Button(actions, text="Save changes", style="Primary.TButton", command=self.save_account_data).pack(side="left")
        ttk.Button(actions, text="Max everything", style="Secondary.TButton", command=self.max_account).pack(side="left", padx=10)
        return page

    def build_gacha_view(self, parent):
        page, body = self.build_page_shell(parent, "Gacha controls", "Adjust test banner behavior and featured outcomes without leaving the console.")
        body.grid_columnconfigure(0, weight=1)
        body.grid_columnconfigure(1, weight=1)
        body.grid_rowconfigure(1, weight=1)

        rates = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        rates.grid(row=0, column=0, sticky="nsew", padx=(0, 10), pady=(0, 16))
        ttk.Label(rates, text="Rate override", style="CardTitle.TLabel").grid(row=0, column=0, columnspan=2, sticky="w")
        ttk.Label(rates, text="Useful for local testing. Values must total 100%.", style="Muted.TLabel").grid(row=1, column=0, columnspan=2, sticky="w", pady=(4, 14))
        self.ssr_rate_entry = self._grid_labeled_entry(rates, "SSR rate (%)", 2, "3.0")
        self.sr_rate_entry = self._grid_labeled_entry(rates, "SR rate (%)", 3, "18.5")
        self.r_rate_entry = self._grid_labeled_entry(rates, "R rate (%)", 4, "78.5")
        ttk.Button(rates, text="Apply custom rates", style="Primary.TButton", command=self.apply_custom_rates).grid(row=5, column=0, sticky="w", pady=(16, 0))
        ttk.Button(rates, text="Reset defaults", style="Secondary.TButton", command=self.reset_rates).grid(row=5, column=1, sticky="w", pady=(16, 0), padx=(8, 0))

        guaranteed = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        guaranteed.grid(row=0, column=1, sticky="nsew", pady=(0, 16))
        ttk.Label(guaranteed, text="Guaranteed character", style="CardTitle.TLabel").grid(row=0, column=0, columnspan=3, sticky="w")
        ttk.Label(guaranteed, text="Set or clear the locally configured featured result.", style="Muted.TLabel").grid(row=1, column=0, columnspan=3, sticky="w", pady=(4, 14))
        ttk.Label(guaranteed, text="Character ID", style="Body.TLabel").grid(row=2, column=0, sticky="w")
        self.guarantee_char_entry = ttk.Entry(guaranteed, width=18)
        self.guarantee_char_entry.grid(row=2, column=1, sticky="w")
        ttk.Button(guaranteed, text="Browse", style="Secondary.TButton", command=self.browse_characters).grid(row=2, column=2, sticky="w", padx=(8, 0))
        ttk.Button(guaranteed, text="Set guarantee", style="Primary.TButton", command=self.set_guaranteed_char).grid(row=3, column=0, sticky="w", pady=(16, 0))
        ttk.Button(guaranteed, text="Clear", style="Secondary.TButton", command=self.clear_guaranteed_char).grid(row=3, column=1, sticky="w", pady=(16, 0), padx=(8, 0))

        banners = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        banners.grid(row=1, column=0, columnspan=2, sticky="nsew")
        banners.grid_columnconfigure(0, weight=1)
        banners.grid_rowconfigure(1, weight=1)
        ttk.Label(banners, text="Banner availability", style="CardTitle.TLabel").grid(row=0, column=0, sticky="w")
        self.banner_listbox = tk.Listbox(banners, bg="#0f141e", fg=self.colors["text"], relief="flat", highlightthickness=0, selectbackground=self.colors["accent"], selectforeground="#ffffff", font=("Segoe UI", 10), height=12)
        self.banner_listbox.grid(row=1, column=0, sticky="nsew", pady=(12, 12))
        buttons = ttk.Frame(banners, style="Surface.TFrame")
        buttons.grid(row=2, column=0, sticky="w")
        ttk.Button(buttons, text="Refresh banners", style="Secondary.TButton", command=self.refresh_banners).pack(side="left")
        ttk.Button(buttons, text="Enable selected", style="Primary.TButton", command=self.enable_banner).pack(side="left", padx=8)
        ttk.Button(buttons, text="Disable selected", style="Secondary.TButton", command=self.disable_banner).pack(side="left")
        self.refresh_banners()
        return page

    def build_mail_view(self, parent):
        page, body = self.build_page_shell(parent, "Mail composer", "Create targeted player mail with rewards and expiry rules.")
        body.grid_columnconfigure(0, weight=1)
        body.grid_columnconfigure(1, weight=1)
        body.grid_rowconfigure(1, weight=1)

        top = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        top.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 16))
        ttk.Label(top, text="Target account", style="Body.TLabel").grid(row=0, column=0, sticky="w")
        self.mail_account_combo = ttk.Combobox(top, width=48, state="readonly")
        self.mail_account_combo.grid(row=0, column=1, sticky="w", padx=(12, 0))

        details = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        details.grid(row=1, column=0, sticky="nsew", padx=(0, 10))
        self.mail_sender_entry = self._grid_labeled_entry(details, "Sender name", 0, "System", width=44)
        self.mail_subject_entry = self._grid_labeled_entry(details, "Subject", 1, "", width=44)
        ttk.Label(details, text="Message", style="Body.TLabel").grid(row=2, column=0, sticky="nw", pady=8)
        self.mail_message_text = scrolledtext.ScrolledText(details, width=44, height=11, bg="#0f141e", fg=self.colors["text"], insertbackground=self.colors["text"], relief="flat", font=("Segoe UI", 10))
        self.mail_message_text.grid(row=2, column=1, sticky="nsew", pady=8)
        self.mail_expire_entry = self._grid_labeled_entry(details, "Expire days", 3, "30", width=44)
        details.grid_columnconfigure(1, weight=1)

        rewards = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        rewards.grid(row=1, column=1, sticky="nsew")
        ttk.Label(rewards, text="Rewards", style="CardTitle.TLabel").grid(row=0, column=0, columnspan=2, sticky="w")
        ttk.Label(rewards, text="Stage rewards before sending the mail.", style="Muted.TLabel").grid(row=1, column=0, columnspan=2, sticky="w", pady=(4, 14))
        ttk.Label(rewards, text="Type", style="Body.TLabel").grid(row=2, column=0, sticky="w")
        self.mail_item_type = ttk.Combobox(rewards, values=["Currency", "Item", "Character", "Equipment"], state="readonly", width=18)
        self.mail_item_type.grid(row=2, column=1, sticky="w")
        self.mail_item_type.set("Currency")
        self.mail_item_id_entry = self._grid_labeled_entry(rewards, "Item ID", 3, "", width=22)
        self.mail_item_amount_entry = self._grid_labeled_entry(rewards, "Amount", 4, "1", width=22)
        ttk.Button(rewards, text="Add reward", style="Primary.TButton", command=self.add_mail_item).grid(row=5, column=1, sticky="w", pady=(12, 12))
        self.mail_items_list = tk.Listbox(rewards, bg="#0f141e", fg=self.colors["text"], relief="flat", highlightthickness=0, selectbackground=self.colors["accent"], selectforeground="#ffffff", font=("Segoe UI", 10), height=9)
        self.mail_items_list.grid(row=6, column=0, columnspan=2, sticky="nsew")
        rewards.grid_rowconfigure(6, weight=1)
        action_row = ttk.Frame(rewards, style="Surface.TFrame")
        action_row.grid(row=7, column=0, columnspan=2, sticky="w", pady=(12, 0))
        ttk.Button(action_row, text="Remove selected", style="Secondary.TButton", command=self.remove_mail_item).pack(side="left")
        ttk.Button(action_row, text="Send mail", style="Primary.TButton", command=self.send_mail).pack(side="left", padx=8)
        ttk.Button(action_row, text="Clear form", style="Secondary.TButton", command=self.clear_mail_form).pack(side="left")
        return page

    def build_items_view(self, parent):
        page, body = self.build_page_shell(parent, "Item spawner", "Issue items directly to an account and inspect current holdings.")
        body.grid_columnconfigure(0, weight=1)
        body.grid_columnconfigure(1, weight=1)
        body.grid_rowconfigure(1, weight=1)

        top = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        top.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 16))
        ttk.Label(top, text="Target account", style="Body.TLabel").grid(row=0, column=0, sticky="w")
        self.item_account_combo = ttk.Combobox(top, width=48, state="readonly")
        self.item_account_combo.grid(row=0, column=1, sticky="w", padx=(12, 0))
        self.item_account_combo.bind("<<ComboboxSelected>>", lambda _e: self.refresh_items())

        spawn = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        spawn.grid(row=1, column=0, sticky="nsew", padx=(0, 10))
        self.spawn_item_id_entry = self._grid_labeled_entry(spawn, "Item ID", 0, "", width=28)
        self.spawn_item_amount_entry = self._grid_labeled_entry(spawn, "Amount", 1, "1", width=28)
        self.spawn_item_stack_entry = self._grid_labeled_entry(spawn, "Stack count", 2, "1", width=28)
        ttk.Button(spawn, text="Spawn item", style="Primary.TButton", command=self.spawn_item).grid(row=3, column=1, sticky="w", pady=(14, 0))

        inventory = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        inventory.grid(row=1, column=1, sticky="nsew")
        inventory.grid_columnconfigure(0, weight=1)
        inventory.grid_rowconfigure(1, weight=1)
        ttk.Label(inventory, text="Current items", style="CardTitle.TLabel").grid(row=0, column=0, sticky="w")
        self.items_listbox = tk.Listbox(inventory, bg="#0f141e", fg=self.colors["text"], relief="flat", highlightthickness=0, selectbackground=self.colors["accent"], selectforeground="#ffffff", font=("Segoe UI", 10), height=16)
        self.items_listbox.grid(row=1, column=0, sticky="nsew", pady=(12, 12))
        ttk.Button(inventory, text="Refresh items", style="Secondary.TButton", command=self.refresh_items).grid(row=2, column=0, sticky="w")
        return page

    def build_characters_view(self, parent):
        page, body = self.build_page_shell(parent, "Character spawner", "Create character records for an account and inspect the current roster.")
        body.grid_columnconfigure(0, weight=1)
        body.grid_columnconfigure(1, weight=1)
        body.grid_rowconfigure(1, weight=1)

        top = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        top.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 16))
        ttk.Label(top, text="Target account", style="Body.TLabel").grid(row=0, column=0, sticky="w")
        self.char_account_combo = ttk.Combobox(top, width=48, state="readonly")
        self.char_account_combo.grid(row=0, column=1, sticky="w", padx=(12, 0))
        self.char_account_combo.bind("<<ComboboxSelected>>", lambda _e: self.refresh_characters())

        spawn = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        spawn.grid(row=1, column=0, sticky="nsew", padx=(0, 10))
        self.spawn_char_id_entry = self._grid_labeled_entry(spawn, "Character ID", 0, "", width=28)
        ttk.Button(spawn, text="Browse", style="Secondary.TButton", command=self.browse_spawn_character).grid(row=0, column=2, sticky="w", padx=(8, 0), pady=8)
        self.spawn_char_star_entry = self._grid_labeled_entry(spawn, "Star grade", 1, "3", width=28)
        self.spawn_char_level_entry = self._grid_labeled_entry(spawn, "Level", 2, "1", width=28)
        ttk.Button(spawn, text="Spawn character", style="Primary.TButton", command=self.spawn_character).grid(row=3, column=1, sticky="w", pady=(14, 0))

        roster = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        roster.grid(row=1, column=1, sticky="nsew")
        roster.grid_columnconfigure(0, weight=1)
        roster.grid_rowconfigure(1, weight=1)
        ttk.Label(roster, text="Current characters", style="CardTitle.TLabel").grid(row=0, column=0, sticky="w")
        self.characters_listbox = tk.Listbox(roster, bg="#0f141e", fg=self.colors["text"], relief="flat", highlightthickness=0, selectbackground=self.colors["accent"], selectforeground="#ffffff", font=("Segoe UI", 10), height=16)
        self.characters_listbox.grid(row=1, column=0, sticky="nsew", pady=(12, 12))
        ttk.Button(roster, text="Refresh characters", style="Secondary.TButton", command=self.refresh_characters).grid(row=2, column=0, sticky="w")
        return page

    def build_management_view(self, parent):
        page, body = self.build_page_shell(parent, "Maintenance and packaging", "Repair local prerequisites, manage certificates, and build a distributable console executable.")
        body.grid_columnconfigure(0, weight=1)
        body.grid_columnconfigure(1, weight=1)
        body.grid_rowconfigure(1, weight=1)

        dependency = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        dependency.grid(row=0, column=0, sticky="nsew", padx=(0, 10), pady=(0, 16))
        ttk.Label(dependency, text="Dependencies", style="CardTitle.TLabel").grid(row=0, column=0, sticky="w")
        ttk.Label(dependency, text="State-aware actions for Python tooling, mitmproxy, and project installation status.", style="Muted.TLabel").grid(row=1, column=0, sticky="w", pady=(4, 14))
        dep_actions = ttk.Frame(dependency, style="Surface.TFrame")
        dep_actions.grid(row=2, column=0, sticky="w")
        ttk.Button(dep_actions, text="Refresh status", style="Secondary.TButton", command=self.refresh_environment_async).pack(side="left")
        ttk.Button(dep_actions, text="Install project files", style="Primary.TButton", command=self.install_project_prompt).pack(side="left", padx=8)
        ttk.Button(dep_actions, text="Repair mitmproxy", style="Primary.TButton", command=self.repair_mitmproxy).pack(side="left", padx=8)
        ttk.Button(dep_actions, text="Open .NET download", style="Secondary.TButton", command=lambda: webbrowser.open("https://dotnet.microsoft.com/download")).pack(side="left")
        ttk.Button(dep_actions, text="Open mitmproxy docs", style="Secondary.TButton", command=lambda: webbrowser.open("https://mitmproxy.org" )).pack(side="left", padx=8)

        update_card = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        update_card.grid(row=0, column=1, sticky="nsew", pady=(0, 16))
        ttk.Label(update_card, text="Software updates", style="CardTitle.TLabel").grid(row=0, column=0, sticky="w")
        ttk.Label(update_card, text="Check for new releases and install updates from GitHub.", style="Muted.TLabel").grid(row=1, column=0, sticky="w", pady=(4, 14))
        self.update_status_label = ttk.Label(update_card, text="Not checked yet", style="Body.TLabel")
        self.update_status_label.grid(row=2, column=0, sticky="w", pady=(0, 10))
        update_actions = ttk.Frame(update_card, style="Surface.TFrame")
        update_actions.grid(row=3, column=0, sticky="w")
        ttk.Button(update_actions, text="Check for updates", style="Secondary.TButton", command=lambda: self.check_for_updates_async(initial=False)).pack(side="left")
        ttk.Button(update_actions, text="Install latest update", style="Primary.TButton", command=self.install_latest_update).pack(side="left", padx=8)

        certs = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        certs.grid(row=2, column=0, columnspan=2, sticky="nsew", pady=(16, 0))
        ttk.Label(certs, text="Certificate management", style="CardTitle.TLabel").grid(row=0, column=0, sticky="w")
        ttk.Label(certs, text="Install, remove, or repair the local mitmproxy trust configuration.", style="Muted.TLabel").grid(row=1, column=0, sticky="w", pady=(4, 14))
        cert_actions = ttk.Frame(certs, style="Surface.TFrame")
        cert_actions.grid(row=2, column=0, sticky="w")
        ttk.Button(cert_actions, text="Install certificate", style="Primary.TButton", command=self.install_certificate).pack(side="left")
        ttk.Button(cert_actions, text="Uninstall certificate", style="Secondary.TButton", command=self.uninstall_certificate).pack(side="left", padx=8)
        ttk.Button(cert_actions, text="Repair certificate", style="Secondary.TButton", command=self.repair_certificate).pack(side="left")

        settings = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        settings.grid(row=1, column=0, sticky="nsew", padx=(0, 10))
        settings.grid_columnconfigure(1, weight=1)
        ttk.Label(settings, text="Settings", style="CardTitle.TLabel").grid(row=0, column=0, columnspan=3, sticky="w")
        self.install_dir_entry = self._grid_labeled_entry(settings, "Install directory", 1, str(self.settings.get("install_dir", "").strip() or self.appdata_dir), width=34)
        ttk.Button(settings, text="Browse", style="Secondary.TButton", command=self.browse_install_dir).grid(row=1, column=2, sticky="w", padx=(8, 0), pady=8)
        self.logs_dir_entry = self._grid_labeled_entry(settings, "Log directory", 2, str(self.settings.get("logs_dir", "").strip() or self.logs_dir), width=34)
        ttk.Button(settings, text="Browse", style="Secondary.TButton", command=self.browse_logs_dir).grid(row=2, column=2, sticky="w", padx=(8, 0), pady=8)
        self.server_port_entry = self._grid_labeled_entry(settings, "Server port", 3, str(self.settings.get("server_port", 5000)), width=34)
        self.mitm_listen_port_entry = self._grid_labeled_entry(settings, "MITM listen port", 4, str(self.settings.get("mitm_listen_port", 8080)), width=34)
        self.mitm_web_port_entry = self._grid_labeled_entry(settings, "MITM web port", 5, str(self.settings.get("mitm_web_port", 8081)), width=34)
        self.mitm_mode_entry = self._grid_labeled_entry(settings, "MITM mode", 6, self.settings.get("mitm_mode", "local:BlueArchive.exe"), width=34)
        ttk.Button(settings, text="Save settings", style="Primary.TButton", command=self.save_management_settings).grid(row=7, column=1, sticky="w", pady=(14, 0))

        packaging = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        packaging.grid(row=1, column=1, sticky="nsew")
        ttk.Label(packaging, text="Packaging", style="CardTitle.TLabel").grid(row=0, column=0, sticky="w")
        ttk.Label(packaging, text="Build a distributable executable with this console as the primary entry point.", style="Muted.TLabel").grid(row=1, column=0, sticky="w", pady=(4, 14))
        ttk.Button(packaging, text="Build executable", style="Primary.TButton", command=self.build_console).grid(row=2, column=0, sticky="w")
        ttk.Button(packaging, text="Open dist folder", style="Secondary.TButton", command=self.open_dist_folder).grid(row=3, column=0, sticky="w", pady=(8, 0))

        logs = ttk.Frame(body, style="Surface.TFrame", padding=(18, 16, 18, 16))
        logs.grid(row=3, column=0, columnspan=2, sticky="ew", pady=(16, 0))
        ttk.Label(logs, text="Logs", style="CardTitle.TLabel").grid(row=0, column=0, sticky="w")
        ttk.Label(logs, text="Session logs are stored in your user directory and replaced each time the console starts.", style="Muted.TLabel").grid(row=1, column=0, sticky="w", pady=(4, 14))
        log_actions = ttk.Frame(logs, style="Surface.TFrame")
        log_actions.grid(row=2, column=0, sticky="w")
        ttk.Button(log_actions, text="Open logs folder", style="Secondary.TButton", command=self.open_logs_folder).pack(side="left")
        ttk.Button(log_actions, text="Open current log", style="Secondary.TButton", command=self.open_current_log).pack(side="left", padx=8)
        ttk.Button(log_actions, text="Export logs", style="Primary.TButton", command=self.export_logs).pack(side="left")
        return page

    def _grid_labeled_entry(self, parent, label, row, value, width=20):
        ttk.Label(parent, text=label, style="Body.TLabel").grid(row=row, column=0, sticky="w", pady=8)
        entry = ttk.Entry(parent, width=width)
        entry.grid(row=row, column=1, sticky="w", pady=8)
        if value:
            entry.insert(0, value)
        return entry

    def append_log(self, source: str, message: str):
        stamp = datetime.now().strftime("%H:%M:%S")
        line = f"[{stamp}] {source}: {message}"
        self.log_queue.put((source, line))
        self.write_log_line(line)

    def _prepare_log_file(self):
        header = textwrap.dedent(
            f"""
            Shittim Server Session Log
            Started: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}
            Log File: {self.current_log_path}
            """
        ).strip()
        self.current_log_path.write_text(header + "\n\n", encoding="utf-8")

    def write_log_line(self, line: str):
        with self.current_log_path.open("a", encoding="utf-8") as log_file:
            log_file.write(line + "\n")

    def poll_logs(self):
        try:
            while True:
                _source, message = self.log_queue.get_nowait()
                self.log_text.configure(state="normal")
                self.log_text.insert(tk.END, message + "\n")
                self.log_text.see(tk.END)
                self.log_text.configure(state="disabled")
        except queue.Empty:
            pass
        self.root.after(250, self.poll_logs)

    def refresh_environment_async(self, initial=False):
        if self.refresh_in_progress:
            return
        self.refresh_in_progress = True
        self.append_log("Console", "Refreshing environment checks")

        def worker():
            data = self.collect_environment_status()
            self.root.after(0, lambda: self.apply_environment_status(data, initial))

        threading.Thread(target=worker, daemon=True).start()

    def collect_environment_status(self):
        self.refresh_runtime_paths()
        status = {
            "dotnet": self.check_dotnet(),
            "python": self.check_python(),
            "mitmweb": self.check_mitmweb(),
            "certificate_file": self.check_certificate_file(),
            "certificate_installed": self.check_certificate_installed(),
            "database": self.check_database(),
            "server_project": self.check_server_project(),
            "mitm_script": self.check_mitm_script(),
        }
        return status

    def apply_environment_status(self, status, initial=False):
        self.refresh_in_progress = False
        self.environment = status
        self.update_readiness_views()
        self.maybe_prompt_install()
        if initial:
            self.append_log("Console", "Initial system inspection completed")

    def update_readiness_views(self):
        for item in self.readiness_tree.get_children():
            self.readiness_tree.delete(item)
        labels = {
            "dotnet": ".NET SDK",
            "python": "Python runtime",
            "mitmweb": "mitmproxy tools",
            "certificate_file": "Certificate files",
            "certificate_installed": "Trusted certificate",
            "database": "Database",
            "server_project": "Server project",
            "mitm_script": "Redirect script",
        }
        for key, value in self.environment.items():
            self.readiness_tree.insert("", tk.END, values=(labels[key], self.format_status(value["status"]), value["detail"]))

        ready = self.is_environment_ready()
        readiness_text = "Ready" if ready else "Action required"
        readiness_detail = "All critical checks passed" if ready else "Resolve missing prerequisites before launch"
        self.summary_cards["readiness"][0].configure(text=readiness_text)
        self.summary_cards["readiness"][1].configure(text=readiness_detail)
        self.summary_cards["server"][0].configure(text=self.format_status(self.runtime["server"].state))
        self.summary_cards["server"][1].configure(text=self.runtime["server"].detail)
        self.summary_cards["mitm"][0].configure(text=self.format_status(self.runtime["mitm"].state))
        self.summary_cards["mitm"][1].configure(text=self.runtime["mitm"].detail)
        self.summary_cards["accounts"][0].configure(text=f"{len(self.accounts_cache)} loaded")
        self.summary_cards["accounts"][1].configure(text="Database workspace available" if self.database_path.exists() else "Database not found")

        self.readiness_state_label.configure(text="Ready" if ready else "Not ready", fg=self.colors["success"] if ready else self.colors["warning"])

        next_actions = []
        if self.environment["server_project"]["status"] != "ready" or self.environment["mitm_script"]["status"] != "ready":
            next_actions.append(f"• Install the project files into {self.install_dir} to restore the game server and redirect script.")
        if self.environment["dotnet"]["status"] != "ready":
            next_actions.append("• Install .NET SDK 6 or newer and verify the dotnet command is available.")
        if self.environment["mitmweb"]["status"] != "ready":
            next_actions.append("• Install or repair mitmproxy so the mitmweb command is available in PATH.")
        if self.environment["certificate_installed"]["status"] != "ready":
            next_actions.append("• Install the mitmproxy certificate into the Local Machine trusted root store.")
        if self.environment["database"]["status"] != "ready":
            next_actions.append("• Start the server once or restore the database file before using account management tools.")
        if not next_actions:
            next_actions.append("• The environment is ready. Use Launch server to start the game server and MITM proxy together.")

        self.next_actions_text.configure(state="normal")
        self.next_actions_text.delete("1.0", tk.END)
        self.next_actions_text.insert("1.0", "\n\n".join(next_actions))
        self.next_actions_text.configure(state="disabled")
        self.update_runtime_labels()

    def format_status(self, status):
        return self.status_palette.get(status, self.status_palette["unknown"])["label"]

    def check_dotnet(self):
        if self.has_published_server():
            return {"status": "ready", "detail": "Prepublished server runtime available; dotnet SDK not required for launch"}
        dotnet_path = shutil.which("dotnet")
        if not dotnet_path:
            return {"status": "missing", "detail": ".NET SDK 6+ not found in PATH"}
        try:
            result = subprocess.run(["dotnet", "--list-sdks"], capture_output=True, text=True, timeout=8)
            if result.returncode != 0:
                return {"status": "warning", "detail": "dotnet found but SDK listing failed"}
            versions = [line.strip() for line in result.stdout.splitlines() if line.strip()]
            major_versions = [int(line.split(".")[0]) for line in versions if line[0].isdigit()]
            if any(version >= 6 for version in major_versions):
                return {"status": "ready", "detail": f"SDK detected ({versions[0]})"}
            return {"status": "missing", "detail": "No .NET SDK 6+ installation detected"}
        except Exception as exc:
            return {"status": "warning", "detail": f"Unable to inspect dotnet: {exc}"}

    def check_python(self):
        executable = sys.executable or shutil.which("python")
        if not executable:
            return {"status": "missing", "detail": "Python runtime not found"}
        return {"status": "ready", "detail": f"Using {Path(executable).name}"}

    def check_mitmweb(self):
        mitm_path = shutil.which("mitmweb")
        if not mitm_path:
            return {"status": "missing", "detail": "mitmweb not found in PATH"}
        try:
            result = subprocess.run(["mitmweb", "--version"], capture_output=True, text=True, timeout=8)
            detail = result.stdout.strip() or result.stderr.strip() or "mitmweb available"
            return {"status": "ready" if result.returncode == 0 else "warning", "detail": detail}
        except Exception as exc:
            return {"status": "warning", "detail": f"mitmweb check failed: {exc}"}

    def get_mitm_certificate_path(self):
        user_profile = os.environ.get("USERPROFILE", str(Path.home()))
        return Path(user_profile) / ".mitmproxy" / "mitmproxy-ca-cert.cer"

    def check_certificate_file(self):
        cert_path = self.get_mitm_certificate_path()
        if cert_path.exists():
            return {"status": "ready", "detail": f"Found {cert_path.name}"}
        return {"status": "missing", "detail": "mitmproxy certificate file has not been generated yet"}

    def check_certificate_installed(self):
        try:
            result = subprocess.run(["certutil", "-store", "Root"], capture_output=True, text=True, timeout=12)
            output = f"{result.stdout}\n{result.stderr}".lower()
            if "mitmproxy" in output:
                return {"status": "ready", "detail": "mitmproxy certificate trusted in Local Machine Root"}
            return {"status": "missing", "detail": "mitmproxy certificate is not trusted in Local Machine Root"}
        except Exception as exc:
            return {"status": "warning", "detail": f"Certificate check failed: {exc}"}

    def check_database(self):
        self.refresh_runtime_paths()
        if self.database_path.exists():
            return {"status": "ready", "detail": f"Found {self.database_path.name}"}
        return {"status": "warning", "detail": "Database not found yet; account tools will stay unavailable until created"}

    def check_server_project(self):
        if self.has_published_server():
            return {"status": "ready", "detail": f"Published server ready in {self.server_runtime_dir}"}
        if self.server_project.exists():
            return {"status": "ready", "detail": f"Source server installed in {self.server_dir}"}
        return {"status": "missing", "detail": f"Server files are not installed in {self.install_dir}"}

    def check_mitm_script(self):
        if self.mitm_script_path.exists():
            return {"status": "ready", "detail": "Redirect script located"}
        return {"status": "missing", "detail": "Redirect script is not installed yet"}

    def project_install_required(self):
        return not self.server_installation_available() or self.environment["mitm_script"]["status"] != "ready"

    def maybe_prompt_install(self):
        if self.install_in_progress:
            return
        if not self.project_install_required():
            self.install_prompt_shown = False
            return
        if self.install_prompt_shown:
            return
        self.install_prompt_shown = True
        should_install = messagebox.askyesno(
            APP_TITLE,
            f"Project files are missing. Install Shittim Server into {self.install_dir}?",
        )
        if should_install:
            self.install_project_async()

    def install_project_prompt(self):
        if self.install_in_progress:
            messagebox.showinfo(APP_TITLE, "Project installation is already running")
            return
        should_install = messagebox.askyesno(
            APP_TITLE,
            f"Download and install the project from https://github.com/Neoexm/Shittim-Server into {self.install_dir}?",
        )
        if should_install:
            self.install_project_async()

    def is_environment_ready(self):
        critical = ["dotnet", "python", "mitmweb", "certificate_installed", "server_project", "mitm_script"]
        return all(self.environment[key]["status"] == "ready" for key in critical)

    def update_runtime_labels(self):
        overall_state = self.compute_overall_runtime_state()
        overall_label = self.format_status(overall_state)
        detail = self.runtime["server"].detail if overall_state != "running" else "Environment running"
        self.runtime_status_label.configure(text=f"Status: {overall_label} — {detail}")
        self.header_runtime_label.configure(text=overall_label, bg=self.status_palette.get(overall_state, self.status_palette["unknown"])["color"])

        for item in self.service_tree.get_children():
            self.service_tree.delete(item)
        for process in self.runtime.values():
            self.service_tree.insert("", tk.END, values=(process.name, self.format_status(process.state), process.detail))

    def compute_overall_runtime_state(self):
        states = [proc.state for proc in self.runtime.values()]
        if any(state == "failed" for state in states):
            return "failed"
        if any(state == "starting" for state in states):
            return "starting"
        if all(state == "running" for state in states):
            return "running"
        return "stopped"

    def load_accounts(self):
        if not self.database_path.exists():
            self.accounts_cache = []
            self._apply_account_values([])
            self.update_readiness_views()
            return
        try:
            with self.get_db_connection() as conn:
                cursor = conn.cursor()
                cursor.execute("SELECT ServerId, Nickname FROM Accounts ORDER BY ServerId")
                self.accounts_cache = cursor.fetchall()
            self._apply_account_values(self.accounts_cache)
            if self.accounts_cache:
                self.load_account_data()
        except Exception as exc:
            messagebox.showerror(APP_TITLE, f"Failed to load accounts: {exc}")

    def _apply_account_values(self, accounts):
        values = [f"{account_id} - {nickname}" for account_id, nickname in accounts]
        for combo_name in ["account_combo", "mail_account_combo", "item_account_combo", "char_account_combo"]:
            combo = getattr(self, combo_name)
            combo["values"] = values
            if values:
                combo.current(0)
            else:
                combo.set("")
        self.update_readiness_views()

    def get_db_connection(self):
        return sqlite3.connect(self.database_path)

    def current_account_id_from(self, combo: ttk.Combobox):
        value = combo.get().strip()
        if not value:
            return None
        return int(value.split(" - ")[0])

    def load_account_data(self, _event=None):
        server_id = self.current_account_id_from(self.account_combo)
        if server_id is None or not self.database_path.exists():
            return
        try:
            with self.get_db_connection() as conn:
                cursor = conn.cursor()
                cursor.execute("SELECT Nickname, Level, Exp, Comment FROM Accounts WHERE ServerId = ?", (server_id,))
                account = cursor.fetchone()
                cursor.execute("SELECT CurrencyDict FROM Currencies WHERE AccountServerId = ?", (server_id,))
                currency_row = cursor.fetchone()

            if account:
                self._set_entry(self.nickname_entry, account[0])
                self._set_entry(self.level_entry, account[1])
                self._set_entry(self.exp_entry, account[2])
                self._set_entry(self.comment_entry, account[3] or "")
            currency_dict = json.loads(currency_row[0]) if currency_row and currency_row[0] else {}
            self._set_entry(self.gem_free_entry, currency_dict.get("GemBonus", 0))
            self._set_entry(self.gem_paid_entry, currency_dict.get("GemPaid", 0))
            self._set_entry(self.gold_entry, currency_dict.get("Gold", 0))
            self._set_entry(self.ap_entry, currency_dict.get("ActionPoint", 0))
            self._set_entry(self.arena_entry, currency_dict.get("ArenaTicket", 0))
            self._set_entry(self.raid_entry, currency_dict.get("RaidTicket", 0))
        except Exception as exc:
            messagebox.showerror(APP_TITLE, f"Failed to load account data: {exc}")

    def _set_entry(self, entry, value):
        entry.delete(0, tk.END)
        entry.insert(0, str(value))

    def save_account_data(self):
        server_id = self.current_account_id_from(self.account_combo)
        if server_id is None:
            messagebox.showerror(APP_TITLE, "No account selected")
            return
        try:
            with self.get_db_connection() as conn:
                cursor = conn.cursor()
                cursor.execute(
                    "UPDATE Accounts SET Nickname = ?, Level = ?, Exp = ?, Comment = ? WHERE ServerId = ?",
                    (self.nickname_entry.get(), int(self.level_entry.get()), int(self.exp_entry.get()), self.comment_entry.get(), server_id),
                )
                cursor.execute("SELECT CurrencyDict FROM Currencies WHERE AccountServerId = ?", (server_id,))
                existing = cursor.fetchone()
                currency_dict = json.loads(existing[0]) if existing and existing[0] else {}
                currency_dict.update({
                    "GemBonus": int(self.gem_free_entry.get()),
                    "GemPaid": int(self.gem_paid_entry.get()),
                    "Gold": int(self.gold_entry.get()),
                    "ActionPoint": int(self.ap_entry.get()),
                    "ArenaTicket": int(self.arena_entry.get()),
                    "RaidTicket": int(self.raid_entry.get()),
                })
                cursor.execute("UPDATE Currencies SET CurrencyDict = ? WHERE AccountServerId = ?", (json.dumps(currency_dict), server_id))
                conn.commit()
            self.append_log("Accounts", f"Saved account changes for {server_id}")
            messagebox.showinfo(APP_TITLE, "Account changes saved")
            self.load_accounts()
        except Exception as exc:
            messagebox.showerror(APP_TITLE, f"Failed to save account data: {exc}")

    def max_account(self):
        presets = {
            self.gem_free_entry: 999999999,
            self.gem_paid_entry: 999999999,
            self.gold_entry: 999999999,
            self.ap_entry: 999999,
            self.arena_entry: 999999,
            self.raid_entry: 999999,
            self.level_entry: 90,
        }
        for entry, value in presets.items():
            self._set_entry(entry, value)
        messagebox.showinfo(APP_TITLE, "Maximum values prepared. Save changes to apply them.")

    def apply_custom_rates(self):
        try:
            ssr = float(self.ssr_rate_entry.get())
            sr = float(self.sr_rate_entry.get())
            r = float(self.r_rate_entry.get())
            if abs((ssr + sr + r) - 100.0) > 0.01:
                raise ValueError("Rates must total 100%")
            data = self.read_json(self.gacha_config_path, {"custom_rates": None, "guaranteed_character": None})
            data["custom_rates"] = {"ssr": ssr, "sr": sr, "r": r}
            self.write_json(self.gacha_config_path, data)
            self.append_log("Gacha", "Saved custom gacha rate overrides")
            messagebox.showinfo(APP_TITLE, "Custom rates saved to gacha_config.json")
        except Exception as exc:
            messagebox.showerror(APP_TITLE, f"Failed to apply custom rates: {exc}")

    def reset_rates(self):
        self._set_entry(self.ssr_rate_entry, "3.0")
        self._set_entry(self.sr_rate_entry, "18.5")
        self._set_entry(self.r_rate_entry, "78.5")
        if self.gacha_config_path.exists():
            self.gacha_config_path.unlink()
        messagebox.showinfo(APP_TITLE, "Rates reset to defaults")

    def set_guaranteed_char(self):
        try:
            char_id = int(self.guarantee_char_entry.get())
            data = self.read_json(self.gacha_config_path, {"custom_rates": None, "guaranteed_character": None})
            data["guaranteed_character"] = char_id
            self.write_json(self.gacha_config_path, data)
            self.append_log("Gacha", f"Set guaranteed character to {char_id}")
            messagebox.showinfo(APP_TITLE, f"Guaranteed character set to {char_id}")
        except Exception as exc:
            messagebox.showerror(APP_TITLE, f"Failed to set guaranteed character: {exc}")

    def clear_guaranteed_char(self):
        data = self.read_json(self.gacha_config_path, {"custom_rates": None, "guaranteed_character": None})
        data["guaranteed_character"] = None
        self.write_json(self.gacha_config_path, data)
        self.guarantee_char_entry.delete(0, tk.END)
        messagebox.showinfo(APP_TITLE, "Guaranteed character cleared")

    def browse_characters(self):
        window = tk.Toplevel(self.root)
        window.title("Character Browser")
        window.geometry("620x560")
        window.configure(bg=self.colors["surface"])

        shell = ttk.Frame(window, style="Surface.TFrame", padding=(18, 16, 18, 16))
        shell.pack(fill="both", expand=True)
        ttk.Label(shell, text="Character browser", style="CardTitle.TLabel").pack(anchor="w")
        ttk.Label(shell, text="Search by character name or numeric identifier.", style="Muted.TLabel").pack(anchor="w", pady=(4, 12))

        search_bar = ttk.Frame(shell, style="Surface.TFrame")
        search_bar.pack(fill="x")
        search_entry = ttk.Entry(search_bar, width=36)
        search_entry.pack(side="left")
        char_listbox = tk.Listbox(shell, bg="#0f141e", fg=self.colors["text"], relief="flat", highlightthickness=0, selectbackground=self.colors["accent"], selectforeground="#ffffff", font=("Segoe UI", 10))
        char_listbox.pack(fill="both", expand=True, pady=12)
        characters = self.get_character_list()

        def update_list(term=""):
            char_listbox.delete(0, tk.END)
            for char_id, char_name in characters:
                if not term or term.lower() in char_name.lower() or term in str(char_id):
                    char_listbox.insert(tk.END, f"[{char_id}] {char_name}")

        def select_character():
            selection = char_listbox.curselection()
            if not selection:
                return
            selected_text = char_listbox.get(selection[0])
            char_id = selected_text.split("]")[0].replace("[", "")
            self._set_entry(self.guarantee_char_entry, char_id)
            window.destroy()

        ttk.Button(search_bar, text="Search", style="Secondary.TButton", command=lambda: update_list(search_entry.get())).pack(side="left", padx=8)
        ttk.Button(shell, text="Select character", style="Primary.TButton", command=select_character).pack(anchor="e")
        search_entry.bind("<Return>", lambda _event: update_list(search_entry.get()))
        update_list()

    def browse_spawn_character(self):
        window = tk.Toplevel(self.root)
        window.title("Select Character")
        window.geometry("620x560")
        window.configure(bg=self.colors["surface"])

        shell = ttk.Frame(window, style="Surface.TFrame", padding=(18, 16, 18, 16))
        shell.pack(fill="both", expand=True)
        ttk.Label(shell, text="Select character", style="CardTitle.TLabel").pack(anchor="w")
        ttk.Label(shell, text="Choose a character to populate the spawn form.", style="Muted.TLabel").pack(anchor="w", pady=(4, 12))

        search_bar = ttk.Frame(shell, style="Surface.TFrame")
        search_bar.pack(fill="x")
        search_entry = ttk.Entry(search_bar, width=36)
        search_entry.pack(side="left")
        char_listbox = tk.Listbox(shell, bg="#0f141e", fg=self.colors["text"], relief="flat", highlightthickness=0, selectbackground=self.colors["accent"], selectforeground="#ffffff", font=("Segoe UI", 10))
        char_listbox.pack(fill="both", expand=True, pady=12)
        characters = self.get_character_list()

        def update_list(term=""):
            char_listbox.delete(0, tk.END)
            for char_id, char_name in characters:
                if not term or term.lower() in char_name.lower() or term in str(char_id):
                    char_listbox.insert(tk.END, f"[{char_id}] {char_name}")

        def select_character():
            selection = char_listbox.curselection()
            if not selection:
                return
            selected_text = char_listbox.get(selection[0])
            char_id = selected_text.split("]")[0].replace("[", "")
            self._set_entry(self.spawn_char_id_entry, char_id)
            window.destroy()

        ttk.Button(search_bar, text="Search", style="Secondary.TButton", command=lambda: update_list(search_entry.get())).pack(side="left", padx=8)
        ttk.Button(shell, text="Use character", style="Primary.TButton", command=select_character).pack(anchor="e")
        search_entry.bind("<Return>", lambda _event: update_list(search_entry.get()))
        update_list()

    def get_character_list(self):
        return [
            (10000, "Aru"), (10001, "Aru (New Year)"), (10002, "Hina"), (10003, "Hina (Swimsuit)"),
            (10004, "Iori"), (10005, "Iori (Swimsuit)"), (10006, "Eimi"), (10007, "Eimi (Swimsuit)"),
            (10008, "Shiroko"), (10009, "Shiroko (Riding)"), (10010, "Hoshino"), (10011, "Hoshino (Swimsuit)"),
            (10012, "Serika"), (10013, "Serika (New Year)"), (10014, "Nonomi"), (10015, "Nonomi (Swimsuit)"),
            (10016, "Ayane"), (10017, "Ayane (Swimsuit)"), (13000, "Yuzu"), (13001, "Yuzu (Maid)"),
            (16000, "Neru"), (16001, "Neru (Bunny Girl)"), (10018, "Akane"), (10019, "Akane (Bunny Girl)"),
            (10020, "Mutsuki"), (10021, "Mutsuki (New Year)"), (10022, "Kayoko"), (10023, "Kayoko (New Year)"),
            (10024, "Haruka"), (10025, "Haruka (New Year)"), (10026, "Atsuko"), (10027, "Junko"),
            (10028, "Izumi"), (10029, "Izumi (Swimsuit)"), (10030, "Shun"), (10031, "Shun (Kid)"),
            (10032, "Tsubaki"), (10033, "Tsubaki (Guide)"), (10034, "Moe"), (10035, "Midori"),
            (10036, "Momoi"), (10038, "Yuuka"), (10039, "Noa"), (10040, "Karin"), (10041, "Asuna"),
            (10042, "Asuna (Bunny Girl)"), (10043, "Akari"), (10045, "Haruna"), (10046, "Haruna (New Year)"),
            (10047, "Izuna"), (10048, "Izuna (Swimsuit)"), (10049, "Tsurugi"), (10050, "Tsurugi (Swimsuit)"),
            (10051, "Hifumi"), (10052, "Hifumi (Swimsuit)"), (10053, "Azusa"), (10054, "Azusa (Swimsuit)"),
            (10055, "Koharu"), (10056, "Hanae"), (10057, "Hanae (Christmas)"), (13002, "Aru (Dress)"),
            (13003, "Kayoko (Dress)"), (16002, "Asuna (Christmas)"), (10058, "Saya"), (10059, "Saya (Casual)"),
            (20001, "Serina"), (20002, "Shimiko"), (20004, "Fuuka"), (20005, "Kotama"), (20006, "Juri"),
            (20008, "Toki"), (10060, "Maki"), (10061, "Cherino"), (10062, "Marina"),
        ]

    def refresh_banners(self):
        self.banner_listbox.delete(0, tk.END)
        common_banners = [
            ("Normal Gacha", "Standard recruitment banner"),
            ("Pickup Gacha", "Rate-up banner for featured characters"),
            ("Limited Gacha", "Limited-time exclusive characters"),
            ("Fest Gacha", "Festival banner with increased SSR rates"),
        ]
        config = self.read_json(self.banner_config_path, {"disabled_banners": []})
        disabled = set(config.get("disabled_banners", []))
        for name, description in common_banners:
            enabled = name not in disabled
            mark = "✓" if enabled else "✗"
            self.banner_listbox.insert(tk.END, f"{mark} {name} — {description}")

    def set_banner_enabled(self, enabled: bool):
        selection = self.banner_listbox.curselection()
        if not selection:
            messagebox.showerror(APP_TITLE, "No banner selected")
            return
        text = self.banner_listbox.get(selection[0])
        banner_name = text.split(" — ")[0].replace("✓ ", "").replace("✗ ", "")
        config = self.read_json(self.banner_config_path, {"disabled_banners": []})
        disabled = set(config.get("disabled_banners", []))
        if enabled:
            disabled.discard(banner_name)
        else:
            disabled.add(banner_name)
        config["disabled_banners"] = sorted(disabled)
        self.write_json(self.banner_config_path, config)
        self.refresh_banners()
        state = "enabled" if enabled else "disabled"
        self.append_log("Gacha", f"{banner_name} {state}")

    def enable_banner(self):
        self.set_banner_enabled(True)

    def disable_banner(self):
        self.set_banner_enabled(False)

    def add_mail_item(self):
        try:
            item = {
                "type": self.mail_item_type.get(),
                "id": int(self.mail_item_id_entry.get()),
                "amount": int(self.mail_item_amount_entry.get()),
            }
            self.mail_items.append(item)
            self.mail_items_list.insert(tk.END, f"{item['type']} — ID {item['id']} × {item['amount']}")
            self._set_entry(self.mail_item_id_entry, "")
            self._set_entry(self.mail_item_amount_entry, 1)
        except Exception as exc:
            messagebox.showerror(APP_TITLE, f"Failed to add reward: {exc}")

    def remove_mail_item(self):
        selection = self.mail_items_list.curselection()
        if not selection:
            return
        index = selection[0]
        self.mail_items_list.delete(index)
        self.mail_items.pop(index)

    def send_mail(self):
        server_id = self.current_account_id_from(self.mail_account_combo)
        if server_id is None:
            messagebox.showerror(APP_TITLE, "No account selected")
            return
        try:
            sender = self.mail_sender_entry.get().strip()
            subject = self.mail_subject_entry.get().strip()
            body = self.mail_message_text.get("1.0", tk.END).strip()
            expire_days = int(self.mail_expire_entry.get())
            if not subject:
                raise ValueError("Subject is required")
            send_date = datetime.utcnow()
            expire_date = send_date.replace(year=send_date.year + 1) if expire_days == 0 else send_date + timedelta(days=expire_days)
            parcel_json = json.dumps([
                {"Key": {"Type": self.get_parcel_type(item["type"]), "Id": item["id"]}, "Amount": item["amount"]}
                for item in self.mail_items
            ])
            localized_sender = json.dumps({str(index): sender for index in range(5)})
            localized_comment = json.dumps({str(index): body for index in range(5)})
            with self.get_db_connection() as conn:
                cursor = conn.cursor()
                cursor.execute(
                    """
                    INSERT INTO Mails (AccountServerId, Type, UniqueId, Sender, Comment, LocalizedSender, LocalizedComment, SendDate, ExpireDate, ParcelInfos, RemainParcelInfos, IsRefresher)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """,
                    (
                        server_id,
                        1,
                        0,
                        sender,
                        body,
                        localized_sender,
                        localized_comment,
                        send_date.strftime("%Y-%m-%d %H:%M:%S"),
                        expire_date.strftime("%Y-%m-%d %H:%M:%S"),
                        parcel_json,
                        "[]",
                        0,
                    ),
                )
                conn.commit()
            self.append_log("Mail", f"Sent mail to account {server_id}")
            self.clear_mail_form()
            messagebox.showinfo(APP_TITLE, "Mail sent")
        except Exception as exc:
            messagebox.showerror(APP_TITLE, f"Failed to send mail: {exc}")

    def get_parcel_type(self, item_type):
        return {"Currency": 1, "Item": 2, "Equipment": 3, "Character": 4}.get(item_type, 2)

    def clear_mail_form(self):
        self._set_entry(self.mail_subject_entry, "")
        self.mail_message_text.delete("1.0", tk.END)
        self._set_entry(self.mail_item_id_entry, "")
        self._set_entry(self.mail_item_amount_entry, 1)
        self.mail_items.clear()
        self.mail_items_list.delete(0, tk.END)

    def spawn_item(self):
        server_id = self.current_account_id_from(self.item_account_combo)
        if server_id is None:
            messagebox.showerror(APP_TITLE, "No account selected")
            return
        try:
            item_id = int(self.spawn_item_id_entry.get())
            amount = int(self.spawn_item_amount_entry.get())
            stack_count = int(self.spawn_item_stack_entry.get())
            with self.get_db_connection() as conn:
                cursor = conn.cursor()
                cursor.execute("SELECT ServerId FROM Items WHERE ServerId = ? AND UniqueId = ?", (server_id, item_id))
                exists = cursor.fetchone()
                if exists:
                    cursor.execute("UPDATE Items SET StackCount = StackCount + ? WHERE ServerId = ? AND UniqueId = ?", (amount, server_id, item_id))
                else:
                    cursor.execute("INSERT INTO Items (ServerId, UniqueId, StackCount, IsNew, IsLocked) VALUES (?, ?, ?, ?, ?)", (server_id, item_id, stack_count, 1, 0))
                conn.commit()
            self.append_log("Items", f"Spawned item {item_id} for account {server_id}")
            self.refresh_items()
            messagebox.showinfo(APP_TITLE, "Item spawned")
        except Exception as exc:
            messagebox.showerror(APP_TITLE, f"Failed to spawn item: {exc}")

    def refresh_items(self):
        server_id = self.current_account_id_from(self.item_account_combo)
        self.items_listbox.delete(0, tk.END)
        if server_id is None:
            return
        try:
            with self.get_db_connection() as conn:
                cursor = conn.cursor()
                cursor.execute("SELECT UniqueId, StackCount FROM Items WHERE ServerId = ? ORDER BY UniqueId", (server_id,))
                rows = cursor.fetchall()
            for item_id, count in rows:
                self.items_listbox.insert(tk.END, f"Item ID {item_id} — Count {count}")
        except Exception as exc:
            messagebox.showerror(APP_TITLE, f"Failed to refresh items: {exc}")

    def spawn_character(self):
        server_id = self.current_account_id_from(self.char_account_combo)
        if server_id is None:
            messagebox.showerror(APP_TITLE, "No account selected")
            return
        try:
            char_id = int(self.spawn_char_id_entry.get())
            star_grade = int(self.spawn_char_star_entry.get())
            level = int(self.spawn_char_level_entry.get())
            with self.get_db_connection() as conn:
                cursor = conn.cursor()
                cursor.execute("SELECT MAX(ServerId) FROM Characters WHERE AccountServerId = ?", (server_id,))
                max_server_id = cursor.fetchone()[0] or 0
                next_server_id = max_server_id + 1
                cursor.execute(
                    """
                    INSERT INTO Characters (ServerId, AccountServerId, UniqueId, StarGrade, Level, Exp, FavorRank, FavorExp, PublicSkillLevel, ExSkillLevel, PassiveSkillLevel, ExtraPassiveSkillLevel, LeaderSkillLevel, IsNew, IsLocked, EquipmentServerIds)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """,
                    (next_server_id, server_id, char_id, star_grade, level, 0, 1, 0, 1, 1, 1, 1, 1, 1, 0, "[]"),
                )
                conn.commit()
            self.append_log("Characters", f"Spawned character {char_id} for account {server_id}")
            self.refresh_characters()
            messagebox.showinfo(APP_TITLE, "Character spawned")
        except Exception as exc:
            messagebox.showerror(APP_TITLE, f"Failed to spawn character: {exc}")

    def refresh_characters(self):
        server_id = self.current_account_id_from(self.char_account_combo)
        self.characters_listbox.delete(0, tk.END)
        if server_id is None:
            return
        try:
            with self.get_db_connection() as conn:
                cursor = conn.cursor()
                cursor.execute("SELECT UniqueId, StarGrade, Level FROM Characters WHERE AccountServerId = ? ORDER BY UniqueId", (server_id,))
                rows = cursor.fetchall()
            for unique_id, star_grade, level in rows:
                self.characters_listbox.insert(tk.END, f"Character ID {unique_id} — {star_grade}★ Lv.{level}")
        except Exception as exc:
            messagebox.showerror(APP_TITLE, f"Failed to refresh characters: {exc}")

    def start_environment(self):
        self.refresh_environment_async()
        if self.project_install_required():
            self.install_project_prompt()
            return
        if self.runtime["server"].state in {"running", "starting"} or self.runtime["mitm"].state in {"running", "starting"}:
            messagebox.showinfo(APP_TITLE, "The environment is already starting or running")
            return
        self.append_log("Console", "Launching server services")
        self.runtime["server"].state = "starting"
        self.runtime["server"].detail = "Launching dotnet host"
        self.runtime["mitm"].state = "starting"
        self.runtime["mitm"].detail = "Launching mitmweb"
        self.update_runtime_labels()

        self.start_server_process()
        self.root.after(1500, self.start_mitm_process)

    def start_server_process(self):
        self.refresh_runtime_paths()
        args, cwd, detail = self.get_server_launch_target()
        self.runtime["server"].detail = detail
        self.spawn_process("server", args, cwd)

    def start_mitm_process(self):
        mitm_args = self.build_mitm_command()
        self.spawn_process("mitm", mitm_args, self.mitm_script_dir)

    def build_mitm_command(self):
        base = [
            "mitmweb",
            "--listen-host", "127.0.0.1",
            "--listen-port", str(int(self.settings.get("mitm_listen_port", 8080))),
            "--web-port", str(int(self.settings.get("mitm_web_port", 8081))),
        ]
        remainder = self.settings.get("mitm_arguments", "--no-http2 -s redirect_server.py --set termlog_verbosity=warn --mode local:BlueArchive.exe").split()
        return base + remainder

    def spawn_process(self, key, args, cwd: Path):
        creationflags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
        try:
            process = subprocess.Popen(
                args,
                cwd=str(cwd),
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                stdin=subprocess.DEVNULL,
                text=True,
                bufsize=1,
                creationflags=creationflags,
            )
            self.runtime[key].process = process
            self.runtime[key].state = "starting"
            self.runtime[key].detail = "Process launched"
            self.update_runtime_labels()
            threading.Thread(target=self.read_process_output, args=(key, process), daemon=True).start()
        except Exception as exc:
            self.runtime[key].state = "failed"
            self.runtime[key].detail = str(exc)
            self.update_runtime_labels()
            messagebox.showerror(APP_TITLE, f"Failed to start {self.runtime[key].name}: {exc}")

    def read_process_output(self, key, process):
        source = self.runtime[key].name
        try:
            assert process.stdout is not None
            for line in process.stdout:
                cleaned = line.rstrip()
                if cleaned:
                    self.append_log(source, cleaned)
                    self.process_runtime_output(key, cleaned)
        except Exception as exc:
            self.append_log(source, f"Log stream error: {exc}")

    def process_runtime_output(self, key, line: str):
        lower = line.lower()
        if key == "server":
            if "now listening on" in lower or "application started" in lower:
                self.runtime[key].state = "running"
                self.runtime[key].detail = f"Listening at {self.settings.get('server_url', 'http://localhost:5000')}"
        elif key == "mitm":
            if (
                "listening at" in lower
                or "web server listening" in lower
                or "running mitmweb" in lower
                or "proxy server listening" in lower
                or "web interface listening" in lower
                or "http(s) proxy listening" in lower
                or "127.0.0.1:" in lower
            ):
                self.runtime[key].state = "running"
                self.runtime[key].detail = f"Web UI at {self.settings.get('mitm_url', 'http://127.0.0.1:8081')}"
        if "error" in lower or "failed" in lower or "exception" in lower:
            if self.runtime[key].state != "running":
                self.runtime[key].state = "failed"
                self.runtime[key].detail = line[:140]
        self.root.after(0, self.update_runtime_labels)

    def stop_environment(self):
        for key in ["mitm", "server"]:
            self.stop_process(key)
        self.update_runtime_labels()
        self.append_log("Console", "Service stop requested")

    def install_project_async(self, mark_commit_current=False):
        if self.install_in_progress:
            return
        self.install_in_progress = True
        self.append_log("Installer", f"Installing project files into {self.install_dir}")
        threading.Thread(target=self._install_project_worker, args=(mark_commit_current,), daemon=True).start()

    def _install_project_worker(self, mark_commit_current=False):
        urls = [
            "https://codeload.github.com/Neoexm/Shittim-Server/zip/refs/heads/main",
            "https://codeload.github.com/Neoexm/Shittim-Server/zip/refs/heads/master",
        ]
        temp_dir = None
        try:
            if self.has_local_release_payload():
                self.append_log("Installer", "Installing bundled release payload")
                self.install_bundled_payload()
                self.append_log("Installer", "Bundled release payload installed successfully")
                if mark_commit_current and self.update_status.get("release_tag"):
                    self.update_state["last_seen_release"] = self.update_status.get("release_tag")
                    if self.update_status.get("asset_url"):
                        self.update_state["asset_url"] = self.update_status["asset_url"]
                    self.save_update_state()
                    self.update_status = {
                        "state": "current",
                        "detail": f"Installed latest release {self.update_state['last_seen_release']}",
                        "release_tag": self.update_state["last_seen_release"],
                        "asset_url": self.update_state.get("asset_url"),
                        "release_name": self.update_state.get("release_name"),
                    }
                    self.root.after(0, self.update_update_views)
                self.root.after(0, lambda: messagebox.showinfo(APP_TITLE, f"Bundled server installed into {self.install_dir}"))
                return

            temp_dir = Path(tempfile.mkdtemp(prefix="shittim-console-"))
            archive_path = temp_dir / "shittim-server.zip"
            last_error = None
            for url in urls:
                try:
                    self.append_log("Installer", f"Downloading project archive from {url}")
                    urllib.request.urlretrieve(url, archive_path)
                    last_error = None
                    break
                except Exception as exc:
                    last_error = exc
            if last_error is not None:
                raise RuntimeError(f"Download failed: {last_error}")

            extract_dir = temp_dir / "extract"
            extract_dir.mkdir(parents=True, exist_ok=True)
            with zipfile.ZipFile(archive_path, "r") as zip_ref:
                zip_ref.extractall(extract_dir)

            extracted_roots = [path for path in extract_dir.iterdir() if path.is_dir()]
            if not extracted_roots:
                raise RuntimeError("Downloaded archive did not contain a project folder")

            source_root = extracted_roots[0]
            if self.install_dir.exists():
                for child in self.install_dir.iterdir():
                    if child.name == ".shittim-console":
                        continue
                    if child.is_dir():
                        shutil.rmtree(child, ignore_errors=True)
                    else:
                        child.unlink(missing_ok=True)

            for child in source_root.iterdir():
                destination = self.install_dir / child.name
                if child.is_dir():
                    shutil.copytree(child, destination, dirs_exist_ok=True)
                else:
                    shutil.copy2(child, destination)

            self.append_log("Installer", "Project files installed successfully")
            if mark_commit_current and self.update_status.get("release_tag"):
                self.update_state["last_seen_release"] = self.update_status["release_tag"]
                if self.update_status.get("asset_url"):
                    self.update_state["asset_url"] = self.update_status["asset_url"]
                self.save_update_state()
                self.update_status = {
                    "state": "current",
                    "detail": f"Installed latest release {self.update_state['last_seen_release']}",
                    "release_tag": self.update_state["last_seen_release"],
                    "asset_url": self.update_state.get("asset_url"),
                    "release_name": self.update_state.get("release_name"),
                }
                self.root.after(0, self.update_update_views)
            self.root.after(0, lambda: messagebox.showinfo(APP_TITLE, f"Project installed into {self.install_dir}"))
        except Exception as exc:
            self.append_log("Installer", f"Installation failed: {exc}")
            self.root.after(0, lambda: messagebox.showerror(APP_TITLE, f"Failed to install project: {exc}"))
        finally:
            self.install_in_progress = False
            if temp_dir and temp_dir.exists():
                shutil.rmtree(temp_dir, ignore_errors=True)
            self.refresh_runtime_paths()
            self.root.after(0, self.refresh_environment_async)

    def install_bundled_payload(self):
        payload_root = Path(tempfile.mkdtemp(prefix="shittim-bundled-"))
        try:
            bundle_map = [
                (self.bundled_runtime_dir, payload_root / "server-runtime"),
                (self.bundled_source_dir, payload_root / "Shittim-Server"),
                (self.bundled_scripts_dir, payload_root / "Scripts"),
            ]

            copied_any = False
            for source, destination in bundle_map:
                if source.exists():
                    shutil.copytree(source, destination, dirs_exist_ok=True)
                    copied_any = True

            if not copied_any:
                raise RuntimeError("No bundled release payload was found next to the executable")

            self.install_extracted_payload(payload_root)
        finally:
            shutil.rmtree(payload_root, ignore_errors=True)

    def install_extracted_payload(self, payload_root: Path):
        if self.install_dir.exists():
            for child in self.install_dir.iterdir():
                if child.name == ".shittim-console":
                    continue
                if child.is_dir():
                    shutil.rmtree(child, ignore_errors=True)
                else:
                    child.unlink(missing_ok=True)

        copied_any = False
        for child in payload_root.iterdir():
            destination = self.install_dir / child.name
            if child.is_dir():
                shutil.copytree(child, destination, dirs_exist_ok=True)
                copied_any = True
            elif child.is_file():
                shutil.copy2(child, destination)
                copied_any = True

        if not copied_any:
            raise RuntimeError("No bundled release payload was found next to the executable")

    def stop_process(self, key):
        runtime = self.runtime[key]
        process = runtime.process
        if not process:
            runtime.state = "stopped"
            runtime.detail = f"{runtime.name} is not running"
            return
        try:
            process.terminate()
            try:
                process.wait(timeout=6)
            except subprocess.TimeoutExpired:
                process.kill()
            runtime.state = "stopped"
            runtime.detail = f"{runtime.name} stopped"
        except Exception as exc:
            runtime.state = "failed"
            runtime.detail = f"Stop failed: {exc}"
        finally:
            runtime.process = None

    def monitor_processes(self):
        for runtime in self.runtime.values():
            process = runtime.process
            if process is not None:
                code = process.poll()
                if code is not None:
                    runtime.process = None
                    if runtime.state != "stopped":
                        runtime.state = "failed" if code else "stopped"
                        runtime.detail = f"Exited with code {code}"
                elif runtime.state == "starting":
                    if runtime.name == "MITM Proxy":
                        runtime.state = "running"
                        runtime.detail = f"Running. Open {self.settings.get('mitm_url', 'http://127.0.0.1:8081')} if needed"
        self.update_runtime_labels()
        self.monitor_after_id = self.root.after(1200, self.monitor_processes)

    def install_certificate(self):
        threading.Thread(target=self._install_certificate_worker, daemon=True).start()

    def _install_certificate_worker(self):
        try:
            cert_path = self.get_mitm_certificate_path()
            if not cert_path.exists():
                self.append_log("Certificates", "Certificate file missing; generating mitmproxy assets")
                self.generate_mitm_certificate()
            result = subprocess.run(["certutil", "-addstore", "-f", "Root", str(cert_path)], capture_output=True, text=True, timeout=20)
            if result.returncode != 0:
                raise RuntimeError(result.stderr.strip() or result.stdout.strip() or "certutil failed")
            self.append_log("Certificates", "Certificate installed into Local Machine Root")
            self.root.after(0, lambda: messagebox.showinfo(APP_TITLE, "Certificate installed successfully"))
        except Exception as exc:
            self.append_log("Certificates", f"Install failed: {exc}")
            self.root.after(0, lambda: messagebox.showerror(APP_TITLE, f"Failed to install certificate: {exc}"))
        finally:
            self.root.after(0, self.refresh_environment_async)

    def generate_mitm_certificate(self):
        creationflags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
        process = subprocess.Popen(
            [
                "mitmweb",
                "--listen-host", "127.0.0.1",
                "--listen-port", "8899",
                "--web-port", "8900",
            ],
            cwd=str(self.mitm_script_dir),
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            stdin=subprocess.DEVNULL,
            text=True,
            creationflags=creationflags,
        )
        time.sleep(6)
        process.terminate()
        try:
            process.wait(timeout=4)
        except subprocess.TimeoutExpired:
            process.kill()
        if not self.get_mitm_certificate_path().exists():
            raise RuntimeError("mitmproxy certificate files were not generated")

    def uninstall_certificate(self):
        if not messagebox.askyesno(APP_TITLE, "Remove the mitmproxy certificate from the Local Machine trust store?"):
            return
        threading.Thread(target=self._uninstall_certificate_worker, daemon=True).start()

    def _uninstall_certificate_worker(self):
        try:
            result = subprocess.run(["certutil", "-delstore", "Root", "mitmproxy"], capture_output=True, text=True, timeout=20)
            if result.returncode != 0 and "cannot find" not in (result.stderr + result.stdout).lower():
                raise RuntimeError(result.stderr.strip() or result.stdout.strip() or "certutil failed")
            self.append_log("Certificates", "Certificate removed from Local Machine Root")
            self.root.after(0, lambda: messagebox.showinfo(APP_TITLE, "Certificate uninstall completed"))
        except Exception as exc:
            self.root.after(0, lambda: messagebox.showerror(APP_TITLE, f"Failed to uninstall certificate: {exc}"))
        finally:
            self.root.after(0, self.refresh_environment_async)

    def repair_certificate(self):
        self.append_log("Certificates", "Repair requested")
        self.install_certificate()

    def repair_mitmproxy(self):
        if not messagebox.askyesno(APP_TITLE, "Repair mitmproxy and build tooling using pip?"):
            return
        threading.Thread(target=self._repair_mitmproxy_worker, daemon=True).start()

    def _repair_mitmproxy_worker(self):
        try:
            command = [sys.executable, "-m", "pip", "install", "--upgrade", "mitmproxy", "pyinstaller"]
            self.append_log("Maintenance", "Running pip upgrade for mitmproxy and pyinstaller")
            result = subprocess.run(command, cwd=str(self.base_dir), capture_output=True, text=True, timeout=600)
            output = (result.stdout or "") + (result.stderr or "")
            if result.returncode != 0:
                raise RuntimeError(output.strip() or "pip install failed")
            self.append_log("Maintenance", "mitmproxy repair completed")
            self.root.after(0, lambda: messagebox.showinfo(APP_TITLE, "mitmproxy and packaging tools repaired"))
        except Exception as exc:
            self.append_log("Maintenance", f"Repair failed: {exc}")
            self.root.after(0, lambda: messagebox.showerror(APP_TITLE, f"Repair failed: {exc}"))
        finally:
            self.root.after(0, self.refresh_environment_async)

    def save_management_settings(self):
        try:
            new_install_dir = self.install_dir_entry.get().strip()
            new_logs_dir = self.logs_dir_entry.get().strip()
            default_install = str(self.appdata_dir)
            default_logs = str(self.config_dir / "logs")
            self.settings["install_dir"] = "" if new_install_dir == default_install else new_install_dir
            self.settings["logs_dir"] = "" if new_logs_dir == default_logs else new_logs_dir
            self.settings["server_port"] = int(self.server_port_entry.get())
            self.settings["mitm_listen_port"] = int(self.mitm_listen_port_entry.get())
            self.settings["mitm_web_port"] = int(self.mitm_web_port_entry.get())
            self.settings["mitm_mode"] = self.mitm_mode_entry.get().strip() or "local:BlueArchive.exe"
            self.settings["mitm_arguments"] = f"--no-http2 -s redirect_server.py --set termlog_verbosity=warn --mode {self.settings['mitm_mode']}"
            self.settings["server_url"] = f"http://localhost:{self.settings['server_port']}"
            self.settings["mitm_url"] = f"http://127.0.0.1:{self.settings['mitm_web_port']}"
            self.save_settings()
            self.apply_directory_settings()
            self.refresh_runtime_paths()
            self.append_log("Management", "Settings updated")
            self.update_runtime_labels()
            messagebox.showinfo(APP_TITLE, "Settings saved. Directory changes take effect immediately.")
            self.refresh_environment_async()
        except Exception as exc:
            messagebox.showerror(APP_TITLE, f"Failed to save settings: {exc}")

    def browse_install_dir(self):
        current = self.install_dir_entry.get().strip()
        chosen = filedialog.askdirectory(initialdir=current or str(self.appdata_dir), title="Select install directory")
        if chosen:
            self._set_entry(self.install_dir_entry, chosen)

    def browse_logs_dir(self):
        current = self.logs_dir_entry.get().strip()
        chosen = filedialog.askdirectory(initialdir=current or str(self.logs_dir), title="Select log directory")
        if chosen:
            self._set_entry(self.logs_dir_entry, chosen)

    def build_console(self):
        if not messagebox.askyesno(APP_TITLE, "Build a distributable executable for the console now?"):
            return
        threading.Thread(target=self._build_console_worker, daemon=True).start()

    def _build_console_worker(self):
        try:
            command = [
                sys.executable,
                "-m",
                "PyInstaller",
                "--noconfirm",
                "--clean",
                "--windowed",
                "--name",
                APP_TITLE,
                str(self.base_dir / "shittim_console.py"),
            ]
            self.append_log("Packaging", "Building executable with PyInstaller")
            result = subprocess.run(command, cwd=str(self.base_dir), capture_output=True, text=True, timeout=1200)
            output = (result.stdout or "") + (result.stderr or "")
            if result.returncode != 0:
                raise RuntimeError(output.strip() or "Build failed")
            self.append_log("Packaging", "Build completed successfully")
            self.root.after(0, lambda: messagebox.showinfo(APP_TITLE, "Build completed. Open the dist folder to access the executable."))
        except Exception as exc:
            self.append_log("Packaging", f"Build failed: {exc}")
            self.root.after(0, lambda: messagebox.showerror(APP_TITLE, f"Build failed: {exc}"))

    def open_dist_folder(self):
        dist_path = self.base_dir / "dist"
        dist_path.mkdir(exist_ok=True)
        os.startfile(dist_path)  # type: ignore[attr-defined]

    def open_logs_folder(self):
        self.logs_dir.mkdir(exist_ok=True)
        os.startfile(self.logs_dir)  # type: ignore[attr-defined]

    def open_current_log(self):
        if not self.current_log_path.exists():
            self._prepare_log_file()
        os.startfile(self.current_log_path)  # type: ignore[attr-defined]

    def export_logs(self):
        try:
            export_path = self.logs_dir / "exported-session.log"
            shutil.copy2(self.current_log_path, export_path)
            self.append_log("Logs", f"Exported session log to {export_path}")
            messagebox.showinfo(APP_TITLE, f"Logs exported to {export_path}")
        except Exception as exc:
            messagebox.showerror(APP_TITLE, f"Failed to export logs: {exc}")

    def read_json(self, path: Path, default):
        if not path.exists():
            return default
        try:
            return json.loads(path.read_text(encoding="utf-8"))
        except Exception:
            return default

    def write_json(self, path: Path, data):
        path.write_text(json.dumps(data, indent=2), encoding="utf-8")

    def on_close(self):
        self.stop_environment()
        if self.monitor_after_id:
            self.root.after_cancel(self.monitor_after_id)
        if hasattr(self, "update_check_after_id") and self.update_check_after_id:
            self.root.after_cancel(self.update_check_after_id)
        self.root.destroy()


def main():
    root = tk.Tk()
    ShittimConsole(root)
    root.mainloop()


if __name__ == "__main__":
    main()
