#!/usr/bin/env dotnet fsi
/// Teams Messages Filter — parses raw m365_list_chat_messages JSON and emits a
/// compact, HTML-stripped table of messages.
///
/// Usage:
///   dotnet fsi teams-messages.fsx <messages-json-file>
///                                  [--days N]              (default 30)
///                                  [--from "Name"]         (substring match)
///                                  [--mentions "Name"]     (e.g. your own name)
///                                  [--with-attachments]
///                                  [--min-reactions N]
///                                  [--contains "keyword"]  (text search)
///                                  [--limit N]
///                                  [--format table|tsv]    (default table)
///
/// Input JSON shape — friendly Graph shape from m365_list_chat_messages:
///   { "messages": [ { id, from, fromId, content (html), contentType, date, createdDateTime,
///                     replyToId, mentions:[{mentioned:{user:{displayName}}|mentionText}],
///                     attachments:[{name,contentUrl,contentType,content}],
///                     reactions:[{reactionType,userId}] } ] }
///
/// Output columns (pipe-delimited):
///   DATE|FROM|REPLY|REACTIONS|ATTACHMENTS|MENTIONS|TEXT

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open System.Net

let argv = fsi.CommandLineArgs |> Array.skip 1
if argv.Length < 1 then
    eprintfn "Usage: dotnet fsi teams-messages.fsx <messages-json> [--days N] [--from NAME] [--mentions NAME] [--with-attachments] [--min-reactions N] [--contains KW] [--limit N] [--format table|tsv]"
    exit 1

let path = argv.[0]
let getOpt name =
    let i = argv |> Array.tryFindIndex (fun a -> a = name)
    match i with
    | Some k when k + 1 < argv.Length -> Some argv.[k + 1]
    | _ -> None
let hasFlag name = argv |> Array.contains name

let days         = getOpt "--days" |> Option.map int |> Option.defaultValue 30
let fromFilter   = getOpt "--from"
let mentionsF    = getOpt "--mentions"
let withAtts     = hasFlag "--with-attachments"
let minReact     = getOpt "--min-reactions" |> Option.map int |> Option.defaultValue 0
let contains     = getOpt "--contains"
let limit        = getOpt "--limit" |> Option.map int
let format       = getOpt "--format" |> Option.defaultValue "table"

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

let doc = JsonDocument.Parse(File.ReadAllText(path))
let root = doc.RootElement
let msgsEl =
    match getProp root "messages" with
    | Some v -> v
    | None ->
        match getProp root "value" with
        | Some v -> v
        | None -> root

type M = {
    Date: DateTime
    From: string
    ReplyTo: string
    Reactions: int
    ReactionTypes: string
    Attachments: string
    Mentions: string
    Text: string
    Id: string
}

let rows = ResizeArray<M>()

for m in msgsEl.EnumerateArray() do
    let mtype = getStr m "type"
    if mtype = "" || mtype = "message" then
        let id = getStr m "id"
        let fromName = getStr m "from"
        let replyTo = getStr m "replyToId"
        let dateStr = getStr m "date"
        let date = tryParseDate dateStr |> Option.orElseWith (fun () -> tryParseDate (getStr m "createdDateTime"))
        let content = getStr m "content"
        let text = stripHtml content

        let reactions =
            match getProp m "reactions" with
            | Some v when v.ValueKind = JsonValueKind.Array ->
                v.EnumerateArray() |> Seq.toList
            | _ -> []
        let reactionCount = reactions.Length
        let reactionTypes =
            reactions
            |> List.map (fun r -> getStr r "reactionType")
            |> List.filter (fun s -> s <> "")
            |> List.countBy (fun x -> x)
            |> List.map (fun (t, n) -> if n > 1 then sprintf "%s×%d" t n else t)
            |> String.concat " "

        let atts =
            match getProp m "attachments" with
            | Some v when v.ValueKind = JsonValueKind.Array ->
                v.EnumerateArray()
                |> Seq.map (fun a ->
                    let name = getStr a "name"
                    let ct = getStr a "contentType"
                    let url = getStr a "contentUrl"
                    if name <> "" then name
                    elif ct = "messageReference" then "[reply-ref]"
                    elif url <> "" then url
                    else ct)
                |> Seq.filter (fun s -> s <> "")
                |> Seq.toList
            | _ -> []
        let attStr =
            if atts.Length > 3 then
                (atts |> List.take 3 |> String.concat "; ") + sprintf "; +%d more" (atts.Length - 3)
            else String.concat "; " atts

        let mentions =
            match getProp m "mentions" with
            | Some v when v.ValueKind = JsonValueKind.Array ->
                v.EnumerateArray()
                |> Seq.map (fun me ->
                    let mentioned = getProp me "mentioned" |> Option.bind (fun x -> getProp x "user")
                    match mentioned with
                    | Some u -> getStr u "displayName"
                    | None -> getStr me "mentionText")
                |> Seq.filter (fun s -> s <> "")
                |> Seq.toList
            | _ -> []
        let mentionStr = String.concat ", " mentions

        match date with
        | Some d when d >= cutoff ->
            let fromOk =
                match fromFilter with
                | Some f -> fromName.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0
                | None -> true
            let mentionsOk =
                match mentionsF with
                | Some mf -> mentions |> List.exists (fun x -> x.IndexOf(mf, StringComparison.OrdinalIgnoreCase) >= 0)
                | None -> true
            let attOk = not withAtts || atts.Length > 0
            let reactOk = reactionCount >= minReact
            let containsOk =
                match contains with
                | Some c -> text.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0
                | None -> true
            if fromOk && mentionsOk && attOk && reactOk && containsOk then
                rows.Add({
                    Date = d; From = fromName; ReplyTo = replyTo
                    Reactions = reactionCount; ReactionTypes = reactionTypes
                    Attachments = attStr; Mentions = mentionStr
                    Text = (if text.Length > 300 then text.Substring(0,300) + "…" else text)
                    Id = id
                })
        | _ -> ()

let sorted =
    rows
    |> Seq.sortByDescending (fun r -> r.Date)
    |> fun s -> match limit with Some n -> Seq.truncate n s | None -> s
    |> Seq.toList

eprintfn "teams-messages.fsx: %d message(s) matched (window=%d days, from=%s, mentions=%s, withAtt=%b, minReact=%d)"
    sorted.Length days (defaultArg fromFilter "*") (defaultArg mentionsF "*") withAtts minReact

let escPipe (s: string) = if isNull s then "" else s.Replace("|","¦")

match format with
| "tsv" ->
    printfn "DATE\tFROM\tREPLY\tREACTIONS\tATTACHMENTS\tMENTIONS\tTEXT"
    for r in sorted do
        let reactCol = if r.Reactions > 0 then sprintf "%d %s" r.Reactions r.ReactionTypes else ""
        printfn "%s\t%s\t%s\t%s\t%s\t%s\t%s"
            (r.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
            r.From
            (if r.ReplyTo = "" then "" else "↳")
            reactCol r.Attachments r.Mentions r.Text
| _ ->
    printfn "DATE|FROM|REPLY|REACTIONS|ATTACHMENTS|MENTIONS|TEXT"
    for r in sorted do
        let reactCol = if r.Reactions > 0 then sprintf "%d %s" r.Reactions r.ReactionTypes else ""
        printfn "%s|%s|%s|%s|%s|%s|%s"
            (r.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
            (escPipe r.From)
            (if r.ReplyTo = "" then "" else "↳")
            (escPipe reactCol) (escPipe r.Attachments) (escPipe r.Mentions) (escPipe r.Text)
