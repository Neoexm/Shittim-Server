# Blue Archive Private Server (WIP)

**Status:** in development, currently **non-functional**. It serves config and logs requests. The client currently shows `Request Failed`.

---

## What it does

* Intercepts Blue Archive traffic locally.
* Serves a valid-looking server config for Steam (`364258_Live.json`) and stubs a few endpoints.
* Starts multiple listeners to mirror Nexon’s layout.
* Logs every request to disk for analysis.
* Generates a self-signed TLS cert so HTTPS endpoints actually start.

---

## Ports and layout

* **443** (HTTPS): main entry, serves server config and misc GETs.
* **5000** (HTTPS): API endpoints (intended).
* **5100** (HTTPS): Gateway (intended).
* **58880** (HTTP): anti-cheat/telemetry stub.

You’ll need admin rights on Windows to write to the hosts file.

---

## Domains redirected

Core set to `127.0.0.1`:

* `nxm-eu-bagl.nexon.com` (regional API)
* `public.api.nexon.com` (first API call host)
* `prod-noticepool.game.nexon.com` (config)
* `crash-reporting-api-rs26-usw2.cloud.unity3d.com` (telemetry)

There are more in the scripts; these are the important ones.

---

## Protocol bits wired up

* **Server config**: JSON structure matches the real thing, but points at localhost ports.
* **Crypto scaffolding**: K0lb3’s modules (XOR/MT, FlatBuffers schema, XXHash). Not driving real auth yet.
* **Stubbed endpoints**:

  * `GET /com.nexon.bluearchivesteam/server_config/364258_Live.json`
  * `GET /prod/crexception-prop`
  * `POST /toy/sdk/getCountry.nx` (returns GB/en)
  * `POST /toy/sdk/enterToy.nx`
  * `POST /toy/sdk/getPromotion.nx`

---

## Current state

**Works**

1. Hosts redirection and backup.
2. TLS cert generation and HTTPS listeners.
3. Request logging with payload size/entropy hints.
4. Game fetches and accepts server config without looping.

**Broken**

1. client errors with `Request Failed`.

---

## Setup

**Requirements**

* Windows with Administrator rights (for hosts changes).
* Python 3.8+.
* Blue Archive installed. No, this doesn’t replace it.

**Run**

```bash
# As Administrator
python blue_archive_server.py
```

What the script handles:

* Installs missing Python deps if needed.
* Downloads K0lb3 protocol helpers.
* Adds hosts redirects (keeps a backup).
* Starts listeners on 443/5000/5100/58880.
* Prints where logs are saved.

---

## Sanity checks

* Hosts redirect works:

  ```powershell
  ping nxm-eu-bagl.nexon.com  # should resolve to 127.0.0.1
  ```
* Ports are open:

  ```powershell
  netstat -an | findstr ":443"
  netstat -an | findstr ":5000"
  netstat -an | findstr ":5100"
  netstat -an | findstr ":58880"
  ```
* Requests are actually being logged: check `request_logs/`.

If config hits but `/toy/sdk/getCountry.nx` never shows up, that’s the current blocker.

---

## Disclaimer

This is for protocol research and education. It doesn’t ship game assets, doesn’t bypass purchases, and isn’t production-ready. Editing your hosts file is on you.
