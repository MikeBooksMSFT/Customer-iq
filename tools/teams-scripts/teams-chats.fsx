#!/usr/bin/env dotnet fsi
/// Teams Chats Filter — parses raw m365_list_chats JSON and emits a compact
/// pipe-delimited table of relevant chats.
///
/// Usage:
///   dotnet fsi teams-chats.fsx <chats-json-file>
///                               [--days N]            (default 7)
///                               [--type meeting|group|oneOnOne]
///                               [--unread-only]
///                               [--person "Name"]     (matches topic OR last sender)
///                               [--topic "keyword"]   (substring match on topic)
///                               [--limit N]           (default unlimited)
///                               [--format table|tsv|json]  (default table)
///
/// Input JSON shape is the "friendly" shape from m365_list_chats:
///   { "chats": [ { id, topic, chatType, createdDateTime, lastUpdatedDateTime,
///                  webUrl, viewpoint:{lastMessageReadDateTime}, lastMessagePreview:{content,from,date,contentType},
///                  members:[{displayName,email}] } ] }
///
/// Output columns (pipe-delimited, safe to paste into a table):
///   DATE|TYPE|UNREAD|TOPIC|LAST_FROM|PREVIEW|MEMBERS|ID|WEBURL

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open System.Net

// ─── Argument parsing ────────────────────────────────────────────────────────
let argv = fsi.CommandLineArgs |> Array.skip 1
if argv.Length < 1 then
    eprintfn "Usage: dotnet fsi teams-chats.fsx <chats-json> [--days N] [--type T] [--unread-only] [--person NAME] [--topic KW] [--limit N] [--format table|tsv|json]"
    exit 1

let path = argv.[0]
let getOpt name =
    let i = argv |> Array.tryFindIndex (fun a -> a = name)
    match i with
    | Some k when k + 1 < argv.Length -> Some argv.[k + 1]
    | _ -> None
let hasFlag name = argv |> Array.contains name

let days    = getOpt "--days"   |> Option.map int |> Option.defaultValue 7
let typeF   = getOpt "--type"
let unread  = hasFlag "--unread-only"
let person  = getOpt "--person"
let topicKw = getOpt "--topic"
let limit   = getOpt "--limit"  |> Option.map int
let format  = getOpt "--format" |> Option.defaultValue "table"

let cutoff = DateTime.UtcNow.AddDays(float -days)

// ─── JSON helpers ────────────────────────────────────────────────────────────
let getProp (el: JsonElement) (name: string) =
    if el.ValueKind <> JsonValueKind.Object then None
    else
        match el.TryGetProperty(name) with
        | true, v when v.ValueKind <> JsonValueKind.Null -> Some v
        | _ -> None

let getStr el name =
    getProp el name
    |> Option.map (fun v -> if v.ValueKind = JsonValueKind.String then v.GetString() else v.ToString())
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

// ─── Load chats ──────────────────────────────────────────────────────────────
let doc = JsonDocument.Parse(File.ReadAllText(path))
let root = doc.RootElement
let chatsEl =
    match getProp root "chats" with
    | Some v -> v
    | None ->
        // allow direct array or a Graph-shaped {value:[...]}
        match getProp root "value" with
        | Some v -> v
        | None -> root

type Row = {
    Date: DateTime
    Type: string
    Unread: bool
    Topic: string
    LastFrom: string
    Preview: string
    Members: string
    Id: string
    WebUrl: string
}

let rows = ResizeArray<Row>()

for c in chatsEl.EnumerateArray() do
    let chatType = getStr c "chatType"
    let topic = getStr c "topic"
    let id = getStr c "id"
    let webUrl = getStr c "webUrl"
    let lastUpdated = getStr c "lastUpdatedDateTime"
    let lmp = getProp c "lastMessagePreview"
    let vp = getProp c "viewpoint"
    let lastMsgDate = lmp |> Option.bind (fun x -> getProp x "date") |> Option.map (fun v -> v.GetString()) |> Option.defaultValue ""
    let readAt = vp |> Option.bind (fun x -> getProp x "lastMessageReadDateTime") |> Option.map (fun v -> v.GetString()) |> Option.defaultValue ""
    let fromName = lmp |> Option.bind (fun x -> getProp x "from") |> Option.map (fun v -> v.GetString()) |> Option.defaultValue ""
    let rawContent = lmp |> Option.bind (fun x -> getProp x "content") |> Option.map (fun v -> v.GetString()) |> Option.defaultValue ""
    let preview =
        let t = stripHtml rawContent
        if t.Length > 140 then t.Substring(0, 140) + "…" else t

    let membersList =
        match getProp c "members" with
        | Some v when v.ValueKind = JsonValueKind.Array ->
            v.EnumerateArray()
            |> Seq.map (fun m -> getStr m "displayName")
            |> Seq.filter (fun s -> s <> "")
            |> Seq.distinct
            |> Seq.toList
        | _ -> []
    let members =
        if membersList.Length > 5 then
            (membersList |> List.take 5 |> String.concat ", ") + sprintf ", +%d more" (membersList.Length - 5)
        else String.concat ", " membersList

    let effectiveDate =
        match tryParseDate lastMsgDate with
        | Some d -> Some d
        | None -> tryParseDate lastUpdated

    let isUnread =
        match tryParseDate lastMsgDate, tryParseDate readAt with
        | Some m, Some r -> m > r
        | Some _, None -> true
        | _ -> false

    match effectiveDate with
    | Some d when d >= cutoff ->
        let typeOk = typeF |> Option.map (fun t -> t.Equals(chatType, StringComparison.OrdinalIgnoreCase)) |> Option.defaultValue true
        let unreadOk = not unread || isUnread
        let personOk =
            match person with
            | Some p ->
                let pl = p.ToLowerInvariant()
                topic.ToLowerInvariant().Contains(pl)
                || fromName.ToLowerInvariant().Contains(pl)
                || (members.ToLowerInvariant().Contains(pl))
            | None -> true
        let topicOk =
            match topicKw with
            | Some k -> topic.ToLowerInvariant().Contains(k.ToLowerInvariant())
            | None -> true
        if typeOk && unreadOk && personOk && topicOk then
            rows.Add({
                Date = d; Type = chatType; Unread = isUnread
                Topic = (if topic = "" then "(1:1)" else topic)
                LastFrom = fromName; Preview = preview; Members = members
                Id = id; WebUrl = webUrl
            })
    | _ -> ()

let sorted =
    rows
    |> Seq.sortByDescending (fun r -> r.Date)
    |> fun s -> match limit with Some n -> Seq.truncate n s | None -> s
    |> Seq.toList

// ─── Output ─────────────────────────────────────────────────────────────────
let escPipe (s: string) = s.Replace("|", "¦")

eprintfn "teams-chats.fsx: %d chat(s) matched (window=%d days, type=%s, unread=%b, person=%s, topic=%s)"
    sorted.Length days (defaultArg typeF "*") unread (defaultArg person "*") (defaultArg topicKw "*")

match format with
| "json" ->
    let items =
        sorted |> List.map (fun r ->
            sprintf "{\"date\":\"%s\",\"type\":\"%s\",\"unread\":%b,\"topic\":%s,\"from\":%s,\"preview\":%s,\"members\":%s,\"id\":%s,\"webUrl\":%s}"
                (r.Date.ToString("o"))
                r.Type r.Unread
                (JsonSerializer.Serialize(r.Topic))
                (JsonSerializer.Serialize(r.LastFrom))
                (JsonSerializer.Serialize(r.Preview))
                (JsonSerializer.Serialize(r.Members))
                (JsonSerializer.Serialize(r.Id))
                (JsonSerializer.Serialize(r.WebUrl)))
    printfn "[%s]" (String.concat "," items)
| "tsv" ->
    printfn "DATE\tTYPE\tUNREAD\tTOPIC\tLAST_FROM\tPREVIEW\tMEMBERS\tID\tWEBURL"
    for r in sorted do
        printfn "%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s"
            (r.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
            r.Type
            (if r.Unread then "Y" else "N")
            r.Topic r.LastFrom r.Preview r.Members r.Id r.WebUrl
| _ ->
    printfn "DATE|TYPE|UNREAD|TOPIC|LAST_FROM|PREVIEW|MEMBERS|ID|WEBURL"
    for r in sorted do
        printfn "%s|%s|%s|%s|%s|%s|%s|%s|%s"
            (r.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
            r.Type
            (if r.Unread then "Y" else "N")
            (escPipe r.Topic) (escPipe r.LastFrom) (escPipe r.Preview)
            (escPipe r.Members) r.Id r.WebUrl
