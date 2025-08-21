import os
import sys
import json
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
        # Additional NGS/analytics/config endpoints
        'x-init.ngs.nexon.com',
        'x-update.ngs.nexon.com',
        'x-csauth.ngs.nexon.com',
        'x-config.ngs.nexon.com',
        'psm-log.ngs.nexon.com',
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
        'cryptography'
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
                importlib.import_module(package.replace('-', '_'))
            except ImportError:
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

        missing_packages = self.check_python_packages()
        if missing_packages:
            if not self.install_packages(missing_packages):
                return False
        else:
            print_colored("Python packages look fine.", GREEN)

        if not self.download_k0lb3_files():
            return False

        print_colored("Environment ready.", GREEN)
        return True

class BlueArchiveServer:
    def __init__(self):
        self.current_version = "1.35.115378"
        self.sessions = {}
        self.player_data = {}
        self.crypto_available = self._setup_crypto()
        # Simple in-memory queue state to mimic gateway behavior
        self.queue_ticket_seq = 114980000
        self.queue_allowed_seq = 114980000
        # Minimal persistent account store
        self.data_dir = Path(__file__).parent / 'data'
        self.data_dir.mkdir(exist_ok=True)
        self.accounts_path = self.data_dir / 'accounts.json'
        self.accounts = self._load_accounts()
        # Map transient IAS tickets to a stable user key to avoid new accounts each run
        self.ticket_map = {}
        self.current_user_key = None
        # HAR logging setup
        self._har_entries = []
        self._har_logfile = f"server_log_{int(time.time())}.har"
        self._create_initial_har_file()
        # One-time migration: ensure there is a stable default account key
        try:
            if isinstance(self.accounts, dict) and self.accounts and 'uid:default' not in self.accounts:
                some_key = next(iter(self.accounts.keys()))
                self.accounts['uid:default'] = self.accounts.get(some_key)
                self._save_accounts()
        except Exception as e:
            print_colored(f"Account migration skipped: {e}", YELLOW)

    def _load_accounts(self):
        try:
            if self.accounts_path.exists():
                with open(self.accounts_path, 'r', encoding='utf-8') as f:
                    accounts = json.load(f)
                print_colored(f"Loaded {len(accounts)} accounts from {self.accounts_path}", GREEN)
                return accounts
            else:
                print_colored("No existing accounts file found - starting fresh", YELLOW)
        except Exception as e:
            print_colored(f"Failed to load accounts: {e}", YELLOW)
        return {}

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

    def _save_accounts(self):
        """Save accounts to disk with atomic write to prevent corruption"""
        try:
            # Create a temporary file first to ensure atomic write
            tmp_path = self.accounts_path.with_suffix('.tmp')
            with open(tmp_path, 'w', encoding='utf-8') as f:
                json.dump(self.accounts, f, ensure_ascii=False, indent=2)
            
            # Atomic replace - this prevents corruption if the process is interrupted
            os.replace(tmp_path, self.accounts_path)
            
            print_colored(f"Saved {len(self.accounts)} accounts to disk", GREEN)
        except Exception as e:
            print_colored(f"Failed to save accounts: {e}", YELLOW)
            # Try to clean up the temp file if it exists
            try:
                tmp_path = self.accounts_path.with_suffix('.tmp')
                if tmp_path.exists():
                    tmp_path.unlink()
            except Exception:
                pass

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
        ticket_key = hashlib.sha256((ticket or '').encode('utf-8')).hexdigest()
        # Resolve to a stable user key if we have one
        user_key = self.ticket_map.get(ticket_key)
        if not user_key:
            user_key = self.current_user_key
            if not user_key:
                # Fallback to a default singleton account key to avoid spam
                user_key = 'uid:default'
            # Remember this mapping for this process lifetime
            self.ticket_map[ticket_key] = user_key

        if user_key in self.accounts:
            acct = self.accounts[user_key]
        else:
            # Create a new account under the stable user_key
            platform_user_id, guid, user64 = self._derive_ids_from_token(ticket or user_key, gid)
            acct = {
                "gid": gid,
                "guid": guid,
                "npSN": guid,
                "umKey": f"107:{platform_user_id}",
                "platform_type": "ARENA",
                "platform_user_id": platform_user_id,
                "name": "Skye",
                "level": 1,
                "attribute": [],
                "created_at": int(_t.time()),
                "updated_at": int(_t.time()),
                "last_login": None,
            }
            self.accounts[user_key] = acct
            self._save_accounts()
        return acct, user_key

    def _setup_crypto(self):
        try:
            lib_path = Path(__file__).parent / 'lib'
            if lib_path.exists():
                sys.path.insert(0, str(lib_path))
            import flatbuffers  # noqa: F401
            import xxhash       # noqa: F401
            from Crypto.Util.strxor import strxor  # noqa: F401
            # Import K0lb3's encryption helper
            from lib import TableEncryptionService
            self.table_crypto = TableEncryptionService
            return True
        except ImportError as e:
            print_colored(f"Advanced crypto not available: {e}", YELLOW)
            return False

    def _encrypt_nexon_response(self, json_data, endpoint_name="default"):
        """Encrypt JSON response using K0lb3's TableEncryptionService for Nexon ToySDK"""
        if not self.crypto_available:
            return json.dumps(json_data).encode('utf-8')
        
        try:
            # Convert JSON to string
            json_str = json.dumps(json_data, separators=(',', ':'))
            
            # Use TableEncryptionService to encrypt the string
            # The endpoint name is used as the encryption key seed
            encrypted_bytes = self.table_crypto.XOR(endpoint_name, json_str.encode('utf-8'))
            
            # Return base64 encoded encrypted data
            import base64
            return base64.b64encode(encrypted_bytes)
            
        except Exception as e:
            print_colored(f"Encryption failed for {endpoint_name}: {e}", RED)
            # Fall back to plain JSON if encryption fails
            return json.dumps(json_data).encode('utf-8')

    def create_flask_app(self):
        try:
            from flask import Flask, request, jsonify, Response
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
        def server_config(config_name):
            # Accept any clientId_Live.json the game asks for and feed it our endpoints
            self.log_request(f'/com.nexon.bluearchivesteam/server_config/{config_name}.json')
            try:
                # Extract clientId (number) if present
                client_id = config_name.split('_', 1)[0]
                if not client_id.isdigit():
                    client_id = "364258"
            except Exception:
                client_id = "364258"

            # Build the ConnectionGroupsJson payload as a JSON string (game expects stringified JSON)
            groups = [
                {
                    "Name": "live",
                    "OverrideConnectionGroups": [
                        {
                            "Name": "global",
                            "ApiUrl": "https://nxm-eu-bagl.nexon.com:5000/api/",
                            "GatewayUrl": "https://nxm-eu-bagl.nexon.com:5100/api/",
                            "NXSID": "live-global"
                        }
                    ]
                }
            ]
            try:
                groups_str = json.dumps(groups, separators=(",", ":"))
            except Exception:
                groups_str = "[]"

            server_config = {
                "DefaultConnectionGroup": "live",
                "DefaultConnectionMode": "no",
                "ConnectionGroupsJson": groups_str,
                "desc": f"{self.current_version}"
            }
            print_colored("Server config served.", BOLD + GREEN)
            return jsonify(server_config)

        @app.route('/toy/sdk/getCountry.nx', methods=['POST'])
        def get_country():
            self.log_request('/toy/sdk/getCountry.nx')
            payload = request.get_data()
            if payload and self.crypto_available:
                self.analyze_flatbuffer_payload(payload)

            country_response = {
                "result": {
                    "country": "GB",
                    "language": "en"
                },
                "errorCode": 0
            }

            # Use proper encryption for crypto-enabled responses
            data = server_instance._encrypt_nexon_response(country_response, "getCountry")
            return Response(data, status=200, headers={
                'Content-Type': 'text/html; charset=UTF-8',
                'Content-Length': str(len(data)),
                'errorcode': '0',
                'access-control-allow-origin': '*',
                'cache-control': 'private',
                'date': datetime.now().strftime('%a, %d %b %Y %H:%M:%S GMT'),
                'x-ba-country': 'GB'
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

        @app.route('/api/<path:endpoint>', methods=['GET', 'POST', 'PUT', 'DELETE', 'PATCH'])
        def api_endpoint(endpoint):
            self.log_request(f'/api/{endpoint}')
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
            
            # Encrypt the response using K0lb3's helper
            data = server_instance._encrypt_nexon_response(enter_toy_response, "enterToy")
            date_val = datetime.now().strftime('%a, %d %b %Y %H:%M:%S GMT')
            hdrs = [
                ('Content-Type', 'text/html; charset=UTF-8'),
                ('Content-Length', str(len(data))),
                ('Connection', 'keep-alive'),
                ('date', date_val),
                ('access-control-allow-origin', '*'),
                ('errorcode', '0'),
                ('cache-control', 'private'),
                ('x-envoy-upstream-service-time', '337'),
                ('inface-wasm-filter', '1.8.0'),
                ('server', 'inface'),
                ('x-request-id', 'x9lP5bJD0dJV-n0WRuOl9xgOFYwBIKxHt4we2iatDS1y3EqEuUhtvA=='),
                ('X-Cache', 'Miss from cloudfront'),
                ('Via', '1.1 ecb6880220cec19d7d48fb6d26bdb1f6.cloudfront.net (CloudFront)'),
                ('X-Amz-Cf-Pop', 'LHR50-P5'),
                ('X-Amz-Cf-Id', 'x9lP5bJD0dJV-n0WRuOl9xgOFYwBIKxHt4we2iatDS1y3EqEuUhtvA=='),
            ]
            resp = Response(data, status=200)
            for k, v in hdrs:
                resp.headers.add(k, v)
            return resp

        @app.route('/api/toy/sdk/enterToy.nx', methods=['POST'])
        def enter_toy_api():
            return enter_toy()

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

        @app.route('/toy/sdk/getPromotion.nx', methods=['POST'])
        def get_promotion():
            self.log_request('/toy/sdk/getPromotion.nx')
            payload = request.get_data()
            if payload and self.crypto_available:
                self.analyze_flatbuffer_payload(payload)

            promotion_response = {
                "errorCode": 0,
                "result": {
                    "promotions": [],
                    "banners": []
                },
                "errorText": "Success"
            }

            print_colored("Promotion request.", CYAN)

            # Use proper encryption for crypto-enabled responses
            data = server_instance._encrypt_nexon_response(promotion_response, "getPromotion")
            return Response(data, status=200, headers={
                'Content-Type': 'text/html; charset=UTF-8',
                'Content-Length': str(len(data)),
                'errorcode': '0',
                'access-control-allow-origin': '*',
                'cache-control': 'private'
            })

        @app.route('/api/toy/sdk/getPromotion.nx', methods=['POST'])
        def get_promotion_api():
            return get_promotion()

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
            # Fabricate a plausible web_token similar to production shape
            try:
                import uuid
                now_ms = int(time.time() * 1000)
                web_token = f"ias:wt:{now_ms}:1247143115@{uuid.uuid4()}@{platform}:ANA"
            except Exception:
                web_token = "ias:wt:0:1247143115@00000000-0000-0000-0000-000000000000@STEAM:ANA"

            # If we can infer a stable user id, keep it; else fallback
            local_session_user_id = "76561198000000000"
            lpt = body.get('link_platform_token')
            if isinstance(lpt, str) and len(lpt) > 32:
                # Derive a deterministic 17-digit number from token for stability
                import hashlib
                h = hashlib.sha256(lpt.encode('utf-8')).hexdigest()
                # Map hex to 17-digit range resembling SteamID64
                as_int = int(h[:16], 16)
                base = 76561197960265728  # SteamID64 base
                local_session_user_id = str(base + (as_int % 10**10))

            # Fix the current_user_key for the session based on link token so accounts persist
            try:
                self.current_user_key = f"uid:{local_session_user_id}"
            except Exception:
                pass

            resp = {
                "web_token": web_token,
                "local_session_type": "arena",
                "local_session_user_id": local_session_user_id,
            }
            return jsonify(resp)

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

        # --- Gateway API stub (/api/gateway) ----------------------------------
        def _forward_to_csharp(target_path: str):
            try:
                import requests as _req
            except Exception:
                _req = None
            try:
                from flask import request as _rq
                url = f"http://127.0.0.1:7000{target_path}"
                data = _rq.get_data(cache=False)
                # Filter hop-by-hop headers
                drop = {"host", "content-length", "connection", "accept-encoding"}
                fwd_headers = {k: v for k, v in _rq.headers.items() if k.lower() not in drop}
                if _req:
                    r = _req.request(_rq.method, url, params=_rq.args, data=data, headers=fwd_headers, timeout=15)
                    resp = Response(r.content, status=r.status_code)
                    for k, v in r.headers.items():
                        lk = k.lower()
                        if lk in ("content-length", "transfer-encoding", "connection", "content-encoding"):
                            continue
                        resp.headers.add(k, v)
                    resp.headers['Content-Length'] = str(len(r.content))
                    return resp
            except Exception as e:
                print_colored(f"[proxy] Forward failed: {e}", YELLOW)
            return jsonify({"error": "csharp_unavailable"}), 502

        @app.route('/api/gateway', methods=['POST'])
        def api_gateway():
            self.log_request('/api/gateway')
            return _forward_to_csharp('/api/gateway')

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
            data = server_instance._encrypt_nexon_response(body, "webtoken")
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
            gid = (request.headers.get('gid') or request.headers.get('Gid') or '2079')
            ticket = request.headers.get('x-ias-ticket') or request.headers.get('X-Ias-Ticket') or ''
            acct, _ = self._get_or_create_account(ticket, gid)
            
            # Update account metadata (this is what Nexon does internally)
            import datetime as _dt
            acct['last_login'] = _dt.datetime.utcnow().strftime('%Y-%m-%dT%H:%M:%S.0000000Z')
            acct['updated_at'] = int(time.time())
            
            # Persist the account changes to disk
            self._save_accounts()
            
            resp = {
                "links": [
                    {
                        "platform_type": acct.get("platform_type", "ARENA"),
                        "platform_user_id": acct["platform_user_id"],
                        "guid": acct["guid"],
                        "is_primary": True,
                        "primary_platform_at": int(time.time() * 1000),
                        "game_data": {
                            "guid": acct["guid"],
                            "name": acct.get("name", "Player"),
                            "level": acct.get("level", 1),
                            "attribute": acct.get("attribute", []),
                            "date_last_login": acct['last_login']
                        }
                    }
                ]
            }
            return jsonify(resp)

        # --- Sign in with IAS ticket (toy sdk) --------------------------------
        @app.route('/toy/sdk/signInWithTicket.nx', methods=['POST'])
        @app.route('/api/toy/sdk/signInWithTicket.nx', methods=['POST'])
        def toy_sign_in_with_ticket():
            self.log_request('/toy/sdk/signInWithTicket.nx')
            gid = (request.headers.get('gid') or request.headers.get('Gid') or '2079')
            ticket = request.headers.get('ias-ticket') or request.headers.get('IAS-Ticket') or ''
            acct, _ = self._get_or_create_account(ticket, gid)
            
            # Update account metadata on sign-in
            acct['updated_at'] = int(time.time())
            
            # Persist the account changes to disk
            self._save_accounts()
            
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
            data = server_instance._encrypt_nexon_response(body, "signInWithTicket")
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
            data = server_instance._encrypt_nexon_response(resp, "terms")
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
        # Accept and drop any calls to x-init.ngs.nexon.com
        @app.route('/x-init.ngs.nexon.com/<path:subpath>', methods=['GET', 'POST'])
        def ngs_init(subpath):
            self.log_request(f'/x-init.ngs.nexon.com/{subpath}')
            return Response(b'', status=200)

        # Accept and drop any calls to x-update.ngs.nexon.com
        @app.route('/x-update.ngs.nexon.com/<path:subpath>', methods=['GET', 'POST'])
        def ngs_update(subpath):
            self.log_request(f'/x-update.ngs.nexon.com/{subpath}')
            return Response(b'', status=200)

        # Stub csauth v1/v2; return empty JSON to satisfy the game
        @app.route('/x-csauth.ngs.nexon.com/v1', methods=['POST'])
        @app.route('/x-csauth.ngs.nexon.com/v2', methods=['POST'])
        def ngs_csauth():
            self.log_request('/x-csauth.ngs.nexon.com')
            return jsonify({})

        # Stub x-config; return empty JSON for any path
        @app.route('/x-config.ngs.nexon.com/<path:subpath>', methods=['GET', 'POST'])
        def ngs_config(subpath):
            self.log_request(f'/x-config.ngs.nexon.com/{subpath}')
            return jsonify({})

        # Stub psm‑log; accept and drop analytics logs
        @app.route('/psm-log.ngs.nexon.com/gameclient/log', methods=['POST'])
        def psm_log():
            self.log_request('/psm-log.ngs.nexon.com/gameclient/log')
            return Response(b'', status=200)

        # Stub toy.log; accept and drop logs
        @app.route('/toy.log.nexon.io/', methods=['POST'])
        @app.route('/toy.log.nexon.io', methods=['POST'])
        def toy_log():
            self.log_request('/toy.log.nexon.io/')
            return Response(b'', status=200)

        # Stub gTable; return the static configuration captured from the HAR
        @app.route('/gtable.inface.nexon.com/gid/<gid>.json', methods=['GET'])
        def gtable(gid):
            self.log_request(f'/gtable.inface.nexon.com/gid/{gid}.json')
            # This JSON was taken from nexon.har; adjust fields if your client expects other values.
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
                "alltem_code": None,
                "google_oauth_billing_client_id": None,
                "google_oauth_billing_client_secret": None,
                "arena_service_code": None,
                "str_env_type": "LIVE",
                "game_release_status": "released",
                "game_name_ko": "블루 아카이브",
                "game_name_en": "Blue Archive",
                "gid": gid,
                "last_modified": {
                    "modify_date": "2024-10-10T07:48:12.833Z",
                    "admin_no": 441
                },
                "krpc_alltem_code": None,
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

        @app.route('/crash-reporting-api-rs26-usw2.cloud.unity3d.com/api/reporting/v1/projects/<project>/events', methods=['POST'])
        def crash_reporting(project):
            self.log_request(f'/crash-reporting (project: {project})')
            return jsonify({"status": "ok"})

        @app.route('/<path:path>', methods=['GET', 'POST'])
        def catch_all(path):
            self.log_request(f'/{path}')
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
            if use_ssl:
                ssl_context = create_ssl_context(_get_ssl_hostnames())
                if ssl_context:
                    print_colored(f"{name} server on port {port} (HTTPS)", CYAN)
                    app.run(host='0.0.0.0', port=port, debug=False, use_reloader=False, ssl_context=ssl_context)
                else:
                    print_colored(f"{name} server on port {port} (HTTP, SSL init failed)", YELLOW)
                    app.run(host='0.0.0.0', port=port, debug=False, use_reloader=False)
            else:
                print_colored(f"{name} server on port {port}", CYAN)
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

    print_colored("\nChecking prerequisites...", YELLOW)

    admin = is_admin()
    if admin:
        print_colored("Running as administrator.", GREEN)
    else:
        print_colored("Not running as administrator.", YELLOW)
        print_colored("Automatic domain setup may fail.", YELLOW)

    print_colored("\nSetting up domain redirects...", YELLOW)
    hosts_manager = HostsManager()

    if admin:
        if hosts_manager.add_redirects():
            print_colored("Domain redirects configured.", GREEN)
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
    dep_manager = DependencyManager()
    if not dep_manager.setup_environment():
        print_colored("Environment setup failed.", RED)
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

    ba_server = BlueArchiveServer()
    main_app = ba_server.create_flask_app()
    if not main_app:
        print_colored("Failed to create main server app.", RED)
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
