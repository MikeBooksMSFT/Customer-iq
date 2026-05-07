#!/usr/bin/env dotnet fsi
/// Follow-ups Aging — correlates an inbox dump with a sent-items dump and
/// surfaces conversations where the user's last message has gone unanswered,
/// plus unread inbound items older than N days that still need a reply.
///
/// Usage:
///   dotnet fsi followups-aging.fsx <inbox-json> <sent-json>
///                                  [--my-email <your-email>]
///                                  [--min-age-days N]   (default 2)
///                                  [--max-age-days N]   (default 21)
///                                  [--external-only]    (skip internal recipients)
///                                  [--limit N]
///                                  [--format table|tsv|json]
///
/// Logic:
///   • Group both feeds by conversationId.
///   • For each conversation, pick the latest message and its sender.
///     - If the latest sender is ME and the window is > min-age-days → "awaiting-reply"
///     - If the latest sender is NOT me, I've never replied in the thread,
///       and age > min-age-days → "needs-reply"
///   • Skip conversations > max-age-days old (stale).
///
/// Output columns:
///   AGE_DAYS|STATE|COUNTERPARTY|EMAIL|SUBJECT|LAST_DATE|MY_LAST_DATE|CONV_ID|LAST_MSG_ID

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open System.Net

let argv = fsi.CommandLineArgs |> Array.skip 1
if argv.Length < 2 then
    eprintfn "Usage: dotnet fsi followups-aging.fsx <inbox-json> <sent-json> [--my-email E] [--min-age-days N] [--max-age-days N] [--external-only] [--limit N] [--format table|tsv|json]"
    exit 1

let inboxPath = argv.[0]
let sentPath  = argv.[1]
let getOpt name =
    let i = argv |> Array.tryFindIndex (fun a -> a = name)
    match i with
    | Some k when k + 1 < argv.Length -> Some argv.[k + 1]
    | _ -> None
let hasFlag name = argv |> Array.contains name

let myEmail   = (getOpt "--my-email" |> Option.defaultValue "<your-email>").ToLowerInvariant()
let minAge    = getOpt "--min-age-days" |> Option.map int |> Option.defaultValue 2
let maxAge    = getOpt "--max-age-days" |> Option.map int |> Option.defaultValue 21
let externalOnly = hasFlag "--external-only"
let limit     = getOpt "--limit" |> Option.map int
let format    = getOpt "--format" |> Option.defaultValue "table"

let now = DateTime.UtcNow

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
        | _ -> v.ToString())
    |> Option.defaultValue ""

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

type Msg = {
    Id: string
    ConvId: string
    Date: DateTime
    FromName: string
    FromEmail: string
    ToEmails: string list
    ToNames: string list
    Subject: string
    IsFromMe: bool
    HasExternal: bool
}

let fromPair el =
    match getProp el "from" with
    | Some f ->
        let a =
            match getProp f "emailAddress" with
            | Some x -> x
            | None -> f
        (getStr a "name", (getStr a "address").ToLowerInvariant())
    | None -> "", ""

let recipients el name =
    match getProp el name with
    | Some v when v.ValueKind = JsonValueKind.Array ->
        v.EnumerateArray()
        |> Seq.map (fun r ->
            let a =
                match getProp r "emailAddress" with
                | Some x -> x
                | None -> r
            (getStr a "name", (getStr a "address").ToLowerInvariant()))
        |> Seq.toList
    | _ -> []

let loadMessages (path: string) =
    let doc = JsonDocument.Parse(File.ReadAllText(path))
    let root = doc.RootElement
    let arr =
        match getProp root "emails" with
        | Some v -> v
        | None ->
            match getProp root "value" with
            | Some v -> v
            | None -> root
    [
        for e in arr.EnumerateArray() do
            let dateStr = getStr e "receivedDateTime"
            match tryParseDate dateStr with
            | Some d ->
                let fromName, fromEmail = fromPair e
                let toPairs = recipients e "toRecipients"
                let ccPairs = recipients e "ccRecipients"
                let all = toPairs @ ccPairs
                let isFromMe = fromEmail = myEmail
                let hasExternal =
                    (all |> List.exists (fun (_, em) -> em <> "" && not (em.EndsWith("@microsoft.com"))))
                    || (not (fromEmail.EndsWith("@microsoft.com")) && fromEmail <> "")
                yield {
                    Id = getStr e "id"
                    ConvId = getStr e "conversationId"
                    Date = d
                    FromName = fromName; FromEmail = fromEmail
                    ToEmails = all |> List.map snd
                    ToNames = all |> List.map fst
                    Subject = getStr e "subject"
                    IsFromMe = isFromMe
                    HasExternal = hasExternal
                }
            | None -> ()
    ]

let inbox = loadMessages inboxPath
let sent  = loadMessages sentPath
let allMsgs = inbox @ sent

let byConv =
    allMsgs
    |> List.filter (fun m -> m.ConvId <> "")
    |> List.groupBy (fun m -> m.ConvId)

type Row = {
    AgeDays: float
    State: string
    Counterparty: string
    CpEmail: string
    Subject: string
    LastDate: DateTime
    MyLastDate: DateTime option
    ConvId: string
    LastMsgId: string
}

let rows = ResizeArray<Row>()

for convId, msgs in byConv do
    let msgsSorted = msgs |> List.sortByDescending (fun m -> m.Date)
    let latest = msgsSorted |> List.head
    let ageDays = (now - latest.Date).TotalDays
    if ageDays >= float minAge && ageDays <= float maxAge then
        let mine = msgsSorted |> List.filter (fun m -> m.IsFromMe)
        let myLast = mine |> List.tryHead |> Option.map (fun m -> m.Date)
        let passExternal = not externalOnly || latest.HasExternal
        if passExternal then
            if latest.IsFromMe then
                // awaiting reply from counterparty
                let cpName, cpEmail =
                    match latest.ToNames, latest.ToEmails with
                    | n :: _, e :: _ -> (if n <> "" then n else e), e
                    | _ -> "(unknown)", ""
                rows.Add({
                    AgeDays = ageDays; State = "awaiting-reply"
                    Counterparty = cpName; CpEmail = cpEmail
                    Subject = latest.Subject; LastDate = latest.Date
                    MyLastDate = myLast; ConvId = convId; LastMsgId = latest.Id
                })
            else
                // inbound waiting on me — only include if I've never replied
                // OR my reply is older than the latest inbound
                let needsReply =
                    match myLast with
                    | None -> true
                    | Some m -> m < latest.Date
                if needsReply then
                    rows.Add({
                        AgeDays = ageDays; State = "needs-reply"
                        Counterparty = latest.FromName; CpEmail = latest.FromEmail
                        Subject = latest.Subject; LastDate = latest.Date
                        MyLastDate = myLast; ConvId = convId; LastMsgId = latest.Id
                    })

let sorted =
    rows
    |> Seq.sortByDescending (fun r -> r.AgeDays)
    |> fun s -> match limit with Some n -> Seq.truncate n s | None -> s
    |> Seq.toList

let escPipe (s: string) = if isNull s then "" else s.Replace("|", "¦")

eprintfn "followups-aging.fsx: %d item(s) (window=%d..%d days, external-only=%b, me=%s)"
    sorted.Length minAge maxAge externalOnly myEmail

match format with
| "json" ->
    let items =
        sorted |> List.map (fun r ->
            sprintf "{\"ageDays\":%.1f,\"state\":\"%s\",\"counterparty\":%s,\"email\":%s,\"subject\":%s,\"lastDate\":\"%s\",\"myLastDate\":%s,\"convId\":%s,\"lastMsgId\":%s}"
                r.AgeDays r.State
                (JsonSerializer.Serialize(r.Counterparty))
                (JsonSerializer.Serialize(r.CpEmail))
                (JsonSerializer.Serialize(r.Subject))
                (r.LastDate.ToString("o"))
                (match r.MyLastDate with Some d -> "\"" + d.ToString("o") + "\"" | None -> "null")
                (JsonSerializer.Serialize(r.ConvId))
                (JsonSerializer.Serialize(r.LastMsgId)))
    printfn "[%s]" (String.concat "," items)
| "tsv" ->
    printfn "AGE_DAYS\tSTATE\tCOUNTERPARTY\tEMAIL\tSUBJECT\tLAST_DATE\tMY_LAST_DATE\tCONV_ID\tLAST_MSG_ID"
    for r in sorted do
        printfn "%.1f\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s"
            r.AgeDays r.State r.Counterparty r.CpEmail r.Subject
            (r.LastDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
            (match r.MyLastDate with Some d -> d.ToLocalTime().ToString("yyyy-MM-dd HH:mm") | None -> "-")
            r.ConvId r.LastMsgId
| _ ->
    printfn "AGE_DAYS|STATE|COUNTERPARTY|EMAIL|SUBJECT|LAST_DATE|MY_LAST_DATE|CONV_ID|LAST_MSG_ID"
    for r in sorted do
        printfn "%.1f|%s|%s|%s|%s|%s|%s|%s|%s"
            r.AgeDays r.State
            (escPipe r.Counterparty) (escPipe r.CpEmail) (escPipe r.Subject)
            (r.LastDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
            (match r.MyLastDate with Some d -> d.ToLocalTime().ToString("yyyy-MM-dd HH:mm") | None -> "-")
            r.ConvId r.LastMsgId
