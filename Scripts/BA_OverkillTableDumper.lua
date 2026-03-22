-- Blue Archive OVERKILL Table Bootstrap Dumper (Cheat Engine Lua)
-- Goal: capture as much table/bootstrap/network config evidence as possible.
-- Tested target: Cheat Engine 7.5+ (Windows)

local config = {
    processName = "BlueArchive.exe",
    outDir = "C:\\BA_OverkillTableDump\\",

    -- How aggressive to be
    maxHitsPerPattern = 300,
    dumpWindowBefore = 0x20000, -- 128 KB before hit
    dumpWindowAfter  = 0x20000, -- 128 KB after hit
    maxReadPerDump   = 0x40000, -- 256 KB cap per single dump
    periodicIntervalMs = 2500,
    attachRetryMs = 1000,
    maxJsonCandidatesPerDump = 80,
    minJsonLen = 32,
    maxJsonLen = 1024 * 1024, -- 1MB per candidate

    -- String extraction thresholds
    minAsciiLen = 4,
    minUtf16Len = 4,

    -- Always-on automation
    autoStartPeriodic = true,
}

local state = {
    sessionId = os.date("%Y%m%d_%H%M%S"),
    logFile = nil,
    liveDumpPath = nil,
    timer = nil,
    attachTimer = nil,
    attached = false,
    dumpCount = 0,
    jsonCount = 0,
    patternHitCount = {},
    seenHitKey = {},
}

-- High-value markers for table/bootstrap/debug paths
local KEYWORDS = {
    -- GTable / Inface
    "gtable.inface.nexon.com",
    "dev-gtable.inface.nexon.com",
    "test-gtable.inface.nexon.com",
    "/gid/",
    "gtable_url",
    "_inface_common_gtables",
    "_inface_env",
    "INFACE_VERSION",
    "nxinface_",
    "enxinface_",
    "nxinface.config.json",
    "nxinface.enconfig.json",
    "INFO_PATH",
    "BASE_URL",

    -- Table/bootstrap resources
    "resource-data.json",
    "TableBundles",
    "ExcelDB.db",
    "Excel.zip",
    "HexaMap.zip",

    -- API/bootstrap hosts used during startup
    "public.api.nexon.com",
    "signin.nexon.com",
    "config.na.nexon.com",
    "d2vaidpni345rp.cloudfront.net",

    -- Error anchors / diagnostics
    "game table was not loaded",
    "ERROR_FETCH",
    "ERROR_EMPTY_RES_URL",
    "ERROR_INVALID_RES_URL",
    "SDK_ERROR",
}

local function ensureDir(path)
    os.execute('if not exist "' .. path .. '" mkdir "' .. path .. '"')
end

local function now()
    return os.date("%Y-%m-%d %H:%M:%S")
end

local function log(msg)
    local line = string.format("[%s] %s", now(), msg)
    print(line)
    if state.logFile then
        state.logFile:write(line .. "\n")
        state.logFile:flush()
    end
end

local function openLogs()
    ensureDir(config.outDir)
    local base = config.outDir .. "session_" .. state.sessionId
    state.logFile = io.open(base .. ".log", "w")
    state.liveDumpPath = base .. "_LIVE_DUMP.txt"

    if state.logFile then
        state.logFile:write("Blue Archive OVERKILL Table Bootstrap Dumper\n")
        state.logFile:write("Session: " .. state.sessionId .. "\n\n")
        state.logFile:flush()
    end

    local lf = io.open(state.liveDumpPath, "w")
    if lf then
        lf:write("=== LIVE OVERKILL TABLE DUMP ===\n")
        lf:write("Session: " .. state.sessionId .. "\n")
        lf:write("Started: " .. now() .. "\n\n")
        lf:close()
    end
end

local function appendLiveDump(text)
    if not state.liveDumpPath then return end
    local f = io.open(state.liveDumpPath, "a")
    if f then
        f:write(text)
        f:write("\n")
        f:close()
    end
end

local function toHexByte(n)
    return string.format("%02X", n)
end

local function asciiToAob(s)
    local t = {}
    for i = 1, #s do
        t[#t + 1] = toHexByte(string.byte(s, i))
    end
    return table.concat(t, " ")
end

local function utf16leToAob(s)
    local t = {}
    for i = 1, #s do
        t[#t + 1] = toHexByte(string.byte(s, i))
        t[#t + 1] = "00"
    end
    return table.concat(t, " ")
end

local function safeReadBytes(addr, size)
    local ok, data = pcall(readBytes, addr, size, true)
    if ok and data and type(data) == "table" and #data > 0 then
        return data
    end
    return nil
end

local function bytesToBinString(bytes)
    local out = {}
    for i = 1, #bytes do
        out[i] = string.char(bytes[i])
    end
    return table.concat(out)
end

local function saveBinary(path, bytes)
    local f = io.open(path, "wb")
    if not f then return false end
    f:write(bytesToBinString(bytes))
    f:close()
    return true
end

local function saveText(path, text)
    local f = io.open(path, "w")
    if not f then return false end
    f:write(text)
    f:close()
    return true
end

local function looksLikeUsefulJson(s)
    if not s or #s < config.minJsonLen or #s > config.maxJsonLen then
        return false
    end

    -- High-signal keys for this investigation
    local keys = {
        '"gid"',
        '"BASE_URL"',
        '"INFO_PATH"',
        '"_inface_common_gtables"',
        '"INFACE_VERSION"',
        '"gtable_url"',
        '"toy_service_id"',
        '"portal_game_code"',
        '"na_service_id"'
    }

    for _, k in ipairs(keys) do
        if string.find(s, k, 1, true) then
            return true
        end
    end

    -- Generic fallback
    if string.find(s, '":', 1, true) then
        return true
    end

    return false
end

local function extractJsonCandidatesFromBlob(blob)
    local out = {}
    local n = #blob
    local i = 1

    while i <= n and #out < config.maxJsonCandidatesPerDump do
        local startPos = string.find(blob, "{", i, true)
        if not startPos then break end

        local depth = 0
        local inString = false
        local escaped = false
        local j = startPos
        local foundEnd = nil

        while j <= n do
            local c = string.sub(blob, j, j)

            if inString then
                if escaped then
                    escaped = false
                elseif c == "\\" then
                    escaped = true
                elseif c == '"' then
                    inString = false
                end
            else
                if c == '"' then
                    inString = true
                elseif c == "{" then
                    depth = depth + 1
                elseif c == "}" then
                    depth = depth - 1
                    if depth == 0 then
                        foundEnd = j
                        break
                    elseif depth < 0 then
                        break
                    end
                end
            end

            -- Hard cap for overly large candidates
            if (j - startPos + 1) > config.maxJsonLen then
                break
            end

            j = j + 1
        end

        if foundEnd then
            local candidate = string.sub(blob, startPos, foundEnd)
            if looksLikeUsefulJson(candidate) then
                out[#out + 1] = candidate
            end
            i = foundEnd + 1
        else
            i = startPos + 1
        end
    end

    return out
end

local function saveJsonCandidates(prefix, candidates, tag, encoding, hitAddr)
    if not candidates or #candidates == 0 then
        return 0
    end

    local saved = 0
    for idx, js in ipairs(candidates) do
        state.jsonCount = state.jsonCount + 1
        local jp = string.format("%s_json_%03d_global_%06d.json", prefix, idx, state.jsonCount)
        if saveText(jp, js) then
            saved = saved + 1

            local lf = io.open(config.outDir .. "json_index_" .. state.sessionId .. ".jsonl", "a")
            if lf then
                local row = string.format(
                    '{"session":"%s","json_no":%d,"tag":"%s","encoding":"%s","hit_addr":"0x%X","path":"%s","size":%d}',
                    state.sessionId, state.jsonCount, tag, encoding, hitAddr, jp, #js)
                lf:write(row .. "\n")
                lf:close()
            end
        end
    end

    return saved
end

local function isPrintableAsciiByte(b)
    return b >= 32 and b <= 126
end

local function extractAsciiStrings(bytes, minLen)
    local results = {}
    local buf = {}

    local function flush()
        if #buf >= minLen then
            results[#results + 1] = table.concat(buf)
        end
        buf = {}
    end

    for i = 1, #bytes do
        local b = bytes[i]
        if isPrintableAsciiByte(b) then
            buf[#buf + 1] = string.char(b)
        else
            flush()
        end
    end
    flush()
    return results
end

local function extractUtf16LeStrings(bytes, minLen)
    local results = {}
    local chars = {}

    local function flush()
        if #chars >= minLen then
            results[#results + 1] = table.concat(chars)
        end
        chars = {}
    end

    local i = 1
    while i < #bytes do
        local b0 = bytes[i]
        local b1 = bytes[i + 1]

        if b1 == 0 and isPrintableAsciiByte(b0) then
            chars[#chars + 1] = string.char(b0)
            i = i + 2
        else
            flush()
            i = i + 1
        end
    end
    flush()
    return results
end

local function joinTop(list, limit)
    if not list or #list == 0 then return "" end
    local n = math.min(#list, limit)
    local out = {}
    for i = 1, n do
        out[#out + 1] = list[i]
    end
    return table.concat(out, "\n")
end

local function dumpAroundAddress(hitAddr, tag, encoding)
    local startAddr = hitAddr - config.dumpWindowBefore
    if startAddr < 0 then startAddr = 0 end

    local total = config.dumpWindowBefore + config.dumpWindowAfter
    if total > config.maxReadPerDump then
        total = config.maxReadPerDump
    end

    local bytes = safeReadBytes(startAddr, total)
    if not bytes then
        log(string.format("[WARN] Read failed at 0x%X (tag=%s)", hitAddr, tag))
        return false
    end

    state.dumpCount = state.dumpCount + 1
    local prefix = string.format("%s_dump_%06d_%s_%s_0x%X",
        config.outDir,
        state.dumpCount,
        tag:gsub("[^%w_%-]", "_"),
        encoding,
        hitAddr)

    local okBin = saveBinary(prefix .. ".bin", bytes)
    local blob = bytesToBinString(bytes)

    local ascii = extractAsciiStrings(bytes, config.minAsciiLen)
    local utf16 = extractUtf16LeStrings(bytes, config.minUtf16Len)

    -- Live mega append (intentionally excessive)
    local live = {}
    live[#live + 1] = string.rep("=", 100)
    live[#live + 1] = string.format("[%s] DUMP #%d", now(), state.dumpCount)
    live[#live + 1] = "session=" .. state.sessionId
    live[#live + 1] = "tag=" .. tag
    live[#live + 1] = "encoding=" .. encoding
    live[#live + 1] = string.format("hit_addr=0x%X", hitAddr)
    live[#live + 1] = string.format("start_addr=0x%X", startAddr)
    live[#live + 1] = "bytes=" .. tostring(#bytes)
    live[#live + 1] = ""
    live[#live + 1] = "--- ASCII STRINGS (up to 3000) ---"
    live[#live + 1] = joinTop(ascii, 3000)
    live[#live + 1] = ""
    live[#live + 1] = "--- UTF16-LE STRINGS (up to 3000) ---"
    live[#live + 1] = joinTop(utf16, 3000)
    live[#live + 1] = ""
    appendLiveDump(table.concat(live, "\n"))

    local summary = {}
    summary[#summary + 1] = "=== OVERKILL DUMP SUMMARY ==="
    summary[#summary + 1] = "session=" .. state.sessionId
    summary[#summary + 1] = "hit_tag=" .. tag
    summary[#summary + 1] = "encoding=" .. encoding
    summary[#summary + 1] = string.format("hit_addr=0x%X", hitAddr)
    summary[#summary + 1] = string.format("start_addr=0x%X", startAddr)
    summary[#summary + 1] = "bytes=" .. tostring(#bytes)
    summary[#summary + 1] = ""
    summary[#summary + 1] = "--- TOP ASCII STRINGS (up to 500) ---"
    summary[#summary + 1] = joinTop(ascii, 500)
    summary[#summary + 1] = ""
    summary[#summary + 1] = "--- TOP UTF16-LE STRINGS (up to 500) ---"
    summary[#summary + 1] = joinTop(utf16, 500)
    summary[#summary + 1] = ""

    local okTxt = saveText(prefix .. ".txt", table.concat(summary, "\n"))

    -- Overkill: carve and save JSON-like payload candidates from the same memory region
    local jsonCandidates = extractJsonCandidatesFromBlob(blob)
    local jsonSaved = saveJsonCandidates(prefix, jsonCandidates, tag, encoding, hitAddr)

    local meta = string.format(
        "{\"session\":\"%s\",\"tag\":\"%s\",\"encoding\":\"%s\",\"hit_addr\":\"0x%X\",\"start_addr\":\"0x%X\",\"size\":%d,\"bin\":\"%s\",\"txt\":\"%s\"}",
        state.sessionId, tag, encoding, hitAddr, startAddr, #bytes,
        prefix .. ".bin", prefix .. ".txt")

    local f = io.open(config.outDir .. "index_" .. state.sessionId .. ".jsonl", "a")
    if f then
        f:write(meta .. "\n")
        f:close()
    end

    log(string.format("[DUMP] %s (%s) hit=0x%X size=%d bin=%s txt=%s",
        tag, encoding, hitAddr, #bytes,
        okBin and "ok" or "fail", okTxt and "ok" or "fail"))
    log(string.format("[JSON] candidates=%d saved=%d (hit=0x%X)", #jsonCandidates, jsonSaved, hitAddr))

    return true
end

local function safeDestroy(scan)
    if scan and scan.destroy then
        pcall(function() scan.destroy() end)
    end
end

local function scanPattern(patternName, aobPattern, encodingLabel)
    local ok, scan = pcall(AOBScan, aobPattern)
    if not ok or not scan then
        log(string.format("[WARN] AOB scan failed for %s (%s)", patternName, encodingLabel))
        return 0
    end

    local total = tonumber(scan.Count) or 0
    if total <= 0 then
        safeDestroy(scan)
        return 0
    end

    log(string.format("[HIT] %s (%s): %d matches", patternName, encodingLabel, total))

    local dumped = 0
    local limit = math.min(total, config.maxHitsPerPattern)

    for i = 0, limit - 1 do
        local s = scan[i]
        local addr = tonumber(s, 16) or tonumber(s)
        if addr and addr > 0 then
            local dedupe = string.format("%s|%s|0x%X", patternName, encodingLabel, addr)
            if not state.seenHitKey[dedupe] then
                state.seenHitKey[dedupe] = true
                if dumpAroundAddress(addr, patternName, encodingLabel) then
                    dumped = dumped + 1
                end
            end
        end
    end

    safeDestroy(scan)
    return dumped
end

local function runFullOverkillScan()
    log("===== FULL OVERKILL SCAN START =====")

    local totalDumps = 0

    for _, kw in ipairs(KEYWORDS) do
        local asciiPattern = asciiToAob(kw)
        local utf16Pattern = utf16leToAob(kw)

        local d1 = scanPattern(kw, asciiPattern, "ascii")
        local d2 = scanPattern(kw, utf16Pattern, "utf16le")

        totalDumps = totalDumps + d1 + d2
        state.patternHitCount[kw] = (state.patternHitCount[kw] or 0) + d1 + d2
    end

    log(string.format("===== FULL OVERKILL SCAN END (new dumps: %d) =====", totalDumps))
    return totalDumps
end

local function tryAttach()
    if state.attached then
        return true
    end

    local pid = getProcessIDFromProcessName(config.processName)
    if not pid then
        log("[WAIT] Process not found: " .. config.processName)
        return false
    end

    openProcess(pid)
    state.attached = true
    log(string.format("Attached to %s (PID=%d)", config.processName, pid))
    return true
end

local function startPeriodic(ms)
    if state.timer then
        log("Periodic scanner already running")
        return
    end

    local interval = ms or config.periodicIntervalMs
    state.timer = createTimer(getMainForm())
    state.timer.Interval = interval
    state.timer.OnTimer = function()
        pcall(runFullOverkillScan)
    end

    log(string.format("Periodic overkill scan started (interval=%d ms)", interval))
end

local function stopPeriodic()
    if state.timer then
        state.timer.destroy()
        state.timer = nil
        log("Periodic overkill scan stopped")
    end
end

local function startAutoAttachAndScan()
    if state.attachTimer then return end

    state.attachTimer = createTimer(getMainForm())
    state.attachTimer.Interval = config.attachRetryMs
    state.attachTimer.OnTimer = function()
        if tryAttach() then
            if state.attachTimer then
                state.attachTimer.destroy()
                state.attachTimer = nil
            end

            -- Immediate initial scan on attach, then continuous live scan
            pcall(runFullOverkillScan)
            startPeriodic(config.periodicIntervalMs)
        end
    end

    log(string.format("Auto-attach loop started (retry=%d ms)", config.attachRetryMs))
end

local function init()
    ensureDir(config.outDir)
    openLogs()

    print("\n============================================================")
    print(" Blue Archive OVERKILL Table Bootstrap Dumper")
    print("============================================================")
    log("Output directory: " .. config.outDir)
    log("Session: " .. state.sessionId)
    log("Live dump file: " .. (state.liveDumpPath or "(unavailable)"))
    log("Mode: fully automatic (no controls)")

    startAutoAttachAndScan()

    print("============================================================\n")
end

local function cleanup()
    stopPeriodic()
    if state.attachTimer then
        state.attachTimer.destroy()
        state.attachTimer = nil
    end
end

if not syntaxcheck then
    init()
end

