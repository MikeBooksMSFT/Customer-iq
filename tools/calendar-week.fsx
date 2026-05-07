#!/usr/bin/env dotnet fsi
/// Calendar Week Filter — parses raw m365_list_events JSON and emits a compact
/// pipe-delimited table of upcoming (or past) meetings, flagging customer-facing ones.
///
/// Usage:
///   dotnet fsi calendar-week.fsx <events-json-file>
///                                [--days N]              (default 7; forward window from --from)
///                                [--from YYYY-MM-DD]     (default today)
///                                [--external-only]       (skip internal-only)
///                                [--customer-only]       (require account match)
///                                [--exclude-internal-patterns] (default on; use --include-all to disable)
///                                [--include-all]
///                                [--accounts-csv PATH]
///                                [--limit N]
///                                [--format table|tsv|json]   (default table)
///
/// Input JSON shape — the friendly shape from m365_list_events:
///   { "events": [ { id, subject, start:{dateTime,timeZone}, end:{dateTime,timeZone},
///                   organizer:{name,email}, location:{displayName},
///                   attendees:[{name,email,type}], isCancelled, isOnlineMeeting,
///                   onlineMeetingUrl, webLink, bodyPreview } ] }
///
/// Output columns (pipe-delimited):
///   DATE|TIME|MINS|EXTERNAL|CUSTOMER|SUBJECT|ORGANIZER|LOCATION|ATTENDEES|ID

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions

let argv = fsi.CommandLineArgs |> Array.skip 1
if argv.Length < 1 then
    eprintfn "Usage: dotnet fsi calendar-week.fsx <events-json> [--days N] [--from YYYY-MM-DD] [--external-only] [--customer-only] [--include-all] [--accounts-csv PATH] [--limit N] [--format table|tsv|json]"
    exit 1

let path = argv.[0]
let getOpt name =
    let i = argv |> Array.tryFindIndex (fun a -> a = name)
    match i with
    | Some k when k + 1 < argv.Length -> Some argv.[k + 1]
    | _ -> None
let hasFlag name = argv |> Array.contains name

let days         = getOpt "--days" |> Option.map int |> Option.defaultValue 7
let fromOpt      = getOpt "--from" |> Option.map (fun s -> DateTime.Parse(s))
let externalOnly = hasFlag "--external-only"
let customerOnly = hasFlag "--customer-only"
let includeAll   = hasFlag "--include-all"
let limit        = getOpt "--limit" |> Option.map int
let format       = getOpt "--format" |> Option.defaultValue "table"

let scriptDir = Path.GetDirectoryName(fsi.CommandLineArgs.[0])
let defaultCsv = Path.Combine(scriptDir, "accounts.csv")
let accountsCsv = getOpt "--accounts-csv" |> Option.defaultValue defaultCsv

let windowStart = (fromOpt |> Option.defaultValue DateTime.Today).Date
let windowEnd = windowStart.AddDays(float days)

// ─── Helpers ────────────────────────────────────────────────────────────────
let getProp (el: JsonElement) (name: string) =
    if el.ValueKind <> JsonValueKind.Object then None
    else
        match el.TryGetProperty(name) with
        | true, v when v.ValueKind <> JsonValueKind.Null -> Some v
        | _ -> None

let getStr el name =
    getProp el name
    |> Option.map (fun v ->
        match v.ValueKind with
        | JsonValueKind.String -> v.GetString()
        | JsonValueKind.True   -> "true"
        | JsonValueKind.False  -> "false"
        | _                    -> v.ToString())
    |> Option.defaultValue ""

let getBool el name =
    getProp el name
    |> Option.map (fun v -> v.ValueKind = JsonValueKind.True)
    |> Option.defaultValue false

let tryParseDate (s: string) =
    if String.IsNullOrWhiteSpace s then None
    else
        match DateTime.TryParse(s, Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.AssumeUniversal ||| Globalization.DateTimeStyles.AdjustToUniversal) with
        | true, d -> Some d
        | _ -> None

// ─── Internal-pattern filter ────────────────────────────────────────────────
let internalPatterns = [
    @"\[Internal\]"; @"\b1[:\s]?1\b"; @"\bone.on.one\b"; @"\bROB\b"
    @"\bRhythm\s+of\s+Business\b"; @"\bstand.?up\b"; @"\ball.?hands\b"
    @"\boffice\s+hours\b"; @"\bbrown\s*bag\b"; @"\bteam\s+(meeting|sync|standup)\b"
    @"\btraining\b"; @"\bskilling\b"; @"\blearning\s+hour\b"
    @"\bretro(spective)?\b"; @"\bsprint\s+(planning|review)\b"
    @"\binternal\s+prep\b"; @"\bEntra\s+Expert\s+Connect\b"
    @"\bAsk\s+the\s+Experts\b"; @"\bRoadmap\s+Review\b"
    @"\bSE\s+Skilling\b"; @"\bField\s+Readiness\b"
]
let internalRegexes = internalPatterns |> List.map (fun p -> Regex(p, RegexOptions.IgnoreCase ||| RegexOptions.Compiled))
let isInternalSubject (s: string) = internalRegexes |> List.exists (fun r -> r.IsMatch(s))

// ─── Accounts CSV ───────────────────────────────────────────────────────────
let loadAccounts () =
    if File.Exists accountsCsv then
        File.ReadAllLines accountsCsv
        |> Array.skip 1
        |> Array.choose (fun line ->
            let parts = line.Split(',')
            if parts.Length >= 2 then Some (parts.[1].Trim().Trim('"').ToLowerInvariant()) else None)
        |> Array.filter (fun s -> s.Length >= 3)
        |> Array.distinct
        |> Array.toList
    else []

let accountNames = loadAccounts ()
let accountTokens =
    accountNames
    |> List.map (fun n ->
        let toks = Regex.Split(n, @"[\s,.\-_/&]+") |> Array.filter (fun t -> t.Length >= 4)
        if toks.Length = 0 then n else toks.[0])
    |> List.distinct

let subjectMatchesCustomer (subject: string) =
    let s = subject.ToLowerInvariant()
    accountNames |> List.exists (fun n -> s.Contains n)
    || accountTokens |> List.exists (fun t -> s.Contains t)

// ─── Load ───────────────────────────────────────────────────────────────────
let doc = JsonDocument.Parse(File.ReadAllText(path))
let root = doc.RootElement
let eventsEl =
    match getProp root "events" with
    | Some v -> v
    | None ->
        match getProp root "value" with
        | Some v -> v
        | None -> root

type Row = {
    Start: DateTime
    MinutesLen: int
    External: bool
    Customer: bool
    Subject: string
    Organizer: string
    Location: string
    Attendees: string
    Id: string
}

let rows = ResizeArray<Row>()

for e in eventsEl.EnumerateArray() do
    let subject = getStr e "subject"
    let id = getStr e "id"
    let isCancelled = getBool e "isCancelled"
    let startEl = getProp e "start"
    let endEl = getProp e "end"
    let startDt =
        startEl |> Option.bind (fun s -> tryParseDate (getStr s "dateTime"))
    let endDt =
        endEl |> Option.bind (fun s -> tryParseDate (getStr s "dateTime"))
    let minutes =
        match startDt, endDt with
        | Some s, Some e -> int (e - s).TotalMinutes
        | _ -> 0

    let organizer =
        match getProp e "organizer" with
        | Some o ->
            let addrObj =
                match getProp o "emailAddress" with
                | Some a -> a
                | None -> o
            let nm = getStr addrObj "name"
            let em = getStr addrObj "address"
            if nm <> "" then nm else em
        | None -> ""

    let location =
        match getProp e "location" with
        | Some l -> getStr l "displayName"
        | None -> ""

    let attendeesList =
        match getProp e "attendees" with
        | Some v when v.ValueKind = JsonValueKind.Array ->
            v.EnumerateArray()
            |> Seq.map (fun a ->
                let addrObj =
                    match getProp a "emailAddress" with
                    | Some x -> x
                    | None -> a
                let nm = getStr addrObj "name"
                let em = (getStr addrObj "address").ToLowerInvariant()
                nm, em)
            |> Seq.toList
        | _ -> []

    let hasExternal =
        attendeesList |> List.exists (fun (_, em) -> em <> "" && not (em.EndsWith("@microsoft.com")))
    let isCustomer = hasExternal || subjectMatchesCustomer subject

    let attendeeDisplay =
        let names =
            attendeesList
            |> List.map (fun (n, e) -> if n <> "" then n else e)
            |> List.filter (fun s -> s <> "")
            |> List.distinct
        if names.Length > 5 then
            (names |> List.take 5 |> String.concat ", ") + sprintf ", +%d more" (names.Length - 5)
        else String.concat ", " names

    match startDt with
    | Some s when (not isCancelled)
                  && s >= windowStart
                  && s < windowEnd.AddDays(1.0)
                  && (includeAll || not (isInternalSubject subject))
                  && (not externalOnly || hasExternal)
                  && (not customerOnly || isCustomer) ->
        rows.Add({
            Start = s; MinutesLen = minutes
            External = hasExternal; Customer = isCustomer
            Subject = subject; Organizer = organizer
            Location = location; Attendees = attendeeDisplay; Id = id
        })
    | _ -> ()

let sorted =
    rows
    |> Seq.sortBy (fun r -> r.Start)
    |> fun s -> match limit with Some n -> Seq.truncate n s | None -> s
    |> Seq.toList

let escPipe (s: string) = if isNull s then "" else s.Replace("|", "¦")

eprintfn "calendar-week.fsx: %d event(s) matched (window=%s..%s, externalOnly=%b, customerOnly=%b)"
    sorted.Length (windowStart.ToString("yyyy-MM-dd")) (windowEnd.ToString("yyyy-MM-dd")) externalOnly customerOnly

match format with
| "json" ->
    let items =
        sorted |> List.map (fun r ->
            sprintf "{\"start\":\"%s\",\"mins\":%d,\"external\":%b,\"customer\":%b,\"subject\":%s,\"organizer\":%s,\"location\":%s,\"attendees\":%s,\"id\":%s}"
                (r.Start.ToString("o")) r.MinutesLen r.External r.Customer
                (JsonSerializer.Serialize(r.Subject))
                (JsonSerializer.Serialize(r.Organizer))
                (JsonSerializer.Serialize(r.Location))
                (JsonSerializer.Serialize(r.Attendees))
                (JsonSerializer.Serialize(r.Id)))
    printfn "[%s]" (String.concat "," items)
| "tsv" ->
    printfn "DATE\tTIME\tMINS\tEXTERNAL\tCUSTOMER\tSUBJECT\tORGANIZER\tLOCATION\tATTENDEES\tID"
    for r in sorted do
        let lt = r.Start.ToLocalTime()
        printfn "%s\t%s\t%d\t%s\t%s\t%s\t%s\t%s\t%s\t%s"
            (lt.ToString("yyyy-MM-dd")) (lt.ToString("HH:mm")) r.MinutesLen
            (if r.External then "Y" else "N")
            (if r.Customer then "Y" else "N")
            r.Subject r.Organizer r.Location r.Attendees r.Id
| _ ->
    printfn "DATE|TIME|MINS|EXTERNAL|CUSTOMER|SUBJECT|ORGANIZER|LOCATION|ATTENDEES|ID"
    for r in sorted do
        let lt = r.Start.ToLocalTime()
        printfn "%s|%s|%d|%s|%s|%s|%s|%s|%s|%s"
            (lt.ToString("yyyy-MM-dd")) (lt.ToString("HH:mm")) r.MinutesLen
            (if r.External then "Y" else "N")
            (if r.Customer then "Y" else "N")
            (escPipe r.Subject) (escPipe r.Organizer) (escPipe r.Location) (escPipe r.Attendees) r.Id
