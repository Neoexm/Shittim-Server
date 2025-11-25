-- Blue Archive Advanced Response Dumper
-- Specifically targets Unity IL2CPP networking
-- Use with Cheat Engine 7.5+

local config = {
    dumpPath = "C:\\BA_ResponseDumps\\",
    processName = "BlueArchive.exe",
    maxJsonSize = 2 * 1024 * 1024,  -- 2MB
    autoMonitor = false,
    monitorInterval = 500,  -- ms
}

local state = {
    logFile = nil,
    dumpCount = 0,
    monitoring = false,
    timer = nil,
    foundAddresses = {}
}

-- Utilities
local function log(msg)
    local timestamp = os.date("[%H:%M:%S]")
    local fullMsg = timestamp .. " " .. msg
    print(fullMsg)
    if state.logFile then
        state.logFile:write(fullMsg .. "\n")
        state.logFile:flush()
    end
end

local function ensureDumpDirectory()
    os.execute('if not exist "' .. config.dumpPath .. '" mkdir "' .. config.dumpPath .. '"')
end

local function initLogFile()
    ensureDumpDirectory()
    local timestamp = os.date("%Y%m%d_%H%M%S")
    state.logFile = io.open(config.dumpPath .. "session_" .. timestamp .. ".log", "w")
    if state.logFile then
        state.logFile:write("=== Blue Archive Response Dumper Session ===\n")
        state.logFile:write("Started: " .. os.date("%Y-%m-%d %H:%M:%S") .. "\n\n")
    end
end

-- String reading functions
local function readUTF8String(addr, maxLen)
    if not addr or addr == 0 then return nil end
    maxLen = maxLen or config.maxJsonSize
    
    local success, result = pcall(readString, addr, maxLen, true)
    if success and result and #result > 0 then
        return result
    end
    return nil
end

local function readUnityString(strObjAddr)
    if not strObjAddr or strObjAddr == 0 then return nil end
    
    -- Try different Unity string layouts
    local layouts = {
        {lengthOffset = 0x10, dataOffset = 0x14, charSize = 2},  -- IL2CPP
        {lengthOffset = 0x8, dataOffset = 0xC, charSize = 2},    -- Mono
        {lengthOffset = 0x10, dataOffset = 0x14, charSize = 1},  -- UTF8
    }
    
    for _, layout in ipairs(layouts) do
        local success, length = pcall(readInteger, strObjAddr + layout.lengthOffset)
        if success and length and length > 0 and length < config.maxJsonSize then
            local chars = {}
            local dataAddr = strObjAddr + layout.dataOffset
            
            for i = 0, length - 1 do
                local success2, char = pcall(readSmallInteger, dataAddr + (i * layout.charSize))
                if success2 and char and char > 0 and char < 65536 then
                    table.insert(chars, string.char(char % 256))
                else
                    break
                end
            end
            
            if #chars > 10 then
                return table.concat(chars)
            end
        end
    end
    
    return nil
end

-- JSON validation and parsing
local function isValidJson(str)
    if not str or #str < 2 then return false end
    
    local trimmed = str:match("^%s*(.-)%s*$")
    if not trimmed or #trimmed < 2 then return false end
    
    local firstChar = trimmed:sub(1,1)
    local lastChar = trimmed:sub(-1)
    
    return (firstChar == "{" and lastChar == "}") or
           (firstChar == "[" and lastChar == "]")
end

local function extractProtocol(jsonStr)
    -- Try multiple protocol field patterns
    local patterns = {
        '"Protocol"%s*:%s*"([^"]+)"',
        '"protocol"%s*:%s*"([^"]+)"',
        '"Protocol"%s*:%s*(%d+)',
        '"protocol"%s*:%s*(%d+)',
    }
    
    for _, pattern in ipairs(patterns) do
        local protocol = jsonStr:match(pattern)
        if protocol then return protocol end
    end
    
    return "Unknown"
end

-- Response dumping
local function dumpJsonToFile(jsonData, protocol, source)
    state.dumpCount = state.dumpCount + 1
    local timestamp = os.date("%Y%m%d_%H%M%S_%f")
    local safeProtocol = protocol:gsub("[^%w_]", "_")
    local filename = string.format("resp_%05d_%s_%s.json", 
        state.dumpCount, safeProtocol, timestamp)
    
    local fullPath = config.dumpPath .. filename
    local file = io.open(fullPath, "w")
    if file then
        file:write(jsonData)
        file:close()
        log(string.format("[DUMP #%d] %s (%d bytes) [%s]", 
            state.dumpCount, protocol, #jsonData, source))
        return true
    else
        log("ERROR: Failed to write " .. fullPath)
        return false
    end
end

-- Memory scanning
local function scanMemoryForJson()
    log("Scanning memory for JSON responses...")
    
    local patterns = {
        {name = "Protocol", aob = '7B 22 50 72 6F 74 6F 63 6F 6C 22 3A'},  -- {"Protocol":
        {name = "protocol", aob = '7B 22 70 72 6F 74 6F 63 6F 6C 22 3A'},  -- {"protocol":
        {name = "Packet", aob = '22 50 61 63 6B 65 74 22 3A'},            -- "Packet":
    }
    
    local foundCount = 0
    
    for _, pattern in ipairs(patterns) do
        local results = AOBScan(pattern.aob)
        
        if results and results.Count > 0 then
            log(string.format("Found %d matches for pattern '%s'", results.Count, pattern.name))
            
            for i = 0, math.min(results.Count - 1, 50) do  -- Limit to 50 per pattern
                local addrStr = results[i]
                local addr = tonumber(addrStr, 16)  -- Convert string hex to number
                
                if not addr then
                    -- Sometimes it's already a number
                    addr = tonumber(addrStr)
                end
                
                if addr then
                    -- Try to read backwards to find start of JSON
                    local startAddr = addr
                    for offset = 100, 0, -1 do
                        local testAddr = addr - offset
                        local success, char = pcall(readBytes, testAddr, 1, true)
                        if success and char == 0x7B then  -- {
                            startAddr = testAddr
                            break
                        end
                    end
                    
                    local jsonStr = readUTF8String(startAddr, 512 * 1024)
                    
                    if jsonStr then
                        -- Debug: Show what we found
                        if i == 0 then  -- Only log first match to avoid spam
                            local preview = jsonStr:sub(1, 200)
                            log(string.format("  [DEBUG] Read %d bytes from 0x%X: %s...", #jsonStr, startAddr, preview))
                        end
                        
                        if isValidJson(jsonStr) then
                            local protocol = extractProtocol(jsonStr)
                            
                            -- Check if we already dumped this (avoid duplicates)
                            local hash = tostring(#jsonStr) .. "_" .. protocol
                            if not state.foundAddresses[hash] then
                                state.foundAddresses[hash] = true
                                dumpJsonToFile(jsonStr, protocol, "MemScan")
                                foundCount = foundCount + 1
                            end
                        elseif i == 0 then
                            log("  [DEBUG] Failed validation - not valid JSON")
                        end
                    elseif i == 0 then
                        log(string.format("  [DEBUG] Failed to read string from 0x%X", startAddr))
                    end
                end
            end
            
            results.destroy()
        end
    end
    
    if foundCount == 0 then
        log("No new JSON responses found")
    else
        log(string.format("Dumped %d new responses", foundCount))
    end
    
    return foundCount
end

-- IL2CPP specific hooks
local function findIL2CPPMethods()
    log("Searching for IL2CPP network methods...")
    
    -- Common IL2CPP network method patterns
    local methodPatterns = {
        "UnityEngine.Networking.UnityWebRequest",
        "System.Net.Http.HttpClient",
        "UnityEngine.WWW",
        "Newtonsoft.Json.JsonConvert",
        "System.Text.Json.JsonSerializer",
    }
    
    -- This requires Cheat Engine's Mono/IL2CPP features
    if mono_enumAssemblies then
        log("Mono features available, searching for network assemblies...")
        -- More advanced hooking would go here
    else
        log("WARNING: Mono features not available. Manual hooking required.")
    end
end

-- Monitoring control
function startMonitoring(intervalMs)
    if state.monitoring then
        log("Already monitoring!")
        return
    end
    
    intervalMs = intervalMs or config.monitorInterval
    state.monitoring = true
    
    log(string.format("Starting continuous monitoring (interval: %dms)", intervalMs))
    log("Call stopMonitoring() to stop")
    
    state.timer = createTimer(getMainForm())
    state.timer.Interval = intervalMs
    state.timer.OnTimer = function()
        scanMemoryForJson()
    end
end

function stopMonitoring()
    if state.timer then
        state.timer.destroy()
        state.timer = nil
    end
    state.monitoring = false
    log("Monitoring stopped")
    log(string.format("Total responses dumped this session: %d", state.dumpCount))
    log("Files saved to: " .. config.dumpPath)
end

-- Manual dump from address
function dumpFromAddress(address)
    if type(address) == "string" then
        address = getAddress(address)
    end
    
    if not address or address == 0 then
        log("ERROR: Invalid address")
        return false
    end
    
    log(string.format("Attempting to dump from address: %X", address))
    
    -- Try reading as direct string
    local jsonStr = readUTF8String(address, config.maxJsonSize)
    
    -- Try reading as Unity string object
    if not jsonStr or not isValidJson(jsonStr) then
        jsonStr = readUnityString(address)
    end
    
    -- Try reading as pointer to string
    if not jsonStr or not isValidJson(jsonStr) then
        local ptr = readPointer(address)
        if ptr and ptr ~= 0 then
            jsonStr = readUTF8String(ptr, config.maxJsonSize)
        end
    end
    
    if jsonStr and isValidJson(jsonStr) then
        local protocol = extractProtocol(jsonStr)
        return dumpJsonToFile(jsonStr, protocol, "Manual")
    else
        log("ERROR: Could not read valid JSON from address")
        return false
    end
end

-- Clipboard monitoring
function dumpFromClipboard()
    local jsonStr = readFromClipboard()
    if jsonStr and isValidJson(jsonStr) then
        local protocol = extractProtocol(jsonStr)
        return dumpJsonToFile(jsonStr, protocol, "Clipboard")
    else
        log("ERROR: Clipboard does not contain valid JSON")
        return false
    end
end

-- Initialize
local function init()
    print("\n" .. string.rep("=", 60))
    print("    Blue Archive Advanced Response Dumper")
    print(string.rep("=", 60))
    
    initLogFile()
    ensureDumpDirectory()
    
    log("Output directory: " .. config.dumpPath)
    log("")
    log("Available commands:")
    log("  startMonitoring([intervalMs])  - Start continuous memory scanning")
    log("  stopMonitoring()               - Stop monitoring")
    log("  scanMemoryForJson()            - Scan memory once for JSON")
    log("  dumpFromAddress(addr)          - Dump from specific address")
    log("  dumpFromClipboard()            - Dump JSON from clipboard")
    log("")
    
    -- Try to attach
    local pid = getProcessIDFromProcessName(config.processName)
    if pid then
        log("Found process: " .. config.processName .. " (PID: " .. pid .. ")")
        openProcess(pid)
        
        log("")
        log("Quick start:")
        log("1. Play the game and perform some actions")
        log("2. Run: scanMemoryForJson()")
        log("3. Or run: startMonitoring() for continuous capture")
        log("")
        
        if config.autoMonitor then
            startMonitoring()
        end
    else
        log("WARNING: Process not found: " .. config.processName)
        log("Make sure the game is running and attach manually")
    end
    
    print(string.rep("=", 60) .. "\n")
end

-- Cleanup
local function cleanup()
    stopMonitoring()
    if state.logFile then
        state.logFile:write("\n=== Session Ended ===\n")
        state.logFile:write(string.format("Total responses dumped: %d\n", state.dumpCount))
        state.logFile:close()
    end
    log("Dumper closed. Total responses: " .. state.dumpCount)
end

-- Run initialization
if not syntaxcheck then
    init()
end
