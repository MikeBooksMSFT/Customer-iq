#!/usr/bin/env dotnet fsi
/// Teams Activity Roll-up — takes one or more m365_list_chats JSON files and
/// produces a high-level activity overview.
///
/// Usage:
///   dotnet fsi teams-activity.fsx <chats-json> [<chats-json> ...]
///                                  [--days N]           (default 7)
///                                  [--my-name "Name"]   (exclude from "top senders")
///
/// Sections:
///   # SUMMARY              (totals: chats touched, unread, meeting/group/1:1)
///   # TOP SENDERS          (last_from frequency across chats)
///   # UNREAD               (chats with viewpoint older than last message)
///   # BUSY MEETING CHATS   (meeting chats ranked by recency)
///   # HOT GROUP CHATS      (group chats ranked by recency)

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open System.Net

let argv = fsi.CommandLineArgs |> Array.skip 1
if argv.Length < 1 then
    eprintfn "Usage: dotnet fsi teams-activity.fsx <chats-json> [<chats-json> ...] [--days N] [--my-name NAME]"
    exit 1

let getOpt name =
    let i = argv |> Array.tryFindIndex (fun a -> a = name)
    match i with
    | Some k when k + 1 < argv.Length -> Some argv.[k + 1]
    | _ -> None

let days    = getOpt "--days" |> Option.map int |> Option.defaultValue 7
let myName  = getOpt "--my-name"
let paths =
    argv
    |> Array.indexed
    |> Array.filter (fun (_, a) -> not (a.StartsWith("--")))
    |> Array.filter (fun (i, _) ->
        // not the value immediately after a --flag
        i = 0 || not (argv.[i-1].StartsWith("--")))
    |> Array.map snd

let cutoff = DateTime.UtcNow.AddDays(float -days)

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
    if String.IsNullOrEmpty s then ""
    else
        let noTags = Regex.Replace(s, "<[^>]+>", " ")
        let decoded = WebUtility.HtmlDecode(noTags)
        Regex.Replace(decoded, @"\s+", " ").Trim()

type C = {
    Id: string; Topic: string; Type: string
    Date: DateTime; From: string; Preview: string
    Unread: bool
}

let loadFile (p: string) =
    let doc = JsonDocument.Parse(File.ReadAllText(p))
    let root = doc.RootElement
    let chatsEl =
        match getProp root "chats" with
        | Some v -> v
        | None ->
            match getProp root "value" with
            | Some v -> v
            | None -> root
    [
        for c in chatsEl.EnumerateArray() do
            let lmp = getProp c "lastMessagePreview"
            let vp = getProp c "viewpoint"
            let lastDate = lmp |> Option.bind (fun x -> getProp x "date") |> Option.map (fun v -> v.GetString()) |> Option.defaultValue ""
            let readAt = vp |> Option.bind (fun x -> getProp x "lastMessageReadDateTime") |> Option.map (fun v -> v.GetString()) |> Option.defaultValue ""
            let fromName = lmp |> Option.bind (fun x -> getProp x "from") |> Option.map (fun v -> v.GetString()) |> Option.defaultValue ""
            let content = lmp |> Option.bind (fun x -> getProp x "content") |> Option.map (fun v -> v.GetString()) |> Option.defaultValue ""
            let effective =
                match tryParseDate lastDate with
                | Some d -> Some d
                | None -> tryParseDate (getStr c "lastUpdatedDateTime")
            let unread =
                match tryParseDate lastDate, tryParseDate readAt with
                | Some m, Some r -> m > r
                | Some _, None -> true
                | _ -> false
            match effective with
            | Some d when d >= cutoff ->
                let topic = getStr c "topic"
                yield {
                    Id = getStr c "id"
                    Topic = if topic = "" then "(1:1)" else topic
                    Type = getStr c "chatType"
                    Date = d; From = fromName
                    Preview = stripHtml content
                    Unread = unread
                }
            | _ -> ()
    ]

let all =
    paths
    |> Array.collect (fun p -> loadFile p |> List.toArray)
    |> Array.distinctBy (fun c -> c.Id)
    |> Array.toList

let byType = all |> List.countBy (fun c -> c.Type) |> Map.ofList
let unread = all |> List.filter (fun c -> c.Unread)

printfn "# SUMMARY (window=%d days)" days
printfn "- Chats touched:  %d" all.Length
printfn "- Unread:         %d" unread.Length
printfn "- Meeting chats:  %d" (Map.tryFind "meeting" byType |> Option.defaultValue 0)
printfn "- Group chats:    %d" (Map.tryFind "group" byType |> Option.defaultValue 0)
printfn "- 1:1 chats:      %d" (Map.tryFind "oneOnOne" byType |> Option.defaultValue 0)
printfn ""

printfn "# TOP SENDERS"
let senders =
    all
    |> List.choose (fun c -> if c.From = "" then None else Some c.From)
    |> List.filter (fun s ->
        match myName with
        | Some m -> s.IndexOf(m, StringComparison.OrdinalIgnoreCase) < 0
        | None -> true)
    |> List.countBy id
    |> List.sortByDescending snd
    |> List.truncate 10
for (name, count) in senders do
    printfn "  %-30s  %d" name count
printfn ""

printfn "# UNREAD (%d)" unread.Length
for c in unread |> List.sortByDescending (fun c -> c.Date) do
    let typ =
        match c.Type with
        | "meeting" -> "mtg"
        | "group" -> "grp"
        | "oneOnOne" -> "1:1"
        | s -> s
    let prev = if c.Preview.Length > 80 then c.Preview.Substring(0,80) + "…" else c.Preview
    printfn "  %s  %-3s  %-40s  (%s) %s"
        (c.Date.ToLocalTime().ToString("MM-dd HH:mm")) typ
        (if c.Topic.Length > 40 then c.Topic.Substring(0,40) else c.Topic)
        c.From prev
printfn ""

let meetingChats = all |> List.filter (fun c -> c.Type = "meeting")
printfn "# BUSY MEETING CHATS (%d)" meetingChats.Length
for c in meetingChats |> List.sortByDescending (fun c -> c.Date) |> List.truncate 15 do
    let u = if c.Unread then "🔴" else "  "
    printfn "  %s %s  %-50s  %s"
        (c.Date.ToLocalTime().ToString("MM-dd HH:mm")) u
        (if c.Topic.Length > 50 then c.Topic.Substring(0,50) else c.Topic)
        c.From
printfn ""

let groupChats = all |> List.filter (fun c -> c.Type = "group")
printfn "# HOT GROUP CHATS (%d)" groupChats.Length
for c in groupChats |> List.sortByDescending (fun c -> c.Date) |> List.truncate 10 do
    let u = if c.Unread then "🔴" else "  "
    let prev = if c.Preview.Length > 80 then c.Preview.Substring(0,80) + "…" else c.Preview
    printfn "  %s %s  %-40s  (%s) %s"
        (c.Date.ToLocalTime().ToString("MM-dd HH:mm")) u
        (if c.Topic.Length > 40 then c.Topic.Substring(0,40) else c.Topic)
        c.From prev
