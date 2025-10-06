import os
import sys
import json
import sqlite3
import subprocess
import importlib
from pathlib import Path
from datetime import datetime
import threading
import time
import socket
import platform
import ctypes
import tempfile
import atexit
import io

# Force UTF-8 encoding for stdout/stderr on Windows to handle Unicode characters
if sys.platform == 'win32':
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

# Initialize colorama for cross-platform ANSI support
try:
    from colorama import init, Fore, Style
    init(autoreset=True)
    RED = Fore.RED
    GREEN = Fore.GREEN
    YELLOW = Fore.YELLOW
    BLUE = Fore.BLUE
    MAGENTA = Fore.MAGENTA
    CYAN = Fore.CYAN
    WHITE = Fore.WHITE
    BOLD = Style.BRIGHT
    RESET = Style.RESET_ALL
except ImportError:
    # Fallback to ANSI codes with Windows console enable
    if sys.platform == 'win32':
        try:
            kernel32 = ctypes.windll.kernel32
            kernel32.SetConsoleMode(kernel32.GetStdHandle(-11), 7)
        except Exception:
            pass
    RED = '\033[91m'
    GREEN = '\033[92m'
    YELLOW = '\033[93m'
    BLUE = '\033[94m'
    MAGENTA = '\033[95m'
    CYAN = '\033[96m'
    WHITE = '\033[97m'
    BOLD = '\033[1m'
    RESET = '\033[0m'

def print_colored(text, color=WHITE):
    print(f"{color}{text}{RESET}")

def is_admin():
    try:
        if platform.system() == "Windows":
            return ctypes.windll.shell32.IsUserAnAdmin()
        else:
            return os.geteuid() == 0
    except:
        return False

class HostsManager:
    # Domains used by the official Blue Archive client.  Any domain not
    # redirected will hit the real server and can cause 10001 errors if
    # network connectivity is blocked.  Extend this list to cover all
    # endpoints observed in nexon.har.  These values are only added to
    # your hosts file if you run with administrator privileges.
    BLUE_ARCHIVE_DOMAINS = [
        'public.api.nexon.com',
        'signin.nexon.com',
        'prod-noticepool.game.nexon.com',
        # Regional API domains used by Blue Archive
        'nxm-eu-bagl.nexon.com',
        'nxm-ios-bagl.nexon.com',
        'nxm-kr-bagl.nexon.com',
        'nxm-tw-bagl.nexon.com',
        'nxm-th-bagl.nexon.com',
        'nxm-or-bagl.nexon.com',
        # Additional analytics/config endpoints (NGS domains removed - let them talk to real servers)
        'toy.log.nexon.io',
        'gtable.inface.nexon.com',
        'config.na.nexon.com',
        # AWS API Gateway used by the web SDK
        'bolo7yechd.execute-api.ap-northeast-1.amazonaws.com',
        'nexon-sdk.nexon.com',
        'api-pub.nexon.com',
        'member.nexon.com',
        'sdk-push.mp.nexon.com',
        'ba.dn.nexoncdn.co.kr',
        'd2vaidpni345rp.cloudfront.net',
        'prod-noticeview.bluearchiveyostar.com',
        'yostarjp.s3-ap-northeast-1.amazonaws.com',
        'yostar-serverinfo.bluearchiveyostar.com',
        'ba-gl-web.bluearchiveyostar.com',
        'ba-gl-kor-web.bluearchiveyostar.com',
        'crash-reporting-api-rs26-usw2.cloud.unity3d.com',
        'uca-cloud-api.cloud.unity3d.com',
        '54.238.121.146',
        'ba-patch.bluearchiveyostar.com',
        'ba-web.bluearchiveyostar.com'
    ]

    def __init__(self):
        if platform.system() == "Windows":
            self.hosts_path = Path(r"C:\Windows\System32\drivers\etc\hosts")
        else:
            self.hosts_path = Path("/etc/hosts")
        self.backup_path = self.hosts_path.parent / "hosts.backup"

    def backup_hosts_file(self):
        try:
            if self.hosts_path.exists() and not self.backup_path.exists():
                import shutil
                shutil.copy2(self.hosts_path, self.backup_path)
                print_colored("Backed up hosts file.", GREEN)
                return True
        except Exception as e:
            print_colored(f"Failed to back up hosts file: {e}", RED)
            return False
        return True

    def read_hosts_file(self):
        try:
            if self.hosts_path.exists():
                return self.hosts_path.read_text(encoding='utf-8')
            return ""
        except Exception as e:
            print_colored(f"Couldn't read hosts file: {e}", YELLOW)
            return ""

    def write_hosts_file(self, content):
        try:
            with open(self.hosts_path, 'w', encoding='utf-8') as f:
                f.write(content)
            return True
        except Exception as e:
            print_colored(f"Failed to write hosts file: {e}", RED)
            print_colored("Run this as administrator.", YELLOW)
            return False

    def add_redirects(self):
        if not is_admin():
            print_colored("Administrator privileges required to edit the hosts file.", RED)
            print_colored("Run this as administrator if you want automatic domain setup.", YELLOW)
            return False

        if not self.backup_hosts_file():
            return False

        current_content = self.read_hosts_file()

        if "# Blue Archive Redirects" in current_content:
            print_colored("Blue Archive redirects already present.", GREEN)
            return True

        redirect_section = "\n# Blue Archive Redirects\n"
        for domain in self.BLUE_ARCHIVE_DOMAINS:
            redirect_section += f"127.0.0.1 {domain}\n"
        redirect_section += "# End Blue Archive Redirects\n"

        new_content = current_content + redirect_section

        if self.write_hosts_file(new_content):
            print_colored(f"Added {len(self.BLUE_ARCHIVE_DOMAINS)} redirect entries.", GREEN)
            return True

        return False

    def remove_redirects(self):
        if not is_admin():
            print_colored("Administrator privileges required to edit the hosts file.", RED)
            return False

        current_content = self.read_hosts_file()

        if "# Blue Archive Redirects" not in current_content:
            print_colored("No Blue Archive redirects found.", GREEN)
            return True

        lines = current_content.split('\n')
        new_lines = []
        skip = False

        for line in lines:
            if "# Blue Archive Redirects" in line:
                skip = True
                continue
            elif "# End Blue Archive Redirects" in line:
                skip = False
                continue
            elif not skip:
                new_lines.append(line)

        new_content = '\n'.join(new_lines)

        if self.write_hosts_file(new_content):
            print_colored("Removed Blue Archive redirect entries.", GREEN)
            return True

        return False

    def show_manual_instructions(self):
        print_colored("\nManual domain redirect setup:", YELLOW)
        print_colored("Add these lines to your hosts file:", WHITE)
        print_colored(f"Location: {self.hosts_path}", CYAN)
        print_colored("", WHITE)
        for domain in self.BLUE_ARCHIVE_DOMAINS:
            print_colored(f"127.0.0.1 {domain}", WHITE)

        print_colored("\nHow to edit the hosts file:", YELLOW)
        print_colored("1) Open a text editor as Administrator.", WHITE)
        print_colored(f"2) Open: {self.hosts_path}", WHITE)
        print_colored("3) Paste the lines shown above.", WHITE)
        print_colored("4) Save.", WHITE)

class DependencyManager:
    REQUIRED_PACKAGES = [
        'flask',
        'requests',
        'flatbuffers',
        'xxhash',
        'pycryptodome',
        'unitypy',
        'cryptography',
        'colorama'
    ]

    K0LB3_FILES = {
        'lib/TableEncryptionService.py': 'https://raw.githubusercontent.com/K0lb3s-Datamines/Blue-Archive---Asset-Downloader/main/lib/TableEncryptionService.py',
        'lib/StringCipher.py': 'https://raw.githubusercontent.com/K0lb3s-Datamines/Blue-Archive---Asset-Downloader/main/lib/StringCipher.py',
        'lib/MersenneTwister.py': 'https://raw.githubusercontent.com/K0lb3s-Datamines/Blue-Archive---Asset-Downloader/main/lib/MersenneTwister.py',
        'lib/XXHashService.py': 'https://raw.githubusercontent.com/K0lb3s-Datamines/Blue-Archive---Asset-Downloader/main/lib/XXHashService.py',
        'lib/AESEncryptionService.py': 'https://raw.githubusercontent.com/K0lb3s-Datamines/Blue-Archive---Asset-Downloader/main/lib/AESEncryptionService.py',
        'lib/TableService.py': 'https://raw.githubusercontent.com/K0lb3s-Datamines/Blue-Archive---Asset-Downloader/main/lib/TableService.py',
        'BlueArchive.fbs': 'https://raw.githubusercontent.com/K0lb3s-Datamines/Blue-Archive---Asset-Downloader/main/BlueArchive.fbs'
    }

    def __init__(self):
        self.root_path = Path(__file__).parent

    def check_python_packages(self):
        missing = []
        for package in self.REQUIRED_PACKAGES:
            try:
                print_colored(f"  Checking {package}...", BLUE)
                # Handle special package import names
                if package == 'pycryptodome':
                    importlib.import_module('Crypto')
                elif package == 'unitypy':
                    importlib.import_module('UnityPy')
                else:
                    importlib.import_module(package.replace('-', '_'))
                print_colored(f"  ✓ {package}", GREEN)
            except ImportError as e:
                print_colored(f"  ✗ {package}: {e}", RED)
                missing.append(package)
            except Exception as e:
                print_colored(f"  CRASH checking {package}: {e}", RED)
                import traceback
                traceback.print_exc()
                missing.append(package)
        return missing

    def install_packages(self, packages):
        print_colored(f"Installing {len(packages)} packages...", YELLOW)
        for package in packages:
            print_colored(f"  {package}", CYAN)
            try:
                subprocess.check_call(
                    [sys.executable, '-m', 'pip', 'install', package],
                    stdout=subprocess.DEVNULL,
                    stderr=subprocess.DEVNULL
                )
                print_colored(f"  Installed {package}", GREEN)
            except subprocess.CalledProcessError:
                print_colored(f"  Failed to install {package}", RED)
                return False
        return True

    def download_k0lb3_files(self):
        import requests

        missing_files = []
        for rel_path in self.K0LB3_FILES:
            full_path = self.root_path / rel_path
            if not full_path.exists():
                missing_files.append(rel_path)

        if not missing_files:
            print_colored("All protocol files are present.", GREEN)
            return True

        print_colored(f"Downloading {len(missing_files)} protocol files...", YELLOW)

        for rel_path in missing_files:
            full_path = self.root_path / rel_path
            url = self.K0LB3_FILES[rel_path]
            full_path.parent.mkdir(parents=True, exist_ok=True)

            try:
                print_colored(f"  {rel_path}", CYAN)
                response = requests.get(url, timeout=30)
                response.raise_for_status()
                with open(full_path, 'wb') as f:
                    f.write(response.content)
                print_colored(f"  Saved ({len(response.content)} bytes)", GREEN)
            except Exception as e:
                print_colored(f"  Failed: {e}", RED)
                return False

        return True

    def setup_environment(self):
        print_colored("Setting up environment...", BOLD)

        try:
            print_colored("Checking packages...", YELLOW)
            missing_packages = self.check_python_packages()
            print_colored(f"Missing packages: {missing_packages}", CYAN)
            if missing_packages:
                if not self.install_packages(missing_packages):
                    return False
            else:
                print_colored("Python packages look fine.", GREEN)

            print_colored("Downloading protocol files...", YELLOW)
            if not self.download_k0lb3_files():
                return False

            print_colored("Environment ready.", GREEN)
            return True
        except Exception as e:
            print_colored(f"Environment setup crashed: {e}", RED)
            import traceback
            traceback.print_exc()
            return False

class BlueArchiveServer:
    def __init__(self):
        self.current_version = "1.35.115378"
        self.sessions = {}
        self.player_data = {}
        self.crypto_available = self._setup_crypto()
        # Simple in-memory queue state to mimic gateway behavior
        self.queue_ticket_seq = 114980000
        self.queue_allowed_seq = 114980000
        # Database persistent account store
        self.data_dir = Path(__file__).parent / 'data'
        self.data_dir.mkdir(exist_ok=True)
        self.db_path = self.data_dir / 'accounts.db'
        self.db_lock = threading.Lock()
        self._setup_database()
        # Map transient IAS tickets to a stable user key to avoid new accounts each run
        self.ticket_map = {}
        self.current_user_key = None
        # HAR logging setup
        self._har_entries = []
        self._har_logfile = f"server_log_{int(time.time())}.har"
        self._create_initial_har_file()
        # Track getCountry call count for different error codes
        self.get_country_count = 0
        # NGS endpoint request-response mappings (extracted from HAR file)
        self.ngs_mappings = self._load_ngs_mappings()
        # One-time migration: skip - using SQLite database now
        print_colored("Account migration skipped: Using SQLite database", CYAN)

    def _create_initial_har_file(self):
        """Create the HAR file immediately with empty entries"""
        try:
            data = {
                "log": {
                    "version": "1.2",
                    "creator": {"name": "ShittimServer", "version": "1.0"},
                    "entries": []
                }
            }
            with open(self._har_logfile, 'w', encoding='utf-8') as f:
                json.dump(data, f, indent=2)
            print_colored(f"HAR file created: {self._har_logfile}", GREEN)
        except Exception as e:
            print_colored(f"Failed to create HAR file: {e}", RED)

    def _update_har_file(self):
        """Update the HAR file with current entries"""
        try:
            data = {
                "log": {
                    "version": "1.2",
                    "creator": {"name": "ShittimServer", "version": "1.0"},
                    "entries": self._har_entries
                }
            }
            with open(self._har_logfile, 'w', encoding='utf-8') as f:
                json.dump(data, f, indent=2)
        except Exception as e:
            print_colored(f"HAR update error: {e}", YELLOW)

    def _load_ngs_mappings(self):
        """Hardcoded NGS endpoint request-response mappings extracted from official server"""
        # NGS mappings removed - let the game talk to real NGS servers for anti-cheat verification
        print_colored("NGS endpoint mappings disabled - using real NGS servers", GREEN)
        return {}

    def _handle_ngs_endpoint(self, endpoint, request_body):
        """NGS endpoint handling disabled - requests go to real servers"""
        print_colored(f"NGS endpoint {endpoint} bypassed to real server", YELLOW)
        return None
    def _derive_ids_from_token(self, token: str, gid: str = '2079'):
        import hashlib
        h = hashlib.sha256((token or '').encode('utf-8')).hexdigest()
        base = 76561197960265728
        user64 = base + (int(h[:16], 16) % 10**10)
        platform_user_id = int(h[16:24], 16) % 10**8
        guid = f"{gid}{str(user64)[-13:]}"
        return str(platform_user_id), str(guid), str(user64)

    def _get_or_create_account(self, ticket: str, gid: str = '2079'):
        import hashlib, time as _t
        
        # Extract Steam ID or other unique identifier from ticket
        user_key = self._extract_user_id_from_ticket(ticket, gid)
        
        # Check if account exists in database
        account = self._get_account(user_key)
        
        if account:
            # Existing account - update last login
            account['last_login'] = int(_t.time())
            account['updated_at'] = int(_t.time())
            self._save_account(user_key, account)
            print_colored(f"Existing account login: {account.get('name', 'Unknown')} (ID: {user_key})", GREEN)
            return account, user_key, False  # False = not new account
        else:
            # New account - create with unique IDs
            platform_user_id, guid, user64 = self._derive_ids_from_token(ticket or user_key, gid)
            acct = {
                "gid": gid,
                "guid": guid,
                "npSN": guid,
                "umKey": f"107:{platform_user_id}",
                "platform_type": "STEAM" if "steam" in user_key.lower() else "ARENA",
                "platform_user_id": platform_user_id,
                "steam_id": self._extract_steam_id(ticket) if ticket else None,
                "name": f"User{platform_user_id[-6:]}",  # Generate unique name
                "level": 1,
                "attribute": [],
                "created_at": int(_t.time()),
                "updated_at": int(_t.time()),
                "last_login": int(_t.time()),
                "is_new": True  # Flag for first-time setup
            }
            self._save_account(user_key, acct)
            print_colored(f"NEW account created: {acct['name']} (ID: {user_key})", BOLD + GREEN)
            return acct, user_key, True  # True = new account

    def _extract_user_id_from_ticket(self, ticket: str, gid: str) -> str:
        """Extract a unique user identifier from the authentication ticket"""
        import hashlib
        
        # Try to extract Steam ID from ticket if possible
        steam_id = self._extract_steam_id(ticket)
        if steam_id:
            return f"steam:{steam_id}"
            
        # Fallback to hashed ticket for unique identification
        if ticket:
            ticket_hash = hashlib.sha256(ticket.encode('utf-8')).hexdigest()[:16]
            return f"ticket:{ticket_hash}"
            
        # Last resort - generate a random ID
        import uuid
        random_id = str(uuid.uuid4())[:8]
        return f"anon:{random_id}"

    def _extract_steam_id(self, ticket: str) -> str:
        """Try to extract Steam ID from ticket data"""
        if not ticket:
            return None
            
        # This would need to be implemented based on the actual ticket format
        # For now, return None since we don't know the ticket structure
        # TODO: Analyze actual Steam tickets to implement this properly
        return None

    def _extract_steam_id_from_platform_token(self, token: str) -> str:
        """Extract Steam ID from the platform token"""
        if not token:
            return None
            
        try:
            # The HAR shows the platform token contains the Steam ID
            # From the HAR example: link_platform_user_id: "76561198260711461"
            # The token appears to be a complex binary structure, but we can try to extract the Steam ID
            
            import re
            
            # Look for Steam ID pattern in the token - Steam IDs are typically 17-digit numbers starting with 765611
            if len(token) > 32:  # Ensure it's long enough to contain Steam ID
                # Try to find 17-digit Steam ID pattern
                steam_id_pattern = r'765611\d{11}'
                
                # First try to find it directly in the token string
                match = re.search(steam_id_pattern, token)
                if match:
                    steam_id = match.group()
                    print_colored(f"Extracted Steam ID from token: {steam_id}", GREEN)
                    return steam_id
            
            # If we can't parse it, return the known Steam ID from HAR for now
            print_colored("Could not parse Steam ID from token, using HAR default", YELLOW)
            return "76561198260711461"  # From the HAR file
            
        except Exception as e:
            print_colored(f"Error extracting Steam ID: {e}", RED)
            return "76561198260711461"  # Fallback

    def _setup_database(self):
        """Initialize SQLite database for account storage"""
        try:
            with sqlite3.connect(self.db_path) as conn:
                conn.execute('''
                    CREATE TABLE IF NOT EXISTS accounts (
                        user_key TEXT PRIMARY KEY,
                        gid TEXT NOT NULL,
                        guid TEXT NOT NULL,
                        npSN TEXT NOT NULL,
                        umKey TEXT NOT NULL,
                        platform_type TEXT NOT NULL,
                        platform_user_id TEXT NOT NULL,
                        steam_id TEXT,
                        name TEXT NOT NULL,
                        level INTEGER DEFAULT 1,
                        attribute TEXT DEFAULT '[]',
                        created_at INTEGER NOT NULL,
                        updated_at INTEGER NOT NULL,
                        last_login INTEGER,
                        is_guest BOOLEAN DEFAULT 0,
                        needs_name_setup BOOLEAN DEFAULT 0,
                        extra_data TEXT DEFAULT '{}'
                    )
                ''')
                conn.commit()
                
            # Get account count
            count = self._get_account_count()
            print_colored(f"Database initialized with {count} accounts", GREEN)
        except Exception as e:
            print_colored(f"Failed to setup database: {e}", RED)
            
    def _get_account_count(self):
        """Get total number of accounts in database"""
        try:
            with sqlite3.connect(self.db_path) as conn:
                cursor = conn.execute('SELECT COUNT(*) FROM accounts')
                return cursor.fetchone()[0]
        except Exception:
            return 0
            
    def _get_account(self, user_key: str):
        """Get account by user key from database"""
        try:
            with sqlite3.connect(self.db_path) as conn:
                conn.row_factory = sqlite3.Row
                cursor = conn.execute('SELECT * FROM accounts WHERE user_key = ?', (user_key,))
                row = cursor.fetchone()
                if row:
                    account = dict(row)
                    # Convert JSON fields back to objects
                    account['attribute'] = json.loads(account['attribute'])
                    account['extra_data'] = json.loads(account['extra_data'])
                    account['is_guest'] = bool(account['is_guest'])
                    account['needs_name_setup'] = bool(account['needs_name_setup'])
                    return account
                return None
        except Exception as e:
            print_colored(f"Failed to get account {user_key}: {e}", RED)
            return None
            
    def _save_account(self, user_key: str, account: dict):
        """Save account to database"""
        try:
            with self.db_lock:
                with sqlite3.connect(self.db_path) as conn:
                    # Prepare data for database
                    data = {
                        'user_key': user_key,
                        'gid': account['gid'],
                        'guid': account['guid'],
                        'npSN': account['npSN'],
                        'umKey': account['umKey'],
                        'platform_type': account['platform_type'],
                        'platform_user_id': account['platform_user_id'],
                        'steam_id': account.get('steam_id'),
                        'name': account['name'],
                        'level': account['level'],
                        'attribute': json.dumps(account.get('attribute', [])),
                        'created_at': account['created_at'],
                        'updated_at': account['updated_at'],
                        'last_login': account.get('last_login'),
                        'is_guest': int(account.get('is_guest', False)),
                        'needs_name_setup': int(account.get('needs_name_setup', False)),
                        'extra_data': json.dumps(account.get('extra_data', {}))
                    }
                    
                    # Insert or update account
                    conn.execute('''
                        INSERT OR REPLACE INTO accounts (
                            user_key, gid, guid, npSN, umKey, platform_type,
                            platform_user_id, steam_id, name, level, attribute,
                            created_at, updated_at, last_login, is_guest,
                            needs_name_setup, extra_data
                        ) VALUES (
                            :user_key, :gid, :guid, :npSN, :umKey, :platform_type,
                            :platform_user_id, :steam_id, :name, :level, :attribute,
                            :created_at, :updated_at, :last_login, :is_guest,
                            :needs_name_setup, :extra_data
                        )
                    ''', data)
                    conn.commit()
                    
            print_colored(f"Saved account {account['name']} to database", GREEN)
        except Exception as e:
            print_colored(f"Failed to save account {user_key}: {e}", RED)
            
    def _account_exists(self, user_key: str) -> bool:
        """Check if account exists in database"""
        try:
            with sqlite3.connect(self.db_path) as conn:
                cursor = conn.execute('SELECT 1 FROM accounts WHERE user_key = ?', (user_key,))
                return cursor.fetchone() is not None
        except Exception:
            return False

    def _setup_crypto(self):
        try:
            lib_path = Path(__file__).parent / 'lib'
            if lib_path.exists():
                sys.path.insert(0, str(lib_path))
            import flatbuffers  # noqa: F401
            import xxhash       # noqa: F401
            
            # Test AES encryption instead of the broken TableEncryptionService
            from Crypto.Cipher import AES
            from Crypto.Util.Padding import pad, unpad
            from Crypto.Random import get_random_bytes
            import hashlib
            
            # Test AES encryption works
            test_key = hashlib.md5(b"test_key").digest()
            test_iv = get_random_bytes(16)
            test_data = pad(b"test_data", 16)
            
            cipher = AES.new(test_key, AES.MODE_CBC, test_iv)
            encrypted = cipher.encrypt(test_data)
            
            print_colored(f"AES crypto test: {len(encrypted)} bytes encrypted", GREEN)
            return True
        except ImportError as e:
            print_colored(f"AES crypto not available: {e}", YELLOW)
            return False

    def _encrypt_nexon_response(self, json_data, npparams_header=None):
        """Encrypt JSON response using AES-CBC like Nexon's official server"""
        if not self.crypto_available:
            print_colored(f"Crypto unavailable, returning plain JSON", YELLOW)
            return json.dumps(json_data, separators=(',', ':')).encode('utf-8')
        
        try:
            from Crypto.Cipher import AES
            from Crypto.Util.Padding import pad
            from Crypto.Random import get_random_bytes
            import hashlib
            import base64
            
            # Convert JSON to string with minimal formatting
            json_str = json.dumps(json_data, separators=(',', ':'), ensure_ascii=False)
            print_colored(f"Encrypting response: {json_str}", CYAN)
            
            # Extract or derive session key from npparams
            if npparams_header:
                try:
                    npparams_bytes = bytes.fromhex(npparams_header)
                    # Key derivation: MD5 of first 32 bytes of npparams
                    session_key = hashlib.md5(npparams_bytes[:32]).digest()
                    # IV from next 16 bytes or generate random
                    if len(npparams_bytes) >= 48:
                        iv = npparams_bytes[32:48]
                    else:
                        iv = get_random_bytes(16)
                    print_colored(f"Using session key from npparams: {session_key.hex()[:16]}...", GREEN)
                except Exception as e:
                    print_colored(f"npparams parsing failed: {e}, using fallback", YELLOW)
                    # Fallback to default key if npparams parsing fails
                    session_key = hashlib.md5(b"nexon_default_key").digest()
                    iv = get_random_bytes(16)
            else:
                # No npparams - use fixed key for compatibility
                session_key = hashlib.md5(b"nexon_default_key").digest()
                iv = get_random_bytes(16)
                print_colored("No npparams, using default key", YELLOW)
            
            # Encrypt with AES-CBC
            cipher = AES.new(session_key, AES.MODE_CBC, iv)
            padded_data = pad(json_str.encode('utf-8'), 16)
            encrypted = cipher.encrypt(padded_data)
            
            # Prepend IV to encrypted data (standard practice)
            final_data = iv + encrypted
            
            print_colored(f"AES encrypted {len(json_str)} chars -> {len(final_data)} bytes", GREEN)
            
            # Return base64 encoded result
            b64_result = base64.b64encode(final_data)
            print_colored(f"Base64 result: {b64_result[:50]}...", BLUE)
            return b64_result
            
        except Exception as e:
            print_colored(f"AES encryption failed: {e}", RED)
            import traceback
            traceback.print_exc()
            # Return the HAR response as fallback
            har_fallback = "P7YhWb6oQDCGjNqIPAqrihEwV4IUd1WhKtl1Te3Gr/corlbn8O/eWMg7j8MB/WbLU1WDTzGxCs/0lyWlxb8QnRAyApYcbY+cyfPTomqVNUKdrpjPCnf+YUAiG4qQJA4ok1PR+cRevfdO+DU8UGQgDg=="
            print_colored("Using HAR fallback response", MAGENTA)
            return base64.b64decode(har_fallback)
            traceback.print_exc()
            # Fall back to plain JSON if encryption fails
            return json.dumps(json_data).encode('utf-8')

    def create_flask_app(self):
        try:
            from flask import Flask, request, jsonify, Response, redirect
        except ImportError:
            print_colored("Flask isn't installed. Run the dependency setup.", RED)
            return None

        app = Flask(__name__)

        # Reference to self for closures
        server_instance = self

        @app.before_request
        def _har_before_request():
            from flask import request as _req, g as _g
            _g._har_start_ts = time.time()
            try:
                _g._har_req_body = _req.get_data(cache=True)
            except Exception:
                _g._har_req_body = b''

        @app.after_request
        def _har_after_request(response):
            try:
                from flask import request as _req, g as _g
                import datetime as _dt, http.client as _http
                started = _dt.datetime.utcnow().isoformat() + 'Z'
                duration = int((time.time() - getattr(_g, '_har_start_ts', time.time())) * 1000)
                req_headers = [{"name": k, "value": v} for k, v in _req.headers.items()]
                req_query = [{"name": k, "value": v} for k, v in _req.args.items()]
                req_cookies = [{"name": k, "value": v} for k, v in _req.cookies.items()]
                body_bytes = getattr(_g, '_har_req_body', b'') or b''
                try:
                    body_text = body_bytes.decode('latin1')
                except Exception:
                    body_text = ''
                resp_headers = [{"name": k, "value": v} for k, v in response.headers.items()]
                resp_body = response.get_data() or b''
                try:
                    resp_text = resp_body.decode('latin1')
                except Exception:
                    resp_text = ''
                entry = {
                    "startedDateTime": started,
                    "time": duration,
                    "request": {
                        "method": _req.method,
                        "url": _req.url,
                        "httpVersion": "HTTP/1.1",
                        "headers": req_headers,
                        "queryString": req_query,
                        "cookies": req_cookies,
                        "headersSize": -1,
                        "bodySize": len(body_bytes),
                        "postData": {"mimeType": _req.headers.get('Content-Type', ''), "text": body_text} if body_text else {}
                    },
                    "response": {
                        "status": response.status_code,
                        "statusText": _http.responses.get(response.status_code, ''),
                        "httpVersion": "HTTP/1.1",
                        "headers": resp_headers,
                        "content": {
                            "size": len(resp_body),
                            "mimeType": response.headers.get('Content-Type', ''),
                            "text": resp_text
                        },
                        "redirectURL": response.headers.get('Location', ''),
                        "headersSize": -1,
                        "bodySize": len(resp_body)
                    },
                    "cache": {},
                    "timings": {"send": 0, "wait": duration, "receive": 0},
                    "serverIPAddress": _req.host.split(':')[0] if _req.host else '',
                    "connection": str(_req.environ.get('REMOTE_PORT', ''))
                }
                server_instance._har_entries.append(entry)
                server_instance._update_har_file()
                print_colored(f"HAR: Added entry #{len(server_instance._har_entries)} for {_req.method} {_req.path}", BLUE)
                # Debug: print the first entry to see its structure
                if len(server_instance._har_entries) == 1:
                    print_colored(f"First HAR entry keys: {list(entry.keys())}", MAGENTA)
            except Exception as e:
                print_colored(f"HAR logging error: {e}", YELLOW)
            return response

        @app.route('/com.nexon.bluearchivesteam/server_config/<config_name>.json', methods=['GET'])
        def server_config_redirect(config_name):
            # Return server configuration JSON pointing to our local API
            self.log_request(f'/com.nexon.bluearchivesteam/server_config/{config_name}.json')
            
            # Create the ConnectionGroupsJson as an escaped JSON string (this is the critical part)
            # This MUST match the exact format from the original server config
            # Use nxm-eu-bagl.nexon.com since it's redirected to 127.0.0.1 in hosts file
            # HTTPS URLs to match official server behavior
            connection_groups_json = """[
	{
		"Name": "review",
		"ApiUrl": "https://shittem-server.com:5000/api/",
		"GatewayUrl": "https://shittem-server.com:5100/api/",
		"DisableWebviewBanner": true,
		"NXSID": "stage-review"
	},
	{
		"Name": "live",
		"OverrideConnectionGroups": [
			{
				"Name": "kr",
				"ApiUrl": "https://shittem-server.com:5000/api/",
				"GatewayUrl": "https://shittem-server.com:5100/api/",
				"NXSID": "live-kr"
			},
			{
				"Name": "tw",
				"ApiUrl": "https://shittem-server.com:5000/api/",
				"GatewayUrl": "https://shittem-server.com:5100/api/",
				"NXSID": "live-tw"
			},
			{
				"Name": "asia",
				"ApiUrl": "https://shittem-server.com:5000/api/",
				"GatewayUrl": "https://shittem-server.com:5100/api/",
				"NXSID": "live-asia"
			},
			{
				"Name": "na",
				"ApiUrl": "https://shittem-server.com:5000/api/",
				"GatewayUrl": "https://shittem-server.com:5100/api/",
				"NXSID": "live-na"
			},
			{
				"Name": "global",
				"ApiUrl": "https://shittem-server.com:5000/api/",
				"GatewayUrl": "https://shittem-server.com:5100/api/",
				"NXSID": "live-global"
			}
		]
	}]"""
            
            # Server configuration matching the exact format expected by the game
            server_config = {
                "DefaultConnectionGroup": "live",
                "DefaultConnectionMode": "no", 
                "ConnectionGroupsJson": connection_groups_json,
                "desc": "1.35.369006"
            }
            
            # Return as text/plain (critical!) with JSON content
            response = jsonify(server_config)
            response.headers['Content-Type'] = 'text/plain'
            return response

        @app.route('/com.nexon.bluearchive/server_config/<csv_name>.csv', methods=['GET'])
        def server_config_csv(csv_name):
            self.log_request(f'/com.nexon.bluearchive/server_config/{csv_name}.csv')
            
            # Serve CSV files from server_configs directory
            csv_path = f"server_configs/{csv_name}.csv"
            try:
                with open(csv_path, 'r') as f:
                    content = f.read()
                return Response(content, status=200, headers={
                    'Content-Type': 'text/csv',
                    'Content-Length': str(len(content))
                })
            except FileNotFoundError:
                print_colored(f"CSV file not found: {csv_path}", RED)
                return Response("", status=404)

        @app.route('/toy/sdk/getCountry.nx', methods=['POST'])
        def get_country():
            self.log_request('/toy/sdk/getCountry.nx')
            
            payload = request.get_data()
            npparams = request.headers.get('npparams')
            gid = request.headers.get('gid')
            toy_service_id = request.headers.get('x-toy-service-id')
            
            if payload and self.crypto_available:
                self.analyze_flatbuffer_payload(payload)

            # Use the exact encrypted response from the official server (HAR file)
            # This is known to work and the client can decrypt it successfully
            har_response = "P7YhWb6oQDCGjNqIPAqrihEwV4IUd1WhKtl1Te3Gr/corlbn8O/eWMg7j8MB/WbLU1WDTzGxCs/0lyWlxb8QnRAyApYcbY+cyfPTomqVNUKdrpjPCnf+YUAiG4qQJA4ok1PR+cRevfdO+DU8UGQgDg=="
            
            # Official server logic: -2 if no gid header, 0 if gid header present
            if gid and toy_service_id:
                errorcode = '0'
                print_colored(f"getCountry with gid={gid}, returning errorcode 0", GREEN)
            else:
                errorcode = '-2'
                print_colored(f"getCountry without gid, returning errorcode -2", YELLOW)
            
            return Response(har_response, status=200, headers={
                'Content-Type': 'text/html; charset=UTF-8',
                'Content-Length': str(len(har_response)),
                'errorcode': errorcode,
                'access-control-allow-origin': '*',
                'cache-control': 'private',
                'date': datetime.now().strftime('%a, %d %b %Y %H:%M:%S GMT'),
                'x-ba-country': 'GB',
                'Content-Encoding': 'base64'
            })

        # API-prefixed variants used when client applies ApiUrl base
        @app.route('/api/toy/sdk/getCountry.nx', methods=['POST'])
        def get_country_api():
            return get_country()

        @app.route('/prod/crexception-prop', methods=['GET'])
        def crash_exception_prop():
            self.log_request('/prod/crexception-prop')
            config = {
                "propCheck": True,
                "period": 10,
                "ratio": 10
            }
            return jsonify(config)

        # Catch-all for root path requests (combine all functionality)
        @app.route('/', methods=['GET', 'POST'])
        def root_handler():
            self.log_request('/')
            payload = request.get_data()
            
            # For POST requests, return 2-byte response as expected (probably NGS/anti-cheat related)
            if request.method == 'POST':
                return Response(b"ok", status=200, headers={
                    'Content-Type': 'text/plain',
                    'Content-Length': '2'
                })
                
            return jsonify({"path": "/", "status": "handled"})

        @app.route('/stamp/live/v2/restore/steam', methods=['POST'])
        def stamp_restore_steam():
            self.log_request('/stamp/live/v2/restore/steam')
            response = {"restore_stamps": []}
            return Response(json.dumps(response), status=200, headers={
                'Content-Type': 'application/json;charset=UTF-8'
            })
            
        @app.route('/stamp/live/v2/restore/mp', methods=['POST'])
        def stamp_restore_mp():
            self.log_request('/stamp/live/v2/restore/mp')
            response = {}
            return Response(json.dumps(response), status=200, headers={
                'Content-Type': 'application/json;charset=UTF-8'
            })

        @app.route('/stamp/live/v1/enter', methods=['GET'])
        def stamp_enter():
            self.log_request('/stamp/live/v1/enter')
            # Return empty response for stamp enter
            return Response("", status=200, headers={
                'Content-Type': 'text/plain'
            })

        @app.route('/oneshot-url', methods=['POST'])
        def oneshot_url():
            self.log_request('/oneshot-url')
            # Return large dummy response to match expected size (4000+ bytes)
            dummy_data = "ONESHOT_DATA_" + "X" * 4000
            return Response(dummy_data, status=200, headers={
                'Content-Type': 'text/plain;charset=UTF-8',
                'Content-Length': str(len(dummy_data))
            })

        @app.route('/api/<path:endpoint>', methods=['GET', 'POST', 'PUT', 'DELETE', 'PATCH'])
        def api_endpoint(endpoint):
            self.log_request(f'/api/{endpoint}')
            
            # Special handling for gateway endpoint (this shouldn't be hit since we have specific route)
            if endpoint == 'gateway':
                print_colored(f"Gateway request via generic API route", YELLOW)
                return jsonify({"protocol": "Error", "packet": '{"Error":"Use specific gateway route"}'})
            
            print_colored(f"API hit: /api/{endpoint}", BOLD + CYAN)
            return jsonify({
                "errorCode": 0,
                "result": {},
                "message": "OK"
            })

        @app.route('/toy/sdk/enterToy.nx', methods=['POST'])
        def enter_toy():
            self.log_request('/toy/sdk/enterToy.nx')
            payload = request.get_data()
            if payload and self.crypto_available:
                self.analyze_flatbuffer_payload(payload)
            print_colored("enterToy called. Initializing.", BOLD + GREEN)
            
            # Build the response as a proper JSON object instead of hardcoded string
            enter_toy_response = {
                "errorCode": 0,
                "result": {
                    "service": {
                        "title": "Blue Archive",
                        "buildVer": "2",
                        "policyApiVer": "2",
                        "termsApiVer": "2",
                        "useTPA": 0,
                        "useGbNpsn": 1,
                        "useGbKrpc": 1,
                        "useGbArena": 1,
                        "useGbJppc": 0,
                        "useGamania": 0,
                        "useToyBanDialog": 0,
                        "grbRating": "",
                        "networkCheckSampleRate": "3",
                        "nkMemberAccessCode": "0",
                        "useIdfaCollection": 0,
                        "useIdfaDialog": 0,
                        "useIdfaDialogNTest": 0,
                        "useNexonOTP": 0,
                        "useRegionLock": 0,
                        "usePcDirectRun": 0,
                        "useArenaCSByRegion": 0,
                        "usePlayNow": 0,
                        "methinksUsage": {
                            "useAlwaysOnRecording": 0,
                            "useScreenshot": 0,
                            "useStreaming": 0,
                            "useSurvey": 0
                        },
                        "livestreamUsage": {
                            "useIM": 0
                        },
                        "useExactAlarmActivation": 0,
                        "useCollectUserActivity": 0,
                        "userActivityDataPushNotification": {
                            "changePoints": [],
                            "notificationType": ""
                        },
                        "appAppAuthLoginIconUrl": "",
                        "useGuidCreationBlk": 0,
                        "guidCreationBlkWlCo": [],
                        "useArena2FA": 0,
                        "usePrimary": 1,
                        "loginUIType": "1",
                        "clientId": "MjcwOA",
                        "useMemberships": [101, 103, 110, 107, 9999],
                        "useMembershipsInfo": {
                            "nexonNetSecretKey": "",
                            "nexonNetProductId": "",
                            "nexonNetRedirectUri": ""
                        }
                    },
                    "endBanner": {},
                    "country": "GB",
                    "idfa": {
                        "dialog": [],
                        "imgUrl": "",
                        "language": ""
                    },
                    "useLocalPolicy": ["0", "0"],
                    "enableLogging": False,
                    "enablePlexLogging": False,
                    "enableForcePingLogging": False,
                    "userArenaRegion": 1,
                    "offerwall": {
                        "id": 0,
                        "title": ""
                    },
                    "useYoutubeRewardEvent": False,
                    "gpgCycle": 0,
                    "eve": {
                        "domain": "https://eve.nexon.com",
                        "g-api": "https://g-eve-apis.nexon.com"
                    },
                    "insign": {
                        "useSimpleSignup": 0,
                        "useKrpcSimpleSignup": 0,
                        "useArenaSimpleSignup": 0
                    }
                },
                "errorText": "Success",
                "errorDetail": ""
            }
            
            # Return plain JSON response (not encrypted) - matches official server behavior
            print_colored("Returning plain JSON response for enterToy", GREEN)
            return jsonify(enter_toy_response)

        @app.route('/api/toy/sdk/enterToy.nx', methods=['POST'])
        def enter_toy_api():
            return enter_toy()

        @app.route('/toy/sdk/signInWithTicket.nx', methods=['POST'])
        def sign_in_with_ticket():
            self.log_request('/toy/sdk/signInWithTicket.nx')
            payload = request.get_data()
            ticket = None
            
            if payload and self.crypto_available:
                # Try to extract ticket from payload
                ticket = self.analyze_flatbuffer_payload(payload)
            
            # Get or create account based on actual ticket
            acct, user_key, is_new = self._get_or_create_account(ticket or '', '2079')
            
            if is_new:
                # New account - could return different response to trigger account creation UI
                print_colored(f"New account detected - returning registration flow", YELLOW)
            
            # Return REAL account data from storage
            sign_in_response = {
                "errorCode": 0,
                "result": {
                    "npSN": acct["npSN"],
                    "guid": acct["guid"], 
                    "umKey": acct["umKey"],
                    "isSwap": False,
                    "termsAgree": [
                        {
                            "termID": 304,
                            "type": [],
                            "optional": 0,
                            "exposureType": "NORMAL",
                            "title": "TERMS OF SERVICE AND END USER LICENSE AGREEMENT",
                            "titleReplacements": [],
                            "isAgree": 1,
                            "isUpdate": 0
                        },
                        {
                            "termID": 305,
                            "type": [],
                            "optional": 0,
                            "exposureType": "NORMAL", 
                            "title": "Privacy Policy",
                            "titleReplacements": [],
                            "isAgree": 1,
                            "isUpdate": 0
                        }
                    ],
                    "npaCode": "0EY0RZH10H0NN"
                },
                "errorText": "Success",
                "errorDetail": ""
            }
            print_colored(f"signInWithTicket: Account {acct['name']} ({user_key}) {'[NEW]' if is_new else '[EXISTING]'}", GREEN)
            return jsonify(sign_in_response)

        @app.route('/api/toy/sdk/signInWithTicket.nx', methods=['POST'])
        def sign_in_with_ticket_api():
            return sign_in_with_ticket()

        @app.route('/toy/sdk/terms.nx', methods=['POST'])
        def terms():
            self.log_request('/toy/sdk/terms.nx') 
            payload = request.get_data()
            if payload and self.crypto_available:
                self.analyze_flatbuffer_payload(payload)
                
            terms_response = {
                "errorCode": 0,
                "result": {
                    "terms": [
                        {
                            "termID": 304,
                            "type": [],
                            "optional": 0,
                            "exposureType": "NORMAL",
                            "title": "TERMS OF SERVICE AND END USER LICENSE AGREEMENT",
                            "titleReplacements": []
                        },
                        {
                            "termID": 305,
                            "type": [],
                            "optional": 0,
                            "exposureType": "NORMAL",
                            "title": "Privacy Policy", 
                            "titleReplacements": []
                        }
                    ]
                },
                "errorText": "Success",
                "errorDetail": ""
            }
            print_colored("terms: Returning terms list", GREEN)
            return jsonify(terms_response)

        @app.route('/api/toy/sdk/terms.nx', methods=['POST'])
        def terms_api():
            return terms()

        @app.route('/toy/sdk/getUserInfo.nx', methods=['POST'])
        def get_user_info():
            self.log_request('/toy/sdk/getUserInfo.nx')
            payload = request.get_data()
            ticket = None
            
            if payload and self.crypto_available:
                ticket = self.analyze_flatbuffer_payload(payload)
                
            # Get the actual account data
            acct, user_key, _ = self._get_or_create_account(ticket or '', '2079')
            
            # Return REAL user info from storage
            user_info_response = {
                "errorCode": 0,
                "result": {
                    "userInfo": {
                        "npSN": acct["npSN"],
                        "guid": acct["guid"],
                        "nickname": acct["name"],
                        "level": acct["level"],
                        "platformType": acct["platform_type"],
                        "steamId": acct.get("steam_id"),
                        "createdAt": acct["created_at"],
                        "lastLogin": acct["last_login"]
                    }
                },
                "errorText": "Success", 
                "errorDetail": ""
            }
            print_colored(f"getUserInfo: {acct['name']} (Level {acct['level']})", GREEN)
            return jsonify(user_info_response)

        @app.route('/api/toy/sdk/getUserInfo.nx', methods=['POST'])
        def get_user_info_api():
            return get_user_info()

        @app.route('/toy/sdk/getPolicyList.nx', methods=['POST'])
        def get_policy_list():
            self.log_request('/toy/sdk/getPolicyList.nx')
            payload = request.get_data()
            if payload and self.crypto_available:
                self.analyze_flatbuffer_payload(payload)
                
            policy_response = {
                "errorCode": 0,
                "result": {
                    "policies": []
                },
                "errorText": "Success",
                "errorDetail": ""
            }
            print_colored("getPolicyList: Returning policy list", GREEN)
            return jsonify(policy_response)

        @app.route('/api/toy/sdk/getPolicyList.nx', methods=['POST'])
        def get_policy_list_api():
            return get_policy_list()

        @app.route('/toy/sdk/getPromotion.nx', methods=['POST'])
        def get_promotion():
            self.log_request('/toy/sdk/getPromotion.nx')
            payload = request.get_data()
            if payload and self.crypto_available:
                self.analyze_flatbuffer_payload(payload)
                
            # Return empty HTML response to match expected format  
            html_response = "<html><body></body></html>"
            print_colored("getPromotion: Returning empty HTML", GREEN)
            return Response(html_response, status=200, headers={
                'Content-Type': 'text/html; charset=UTF-8',
                'Content-Length': str(len(html_response)),
                'Cache-Control': 'no-cache, no-store, must-revalidate'
            })

        @app.route('/api/toy/sdk/getPromotion.nx', methods=['POST'])
        def get_promotion_api():
            return get_promotion()

        @app.route('/sdk/push/token', methods=['POST'])
        def push_token():
            self.log_request('/sdk/push/token')
            print_colored("Push token registration.", YELLOW)
            return Response('', status=200, headers={
                'Content-Length': '0',
                'Cache-Control': 'no-cache, no-store, max-age=0, must-revalidate',
                'X-XSS-Protection': '1; mode=block',
                'Pragma': 'no-cache',
                'X-Frame-Options': 'DENY',
                'X-Content-Type-Options': 'nosniff'
            })

        # --- IAS v2 login/link -------------------------------------------------
        @app.route('/ias/live/public/v2/login/link', methods=['POST'])
        @app.route('/api/ias/live/public/v2/login/link', methods=['POST'])
        def ias_login_link():
            self.log_request('/ias/live/public/v2/login/link')
            try:
                body = request.get_json(silent=True) or {}
            except Exception:
                body = {}

            platform = str(body.get('link_platform_type', 'STEAM'))
            link_platform_token = body.get('link_platform_token', '')
            
            # Extract Steam ID from the platform token for account lookup
            steam_id = self._extract_steam_id_from_platform_token(link_platform_token)
            
            # Check if account exists for this Steam ID
            user_key = f"steam:{steam_id}" if steam_id else None
            account_exists = user_key and self._account_exists(user_key)
            
            if not account_exists:
                # No account found - return 401 to trigger guest account creation
                print_colored(f"No account found for Steam ID: {steam_id} - returning 401", YELLOW)
                return jsonify({
                    "error": {
                        "code": 12021,
                        "name": "NOT_FOUND_CONSOLE_LINK",
                        "message": "console link information not found"
                    }
                }), 401
            
            # Account exists - return login token
            try:
                import uuid
                now_ms = int(time.time() * 1000)
                web_token = f"ias:wt:{now_ms}:647987642@{uuid.uuid4()}@{platform}:TOY"
            except Exception:
                web_token = f"ias:wt:0:647987642@00000000-0000-0000-0000-000000000000@{platform}:TOY"

            # Return existing account login response
            print_colored(f"Existing account login for Steam ID: {steam_id}", GREEN)
            return jsonify({
                "web_token": web_token,
                "local_session_type": "toy",
                "local_session_user_id": steam_id or "76561198000000000"
            })

        # --- Guest Account Creation -------------------------------------------
        @app.route('/ims/public/v1/link/guest', methods=['POST'])
        @app.route('/api/ims/public/v1/link/guest', methods=['POST'])
        def ims_link_guest():
            self.log_request('/ims/public/v1/link/guest')
            try:
                body = request.get_json(silent=True) or {}
            except Exception:
                body = {}

            platform = str(body.get('link_platform_type', 'STEAM'))
            link_platform_token = body.get('link_platform_token', '')
            
            # Extract Steam ID for new account creation
            steam_id = self._extract_steam_id_from_platform_token(link_platform_token)
            
            # Create new guest account
            user_key = f"steam:{steam_id}" if steam_id else f"guest:{int(time.time())}"
            
            import time as _t
            platform_user_id, guid, user64 = self._derive_ids_from_token(link_platform_token or user_key, '2079')
            acct = {
                "gid": "2079",
                "guid": guid,
                "npSN": guid,
                "umKey": f"107:{platform_user_id}",
                "platform_type": "STEAM",
                "platform_user_id": platform_user_id,
                "steam_id": steam_id,
                "name": f"Guest{platform_user_id[-6:]}",  # Temporary name
                "level": 1,
                "attribute": [],
                "created_at": int(_t.time()),
                "updated_at": int(_t.time()),
                "last_login": int(_t.time()),
                "is_guest": True,
                "needs_name_setup": True  # Flag for name input screen
            }
            
            self._save_account(user_key, acct)
            
            # Generate tokens for new account
            try:
                import uuid
                now_ms = int(time.time() * 1000)
                web_token = f"ias:wt:{now_ms}:647987642@{uuid.uuid4()}@{platform}:TOY"
            except Exception:
                web_token = f"ias:wt:0:647987642@00000000-0000-0000-0000-000000000000@{platform}:TOY"

            print_colored(f"Guest account created: {acct['name']} (Steam ID: {steam_id})", BOLD + GREEN)
            
            return jsonify({
                "web_token": web_token,
                "link_platform_user_id": steam_id or platform_user_id
            })

        # --- IAS v3 ticket by web token ---------------------------------------
        @app.route('/ias/live/public/v3/issue/ticket/by-web-token', methods=['POST'])
        @app.route('/api/ias/live/public/v3/issue/ticket/by-web-token', methods=['POST'])
        def ias_ticket_by_webtoken():
            self.log_request('/ias/live/public/v3/issue/ticket/by-web-token')
            try:
                body = request.get_json(silent=True) or {}
            except Exception:
                body = {}
            # Prefer official header X-Ias-Web-Token per HAR, fallback to body
            wt = request.headers.get('X-Ias-Web-Token') or request.headers.get('x-ias-web-token')
            if not wt:
                wt = body.get('web_token') or body.get('webToken') or ''
            platform = 'STEAM'
            try:
                parts = wt.split('@')
                if len(parts) >= 3:
                    platform = parts[-1].split(':')[0] or 'STEAM'
            except Exception:
                pass

            try:
                now_ms = int(time.time() * 1000)
                # Build a game token similar to production: ias:gt:TIMESTAMP:1247143115@<uuid>@PLATFORM:ANA
                import uuid
                ticket_token = f"ias:t:{now_ms}:1247143115@{uuid.uuid4()}@{platform}:ANA"
            except Exception:
                ticket_token = "ias:t:0:1247143115@00000000-0000-0000-0000-000000000000@STEAM:ANA"

            user_id = "76561198000000000"
            if wt:
                import hashlib
                h = hashlib.sha256(wt.encode('utf-8')).hexdigest()
                base = 76561197960265728
                user_id = str(base + (int(h[:16], 16) % 10**10))

            # Match official v3 shape: { "ticket": "..." }
            resp = {
                "ticket": ticket_token
            }
            return jsonify(resp)

        # --- IAS v1 game-token by IAS ticket --------------------------------
        @app.route('/ias/live/public/v1/issue/game-token/by-ticket', methods=['POST'])
        @app.route('/api/ias/live/public/v1/issue/game-token/by-ticket', methods=['POST'])
        def ias_game_token_by_ticket():
            self.log_request('/ias/live/public/v1/issue/game-token/by-ticket')
            # Generate a unique game token tied to the incoming ticket.  The official API
            # derives the game token from the ticket; returning a hard‑coded token causes
            # the client to reject the login.  We build a new token using the current
            # timestamp and a random UUID.
            payload = request.get_json(silent=True) or {}
            ticket = payload.get('ticket') or ''
            now_ms = int(time.time() * 1000)
            platform = 'STEAM'
            import uuid as _uuid
            game_token = f"ias:gt:{now_ms}:1247143115@{_uuid.uuid4()}@{platform}:ANA"
            return jsonify({"game_token": game_token})

        # --- IAS WebToken stubs -------------------------------------------------
        def _mint_dummy_webtoken(client_id: str = "364258", region: str = "global") -> str:
            try:
                header = {"alg": "none", "typ": "JWT"}
                now = int(time.time())
                payload = {
                    "clientId": client_id,
                    "region": region,
                    "iat": now,
                    "exp": now + 3600,
                    "aud": "blue_archive",
                }
                import base64
                def b64(x):
                    s = json.dumps(x, separators=(',', ':')).encode('utf-8')
                    return base64.urlsafe_b64encode(s).rstrip(b'=').decode('ascii')
                return f"{b64(header)}.{b64(payload)}."  # no signature
            except Exception:
                return "dummy.token.no.sig"

        def _webtoken_response(token: str):
            body = {
                "errorCode": 0,
                "errorText": "Success",
                "result": {
                    "webToken": token,
                    "expireIn": 3600
                }
            }
            
            # Use proper encryption for crypto-enabled responses
            data = server_instance._encrypt_nexon_response(body, request.headers.get('npparams'))
            return Response(data, status=200, headers={
                'Content-Type': 'text/html; charset=UTF-8',
                'Content-Length': str(len(data)),
                'errorcode': '0',
                'access-control-allow-origin': '*',
                'cache-control': 'private'
            })

        def _extract_webtoken_from_request():
            try:
                # Prefer explicit IAS header if present
                hdr = request.headers.get('ias-game-token') or request.headers.get('IAS-Game-Token')
                if hdr and isinstance(hdr, str) and hdr.strip():
                    return hdr.strip()
                if request.is_json:
                    j = request.get_json(silent=True) or {}
                    if isinstance(j, dict):
                        for k in ("token", "webToken", "webtoken"):
                            if k in j and isinstance(j[k], str) and j[k]:
                                return j[k]
                raw = request.get_data() or b''
                if raw:
                    try:
                        j = json.loads(raw.decode('utf-8', errors='ignore'))
                        if isinstance(j, dict):
                            return j.get('webToken') or j.get('token')
                    except Exception:
                        pass
            except Exception:
                pass
            # Fall back to fixed token if configured
            return self.fixed_webtoken

        @app.route('/toy/ias/issueWebToken', methods=['POST'])
        @app.route('/toy/ias/verifyWebToken', methods=['POST'])
        @app.route('/toy/sdk/issueWebToken.nx', methods=['POST'])
        @app.route('/toy/sdk/verifyWebToken.nx', methods=['POST'])
        def ias_webtoken():
            self.log_request('/toy/ias/webtoken')
            token = _extract_webtoken_from_request() or self.fixed_webtoken or _mint_dummy_webtoken()
            return _webtoken_response(token)

        @app.route('/api/toy/ias/issueWebToken', methods=['POST'])
        @app.route('/api/toy/ias/verifyWebToken', methods=['POST'])
        @app.route('/api/toy/sdk/issueWebToken.nx', methods=['POST'])
        @app.route('/api/toy/sdk/verifyWebToken.nx', methods=['POST'])
        def ias_webtoken_api():
            return ias_webtoken()

        # --- IMS Account Link (primary platform) ------------------------------
        @app.route('/ims/public/v1/link/account/platform/primary', methods=['GET'])
        def ims_primary_platform():
            self.log_request('/ims/public/v1/link/account/platform/primary')
            
            # This endpoint should return existing account info, not create new accounts
            # Try to get account from current session
            user_key = getattr(self, 'current_user_key', None)
            if user_key:
                acct = self._get_account(user_key)
                if acct:
                    print_colored(f"Returning primary platform for existing account: {acct['name']}", GREEN)
                    # Update last access
                    import datetime as _dt
                    acct['last_login'] = _dt.datetime.utcnow().strftime('%Y-%m-%dT%H:%M:%S.0000000Z')
                    acct['updated_at'] = int(time.time())
                    self._save_account(user_key, acct)
                    
                    resp = {
                        "links": [
                            {
                                "platform_type": acct.get("platform_type", "STEAM"),
                                "platform_user_id": acct["platform_user_id"],
                                "guid": acct["guid"],
                                "is_primary": True,
                                "primary_platform_at": int(time.time() * 1000),
                                "game_data": {
                                    "gid": acct["gid"],
                                    "guid": acct["guid"]
                                }
                            }
                        ],
                        "user_verified": True
                    }
                    return jsonify(resp)
            
            # Fallback if no session - return default Steam response
            print_colored("No session found, returning default primary platform response", YELLOW)
            return jsonify({
                "links": [
                    {
                        "platform_type": "STEAM",
                        "platform_user_id": "76561198260711461",
                        "guid": "20790000041274554",
                        "is_primary": True,
                        "primary_platform_at": int(time.time() * 1000),
                        "game_data": {
                            "gid": "2079",
                            "guid": "20790000041274554"
                        }
                    }
                ],
                "user_verified": True
            })
        # --- Sign in with IAS ticket (toy sdk) --------------------------------
        @app.route('/toy/sdk/signInWithTicket.nx', methods=['POST'])
        @app.route('/api/toy/sdk/signInWithTicket.nx', methods=['POST'])
        def toy_sign_in_with_ticket():
            self.log_request('/toy/sdk/signInWithTicket.nx')
            gid = (request.headers.get('gid') or request.headers.get('Gid') or '2079')
            ticket = request.headers.get('ias-ticket') or request.headers.get('IAS-Ticket') or ''
            acct, user_key, _ = self._get_or_create_account(ticket, gid)
            
            # Update account metadata on sign-in
            acct['updated_at'] = int(time.time())
            
            # Persist the account changes to disk
            self._save_account(user_key, acct)
            
            result = {
                "npSN": acct["npSN"],
                "guid": acct["guid"],
                "umKey": acct["umKey"],
                "isSwap": False,
                "termsAgree": [
                    {"termID": 304, "type": [], "optional": 0, "exposureType": "NORMAL", "title": "TERMS OF SERVICE AND END USER LICENSE AGREEMENT", "titleReplacements": [], "isAgree": 1, "isUpdate": 0},
                    {"termID": 305, "type": [], "optional": 0, "exposureType": "NORMAL", "title": "Privacy Policy", "titleReplacements": [], "isAgree": 1, "isUpdate": 0}
                ],
                "npaCode": "0EP0RZW1060XL"
            }
            body = {
                "errorCode": 0,
                "result": result,
                "errorText": "Success",
                "errorDetail": ""
            }
            
            # Use proper encryption for crypto-enabled responses
            data = server_instance._encrypt_nexon_response(body, request.headers.get('npparams'))
            return Response(data, status=200, headers={
                'Content-Type': 'text/html; charset=UTF-8',
                'Content-Length': str(len(data)),
                'errorcode': '0',
                'access-control-allow-origin': '*',
                'cache-control': 'private'
            })

        # --- Terms list endpoint (post-sign-in) ------------------------------
        @app.route('/toy/sdk/terms.nx', methods=['POST'])
        @app.route('/api/toy/sdk/terms.nx', methods=['POST'])
        def toy_terms():
            self.log_request('/toy/sdk/terms.nx')
            # Client sends: { gid, locale, method, npsn, termsApiVer, uuid }
            try:
                _ = request.get_json(silent=True) or {}
            except Exception:
                _ = {}
            resp = {
                "errorCode": 0,
                "result": {
                    "terms": [
                        {"termID": 304, "type": [], "optional": 0, "exposureType": "NORMAL", "title": "TERMS OF SERVICE AND END USER LICENSE AGREEMENT", "titleReplacements": []},
                        {"termID": 305, "type": [], "optional": 0, "exposureType": "NORMAL", "title": "Privacy Policy", "titleReplacements": []}
                    ]
                },
                "errorText": "Success",
                "errorDetail": ""
            }
            
            # Use proper encryption for crypto-enabled responses
            data = server_instance._encrypt_nexon_response(resp, request.headers.get('npparams'))
            return Response(data, status=200, headers={
                'Content-Type': 'text/html; charset=UTF-8',
                'Content-Length': str(len(data)),
                'errorcode': '0',
                'access-control-allow-origin': '*',
                'cache-control': 'private'
            })

        # --- Country v2 (used by analytics flow) -----------------------------
        @app.route('/toy/v2/country', methods=['GET'])
        def toy_country_v2():
            self.log_request('/toy/v2/country')
            try:
                from flask import request as _req
                ip = getattr(_req, 'remote_addr', '127.0.0.1')
            except Exception:
                ip = '127.0.0.1'
            body = {"ip": ip, "country-code": "GB"}
            return jsonify(body)

        # --- Push policy (toy-push) -----------------------------------------
        @app.route('/toy-push/live/sdk/push/policy', methods=['GET'])
        def toy_push_policy_get():
            self.log_request('/toy-push/live/sdk/push/policy')
            svc_id = request.args.get('svcID', '2079')
            np_token = request.args.get('npToken', '')
            body = {
                "push": {
                    "name": "토이 푸시",
                    "policies": {
                        "1": {"enable": True, "name": "AD Push Policy (광고성 푸시 정책)"},
                        "2": {"enable": True, "name": "Nocturnal Push Policy (야간 푸시 정책)"}
                    }
                },
                "kind": {"name": "게임 푸시", "policies": {}},
                "svcID": int(svc_id) if str(svc_id).isdigit() else 2079,
                "npToken": np_token
            }
            return jsonify(body)

        @app.route('/toy-push/live/sdk/push/policy', methods=['POST'])
        def toy_push_policy_post():
            self.log_request('/toy-push/live/sdk/push/policy')
            return Response('', status=200, headers={
                'Content-Length': '0',
                'expires': '0',
                'cache-control': 'no-cache, no-store, max-age=0, must-revalidate',
                'x-xss-protection': '1; mode=block',
                'pragma': 'no-cache',
                'x-frame-options': 'DENY',
                'x-content-type-options': 'nosniff'
            })

        # --- SDK API: user-meta last-login ----------------------------------
        @app.route('/sdk-api/user-meta/last-login', methods=['POST'])
        def sdk_api_user_meta_last_login():
            self.log_request('/sdk-api/user-meta/last-login')
            data = b"{}"
            return Response(data, status=200, headers={
                'Content-Type': 'application/json',
                'Content-Length': str(len(data))
            })

        # --- Analytics stream processor proxy -------------------------------
        @app.route('/stream-processor-proxy/<region>/client.all.secure', methods=['POST', 'GET'])
        def stream_processor_proxy(region):
            self.log_request(f'/stream-processor-proxy/{region}/client.all.secure')
            # Accept and drop NDJSON analytics batches like prod; respond 200 with empty body per HAR
            try:
                _ = request.get_data(cache=False, as_text=False)
            except Exception:
                pass
            # Mimic prod-ish headers to appease picky clients
            import uuid as _uuid
            # Header order matters for some strict clients; build via list of tuples
            hdrs = [
                ('Content-Length', '0'),
                ('Connection', 'keep-alive'),
                ('date', datetime.now().strftime('%a, %d %b %Y %H:%M:%S GMT')),
                ('x-envoy-upstream-service-time', '254'),
                ('inface-wasm-filter', '1.8.0'),
                ('server', 'inface'),
                ('x-request-id', 'zoor3VtyCQebeofMJFGLCHOSEaUl17SMd2VXEmLKyHQpIeon9L6HUA=='),
                ('X-Cache', 'Miss from cloudfront'),
                ('Via', '1.1 70ac0c77136f38f37d334f7cae0b6c42.cloudfront.net (CloudFront)'),
                ('X-Amz-Cf-Pop', 'LHR50-P5'),
                ('X-Amz-Cf-Id', 'zoor3VtyCQebeofMJFGLCHOSEaUl17SMd2VXEmLKyHQpIeon9L6HUA=='),
            ]
            resp = Response(b"", status=200)
            for k, v in hdrs:
                resp.headers.add(k, v)
            return resp

        # --- Toy SDK: getPolicyList.nx --------------------------------------
        @app.route('/toy/sdk/getPolicyList.nx', methods=['POST'])
        @app.route('/api/toy/sdk/getPolicyList.nx', methods=['POST'])
        def toy_get_policy_list():
            self.log_request('/toy/sdk/getPolicyList.nx')
            # Return a base64-encoded encrypted blob as per nexon.har so client can decrypt
            import base64 as _b64
            b64_payload = (
                "k+4aDsyElgNVMq3WHbvFz47/iu9MAuxuTBrN5+u6VupRFXuHwOgcfZxGL5XU9v04j6h+CoAxiFph171R7h8AkN03CJs4lWUWHyc2VY3aQUwQ7q+cYv3SINx4azxP5t/O"
            )
            data = _b64.b64decode(b64_payload)
            date_val = datetime.now().strftime('%a, %d %b %Y %H:%M:%S GMT')
            hdrs = [
                ('Content-Type', 'text/html; charset=UTF-8'),
                ('Content-Length', '96'),
                ('Connection', 'keep-alive'),
                ('date', date_val),
                ('access-control-allow-origin', '*'),
                ('errorcode', '0'),
                ('cache-control', 'private'),
                ('x-envoy-upstream-service-time', '467'),
                ('inface-wasm-filter', '1.8.0'),
                ('server', 'inface'),
                ('x-request-id', 'z_rOyDkmglvfxdAKBzL2dZfby1zmsbLImJDZ9QIMQWuj34Z54XXYEw=='),
                ('X-Cache', 'Miss from cloudfront'),
                ('Via', '1.1 32454b720dce934befa2d50bacc6d890.cloudfront.net (CloudFront)'),
                ('X-Amz-Cf-Pop', 'LHR50-P5'),
                ('X-Amz-Cf-Id', 'z_rOyDkmglvfxdAKBzL2dZfby1zmsbLImJDZ9QIMQWuj34Z54XXYEw=='),
            ]
            resp = Response(data, status=200)
            for k, v in hdrs:
                resp.headers.add(k, v)
            return resp

        # --- Toy SDK: getUserInfo.nx ----------------------------------------
        @app.route('/toy/sdk/getUserInfo.nx', methods=['POST'])
        @app.route('/api/toy/sdk/getUserInfo.nx', methods=['POST'])
        def toy_get_user_info():
            self.log_request('/toy/sdk/getUserInfo.nx')
            # Return decoded binary blob matching nexon.har
            import base64 as _b64
            b64_payload = (
                "k+4aDsyElgNVMq3WHbvFz50L26Pzv1CFhY5nVa6Toq4WT+yEYOcHcF8ub3ADbx9wp0YzIt4xDVk4RpaKLgp2YXLgn/vazQ+W6z300q0pno0RGsDlP9gQ8BDZpnouryaqUjCBWLhHeIzasQNWLnj8/I1JmHWf6ipzgWSLXlI1FQXMnc4PsVWdAI4Hfp8MUc3QtgbyxuP8DLPZLCHB8+KWJQend9JHlbxiZbXkuWue9+SsplJHDUtQqFR8dfs2PjeIaFukemOSv1XzQLgsu85+e7hrihHJcnv/LxHURmWl47FEf5pMfNrNx86gmyxKN4McUhAQjtXbrTDHZZcOsteQRnZd8OVYGthrj1r0mOs6L19b2o8fWLcuVlsARxtQerxDwfdioYMqKbLYhmh8/ZIFc4+ofgqAMYhaYde9Ue4fAJDdNwibOJVlFh8nNlWN2kFMEO6vnGL90iDceGs8T+bfzg=="
            )
            data = _b64.b64decode(b64_payload)
            return Response(data, status=200, headers={
                'Content-Type': 'text/html; charset=UTF-8',
                'Content-Length': str(len(data)),
                'errorcode': '0',
                'access-control-allow-origin': '*',
                'cache-control': 'private'
            })

        # --- Stub NGS and analytics endpoints ---------------------------------
        # NGS route handlers removed - let the game talk to real NGS servers
        # This allows proper anti-cheat verification while we handle game protocol
        
        # Accept and drop any calls to x-init.ngs.nexon.com (shouldn't be reached now)
        # @app.route('/x-init.ngs.nexon.com/<path:subpath>', methods=['GET', 'POST'])
        # def ngs_init(subpath):
        #     self.log_request(f'/x-init.ngs.nexon.com/{subpath}')
        #     return Response(b'', status=200)

        # Accept and drop any calls to x-update.ngs.nexon.com (shouldn't be reached now)
        # @app.route('/x-update.ngs.nexon.com/<path:subpath>', methods=['GET', 'POST'])
        # def ngs_update(subpath):
        #     self.log_request(f'/x-update.ngs.nexon.com/{subpath}')
        #     return Response(b'', status=200)

        # Stub csauth v1/v2; return empty JSON to satisfy the game (shouldn't be reached now)
        # @app.route('/x-csauth.ngs.nexon.com/v1', methods=['POST'])
        # @app.route('/x-csauth.ngs.nexon.com/v2', methods=['POST'])
        # def ngs_csauth():
        #     self.log_request('/x-csauth.ngs.nexon.com')
        #     return jsonify({})

        # Stub x-config; return empty JSON for any path (shouldn't be reached now)
        # @app.route('/x-config.ngs.nexon.com/<path:subpath>', methods=['GET', 'POST'])
        # def ngs_config(subpath):
        #     self.log_request(f'/x-config.ngs.nexon.com/{subpath}')
        #     return jsonify({})

        # Keep gameclient/log handler for local logs that aren't NGS-specific
        @app.route('/gameclient/log', methods=['POST'])
        def local_gameclient_log():
            self.log_request('/gameclient/log')
            return jsonify({"code": 0})

        # Stub toy.log; accept and drop logs
        @app.route('/toy.log.nexon.io/', methods=['POST'])
        @app.route('/toy.log.nexon.io', methods=['POST'])
        def toy_log():
            self.log_request('/toy.log.nexon.io/')
            return Response("ok", status=200)

        # Stub gTable; return the static configuration captured from the HAR
        @app.route('/gtable.inface.nexon.com/gid/<gid>.json', methods=['GET'])
        def gtable(gid):
            self.log_request(f'/gtable.inface.nexon.com/gid/{gid}.json')
            # Return actual gtable data from real server
            return jsonify({
                "toy_service_id": 2079,
                "arena_product_id": 59754,
                "game_client_id": None,
                "portal_game_code": "1000158",
                "krpc_game_code": 74280,
                "jppc_game_code": None,
                "na_service_id": 1050768977,
                "na_region_host": None,
                "krpc_service_code": None,
                "eve_gameinfo_id": None,
                "twitch_game_id": None,
                "chzzk_game_id": None,
                "project_id": "d8e6e343",
                "guss_service_code": None,
                "guid": "guid",
                "world_id": None,
                "gcid": None,
                "krpc_member_access_code": None,
                "jppc_gm": None,
                "google_oauth_billing_client_redirect_uri": None,
                "krpc_product_type": None,
                "jppc_product_type": None,
                "coin_type": None,
                "alltem_code": "bluearchive",
                "nisms_code": None,
                "nxshop_code": None,
                "google_oauth_billing_client_id": None,
                "google_oauth_billing_client_secret": None,
                "arena_service_code": None,
                "str_env_type": "LIVE",
                "game_release_status": "released",
                "nemo_service_id": None,
                "game_name_ko": "블루 아카이브",
                "game_name_en": "Blue Archive",
                "gid": gid,
                "last_modified": {
                    "modify_date": "2025-09-09T04:33:53.331Z",
                    "admin_no": 441
                },
                "krpc_alltem_code": "bluearchive",
                "created": {
                    "create_date": "2021-10-28T07:35:22.366Z",
                    "admin_no": 2
                }
            })

        @app.route('/gid/<gid>.json', methods=['GET'])
        def gid_config(gid):
            self.log_request(f'/gid/{gid}.json')
            # Same response as gtable but for direct /gid/ requests
            return jsonify({
                "toy_service_id": 2079,
                "arena_product_id": 59754,
                "game_client_id": None,
                "portal_game_code": "1000158",
                "krpc_game_code": 74280,
                "jppc_game_code": None,
                "na_service_id": 1050768977,
                "na_region_host": None,
                "krpc_service_code": None,
                "eve_gameinfo_id": None,
                "twitch_game_id": None,
                "chzzk_game_id": None,
                "project_id": "d8e6e343",
                "guss_service_code": None,
                "guid": "guid",
                "world_id": None,
                "gcid": None,
                "krpc_member_access_code": None,
                "jppc_gm": None,
                "google_oauth_billing_client_redirect_uri": None,
                "krpc_product_type": None,
                "jppc_product_type": None,
                "coin_type": None,
                "alltem_code": "bluearchive",
                "nisms_code": None,
                "nxshop_code": None,
                "google_oauth_billing_client_id": None,
                "google_oauth_billing_client_secret": None,
                "arena_service_code": None,
                "str_env_type": "LIVE",
                "game_release_status": "released",
                "nemo_service_id": None,
                "game_name_ko": "블루 아카이브",
                "game_name_en": "Blue Archive",
                "gid": gid,
                "last_modified": {
                    "modify_date": "2025-09-09T04:33:53.331Z",
                    "admin_no": 441
                },
                "krpc_alltem_code": "bluearchive",
                "created": {
                    "create_date": "2021-10-28T07:35:22.366Z",
                    "admin_no": 2
                }
            })

        # Stub NA configuration; return an empty dict for any key
        @app.route('/config.na.nexon.com/v2/configurations/<path:key>', methods=['GET'])
        def config_na(key):
            self.log_request(f'/config.na.nexon.com/v2/configurations/{key}')
            return jsonify({})

        # NA configuration endpoints (without domain prefix)
        @app.route('/v2/configurations/na_time_sync', methods=['GET'])
        def na_time_sync():
            self.log_request('/v2/configurations/na_time_sync')
            # Return proper time sync data matching the real server format
            return jsonify({
                "log-server": "livelog.nexon.com",
                "nxlog-flag": "true", 
                "ip-address": "127.0.0.1",  # Use localhost for local dev
                "time": datetime.now().strftime('%Y-%m-%d %H:%M:%S.%f')[:-3]
            })

        @app.route('/v2/configurations/na_grclist_query', methods=['GET'])
        def na_grclist_query():
            self.log_request('/v2/configurations/na_grclist_query')
            # Return the actual anti-cheat process detection list from the real server
            # This is NOT game server configuration - it's process monitoring for anti-cheat
            grc_data = [
                {"code": "A901", "caption": "WorldClass", "process": "WCCustomer.exe", "updated": "2025-03-21T16:30:01"},
                {"code": "M901", "caption": "WmCltMain", "process": "WmClt.exe", "updated": "2025-03-21T16:36:16"},
                {"code": "M902", "caption": "", "process": "dlx5.exe", "updated": "2025-03-21T16:37:19"},
                {"code": "M903", "caption": "피카라이브", "process": "pmclient.exe", "updated": "2020-12-18T15:39:47"},
                {"code": "M904", "caption": "", "process": "winCLI.exe", "updated": "2025-03-21T16:39:36"},
                {"code": "M909", "caption": "PicaManagerClient", "process": "pmClientSetup.exe", "updated": "2025-02-27T17:49:55"},
                {"code": "T901", "caption": "", "process": "foreLauncher.exe", "updated": "2025-03-21T16:38:28"},
                {"code": "T902", "caption": "SLauncherUI", "process": "SLauncher.exe", "updated": "2025-03-21T17:04:19"},
                {"code": "T903", "caption": "리치런처 시즌5", "process": "RichLauncher_S5.exe", "updated": "2020-12-18T15:39:47"},
                {"code": "T904", "caption": "EVERRUN Launcher", "process": "EverRun.exe", "updated": "2020-12-18T15:39:47"},
                {"code": "T905", "caption": "", "process": "XWall.exe", "updated": "2025-04-03T14:33:28"},
                {"code": "T906", "caption": "", "process": "PFDesktopClient.exe", "updated": "2020-12-18T15:39:47"},
                {"code": "T909", "caption": "NoxMobileLauncher", "process": "MobileLauncher.exe", "updated": "2025-03-21T16:40:18"},
                {"code": "T910", "caption": "", "process": "PICALauncherMini.exe", "updated": "2020-12-18T15:39:47"},
                {"code": "T911", "caption": "", "process": "barclientview.exe", "updated": "2025-03-19T18:23:44"},
                # Truncated for brevity - add more if needed
            ]
            return jsonify(grc_data)

        @app.route('/com.nexon.bluearchive/server_config/blacklist.csv', methods=['GET'])
        def blacklist_csv():
            self.log_request('/com.nexon.bluearchive/server_config/blacklist.csv')
            # Return empty blacklist CSV
            return Response("", status=200, headers={
                'Content-Type': 'text/csv',
                'Content-Length': '0'
            })

        @app.route('/com.nexon.bluearchive/server_config/chattingblacklist.csv', methods=['GET'])
        def chatting_blacklist_csv():
            self.log_request('/com.nexon.bluearchive/server_config/chattingblacklist.csv')
            # Return empty chatting blacklist CSV
            return Response("", status=200, headers={
                'Content-Type': 'text/csv',
                'Content-Length': '0'
            })

        @app.route('/com.nexon.bluearchive/server_config/whitelist.csv', methods=['GET'])
        def whitelist_csv():
            self.log_request('/com.nexon.bluearchive/server_config/whitelist.csv')
            # Return empty whitelist CSV
            return Response("", status=200, headers={
                'Content-Type': 'text/csv',
                'Content-Length': '0'
            })

        @app.route('/crash-reporting-api-rs26-usw2.cloud.unity3d.com/api/reporting/v1/projects/<project>/events', methods=['POST'])
        def crash_reporting(project):
            self.log_request(f'/crash-reporting (project: {project})')
            return jsonify({"status": "ok"})

        # Initialize NGS endpoint response cache
        self.ngs_responses = {
            'v1': {},  # request_hash -> response mapping
            'v2': {}
        }
        
        @app.route('/<path:path>', methods=['GET', 'POST'])
        def catch_all(path):
            self.log_request(f'/{path}')
            
            # Handle specific NGS endpoints with dynamic response mapping
            if path in ['v2', 'v1']:
                request_body = request.get_data(as_text=True) if request.method == 'POST' else ''
                response = self._handle_ngs_endpoint(path, request_body)
                
                if response:
                    print_colored(f"NGS {path}: Returning mapped response ({len(response)} chars)", CYAN)
                    return Response(response, status=200, headers={
                        'Content-Type': 'application/json',
                        'Content-Length': str(len(response))
                    })
                else:
                    print_colored(f"NGS {path}: No mapping found, returning 500", RED)
                    return Response("Internal Server Error", status=500)
            
            # Handle stamp endpoints
            if path.startswith('stamp/'):
                print_colored(f"Handling stamp endpoint: /{path}", YELLOW)
                if 'restore/steam' in path:
                    return jsonify({"restore_stamps": []})
                elif 'restore/mp' in path:
                    return jsonify({})
                return jsonify({"status": "ok"})
            
            print_colored(f"Caught: /{path}", BOLD + MAGENTA)
            return jsonify({"status": "handled", "path": path})

        return app

    def log_request(self, endpoint):
        try:
            from flask import request
            timestamp = datetime.now().strftime('%H:%M:%S')
            method = getattr(request, 'method', 'GET')
            remote_addr = getattr(request, 'remote_addr', 'unknown')
            print_colored(f"[{timestamp}] {method} {endpoint} from {remote_addr}", CYAN)

            if method == 'POST':
                try:
                    payload = request.get_data()
                    if payload:
                        print_colored(f"  Payload: {len(payload)} bytes", BLUE)
                        if self.crypto_available:
                            fb_info = self.analyze_flatbuffer_payload(payload)
                            if fb_info:
                                print_colored(f"  FlatBuffer hint: {fb_info}", MAGENTA)
                except Exception as e:
                    print_colored(f"  Payload analysis failed: {e}", YELLOW)
        except Exception as e:
            print_colored(f"[log error] {endpoint}: {e}", RED)

    def analyze_flatbuffer_payload(self, payload):
        try:
            if not payload or len(payload) < 8:
                return None

            import struct
            offset = struct.unpack('<I', payload[:4])[0]
            if offset >= len(payload):
                return None

            byte_counts = [0] * 256
            sample_size = min(1024, len(payload))
            for b in payload[:sample_size]:
                byte_counts[b] += 1

            total = sum(byte_counts)
            entropy = -sum((count/total) * (count/total).bit_length()
                           for count in byte_counts if count > 0)

            return {
                "size": len(payload),
                "offset": offset,
                "encrypted": entropy > 7.5,
                "entropy": round(entropy, 2)
            }
        except Exception as e:
            print_colored(f"  FlatBuffer analysis failed: {e}", YELLOW)
            return None

    def create_game_config(self):
        return {
            "errorCode": 0,
            "result": {
                "service": {
                    "title": "Blue Archive",
                    "buildVer": self.current_version,
                    "policyApiVer": "2",
                    "termsApiVer": "2",
                    "useTPA": 0,
                    "useGbNpsn": 1,
                    "useGbKrpc": 1,
                    "useGbArena": 1,
                    "usePlayNow": 1,
                    "usePcDirectRun": 1,
                    "clientId": "MjcwOA",
                    "useMemberships": [101, 103, 110, 107, 9999]
                },
                "country": "US",
                "userArenaRegion": 1,
                "enableLogging": False,
                "advanced": {
                    "cryptoSupport": self.crypto_available,
                    "version": self.current_version,
                    "protocolSupport": ["flatbuffer", "xor_encryption", "nexon_api"]
                }
            },
            "errorText": "Success"
        }

    def _load_fixed_webtoken(self):
        # 1) Environment variable takes priority
        tok = os.environ.get('IAS_FIXED_WEBTOKEN')
        if tok and tok.strip():
            print_colored("Using IAS_FIXED_WEBTOKEN from environment.", YELLOW)
            return tok.strip()
        # 2) Fallback: try to read from requests/5.txt (header capture)
        try:
            root = Path(__file__).parent
            p = root / 'requests' / '5.txt'
            if p.exists():
                txt = p.read_text(encoding='utf-8', errors='ignore')
                for line in txt.splitlines():
                    if line.lower().startswith('ias-game-token:'):
                        return line.split(':', 1)[1].strip()
        except Exception:
            pass
        return None

class AntiCheatServer:
    def create_flask_app(self):
        try:
            from flask import Flask, jsonify
        except ImportError:
            return None

        app = Flask(__name__)

        @app.route('/', defaults={'path': ''})
        @app.route('/<path:path>', methods=['GET', 'POST', 'PUT', 'DELETE'])
        def anti_cheat_handler(path):
            print_colored(f"[AC] /{path}", YELLOW)
            return jsonify({"status": "ok", "anti_cheat": True})

        return app

def check_port_available(port):
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.settimeout(1)
            result = s.connect_ex(('localhost', port))
            return result != 0
    except:
        return False

def _get_ssl_hostnames():
    # Domains the client will connect to; include localhost for manual testing
    hostnames = {
        'localhost',
        '127.0.0.1',
        'public.api.nexon.com',
    'signin.nexon.com',
        'prod-noticepool.game.nexon.com',
        'nxm-eu-bagl.nexon.com',
        'nxm-ios-bagl.nexon.com',
        'nxm-kr-bagl.nexon.com',
        'nxm-tw-bagl.nexon.com',
        'nxm-th-bagl.nexon.com',
        'nxm-or-bagl.nexon.com',
        'crash-reporting-api-rs26-usw2.cloud.unity3d.com',
    # Added NGS/analytics/config endpoints to avoid TLS name mismatch
    'x-init.ngs.nexon.com',
    'x-update.ngs.nexon.com',
    'x-csauth.ngs.nexon.com',
    'x-config.ngs.nexon.com',
    'psm-log.ngs.nexon.com',
    'toy.log.nexon.io',
    'gtable.inface.nexon.com',
    'config.na.nexon.com',
    # SDK and CDN endpoints sometimes contacted directly by the client
    'bolo7yechd.execute-api.ap-northeast-1.amazonaws.com',
    'nexon-sdk.nexon.com',
    'api-pub.nexon.com',
    'member.nexon.com',
    'sdk-push.mp.nexon.com',
    'ba.dn.nexoncdn.co.kr',
    'd2vaidpni345rp.cloudfront.net',
    'prod-noticeview.bluearchiveyostar.com',
    'yostarjp.s3-ap-northeast-1.amazonaws.com',
    'yostar-serverinfo.bluearchiveyostar.com',
    'ba-gl-web.bluearchiveyostar.com',
    'ba-gl-kor-web.bluearchiveyostar.com',
    '54.238.121.146',
    }
    return sorted(hostnames)

def _get_cert_paths():
    from pathlib import Path as _Path
    cert_dir = _Path(__file__).parent / 'certs'
    cert_path = cert_dir / 'selfsigned_cert.pem'
    key_path = cert_dir / 'selfsigned_key.pem'
    return cert_path, key_path

def _install_cert_windows(cert_path):
    try:
        if platform.system() != "Windows":
            return False
        if not is_admin():
            print_colored("Admin required to trust certificate automatically.", YELLOW)
            return False
        if not cert_path.exists():
            return False
        # Use certutil to add to Trusted Root Certification Authorities
        result = subprocess.run(["certutil", "-addstore", "root", str(cert_path)], capture_output=True, text=True)
        if result.returncode == 0:
            print_colored("Trusted self-signed certificate in Windows Root store.", GREEN)
            return True
        else:
            print_colored(f"certutil failed: {result.stderr.strip()}", YELLOW)
            return False
    except Exception as e:
        print_colored(f"certutil error: {e}", YELLOW)
        return False

def start_server_thread(app, port, name, use_ssl=False):
    def run_server():
        try:
            # Silence Flask/Werkzeug's annoying startup messages
            import logging
            log = logging.getLogger('werkzeug')
            log.setLevel(logging.ERROR)
            
            if use_ssl:
                ssl_context = create_ssl_context(_get_ssl_hostnames())
                if ssl_context:
                    print_colored(f"{name} server starting on port {port} (HTTPS)", CYAN)
                    app.run(host='0.0.0.0', port=port, debug=False, use_reloader=False, ssl_context=ssl_context)
                else:
                    print_colored(f"{name} server starting on port {port} (HTTP, SSL init failed)", YELLOW)
                    app.run(host='0.0.0.0', port=port, debug=False, use_reloader=False)
            else:
                print_colored(f"{name} server starting on port {port}", CYAN)
                app.run(host='0.0.0.0', port=port, debug=False, use_reloader=False)
        except Exception as e:
            print_colored(f"{name} server failed: {e}", RED)

    thread = threading.Thread(target=run_server, daemon=True)
    thread.start()
    return thread

def create_ssl_context(hostnames=None):
    try:
        import ssl
        from cryptography import x509
        from cryptography.x509.oid import NameOID
        from cryptography.hazmat.primitives import hashes
        from cryptography.hazmat.primitives.asymmetric import rsa
        from cryptography.hazmat.primitives import serialization
        import datetime
        import ipaddress
        from pathlib import Path as _Path

        # Reuse persistent cert/key if they exist
        cert_dir = _Path(__file__).parent / 'certs'
        cert_dir.mkdir(exist_ok=True)
        cert_path = cert_dir / 'selfsigned_cert.pem'
        key_path = cert_dir / 'selfsigned_key.pem'

        if cert_path.exists() and key_path.exists():
            context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
            context.load_cert_chain(str(cert_path), str(key_path))
            return context

        private_key = rsa.generate_private_key(public_exponent=65537, key_size=2048)

        subject = issuer = x509.Name([
            x509.NameAttribute(NameOID.COUNTRY_NAME, "US"),
            x509.NameAttribute(NameOID.STATE_OR_PROVINCE_NAME, "CA"),
            x509.NameAttribute(NameOID.LOCALITY_NAME, "San Francisco"),
            x509.NameAttribute(NameOID.ORGANIZATION_NAME, "Blue Archive Server"),
            x509.NameAttribute(NameOID.COMMON_NAME, "localhost"),
        ])

        # Build SANs including all expected hostnames to satisfy SNI checks
        names = {"localhost", "127.0.0.1"}
        if hostnames:
            names.update(set(hostnames))
        san_entries = []
        for hn in sorted(names):
            try:
                san_entries.append(x509.IPAddress(ipaddress.ip_address(hn)))
            except ValueError:
                san_entries.append(x509.DNSName(hn))

        cert = (
            x509.CertificateBuilder()
            .subject_name(subject)
            .issuer_name(issuer)
            .public_key(private_key.public_key())
            .serial_number(x509.random_serial_number())
            .not_valid_before(datetime.datetime.utcnow())
            .not_valid_after(datetime.datetime.utcnow() + datetime.timedelta(days=365))
            .add_extension(x509.SubjectAlternativeName(san_entries), critical=False)
            .sign(private_key, hashes.SHA256())
        )

        # Persist to disk so the certificate can be trusted via Windows cert store
        with open(cert_path, 'wb') as cf:
            cf.write(cert.public_bytes(serialization.Encoding.PEM))
        with open(key_path, 'wb') as kf:
            kf.write(
                private_key.private_bytes(
                    encoding=serialization.Encoding.PEM,
                    format=serialization.PrivateFormat.PKCS8,
                    encryption_algorithm=serialization.NoEncryption(),
                )
            )

        context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        context.load_cert_chain(str(cert_path), str(key_path))
        return context

    except ImportError:
        print_colored("cryptography isn't installed; HTTPS disabled.", YELLOW)
        return None
    except Exception as e:
        print_colored(f"SSL setup failed: {e}", YELLOW)
        return None

def main():
    print_colored("Blue Archive Private Server", BOLD + CYAN)
    print_colored("=" * 30, CYAN)
    print_colored("Based on K0lb3's protocol notes", CYAN)
    print_colored("=" * 30, CYAN)
    sys.stdout.flush()
    sys.stderr.flush()

    print_colored("\nChecking prerequisites...", YELLOW)
    sys.stdout.flush()

    admin = is_admin()
    if admin:
        print_colored("Running as administrator.", GREEN)
    else:
        print_colored("Not running as administrator.", YELLOW)
        print_colored("Automatic domain setup may fail.", YELLOW)

    print_colored("\nSetting up domain redirects...", YELLOW)
    sys.stdout.flush()
    hosts_manager = HostsManager()

    if admin:
        if hosts_manager.add_redirects():
            print_colored("Domain redirects configured.", GREEN)
            sys.stdout.flush()
        else:
            print_colored("Domain setup failed.", RED)
            hosts_manager.show_manual_instructions()
    else:
        print_colored("Admin rights needed for automatic setup.", YELLOW)
        try:
            response = input("Show manual instructions? (y/n): ").lower().strip()
            if response in ['y', 'yes']:
                hosts_manager.show_manual_instructions()
        except KeyboardInterrupt:
            print_colored("\nSetup cancelled.", YELLOW)
            return 1

    print_colored("\nInstalling dependencies and protocol files...", YELLOW)
    sys.stdout.flush()
    dep_manager = DependencyManager()
    if not dep_manager.setup_environment():
        print_colored("Environment setup failed.", RED)
        sys.stdout.flush()
        input("Press Enter to exit...")
        return 1

    # Ensure certificate exists on disk and try to trust it on Windows
    try:
        ctx = create_ssl_context(_get_ssl_hostnames())
        if ctx is None:
            print_colored("TLS context unavailable; HTTPS may be disabled.", YELLOW)
        cert_path, _ = _get_cert_paths()
        _install_cert_windows(cert_path)
    except Exception as e:
        print_colored(f"Certificate setup step failed: {e}", YELLOW)

    print_colored("\nStarting services...", GREEN)
    sys.stdout.flush()

    ba_server = BlueArchiveServer()
    main_app = ba_server.create_flask_app()
    if not main_app:
        print_colored("Failed to create main server app.", RED)
        sys.stdout.flush()
        return 1

    ac_server = AntiCheatServer()
    ac_app = ac_server.create_flask_app()

    # Required ports
    main_port = 443
    #api_port = 5000
    #gateway_port = 5100
    ac_port = 58880

    # Hard fail if any required port is unavailable
    #unavailable = []
    #if not check_port_available(main_port):
    #    unavailable.append(f"{main_port} (Main)")
    #if not check_port_available(api_port):
    #    unavailable.append(f"{api_port} (API)")
    #if not check_port_available(gateway_port):
    #    unavailable.append(f"{gateway_port} (Gateway)")
    #if ac_app and not check_port_available(ac_port):
    #    unavailable.append(f"{ac_port} (Anti-cheat)")
    #if unavailable:
    #    print_colored("Required port(s) unavailable: " + ", ".join(unavailable), RED)
    #    print_colored("All ports must be free and correct. Exiting.", RED)
    #    return 1

    use_ssl_main = True
    print_colored(f"\nStarting main server on {main_port}...", GREEN)
    sys.stdout.flush()
    main_thread = start_server_thread(main_app, main_port, "Main", use_ssl=use_ssl_main)

    # Do not start Python on 5000/5100; those belong to C# API
    api_thread = None
    gateway_thread = None

    ac_thread = None
    if ac_app:
        print_colored(f"Starting anti-cheat mock on {ac_port}...", GREEN)
        ac_thread = start_server_thread(ac_app, ac_port, "Anti-cheat", use_ssl=False)

    # Give servers a moment to bind, then verify
    time.sleep(2)
    failed = []
    if not (main_thread and main_thread.is_alive()):
        failed.append("Main")
    # API/Gateway are hosted by C#; no need to check here
    if ac_app and not (ac_thread and ac_thread.is_alive()):
        failed.append("Anti-cheat")
    if failed:
        print_colored("Failed to start: " + ", ".join(failed), RED)
        return 1

    print_colored("\nServer is up.", BOLD + GREEN)
    print_colored("=" * 30, GREEN)
    main_proto = "https" if use_ssl_main else "http"
    print_colored(f"Main:    {main_proto}://localhost:{main_port}", CYAN)
    # API and Gateway are started with TLS unconditionally above
    print_colored(f"API:     https://localhost:5000 (C#)", CYAN)
    if ac_app:
        print_colored(f"AC:      http://localhost:{ac_port}", CYAN)
    print_colored(f"Crypto:  {'enabled' if ba_server.crypto_available else 'basic'}", MAGENTA)
    print_colored(f"Version: {ba_server.current_version}", BLUE)
    print_colored("=" * 30, GREEN)
    sys.stdout.flush()
    sys.stderr.flush()

    print_colored("\nNetwork notes:", CYAN)
    print_colored("The game generally expects HTTPS on 443.", WHITE)

    if admin:
        print_colored("\nSetup complete. Launch the game.", BOLD + GREEN)
    else:
        print_colored("\nManual steps needed:", YELLOW)
        print_colored("1) Configure domain redirects (see above).", WHITE)
        print_colored("2) Launch the game.", WHITE)

    print_colored("3) Watch this console for requests.", WHITE)

    try:
        print_colored("\nCtrl+C to stop.", CYAN)
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print_colored("\nStopping servers...", YELLOW)
        return 0

if __name__ == '__main__':
    sys.exit(main())
