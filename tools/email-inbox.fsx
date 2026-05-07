#!/usr/bin/env dotnet fsi
/// Email Inbox Filter — parses raw m365_list_emails / m365_search_emails JSON
/// and emits a compact pipe-delimited table of relevant messages.
///
/// Usage:
///   dotnet fsi email-inbox.fsx <emails-json-file>
///                               [--days N]               (default 7)
///                               [--unread-only]
///                               [--from "substring"]
///                               [--subject "substring"]
///                               [--customer-only]        (match against accounts.csv)
///                               [--external-only]        (non-@microsoft.com sender)
///                               [--importance high|normal|low]
///                               [--with-attachments]
///                               [--accounts-csv PATH]    (default accounts.csv alongside this script)
///                               [--limit N]
///                               [--format table|tsv|json]   (default table)
///
/// Input JSON shape — the friendly shape returned by m365_list_emails:
///   { "emails": [ { id, subject, from:{name,address}, toRecipients:[{name,address}],
///                   ccRecipients:[...], receivedDateTime, isRead, importance,
///                   hasAttachments, bodyPreview, webLink, conversationId } ] }
///
/// Output columns (pipe-delimited):
///   DATE|FROM|EMAIL|UNREAD|IMPORTANCE|ATTACH|CUSTOMER|SUBJECT|PREVIEW|ID

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open System.Net

let argv = fsi.CommandLineArgs |> Array.skip 1
if argv.Length < 1 then
    eprintfn "Usage: dotnet fsi email-inbox.fsx <emails-json> [--days N] [--unread-only] [--from S] [--subject S] [--customer-only] [--external-only] [--importance L] [--with-attachments] [--accounts-csv PATH] [--limit N] [--format table|tsv|json]"
    exit 1

let path = argv.[0]
let getOpt name =
    let i = argv |> Array.tryFindIndex (fun a -> a = name)
    match i with
    | Some k when k + 1 < argv.Length -> Some argv.[k + 1]
    | _ -> None
let hasFlag name = argv |> Array.contains name

let days         = getOpt "--days"   |> Option.map int |> Option.defaultValue 7
let unreadOnly   = hasFlag "--unread-only"
let fromSub      = getOpt "--from"
let subjSub      = getOpt "--subject"
let customerOnly = hasFlag "--customer-only"
let externalOnly = hasFlag "--external-only"
let importance   = getOpt "--importance"
let withAttach   = hasFlag "--with-attachments"
let limit        = getOpt "--limit"  |> Option.map int
let format       = getOpt "--format" |> Option.defaultValue "table"

let scriptDir = Path.GetDirectoryName(fsi.CommandLineArgs.[0])
let defaultCsv = Path.Combine(scriptDir, "accounts.csv")
let accountsCsv = getOpt "--accounts-csv" |> Option.defaultValue defaultCsv

let cutoff = DateTime.UtcNow.AddDays(float -days)

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

let stripHtml (s: string) =
    if isNull s then ""
    else
        let noTags = Regex.Replace(s, "<[^>]+>", " ")
        let decoded = WebUtility.HtmlDecode(noTags)
        Regex.Replace(decoded, @"\s+", " ").Trim()

// ─── Internal-pattern filter (mirrors msx-filter.fsx) ───────────────────────
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

/// Extract the first significant 4+ char token of each account name for fuzzy match.
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
let emailsEl =
    match getProp root "emails" with
    | Some v -> v
    | None ->
        match getProp root "value" with
        | Some v -> v
        | None -> root

type Row = {
    Date: DateTime
    FromName: string
    FromEmail: string
    Unread: bool
    Importance: string
    HasAttach: bool
    IsCustomer: bool
    Subject: string
    Preview: string
    Id: string
}

let rows = ResizeArray<Row>()

for e in emailsEl.EnumerateArray() do
    let subject = getStr e "subject"
    let id = getStr e "id"
    let received = getStr e "receivedDateTime"
    let importanceVal = getStr e "importance"
    let hasAttach = getBool e "hasAttachments"
    let isRead = getBool e "isRead"
    let body = stripHtml (getStr e "bodyPreview")

    let fromObj = getProp e "from"
    let fromName, fromEmail =
        match fromObj with
        | Some f ->
            let addrObj =
                match getProp f "emailAddress" with
                | Some a -> a
                | None -> f
            (getStr addrObj "name", (getStr addrObj "address").ToLowerInvariant())
        | None -> "", ""

    let isExternal =
        fromEmail <> "" && not (fromEmail.EndsWith("@microsoft.com"))
    let isCustomer =
        isExternal || subjectMatchesCustomer subject

    match tryParseDate received with
    | Some d when d >= cutoff ->
        let skipInternal = isInternalSubject subject && not isExternal
        let passUnread = not unreadOnly || (not isRead)
        let passFrom = fromSub |> Option.map (fun s ->
            fromName.ToLowerInvariant().Contains(s.ToLowerInvariant())
            || fromEmail.Contains(s.ToLowerInvariant())) |> Option.defaultValue true
        let passSubj = subjSub |> Option.map (fun s -> subject.ToLowerInvariant().Contains(s.ToLowerInvariant())) |> Option.defaultValue true
        let passCustomer = not customerOnly || isCustomer
        let passExternal = not externalOnly || isExternal
        let passImportance = importance |> Option.map (fun i -> i.Equals(importanceVal, StringComparison.OrdinalIgnoreCase)) |> Option.defaultValue true
        let passAttach = not withAttach || hasAttach
        if (not skipInternal) && passUnread && passFrom && passSubj && passCustomer && passExternal && passImportance && passAttach then
            let preview = if body.Length > 160 then body.Substring(0, 160) + "…" else body
            rows.Add({
                Date = d; FromName = fromName; FromEmail = fromEmail
                Unread = not isRead; Importance = importanceVal; HasAttach = hasAttach
                IsCustomer = isCustomer; Subject = subject; Preview = preview; Id = id
            })
    | _ -> ()

let sorted =
    rows
    |> Seq.sortByDescending (fun r -> r.Date)
    |> fun s -> match limit with Some n -> Seq.truncate n s | None -> s
    |> Seq.toList

let escPipe (s: string) = if isNull s then "" else s.Replace("|", "¦")

eprintfn "email-inbox.fsx: %d email(s) matched (window=%d days, unread-only=%b, customer-only=%b, external-only=%b, accounts=%d)"
    sorted.Length days unreadOnly customerOnly externalOnly accountNames.Length

match format with
| "json" ->
    let items =
        sorted |> List.map (fun r ->
            sprintf "{\"date\":\"%s\",\"from\":%s,\"email\":%s,\"unread\":%b,\"importance\":%s,\"hasAttach\":%b,\"customer\":%b,\"subject\":%s,\"preview\":%s,\"id\":%s}"
                (r.Date.ToString("o"))
                (JsonSerializer.Serialize(r.FromName))
                (JsonSerializer.Serialize(r.FromEmail))
                r.Unread
                (JsonSerializer.Serialize(r.Importance))
                r.HasAttach r.IsCustomer
                (JsonSerializer.Serialize(r.Subject))
                (JsonSerializer.Serialize(r.Preview))
                (JsonSerializer.Serialize(r.Id)))
    printfn "[%s]" (String.concat "," items)
| "tsv" ->
    printfn "DATE\tFROM\tEMAIL\tUNREAD\tIMPORTANCE\tATTACH\tCUSTOMER\tSUBJECT\tPREVIEW\tID"
    for r in sorted do
        printfn "%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s"
            (r.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
            r.FromName r.FromEmail
            (if r.Unread then "Y" else "N")
            r.Importance
            (if r.HasAttach then "Y" else "N")
            (if r.IsCustomer then "Y" else "N")
            r.Subject r.Preview r.Id
| _ ->
    printfn "DATE|FROM|EMAIL|UNREAD|IMPORTANCE|ATTACH|CUSTOMER|SUBJECT|PREVIEW|ID"
    for r in sorted do
        printfn "%s|%s|%s|%s|%s|%s|%s|%s|%s|%s"
            (r.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
            (escPipe r.FromName) (escPipe r.FromEmail)
            (if r.Unread then "Y" else "N")
            (escPipe r.Importance)
            (if r.HasAttach then "Y" else "N")
            (if r.IsCustomer then "Y" else "N")
            (escPipe r.Subject) (escPipe r.Preview) r.Id
