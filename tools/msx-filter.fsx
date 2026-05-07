#!/usr/bin/env dotnet fsi
/// MSX Update Helper — Filters calendar events to customer-facing meetings
/// and matches them against accounts.csv for MSX activity logging.
///
/// Usage: dotnet fsi msx-filter.fsx <events-json-file> [accounts-csv]
/// Output: JSON array of matched meetings (stdout), summary (stderr)
///
/// Logic:
///   1. Skip cancelled events
///   2. Skip meetings where ALL attendees are @microsoft.com (internal-only)
///   3. Skip meetings matching known internal patterns (ROB, 1:1, skilling, etc.)
///   4. Match remaining meetings to accounts using SUBJECT LINE only
///      (no email domain matching — just fuzzy text match on subject vs account names)

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions

// ─── Internal meeting patterns to skip ───────────────────────────────────────

let internalPatterns = [
    @"\[Internal\]"
    @"\b1[:\s]?1\b"
    @"\bone.on.one\b"
    @"\bROB\b"
    @"\bRhythm\s+of\s+Business\b"
    @"\bstand.?up\b"
    @"\ball.?hands\b"
    @"\boffice\s+hours\b"
    @"\bbrown\s*bag\b"
    @"\bteam\s+(meeting|sync|standup)\b"
    @"\btraining\b"
    @"\bskilling\b"
    @"\blearning\s+hour\b"
    @"\bretro(spective)?\b"
    @"\bsprint\s+(planning|review)\b"
    @"\binternal\s+prep\b"
    @"\bEntra\s+Expert\s+Connect\b"
    @"\bAsk\s+the\s+Experts\b"
    @"\bRoadmap\s+Review\b"
    @"\bSE\s+Skilling\b"
    @"\bField\s+Readiness\b"
    @"\bAccelerate\s*-\s*Ask\b"
]

let internalRegexes = 
    internalPatterns |> List.map (fun p -> Regex(p, RegexOptions.IgnoreCase ||| RegexOptions.Compiled))

let isInternalSubject (subject: string) =
    internalRegexes |> List.exists (fun r -> r.IsMatch(subject))

// ─── Task category detection ─────────────────────────────────────────────────

let detectCategory (subject: string) =
    let s = subject.ToLowerInvariant()
    if s.Contains("ads") || s.Contains("architecture design") then (861980004, "Architecture Design Session")
    elif s.Contains("demo") || s.Contains("showcase") then (861980002, "Demo")
    elif s.Contains("poc") || s.Contains("pilot") || s.Contains("proof of concept") then (861980005, "PoC/Pilot")
    elif s.Contains("workshop") then (861980001, "Workshop")
    elif s.Contains("close") || s.Contains("win plan") then (606820005, "Technical Close/Win Plan")
    elif s.Contains("consumption") || s.Contains("usage review") then (861980007, "Consumption Plan")
    elif s.Contains("escalation") || s.Contains("blocker") then (861980006, "Blocker Escalation")
    else (861980000, "Customer Engagement")

// ─── Alias loading ───────────────────────────────────────────────────────────

let loadAliases (scriptDir: string) =
    let aliasFile = Path.Combine(scriptDir, "account-aliases.sample.json")
    if File.Exists aliasFile then
        let json = File.ReadAllText(aliasFile, Text.Encoding.UTF8)
        let doc = JsonDocument.Parse(json)
        let aliases = doc.RootElement.GetProperty("aliases")
        [| for prop in aliases.EnumerateObject() do
            let v = prop.Value.GetString()
            if not (String.IsNullOrEmpty(v)) then
                yield (prop.Name, v) |]
    else [||]

// ─── CSV parsing ─────────────────────────────────────────────────────────────

let parseCsvLine (line: string) =
    let fields = ResizeArray<string>()
    let mutable inQuotes = false
    let mutable current = System.Text.StringBuilder()
    for ch in line do
        match ch with
        | '"' -> inQuotes <- not inQuotes
        | ',' when not inQuotes ->
            fields.Add(current.ToString().Trim())
            current <- System.Text.StringBuilder()
        | _ -> current.Append(ch) |> ignore
    fields.Add(current.ToString().Trim())
    fields |> Seq.toArray

type Account = {
    MsSalesId: string
    AccountName: string
    Segment: string
    City: string
    State: string
    SspName: string
}

let loadAccounts (csvPath: string) =
    File.ReadAllLines(csvPath)
    |> Array.skip 1
    |> Array.filter (fun l -> l.Trim().Length > 0)
    |> Array.map (fun line ->
        let f = parseCsvLine line
        { MsSalesId = f.[0]
          AccountName = f.[1]
          Segment = if f.Length > 2 then f.[2] else ""
          City = if f.Length > 6 then f.[6] else ""
          State = if f.Length > 7 then f.[7] else ""
          SspName = if f.Length > 8 then f.[8] else "" })

// ─── Account matching — SUBJECT LINE ONLY ────────────────────────────────────

let normalize (s: string) =
    Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9\s]", " ")
    |> fun x -> Regex.Replace(x, @"\s+", " ").Trim()

/// Match meeting subject to an account. Checks aliases first, then fuzzy match.
let matchSubjectToAccount (accounts: Account[]) (aliases: (string * string)[]) (subject: string) =
    let subjNorm = normalize subject
    
    // Strategy 0: Check aliases — highest priority, normalized substring match
    // Use word-boundary-aware matching to avoid "pilot" matching inside "copilot"
    let aliasMatch =
        aliases
        |> Array.choose (fun (alias, salesId) ->
            let aliasNorm = normalize alias
            if aliasNorm.Length >= 3 then
                let pattern = $@"\b{Regex.Escape(aliasNorm)}\b"
                if Regex.IsMatch(subjNorm, pattern) then
                    accounts |> Array.tryFind (fun a -> a.MsSalesId = salesId)
                    |> Option.map (fun acct -> (acct, aliasNorm.Length * 15))
                else None
            else None)
        |> Array.sortByDescending snd
        |> Array.tryHead
        |> Option.map fst
    
    match aliasMatch with
    | Some acct -> Some acct
    | None ->
        accounts
        |> Array.choose (fun acct ->
            let acctNorm = normalize acct.AccountName
            let acctWords = acctNorm.Split(' ') |> Array.filter (fun w -> w.Length >= 4)
            
            // Strategy 1: Full normalized account name found in subject
            if subjNorm.Contains(acctNorm) && acctNorm.Length >= 4 then
                Some (acct, acctNorm.Length * 10)
            // Strategy 2: First significant word of account name in subject (brand match)
            else
                let firstBrand = acctWords |> Array.tryHead
                match firstBrand with
                | Some brand when brand.Length >= 4 && Regex.IsMatch(subjNorm, $@"\b{Regex.Escape(brand)}\b") ->
                    Some (acct, brand.Length * 8)
                | _ ->
                    // Strategy 3: Any significant account word (5+ chars) in subject
                    let hits = acctWords |> Array.filter (fun w -> w.Length >= 5 && Regex.IsMatch(subjNorm, $@"\b{Regex.Escape(w)}\b"))
                    if hits.Length > 0 then
                        Some (acct, hits |> Array.sumBy (fun w -> w.Length * 4))
                    else None)
        |> Array.sortByDescending snd
        |> Array.tryHead
        |> Option.map fst

// ─── JSON parsing ────────────────────────────────────────────────────────────

type Attendee = { Name: string; Email: string; Status: string }
type CalendarEvent = {
    Id: string
    Subject: string
    Start: string
    End: string
    Organizer: string
    IsCancelled: bool
    Attendees: Attendee list
    Preview: string
}

let parseEvents (jsonPath: string) =
    let json = File.ReadAllText(jsonPath, Text.Encoding.UTF8)
    let doc = JsonDocument.Parse(json)
    let eventsArr = doc.RootElement.GetProperty("events")
    
    [| for ev in eventsArr.EnumerateArray() do
        let attendees = 
            if ev.TryGetProperty("attendees") |> fst then
                [for a in (ev.GetProperty("attendees")).EnumerateArray() do
                    { Name = a.GetProperty("name").GetString()
                      Email = a.GetProperty("email").GetString()
                      Status = a.GetProperty("status").GetString() }]
            else []
        { Id = ev.GetProperty("id").GetString()
          Subject = ev.GetProperty("subject").GetString().Trim()
          Start = ev.GetProperty("start").GetString()
          End = ev.GetProperty("end").GetString()
          Organizer = ev.GetProperty("organizer").GetString()
          IsCancelled = ev.GetProperty("isCancelled").GetBoolean()
          Attendees = attendees
          Preview = if ev.TryGetProperty("preview") |> fst then ev.GetProperty("preview").GetString() else "" } |]

// ─── Main logic ──────────────────────────────────────────────────────────────

let run () =
    let args = fsi.CommandLineArgs
    if args.Length < 2 then
        eprintfn "Usage: dotnet fsi msx-filter.fsx <events-json-file> [accounts-csv]"
        exit 1
    
    let eventsFile = args.[1]
    let accountsCsv = 
        if args.Length > 2 then args.[2]
        else Path.Combine(__SOURCE_DIRECTORY__, "accounts.csv")
    
    if not (File.Exists eventsFile) then
        eprintfn $"Error: Events file not found: {eventsFile}"
        exit 1
    if not (File.Exists accountsCsv) then
        eprintfn $"Error: Accounts CSV not found: {accountsCsv}"
        exit 1
    
    let accounts = loadAccounts accountsCsv
    let events = parseEvents eventsFile
    
    // Step 1: Remove cancelled
    let active = events |> Array.filter (fun ev -> not ev.IsCancelled)
    
    // Step 2: Keep only meetings with at least one non-Microsoft attendee
    let hasExternalAttendees (ev: CalendarEvent) =
        ev.Attendees |> List.exists (fun a -> not (a.Email.EndsWith("@microsoft.com")))
    
    let withExternals = active |> Array.filter hasExternalAttendees
    
    // Step 3: Remove meetings matching internal patterns
    let customerMeetings = withExternals |> Array.filter (fun ev -> not (isInternalSubject ev.Subject))
    
    // Step 4: Load aliases and match subject to accounts
    let aliases = loadAliases __SOURCE_DIRECTORY__
    
    // Build a domain-to-account map from aliases + account names for attendee matching
    let domainHints = 
        aliases 
        |> Array.choose (fun (alias, salesId) ->
            // If alias looks like a brand, create a domain hint
            let norm = normalize alias
            if norm.Length >= 4 then Some (norm, salesId) else None)
    
    let results =
        customerMeetings
        |> Array.map (fun ev ->
            let externals = 
                ev.Attendees 
                |> List.filter (fun a -> 
                    not (a.Email.EndsWith("@microsoft.com")) &&
                    not (a.Email.EndsWith("@service.microsoft.com")))
            let externalAttendees = externals |> List.truncate 5 |> List.map (fun a -> $"{a.Name} <{a.Email}>")
            let extCount = externals.Length
            let (catCode, catName) = detectCategory ev.Subject
            let date = ev.Start.Split('T').[0]
            
            // Try subject match first
            let matchedAccount = matchSubjectToAccount accounts aliases ev.Subject
            
            // Fallback: match by attendee email domains
            let finalMatch =
                match matchedAccount with
                | Some _ -> matchedAccount
                | None ->
                    // Extract unique domains from external attendees
                    let extDomains = 
                        externals 
                        |> List.choose (fun a -> 
                            let parts = a.Email.Split('@')
                            if parts.Length = 2 then 
                                let domain = parts.[1].ToLowerInvariant()
                                let domainBase = domain.Split('.').[0]
                                if domainBase.Length >= 4 then Some domainBase else None
                            else None)
                        |> List.distinct
                    
                    // Check if any domain base matches an alias or account name
                    extDomains
                    |> List.tryPick (fun domBase ->
                        // Check aliases
                        aliases |> Array.tryPick (fun (alias, salesId) ->
                            let aliasNorm = normalize alias
                            if aliasNorm.Contains(domBase) || domBase.Contains(aliasNorm) then
                                accounts |> Array.tryFind (fun a -> a.MsSalesId = salesId)
                            else None)
                        |> Option.orElseWith (fun () ->
                            // Check account names
                            accounts |> Array.tryFind (fun a -> 
                                let acctNorm = normalize a.AccountName
                                acctNorm.Contains(domBase) || domBase.Contains(acctNorm.Split(' ').[0]))))
            
            let extSuffix = if extCount > 5 then $" (+{extCount - 5} more)" else ""
            
            match finalMatch with
            | Some acct ->
                {| Date = date
                   Subject = ev.Subject
                   AccountName = acct.AccountName
                   MsSalesId = acct.MsSalesId
                   ExternalAttendees = externalAttendees
                   ExtSuffix = extSuffix
                   CategoryCode = catCode
                   CategoryName = catName
                   ActivityDate = ev.Start
                   Matched = true |}
            | None ->
                {| Date = date
                   Subject = ev.Subject
                   AccountName = ""
                   MsSalesId = ""
                   ExternalAttendees = externalAttendees
                   ExtSuffix = extSuffix
                   CategoryCode = catCode
                   CategoryName = catName
                   ActivityDate = ev.Start
                   Matched = false |})
    
    // ─── Output: Compact pipe-delimited format (stdout) ──────────────────────
    // Header
    printfn "DATE|SALES_ID|ACCOUNT|SUBJECT|CATEGORY_CODE|CATEGORY|ACTIVITY_DATE|MATCHED|EXTERNALS"
    
    for m in results do
        let extStr = (m.ExternalAttendees |> String.concat "; ") + m.ExtSuffix
        let matched = if m.Matched then "Y" else "N"
        printfn $"{m.Date}|{m.MsSalesId}|{m.AccountName}|{m.Subject}|{m.CategoryCode}|{m.CategoryName}|{m.ActivityDate}|{matched}|{extStr}"
    
    // ─── Human-readable summary (stderr) ─────────────────────────────────────
    eprintfn ""
    eprintfn "═══ MSX Filter Results ═══"
    eprintfn $"  Total events: {events.Length} | Active: {active.Length} | External: {withExternals.Length} | Customer: {customerMeetings.Length}"
    eprintfn $"  Matched: {results |> Array.filter (fun m -> m.Matched) |> Array.length} | Unmatched: {results |> Array.filter (fun m -> not m.Matched) |> Array.length}"
    eprintfn ""

run ()
