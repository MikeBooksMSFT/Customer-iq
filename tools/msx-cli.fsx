#!/usr/bin/env dotnet fsi
/// MSX CLI — Dynamics 365 / MSX tool powered by az cli tokens.
/// Usage: dotnet fsi msx-cli.fsx <command> [args...]
///
/// Commands:
///
/// ─── PORTFOLIO INTELLIGENCE ───
///   revenue-all [--segment=X] [--min=0]                  GitHub billed revenue across all accounts
///   seats-all [--segment=X]                              GitHub milestone seats/usage ranked by monthly use
///   pipeline [--segment=X] [--solution=GitHub]           Open deals with close dates, risk, competitors
///   competitors [--segment=X]                            Competitive landscape for GitHub/GHAS opportunities
///   consumption-plan [--product=606820000]               GitHub consumption plans (budget/target/actuals)
///
/// ─── ACCOUNT DEEP-DIVE ───
///   account-health <salesId>                             Full intelligence: metadata + plan + opps + milestones + consumption
///   account-enrichment <salesId1,salesId2,...>            Prospecting metadata: dev count, LinkedIn ID, domain
///   account-tasks <salesId> [--days=365] [--all-users]   All tasks for one account
///
/// ─── STRATEGIC INTELLIGENCE ───
///   account-plan <salesId>                               Deep account plan: mission, challenges, compete strategy, health
///   stakeholders <salesId>                               Org chart from stakeholder maps (roles, sentiment, influence)
///   close-plan <salesId|oppId>                           Deal close plan: commercial strategy, timeline, budget, risks
///   deal-team <salesId|oppId>                            Who's on the deal team
///   violations [--all]                                   Milestone hygiene violations (default: mine only)
///   account-team <salesId>                               Full account team roster (roles, primary, dates)
///
/// ─── ACTIVITY TRACKING ───
///   my-activity <start> <end> [--top=500]                Fully resolved: ACCOUNT|SALES_ID|DATE|SUBJECT|CATEGORY|MILESTONE|STATE
///   coverage [--segment=Accelerate]                      Per-account summary: activity count, last touch, days since
///   odata <entity> <filter> [--select=X] [--top=N] [--orderby=X] [--expand=X]
///                                                        Raw OData query → auto-formatted pipe-delimited output
///
/// ─── MSX OPERATIONS ───
///   whoami                                                Get current user info
///   my-tasks <start> <end>                                Raw tasks (unresolved milestone IDs — use my-activity instead)
///   milestones <salesId1,salesId2,...> [--keyword=X] [--completed]
///   contacts <salesId1,salesId2,...> [--keyword=X]        Get account contacts
///   contacts-odata <filter>                               Search contacts with a raw OData filter
///   create-task <milestoneId> <subject> [--date=X] [--desc=X] [--cat=N] [--priority=N] [--due=X]
///   create-tasks <json-file>                              Bulk-create from JSON array
///   activities <milestoneId> [--days=30]                  Recent activities for a milestone
///   team <milestoneId>                                    Get milestone team members
///   add-team <milestoneId> <userId> [--role=SSP] [--area=X]
///   complete-tasks <odata-filter>                         Bulk PATCH open tasks to completed
///   patch-task <taskId>                                   PATCH single task to completed
///   reclassify <milestoneId> [--to=Demo] [--dry-run]     Re-categorize tasks away from Customer Meeting
///   categories                                            List valid high-value task categories

open System
open System.Diagnostics
open System.Globalization
open System.IO
open System.Net.Http
open System.Text
open System.Text.Json

// ─── Config ──────────────────────────────────────────────────────────────────

let msxResource =
    match Environment.GetEnvironmentVariable("MSX_RESOURCE_URL") with
    | null | "" -> "https://YOUR-DYNAMICS-ORG.crm.dynamics.com"
    | v -> v.TrimEnd('/')

let baseUrl = msxResource + "/api/data/v9.2"
// ─── Token ───────────────────────────────────────────────────────────────────

let mutable token = ""

let getToken () =
    if token <> "" then token
    else
        let psi = ProcessStartInfo("cmd.exe", "/c az account get-access-token --resource " + msxResource + "/ --query accessToken -o tsv")
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true
        let p = Process.Start(psi)
        let out = p.StandardOutput.ReadToEnd().Trim()
        p.WaitForExit()
        if p.ExitCode <> 0 then failwith "az account get-access-token failed"
        token <- out
        token

// ─── HTTP helpers ────────────────────────────────────────────────────────────

let client = new HttpClient()

let get (path: string) =
    let url = baseUrl + path
    use req = new HttpRequestMessage(HttpMethod.Get, url)
    req.Headers.Authorization <- Headers.AuthenticationHeaderValue("Bearer", getToken())
    req.Headers.Add("OData-MaxVersion", "4.0")
    req.Headers.Add("OData-Version", "4.0")
    req.Headers.Accept.Add(Headers.MediaTypeWithQualityHeaderValue("application/json"))
    let resp = client.Send(req)
    let stream = resp.Content.ReadAsStream()
    use reader = new StreamReader(stream)
    let body = reader.ReadToEnd()
    if not resp.IsSuccessStatusCode then
        eprintfn "GET %s → %d" path (int resp.StatusCode)
        eprintfn "%s" body
    body

let post (path: string) (json: string) =
    let url = baseUrl + path
    use req = new HttpRequestMessage(HttpMethod.Post, url)
    req.Headers.Authorization <- Headers.AuthenticationHeaderValue("Bearer", getToken())
    req.Headers.Add("OData-MaxVersion", "4.0")
    req.Headers.Add("OData-Version", "4.0")
    req.Headers.Accept.Add(Headers.MediaTypeWithQualityHeaderValue("application/json"))
    req.Content <- new StringContent(json, Encoding.UTF8, "application/json")
    let resp = client.Send(req)
    let stream = resp.Content.ReadAsStream()
    use reader = new StreamReader(stream)
    let body = reader.ReadToEnd()
    let id =
        if resp.Headers.Contains("OData-EntityId") then
            let raw = resp.Headers.GetValues("OData-EntityId") |> Seq.head
            let i = raw.LastIndexOf('(')
            let j = raw.LastIndexOf(')')
            if i >= 0 && j > i then raw.Substring(i+1, j-i-1) else ""
        else ""
    if not resp.IsSuccessStatusCode then
        eprintfn "POST %s → %d" path (int resp.StatusCode)
        eprintfn "%s" body
    (resp.IsSuccessStatusCode, id, body)

let patch (path: string) (json: string) =
    let url = baseUrl + path
    use req = new HttpRequestMessage(HttpMethod.Patch, url)
    req.Headers.Authorization <- Headers.AuthenticationHeaderValue("Bearer", getToken())
    req.Headers.Add("OData-MaxVersion", "4.0")
    req.Headers.Add("OData-Version", "4.0")
    req.Headers.Accept.Add(Headers.MediaTypeWithQualityHeaderValue("application/json"))
    req.Content <- new StringContent(json, Encoding.UTF8, "application/json")
    let resp = client.Send(req)
    let stream = resp.Content.ReadAsStream()
    use reader = new StreamReader(stream)
    let body = reader.ReadToEnd()
    if not resp.IsSuccessStatusCode then
        eprintfn "PATCH %s → %d" path (int resp.StatusCode)
        eprintfn "%s" body
    resp.IsSuccessStatusCode

// ─── JSON helpers ────────────────────────────────────────────────────────────

let getArray (body: string) =
    let doc = JsonDocument.Parse(body)
    let mutable v = Unchecked.defaultof<JsonElement>
    if doc.RootElement.TryGetProperty("value", &v) then
        v.EnumerateArray() |> Seq.toArray
    else [||]

let getProp (el: JsonElement) (name: string) =
    match el.TryGetProperty(name) with
    | true, v when v.ValueKind <> JsonValueKind.Null -> v.ToString()
    | _ -> ""

let esc (s: string) = s.Replace("'", "''")

// ─── Arg parsing helpers ─────────────────────────────────────────────────────

let args = fsi.CommandLineArgs |> Array.skip 1  // skip script name

let getFlag (name: string) =
    args |> Array.exists (fun a -> a = name)

let getOpt (prefix: string) =
    args |> Array.tryFind (fun a -> a.StartsWith(prefix))
    |> Option.map (fun a -> a.Substring(prefix.Length))

// ─── Task category mapping (7 high-value SE Dashboard categories ONLY) ──────
// NEVER use 861980000 (Customer Meeting) — it is excluded from SE Dashboard.

let validCategories = [
    ("Technical Close/Win Plan", 606820005)
    ("Architecture Design Session", 861980004)
    ("Blocker Escalation", 861980006)
    ("Consumption Plan", 861980007)
    ("Demo", 861980002)
    ("PoC/Pilot", 861980005)
    ("Workshop", 861980001)
]

let defaultCategory = 861980002 // Demo

let parseCategory (s: string) =
    match s.ToLowerInvariant().Trim() with
    | "1" | "technical close" | "win plan" | "tcwp"    -> 606820005
    | "2" | "ads" | "architecture design" | "architecture design session" -> 861980004
    | "3" | "blocker" | "blocker escalation" | "escalation" -> 861980006
    | "4" | "consumption" | "consumption plan"         -> 861980007
    | "5" | "demo"                                      -> 861980002
    | "6" | "poc" | "pilot" | "poc/pilot"              -> 861980005
    | "7" | "workshop"                                  -> 861980001
    | raw ->
        match System.Int32.TryParse(raw) with
        | true, code when validCategories |> List.exists (fun (_, c) -> c = code) -> code
        | true, 861980000 -> failwith "❌ Category 861980000 (Customer Meeting) is NOT allowed. Use one of the 7 high-value categories."
        | true, code -> failwith (sprintf "❌ Unknown category code %d. Run 'categories' to see valid options." code)
        | _ -> failwith (sprintf "❌ Unknown category '%s'. Run 'categories' to see valid options." s)

let categoryName code =
    validCategories |> List.tryFind (fun (_, c) -> c = code) |> Option.map fst |> Option.defaultValue (sprintf "Unknown(%d)" code)

// ─── Commands ────────────────────────────────────────────────────────────────

let cmdMyTasks () =
    if args.Length < 3 then failwith "Usage: my-tasks <startDate> <endDate> (ISO dates, end exclusive)"
    let startDate = args.[1]
    let endDate = args.[2]
    // Resolve current user
    let whoBody = get "/WhoAmI"
    let userId = JsonDocument.Parse(whoBody).RootElement.GetProperty("UserId").GetString()
    eprintfn "UserId: %s | range: %s → %s" userId startDate endDate

    let filter = sprintf "_ownerid_value eq %s and scheduledend ge %s and scheduledend lt %s" userId startDate endDate
    let sel = "activityid,subject,scheduledend,statecode,_regardingobjectid_value"
    let path = sprintf "/tasks?$select=%s&$filter=%s&$orderby=%s&$top=200" (Uri.EscapeDataString(sel)) (Uri.EscapeDataString(filter)) (Uri.EscapeDataString("scheduledend asc"))
    let tasks = getArray (get path)
    eprintfn "Found %d task(s)" tasks.Length
    printfn "DATE|SUBJECT|STATE|REGARDING_MILESTONE"
    for t in tasks do
        let d = getProp t "scheduledend"
        let s = getProp t "subject"
        let st = getProp t "statecode"
        let r = getProp t "_regardingobjectid_value"
        printfn "%s|%s|%s|%s" d s st r

let cmdWhoami () =
    let body = get "/WhoAmI"
    let doc = JsonDocument.Parse(body)
    let userId = doc.RootElement.GetProperty("UserId").GetString()
    printfn "UserId: %s" userId
    // Get user details
    let userBody = get (sprintf "/systemusers(%s)?$select=fullname,internalemailaddress,domainname" userId)
    let user = JsonDocument.Parse(userBody).RootElement
    printfn "Name: %s" (getProp user "fullname")
    printfn "Email: %s" (getProp user "internalemailaddress")
    printfn "Domain: %s" (getProp user "domainname")

let cmdMilestones () =
    let salesIds =
        if args.Length < 2 then failwith "Usage: milestones <salesId1,salesId2,...>"
        args.[1].Split(',') |> Array.map (fun s -> s.Trim()) |> Array.filter (fun s -> s <> "")

    let keyword = getOpt "--keyword="
    let includeCompleted = getFlag "--completed"
    let keywords =
        match keyword with
        | Some kw -> kw.Split(',') |> Array.map (fun s -> s.Trim().ToLowerInvariant()) |> Array.filter (fun s -> s <> "")
        | None -> [||]

    // Step 1: Get accounts
    let acctFilter = salesIds |> Array.map (fun id -> sprintf "msp_mssalesid eq '%s'" (esc id)) |> String.concat " or "
    let acctSelect = "accountid,name,msp_mssalesid"
    let acctPath = sprintf "/accounts?$select=%s&$filter=%s" (Uri.EscapeDataString(acctSelect)) (Uri.EscapeDataString(acctFilter))
    let accounts = getArray (get acctPath)

    if accounts.Length = 0 then
        eprintfn "No accounts found for: %s" (String.Join(", ", salesIds))
        exit 1

    eprintfn "Found %d account(s)" accounts.Length

    // Step 2: Get opportunities
    let oppFilter = accounts |> Array.map (fun a -> sprintf "_parentaccountid_value eq %s" (getProp a "accountid")) |> String.concat " or "
    let oppSelect = "opportunityid,name,statecode,_parentaccountid_value"
    let oppPath = sprintf "/opportunities?$select=%s&$filter=%s&$orderby=%s" (Uri.EscapeDataString(oppSelect)) (Uri.EscapeDataString(oppFilter)) (Uri.EscapeDataString("name asc"))
    let opps = getArray (get oppPath)

    if opps.Length = 0 then
        eprintfn "No opportunities found."
        exit 1

    eprintfn "Found %d opportunity(ies)" opps.Length

    // Step 3: Get milestones (chunked)
    let cutoff = DateTime.UtcNow.AddMonths(-16).ToString("yyyy-MM-ddT00:00:00Z")
    let oppIds = opps |> Array.map (fun o -> getProp o "opportunityid")
    let allMilestones = ResizeArray<JsonElement>()

    let chunks = oppIds |> Array.chunkBySize 100
    for chunk in chunks do
        let oppPart = chunk |> Array.map (fun id -> sprintf "_msp_opportunityid_value eq %s" id) |> String.concat " or "
        let statusExclude =
            if includeCompleted then "msp_milestonestatus ne 861980004"
            else "msp_milestonestatus ne 861980004 and msp_milestonestatus ne 861980003 and msp_milestonestatus ne 861980005 and msp_milestonestatus ne 861980006"
        let msFilter = sprintf "(%s) and msp_milestonedate ge %s and %s" oppPart cutoff statusExclude
        let msSelect = "msp_engagementmilestoneid,msp_name,msp_milestonedate,msp_milestonestatus,_msp_opportunityid_value,msp_milestonesolutionarea"
        let msPath = sprintf "/msp_engagementmilestones?$select=%s&$filter=%s&$orderby=%s" (Uri.EscapeDataString(msSelect)) (Uri.EscapeDataString(msFilter)) (Uri.EscapeDataString("msp_milestonedate asc"))
        let batch = getArray (get msPath)
        allMilestones.AddRange(batch)

    eprintfn "Found %d milestone(s)" allMilestones.Count

    // Filter by keyword(s) if provided (matches milestone name OR opportunity name, any keyword = OR)
    let milestones =
        if keywords.Length = 0 then allMilestones.ToArray()
        else
            let oppNameLookup = opps |> Array.map (fun o -> getProp o "opportunityid", (getProp o "name").ToLowerInvariant()) |> dict
            allMilestones |> Seq.filter (fun m ->
                let name = (getProp m "msp_name").ToLowerInvariant()
                let oppId = getProp m "_msp_opportunityid_value"
                let oppName = if oppNameLookup.ContainsKey(oppId) then oppNameLookup.[oppId] else ""
                keywords |> Array.exists (fun kw -> name.Contains(kw) || oppName.Contains(kw)))
            |> Seq.toArray

    let statusText (code:string) =
        match code with
        | "861980000" -> "NotStarted"
        | "861980001" -> "OnTrack"
        | "861980002" -> "InProgress"
        | "861980003" -> "AtRisk"
        | "861980004" -> "Completed"
        | "861980005" -> "Cancelled"
        | "861980006" -> "Blocked"
        | _ -> code

    // Build account lookup
    let acctLookup = accounts |> Array.map (fun a -> getProp a "accountid", getProp a "name") |> dict
    let oppAcct = opps |> Array.map (fun o -> getProp o "opportunityid", getProp o "_parentaccountid_value") |> dict

    // Print results
    printfn "MILESTONE_ID|ACCOUNT|OPP_NAME|MILESTONE_NAME|DATE|STATUS"
    for m in milestones do
        let msId = getProp m "msp_engagementmilestoneid"
        let msName = getProp m "msp_name"
        let msDate = let d = getProp m "msp_milestonedate" in if d.Length >= 10 then d.Substring(0,10) else d
        let msStatus = statusText (getProp m "msp_milestonestatus")
        let oppId = getProp m "_msp_opportunityid_value"
        let oppName = opps |> Array.tryFind (fun o -> getProp o "opportunityid" = oppId) |> Option.map (fun o -> getProp o "name") |> Option.defaultValue ""
        let acctId = if oppAcct.ContainsKey(oppId) then oppAcct.[oppId] else ""
        let acctName = if acctId <> "" && acctLookup.ContainsKey(acctId) then acctLookup.[acctId] else ""
        printfn "%s|%s|%s|%s|%s|%s" msId acctName oppName msName msDate msStatus

let cmdContacts () =
    let salesIds =
        if args.Length < 2 then failwith "Usage: contacts <salesId1,salesId2,...> [--keyword=X]"
        args.[1].Split(',') |> Array.map (fun s -> s.Trim()) |> Array.filter (fun s -> s <> "")

    let keyword = getOpt "--keyword=" |> Option.map (fun s -> s.ToLowerInvariant())

    let acctFilter = salesIds |> Array.map (fun id -> sprintf "msp_mssalesid eq '%s'" (esc id)) |> String.concat " or "
    let acctSelect = "accountid,name,msp_mssalesid"
    let acctPath = sprintf "/accounts?$select=%s&$filter=%s" (Uri.EscapeDataString(acctSelect)) (Uri.EscapeDataString(acctFilter))
    let accounts = getArray (get acctPath)

    if accounts.Length = 0 then
        eprintfn "No accounts found for: %s" (String.Join(", ", salesIds))
        exit 1

    eprintfn "Found %d account(s)" accounts.Length

    let acctLookup = accounts |> Array.map (fun a -> getProp a "accountid", getProp a "name") |> dict
    let acctPart = accounts |> Array.map (fun a -> sprintf "_parentcustomerid_value eq %s" (getProp a "accountid")) |> String.concat " or "
    let filter = sprintf "(%s) and statecode eq 0" acctPart
    let contactSelect = "contactid,fullname,jobtitle,emailaddress1,telephone1,mobilephone,_parentcustomerid_value"
    let contactPath = sprintf "/contacts?$select=%s&$filter=%s&$orderby=%s&$top=500" (Uri.EscapeDataString(contactSelect)) (Uri.EscapeDataString(filter)) (Uri.EscapeDataString("fullname asc"))
    let contacts =
        getArray (get contactPath)
        |> Array.filter (fun c ->
            match keyword with
            | None -> true
            | Some kw ->
                let haystack = sprintf "%s %s %s" (getProp c "fullname") (getProp c "jobtitle") (getProp c "emailaddress1")
                haystack.ToLowerInvariant().Contains(kw))

    eprintfn "Found %d contact(s)" contacts.Length
    printfn "CONTACT_ID|ACCOUNT|NAME|TITLE|EMAIL|PHONE|MOBILE"
    for c in contacts do
        let acctId = getProp c "_parentcustomerid_value"
        let acctName = if acctId <> "" && acctLookup.ContainsKey(acctId) then acctLookup.[acctId] else ""
        printfn "%s|%s|%s|%s|%s|%s|%s"
            (getProp c "contactid")
            acctName
            (getProp c "fullname")
            (getProp c "jobtitle")
            (getProp c "emailaddress1")
            (getProp c "telephone1")
            (getProp c "mobilephone")

let printContacts (contacts: JsonElement array) =
    printfn "CONTACT_ID|ACCOUNT_ID|NAME|TITLE|EMAIL|PHONE|MOBILE"
    for c in contacts do
        printfn "%s|%s|%s|%s|%s|%s|%s"
            (getProp c "contactid")
            (getProp c "_parentcustomerid_value")
            (getProp c "fullname")
            (getProp c "jobtitle")
            (getProp c "emailaddress1")
            (getProp c "telephone1")
            (getProp c "mobilephone")

let cmdContactsOData () =
    if args.Length < 2 then failwith "Usage: contacts-odata <odata-filter>"
    let filter = args.[1]
    let contactSelect = "contactid,fullname,jobtitle,emailaddress1,telephone1,mobilephone,_parentcustomerid_value"
    let contactPath = sprintf "/contacts?$select=%s&$filter=%s&$orderby=%s&$top=500" (Uri.EscapeDataString(contactSelect)) (Uri.EscapeDataString(filter)) (Uri.EscapeDataString("fullname asc"))
    let contacts = getArray (get contactPath)
    eprintfn "Found %d contact(s)" contacts.Length
    printContacts contacts

let cmdCreateTask () =
    if args.Length < 3 then failwith "Usage: create-task <milestoneId> <subject> [--date=X] [--desc=X] [--cat=N] [--priority=N] [--due=X]"
    let milestoneId = args.[1]
    let subject = args.[2]
    let activityDate = getOpt "--date=" |> Option.defaultValue (DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
    let description = getOpt "--desc=" |> Option.defaultValue ""
    let category = getOpt "--cat=" |> Option.map parseCategory |> Option.defaultValue defaultCategory
    let priority = getOpt "--priority=" |> Option.map int |> Option.defaultValue 1
    let dueDate = getOpt "--due=" |> Option.defaultValue (DateTime.Today.ToString("yyyy-MM-dd"))

    // Add date prefix to subject
    let prefix =
        match DateTimeOffset.TryParse(activityDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal) with
        | true, dto -> dto.ToString("MM/dd")
        | _ -> DateTime.UtcNow.ToString("MM/dd")
    let fullSubject = if subject.TrimStart().StartsWith("[") then subject else sprintf "[%s - GL] %s" prefix subject

    // Create task
    let json = sprintf """{"subject":"%s","description":"%s","scheduledend":"%s","prioritycode":%d,"msp_taskcategory":%d,"regardingobjectid_msp_engagementmilestone@odata.bind":"/msp_engagementmilestones(%s)"}"""
                (fullSubject.Replace("\"", "\\\"")) (description.Replace("\"", "\\\"")) dueDate priority category milestoneId

    let (ok, id, _) = post "/tasks" json
    if not ok then
        eprintfn "❌ Failed to create task"
        exit 1

    eprintfn "✅ Created task: %s (id: %s)" fullSubject id

    // PATCH to completed
    let patchOk = patch (sprintf "/tasks(%s)" id) """{"statecode":1,"statuscode":5}"""
    if patchOk then
        eprintfn "✅ Marked completed"
    else
        eprintfn "⚠️ Created but failed to mark completed"

    printfn "%s" id

let cmdCreateTasks () =
    if args.Length < 2 then failwith "Usage: create-tasks <json-file>"
    let file = args.[1]
    let content = File.ReadAllText(file)
    let doc = JsonDocument.Parse(content)
    let items =
        let root = doc.RootElement
        match root.ValueKind with
        | JsonValueKind.Array -> root.EnumerateArray() |> Seq.toArray
        | JsonValueKind.Object ->
            let ok, prop = root.TryGetProperty("tasks")
            if ok && prop.ValueKind = JsonValueKind.Array then prop.EnumerateArray() |> Seq.toArray
            else failwith "JSON object must have 'tasks' array property"
        | _ -> failwith "JSON must be array or object with 'tasks' array"

    eprintfn "Processing %d tasks..." items.Length
    let mutable ok = 0
    let mutable fail = 0

    for item in items do
        let milestoneId = getProp item "milestoneId"
        let subject = getProp item "subject"
        let activityDate = let d = getProp item "activityDate" in if d = "" then DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") else d
        let description = getProp item "description"
        let category = let c = getProp item "taskCategory" in if c = "" then defaultCategory else parseCategory c
        let priority = let p = getProp item "priority" in if p = "" then 1 else int p
        let dueDate = let d = getProp item "dueDate" in if d = "" then DateTime.Today.ToString("yyyy-MM-dd") else d

        let prefix =
            match DateTimeOffset.TryParse(activityDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal) with
            | true, dto -> dto.ToString("MM/dd")
            | _ -> DateTime.UtcNow.ToString("MM/dd")
        let fullSubject = if subject.TrimStart().StartsWith("[") then subject else sprintf "[%s - GL] %s" prefix subject

        let json = sprintf """{"subject":"%s","description":"%s","scheduledend":"%s","prioritycode":%d,"msp_taskcategory":%d,"regardingobjectid_msp_engagementmilestone@odata.bind":"/msp_engagementmilestones(%s)"}"""
                    (fullSubject.Replace("\"", "\\\"")) (description.Replace("\"", "\\\"")) dueDate priority category milestoneId

        let (created, id, _) = post "/tasks" json
        if created then
            let _ = patch (sprintf "/tasks(%s)" id) """{"statecode":1,"statuscode":5}"""
            eprintfn "  ✅ %s" fullSubject
            ok <- ok + 1
        else
            eprintfn "  ❌ %s" fullSubject
            fail <- fail + 1

    printfn "Done: %d ok, %d failed" ok fail

let cmdActivities () =
    if args.Length < 2 then failwith "Usage: activities <milestoneId> [--days=30]"
    let milestoneId = args.[1]
    let days = getOpt "--days=" |> Option.map int |> Option.defaultValue 30

    // Get milestone to find opportunity
    let msBody = get (sprintf "/msp_engagementmilestones(%s)?$select=msp_engagementmilestoneid,msp_name,_msp_opportunityid_value" milestoneId)
    let ms = JsonDocument.Parse(msBody).RootElement
    let oppId = getProp ms "_msp_opportunityid_value"
    if oppId = "" then
        eprintfn "Milestone has no linked opportunity"
        exit 1

    let cutoff = DateTime.UtcNow.AddDays(float -days).ToString("yyyy-MM-ddT00:00:00Z")
    let filter = sprintf "_regardingobjectid_value eq %s and createdon ge %s" oppId cutoff

    // Get tasks
    let taskSelect = "activityid,subject,description,scheduledend,prioritycode,statecode,createdon"
    let taskPath = sprintf "/tasks?$select=%s&$filter=%s&$orderby=%s&$top=50" (Uri.EscapeDataString(taskSelect)) (Uri.EscapeDataString(filter)) (Uri.EscapeDataString("scheduledend desc"))
    let tasks = getArray (get taskPath)

    // Get appointments
    let apptSelect = "activityid,subject,scheduledstart,statecode,createdon"
    let apptPath = sprintf "/appointments?$select=%s&$filter=%s&$orderby=%s&$top=50" (Uri.EscapeDataString(apptSelect)) (Uri.EscapeDataString(filter)) (Uri.EscapeDataString("scheduledstart desc"))
    let appts = getArray (get apptPath)

    printfn "# Activities for: %s" (getProp ms "msp_name")
    printfn "Opportunity: %s | Last %d days" oppId days
    printfn ""
    printfn "## Tasks (%d)" tasks.Length
    for t in tasks do
        printfn "  [%s] %s | due: %s | state: %s" (getProp t "activityid") (getProp t "subject") (getProp t "scheduledend") (getProp t "statecode")
    printfn ""
    printfn "## Appointments (%d)" appts.Length
    for a in appts do
        printfn "  [%s] %s | start: %s | state: %s" (getProp a "activityid") (getProp a "subject") (getProp a "scheduledstart") (getProp a "statecode")

let cmdTeam () =
    if args.Length < 2 then failwith "Usage: team <milestoneId>"
    let milestoneId = args.[1]
    let teamSelect = "activityid,_msp_memberid_value,msp_role,msp_solutionarea,msp_transitionmode"
    let teamPath = sprintf "/msp_engagementmilestones(%s)/msp_engagementmilestone_msp_transitioningteams?$select=%s" milestoneId (Uri.EscapeDataString(teamSelect))
    let members = getArray (get teamPath)

    printfn "# Team (%d members)" members.Length
    for m in members do
        let role =
            match getProp m "msp_role" with
            | "861980000" -> "ATS" | "861980001" -> "CSAM" | "861980002" -> "SSP"
            | "861980003" -> "DES" | "861980004" -> "PDM" | r -> r
        printfn "  %s | %s | %s" (getProp m "_msp_memberid_value") role (getProp m "msp_solutionarea")

let cmdAddTeam () =
    if args.Length < 3 then failwith "Usage: add-team <milestoneId> <userId> [--role=SSP] [--area=X]"
    let milestoneId = args.[1]
    let userId = args.[2]
    let role =
        match getOpt "--role=" |> Option.map (fun s -> s.ToUpperInvariant()) with
        | Some "ATS" -> 861980000 | Some "CSAM" -> 861980001 | Some "SSP" -> 861980002
        | Some "DES" -> 861980003 | Some "PDM" -> 861980004 | _ -> 861980002
    let area = getOpt "--area=" |> Option.defaultValue ""

    let json = sprintf """{"regardingobjectid_msp_engagementmilestone_msp_transitioningteam@odata.bind":"/msp_engagementmilestones(%s)","msp_memberId_msp_transitioningteam@odata.bind":"/systemusers(%s)","msp_role":%d,"msp_solutionarea":"%s","msp_transitionmode":861980000}"""
                milestoneId userId role (area.Replace("\"", "\\\""))

    let (ok, id, _) = post "/msp_transitioningteams" json
    if ok then printfn "✅ Added (id: %s)" id
    else eprintfn "❌ Failed to add team member"

let cmdPatchTask () =
    if args.Length < 2 then failwith "Usage: patch-task <taskId>"
    let taskId = args.[1]
    let ok = patch (sprintf "/tasks(%s)" taskId) """{"statecode":1,"statuscode":5}"""
    if ok then printfn "✅ Completed: %s" taskId
    else eprintfn "❌ Failed: %s" taskId

let cmdCompleteTasks () =
    if args.Length < 2 then failwith "Usage: complete-tasks <odata-filter>"
    let filter = args.[1]
    let fullFilter = sprintf "(%s) and statecode eq 0" filter
    let taskSelect = "activityid,subject,statecode"
    let taskPath = sprintf "/tasks?$select=%s&$filter=%s&$top=200" (Uri.EscapeDataString(taskSelect)) (Uri.EscapeDataString(fullFilter))
    let tasks = getArray (get taskPath)

    eprintfn "Found %d open tasks to complete." tasks.Length
    let mutable ok = 0
    for t in tasks do
        let id = getProp t "activityid"
        let success = patch (sprintf "/tasks(%s)" id) """{"statecode":1,"statuscode":5}"""
        if success then
            eprintfn "  ✅ %s" (getProp t "subject")
            ok <- ok + 1
        else
            eprintfn "  ❌ %s" (getProp t "subject")
    printfn "Done: %d/%d completed" ok tasks.Length

let cmdCategories () =
    printfn "Valid high-value task categories (Customer Meeting is NEVER allowed):"
    printfn ""
    printfn "  #  | Code      | Name"
    printfn "  ---|-----------|------------------------------"
    validCategories |> List.iteri (fun i (name, code) ->
        let marker = if code = defaultCategory then " ← default" else ""
        printfn "  %d  | %d | %s%s" (i+1) code name marker)
    printfn ""
    printfn "Usage: --cat=Demo  or  --cat=5  or  --cat=861980002"

let cmdReclassify () =
    if args.Length < 2 then failwith "Usage: reclassify <milestoneId> [--to=Demo] [--dry-run]"
    let milestoneId = args.[1]
    let targetCat = getOpt "--to=" |> Option.map parseCategory |> Option.defaultValue defaultCategory
    let dryRun = getFlag "--dry-run"

    eprintfn "Reclassifying tasks on milestone %s → %s (%d)%s" milestoneId (categoryName targetCat) targetCat (if dryRun then " [DRY RUN]" else "")

    // Find tasks with Customer Meeting category (861980000) on this milestone
    let filter = sprintf "_regardingobjectid_value eq %s and msp_taskcategory eq 861980000" milestoneId
    let taskSelect = "activityid,subject,msp_taskcategory,statecode"
    let taskPath = sprintf "/tasks?$select=%s&$filter=%s&$top=500" (Uri.EscapeDataString(taskSelect)) (Uri.EscapeDataString(filter))
    let tasks = getArray (get taskPath)

    if tasks.Length = 0 then
        eprintfn "No tasks with Customer Meeting category found."
    else
        eprintfn "Found %d task(s) with Customer Meeting category" tasks.Length
        let mutable ok = 0
        for t in tasks do
            let id = getProp t "activityid"
            let subj = getProp t "subject"
            if dryRun then
                eprintfn "  🔍 Would reclassify: %s" subj
                ok <- ok + 1
            else
                let patchJson = sprintf """{"msp_taskcategory":%d}""" targetCat
                let success = patch (sprintf "/tasks(%s)" id) patchJson
                if success then
                    eprintfn "  ✅ %s → %s" subj (categoryName targetCat)
                    ok <- ok + 1
                else
                    eprintfn "  ❌ %s" subj
        printfn "Done: %d/%d reclassified" ok tasks.Length

// ─── my-activity: Fully resolved tasks with account names ───────────────────

let cmdMyActivity () =
    if args.Length < 3 then failwith "Usage: my-activity <startDate> <endDate> [--top=500]"
    let startDate = args.[1]
    let endDate = args.[2]
    let top = getOpt "--top=" |> Option.map int |> Option.defaultValue 500

    let whoBody = get "/WhoAmI"
    let userId = JsonDocument.Parse(whoBody).RootElement.GetProperty("UserId").GetString()
    eprintfn "UserId: %s | range: %s → %s" userId startDate endDate

    // Get all my tasks in range
    let filter = sprintf "_ownerid_value eq %s and scheduledend ge %s and scheduledend lt %s" userId startDate endDate
    let sel = "activityid,subject,scheduledend,statecode,msp_taskcategory,_regardingobjectid_value"
    let taskPath = sprintf "/tasks?$select=%s&$filter=%s&$orderby=%s&$top=%d" (Uri.EscapeDataString(sel)) (Uri.EscapeDataString(filter)) (Uri.EscapeDataString("scheduledend asc")) top
    let tasks = getArray (get taskPath)
    eprintfn "Found %d task(s)" tasks.Length

    // Collect unique milestone IDs
    let milestoneIds = tasks |> Array.map (fun t -> getProp t "_regardingobjectid_value") |> Array.distinct |> Array.filter (fun s -> s <> "")
    eprintfn "Unique milestones: %d" milestoneIds.Length

    // Batch resolve milestones → opp IDs (chunked)
    let msLookup = System.Collections.Generic.Dictionary<string, string>() // milestone_id → opp_id
    let msNameLookup = System.Collections.Generic.Dictionary<string, string>() // milestone_id → milestone_name
    for chunk in (milestoneIds |> Array.chunkBySize 50) do
        let msFilter = chunk |> Array.map (fun id -> sprintf "msp_engagementmilestoneid eq %s" id) |> String.concat " or "
        let msSelect = "msp_engagementmilestoneid,msp_name,_msp_opportunityid_value"
        let msPath = sprintf "/msp_engagementmilestones?$select=%s&$filter=%s&$top=500" (Uri.EscapeDataString(msSelect)) (Uri.EscapeDataString(msFilter))
        let msBatch = getArray (get msPath)
        for m in msBatch do
            let mId = getProp m "msp_engagementmilestoneid"
            msLookup.[mId] <- getProp m "_msp_opportunityid_value"
            msNameLookup.[mId] <- getProp m "msp_name"

    eprintfn "Resolved %d milestones" msLookup.Count

    // Batch resolve opps → account IDs
    let oppIds = msLookup.Values |> Seq.distinct |> Seq.filter (fun s -> s <> "") |> Seq.toArray
    let oppAcctLookup = System.Collections.Generic.Dictionary<string, string>() // opp_id → account_id
    let oppNameLookup = System.Collections.Generic.Dictionary<string, string>() // opp_id → opp_name
    for chunk in (oppIds |> Array.chunkBySize 50) do
        let oppFilter = chunk |> Array.map (fun id -> sprintf "opportunityid eq %s" id) |> String.concat " or "
        let oppSelect = "opportunityid,name,_parentaccountid_value"
        let oppPath = sprintf "/opportunities?$select=%s&$filter=%s&$top=500" (Uri.EscapeDataString(oppSelect)) (Uri.EscapeDataString(oppFilter))
        let oppBatch = getArray (get oppPath)
        for o in oppBatch do
            let oId = getProp o "opportunityid"
            oppAcctLookup.[oId] <- getProp o "_parentaccountid_value"
            oppNameLookup.[oId] <- getProp o "name"

    eprintfn "Resolved %d opportunities" oppAcctLookup.Count

    // Batch resolve accounts
    let acctIds = oppAcctLookup.Values |> Seq.distinct |> Seq.filter (fun s -> s <> "") |> Seq.toArray
    let acctNameLookup = System.Collections.Generic.Dictionary<string, string>() // account_id → name
    let acctSalesIdLookup = System.Collections.Generic.Dictionary<string, string>() // account_id → ms_sales_id
    for chunk in (acctIds |> Array.chunkBySize 50) do
        let acctFilter = chunk |> Array.map (fun id -> sprintf "accountid eq %s" id) |> String.concat " or "
        let acctSelect = "accountid,name,msp_mssalesid"
        let acctPath = sprintf "/accounts?$select=%s&$filter=%s&$top=500" (Uri.EscapeDataString(acctSelect)) (Uri.EscapeDataString(acctFilter))
        let acctBatch = getArray (get acctPath)
        for a in acctBatch do
            let aId = getProp a "accountid"
            acctNameLookup.[aId] <- getProp a "name"
            acctSalesIdLookup.[aId] <- getProp a "msp_mssalesid"

    eprintfn "Resolved %d accounts" acctNameLookup.Count

    // Output fully resolved
    printfn "ACCOUNT|SALES_ID|DATE|SUBJECT|CATEGORY|MILESTONE|STATE"
    for t in tasks do
        let msId = getProp t "_regardingobjectid_value"
        let oppId = if msLookup.ContainsKey(msId) then msLookup.[msId] else ""
        let acctId = if oppId <> "" && oppAcctLookup.ContainsKey(oppId) then oppAcctLookup.[oppId] else ""
        let acctName = if acctId <> "" && acctNameLookup.ContainsKey(acctId) then acctNameLookup.[acctId] else "UNKNOWN"
        let salesId = if acctId <> "" && acctSalesIdLookup.ContainsKey(acctId) then acctSalesIdLookup.[acctId] else ""
        let msName = if msNameLookup.ContainsKey(msId) then msNameLookup.[msId] else ""
        let d = let raw = getProp t "scheduledend" in if raw.Length >= 10 then raw.Substring(0,10) else raw
        let s = getProp t "subject"
        let cat = let c = getProp t "msp_taskcategory" in if c = "" then "Unknown" else categoryName (int c)
        let st = getProp t "statecode"
        printfn "%s|%s|%s|%s|%s|%s|%s" acctName salesId d s cat msName st

// ─── coverage: Per-account activity summary ─────────────────────────────────

let cmdCoverage () =
    // Load accounts from CSV
    let csvPath =
        let dir = Path.GetDirectoryName(fsi.CommandLineArgs.[0])
        Path.Combine(dir, "accounts.csv")
    let csvLines = File.ReadAllLines(csvPath)
    let headers = csvLines.[0].Split(',') |> Array.map (fun s -> s.Trim().Trim('"'))
    let idxSalesId = headers |> Array.findIndex (fun h -> h = "ms_sales_id")
    let idxName = headers |> Array.findIndex (fun h -> h = "account_name")
    let idxAccel = headers |> Array.findIndex (fun h -> h = "accelerate")

    let csvAccounts =
        csvLines |> Array.skip 1 |> Array.map (fun line ->
            let parts = line.Split(',') |> Array.map (fun s -> s.Trim().Trim('"'))
            (parts.[idxSalesId], parts.[idxName], parts.[idxAccel]))

    let filterAccel = getOpt "--segment=" |> Option.map (fun s -> s.ToLowerInvariant())
    let filtered =
        match filterAccel with
        | Some seg -> csvAccounts |> Array.filter (fun (_, _, a) -> a.ToLowerInvariant() = seg)
        | None -> csvAccounts

    eprintfn "Checking coverage for %d accounts..." filtered.Length

    // Get current user
    let whoBody = get "/WhoAmI"
    let userId = JsonDocument.Parse(whoBody).RootElement.GetProperty("UserId").GetString()

    // Get ALL my tasks (no date filter — lifetime)
    let filter = sprintf "_ownerid_value eq %s" userId
    let sel = "activityid,subject,scheduledend,_regardingobjectid_value,msp_taskcategory"
    let mutable allTasks = ResizeArray<JsonElement>()
    let mutable page = 1
    let mutable more = true
    let mutable skiptoken = ""

    // Paginate through all tasks
    while more do
        let taskPath =
            if skiptoken = "" then
                sprintf "/tasks?$select=%s&$filter=%s&$orderby=%s&$top=500" (Uri.EscapeDataString(sel)) (Uri.EscapeDataString(filter)) (Uri.EscapeDataString("scheduledend desc"))
            else
                sprintf "/tasks?$select=%s&$filter=%s&$orderby=%s&$top=500&$skiptoken=%s" (Uri.EscapeDataString(sel)) (Uri.EscapeDataString(filter)) (Uri.EscapeDataString("scheduledend desc")) skiptoken
        let body = get taskPath
        let doc = JsonDocument.Parse(body)
        let batch = getArray body
        allTasks.AddRange(batch)
        eprintfn "  page %d: %d tasks (total: %d)" page batch.Length allTasks.Count
        // Check for @odata.nextLink
        match doc.RootElement.TryGetProperty("@odata.nextLink") with
        | true, link ->
            let linkStr = link.GetString()
            let skIdx = linkStr.IndexOf("$skiptoken=")
            if skIdx >= 0 then
                skiptoken <- linkStr.Substring(skIdx + 11)
                // Remove any trailing & params
                let ampIdx = skiptoken.IndexOf("&")
                if ampIdx >= 0 then skiptoken <- skiptoken.Substring(0, ampIdx)
            else more <- false
        | _ -> more <- false
        page <- page + 1

    eprintfn "Total tasks: %d" allTasks.Count

    // Resolve milestones → opp → account (same pattern as my-activity)
    let milestoneIds = allTasks |> Seq.map (fun t -> getProp t "_regardingobjectid_value") |> Seq.distinct |> Seq.filter (fun s -> s <> "") |> Seq.toArray
    eprintfn "Unique milestones to resolve: %d" milestoneIds.Length

    let msLookup = System.Collections.Generic.Dictionary<string, string>()
    for chunk in (milestoneIds |> Array.chunkBySize 50) do
        let msFilter = chunk |> Array.map (fun id -> sprintf "msp_engagementmilestoneid eq %s" id) |> String.concat " or "
        let msSelect = "msp_engagementmilestoneid,_msp_opportunityid_value"
        let msPath = sprintf "/msp_engagementmilestones?$select=%s&$filter=%s&$top=500" (Uri.EscapeDataString(msSelect)) (Uri.EscapeDataString(msFilter))
        let msBatch = getArray (get msPath)
        for m in msBatch do
            msLookup.[getProp m "msp_engagementmilestoneid"] <- getProp m "_msp_opportunityid_value"

    let oppIds = msLookup.Values |> Seq.distinct |> Seq.filter (fun s -> s <> "") |> Seq.toArray
    let oppAcctLookup = System.Collections.Generic.Dictionary<string, string>()
    for chunk in (oppIds |> Array.chunkBySize 50) do
        let oppFilter = chunk |> Array.map (fun id -> sprintf "opportunityid eq %s" id) |> String.concat " or "
        let oppSelect = "opportunityid,_parentaccountid_value"
        let oppPath = sprintf "/opportunities?$select=%s&$filter=%s&$top=500" (Uri.EscapeDataString(oppSelect)) (Uri.EscapeDataString(oppFilter))
        let oppBatch = getArray (get oppPath)
        for o in oppBatch do
            oppAcctLookup.[getProp o "opportunityid"] <- getProp o "_parentaccountid_value"

    let acctIds = oppAcctLookup.Values |> Seq.distinct |> Seq.filter (fun s -> s <> "") |> Seq.toArray
    let acctSalesLookup = System.Collections.Generic.Dictionary<string, string>() // accountid → sales_id
    for chunk in (acctIds |> Array.chunkBySize 50) do
        let acctFilter = chunk |> Array.map (fun id -> sprintf "accountid eq %s" id) |> String.concat " or "
        let acctSelect = "accountid,name,msp_mssalesid"
        let acctPath = sprintf "/accounts?$select=%s&$filter=%s&$top=500" (Uri.EscapeDataString(acctSelect)) (Uri.EscapeDataString(acctFilter))
        let acctBatch = getArray (get acctPath)
        for a in acctBatch do
            acctSalesLookup.[getProp a "accountid"] <- getProp a "msp_mssalesid"

    // Map each task → sales_id
    let tasksBySalesId = System.Collections.Generic.Dictionary<string, ResizeArray<string>>() // sales_id → list of dates
    for t in allTasks do
        let msId = getProp t "_regardingobjectid_value"
        let oppId = if msLookup.ContainsKey(msId) then msLookup.[msId] else ""
        let acctId = if oppId <> "" && oppAcctLookup.ContainsKey(oppId) then oppAcctLookup.[oppId] else ""
        let salesId = if acctId <> "" && acctSalesLookup.ContainsKey(acctId) then acctSalesLookup.[acctId] else ""
        if salesId <> "" then
            if not (tasksBySalesId.ContainsKey(salesId)) then tasksBySalesId.[salesId] <- ResizeArray<string>()
            tasksBySalesId.[salesId].Add(getProp t "scheduledend")

    // Output: one line per account
    printfn "SALES_ID|ACCOUNT|SEGMENT|ACTIVITY_COUNT|LAST_ACTIVITY|DAYS_SINCE"
    let today = DateTime.UtcNow
    for (salesId, name, accel) in filtered |> Array.sortBy (fun (_, _, a) -> if a = "Accelerate" then 0 else 1) do
        let count, lastDate, daysSince =
            if tasksBySalesId.ContainsKey(salesId) then
                let dates = tasksBySalesId.[salesId]
                let latest = dates |> Seq.map (fun d -> if d.Length >= 10 then d.Substring(0,10) else d) |> Seq.sortDescending |> Seq.head
                let daysAgo =
                    match DateTime.TryParse(latest) with
                    | true, dt -> int (today - dt).TotalDays
                    | _ -> -1
                (dates.Count, latest, daysAgo)
            else (0, "", -1)
        let dayStr = if daysSince >= 0 then string daysSince else "never"
        printfn "%s|%s|%s|%d|%s|%s" salesId name accel count lastDate dayStr

// ─── odata: Raw OData query with formatted output ───────────────────────────

let cmdOData () =
    if args.Length < 3 then failwith "Usage: odata <entity> <filter> [--select=X] [--top=N] [--orderby=X] [--expand=X]"
    let entity = args.[1]
    let filter = args.[2]
    let select = getOpt "--select="
    let top = getOpt "--top=" |> Option.map int |> Option.defaultValue 100
    let orderby = getOpt "--orderby="
    let expand = getOpt "--expand="

    let mutable pathParts = ResizeArray<string>()
    pathParts.Add(sprintf "$filter=%s" (Uri.EscapeDataString(filter)))
    pathParts.Add(sprintf "$top=%d" top)
    match select with Some s -> pathParts.Add(sprintf "$select=%s" (Uri.EscapeDataString(s))) | None -> ()
    match orderby with Some o -> pathParts.Add(sprintf "$orderby=%s" (Uri.EscapeDataString(o))) | None -> ()
    match expand with Some e -> pathParts.Add(sprintf "$expand=%s" (Uri.EscapeDataString(e))) | None -> ()

    let path = sprintf "/%s?%s" entity (String.Join("&", pathParts))
    let body = get path
    let results = getArray body
    eprintfn "Found %d result(s)" results.Length

    if results.Length = 0 then
        printfn "(no results)"
    else
        // Auto-detect columns from first result, excluding @odata annotations
        let cols =
            match select with
            | Some s -> s.Split(',') |> Array.map (fun c -> c.Trim())
            | None ->
                results.[0].EnumerateObject()
                |> Seq.map (fun p -> p.Name)
                |> Seq.filter (fun n -> not (n.StartsWith("@")) && not (n.StartsWith("_") && n.EndsWith("_value@")))
                |> Seq.toArray

        // Print header
        printfn "%s" (String.Join("|", cols))
        // Print rows
        for r in results do
            let vals = cols |> Array.map (fun c -> getProp r c)
            printfn "%s" (String.Join("|", vals))

// ─── account-tasks: All tasks for a specific account ────────────────────────

let cmdAccountTasks () =
    if args.Length < 2 then failwith "Usage: account-tasks <salesId> [--days=365] [--all-users]"
    let salesId = args.[1]
    let days = getOpt "--days=" |> Option.map int |> Option.defaultValue 365
    let allUsers = getFlag "--all-users"

    // Resolve account
    let acctPath = sprintf "/accounts?$select=accountid,name&$filter=%s" (Uri.EscapeDataString(sprintf "msp_mssalesid eq '%s'" (esc salesId)))
    let accounts = getArray (get acctPath)
    if accounts.Length = 0 then
        eprintfn "No account found for sales_id: %s" salesId
        exit 1
    let acctId = getProp accounts.[0] "accountid"
    let acctName = getProp accounts.[0] "name"
    eprintfn "Account: %s (%s)" acctName acctId

    // Get opportunities for this account
    let oppPath = sprintf "/opportunities?$select=opportunityid,name&$filter=%s&$top=200" (Uri.EscapeDataString(sprintf "_parentaccountid_value eq %s" acctId))
    let opps = getArray (get oppPath)
    eprintfn "Opportunities: %d" opps.Length

    if opps.Length = 0 then
        printfn "(no opportunities)"
        exit 0

    // Get milestones for these opps
    let oppIds = opps |> Array.map (fun o -> getProp o "opportunityid")
    let allMs = ResizeArray<JsonElement>()
    for chunk in (oppIds |> Array.chunkBySize 50) do
        let oppPart = chunk |> Array.map (fun id -> sprintf "_msp_opportunityid_value eq %s" id) |> String.concat " or "
        let msPath = sprintf "/msp_engagementmilestones?$select=msp_engagementmilestoneid,msp_name&$filter=%s&$top=500" (Uri.EscapeDataString(oppPart))
        allMs.AddRange(getArray (get msPath))
    eprintfn "Milestones: %d" allMs.Count

    if allMs.Count = 0 then
        printfn "(no milestones)"
        exit 0

    let msNameMap = allMs |> Seq.map (fun m -> getProp m "msp_engagementmilestoneid", getProp m "msp_name") |> dict

    // Get tasks against these milestones
    let cutoff = DateTime.UtcNow.AddDays(float -days).ToString("yyyy-MM-ddT00:00:00Z")
    let msIds = allMs |> Seq.map (fun m -> getProp m "msp_engagementmilestoneid") |> Seq.toArray
    let allTasks = ResizeArray<JsonElement>()
    for chunk in (msIds |> Array.chunkBySize 30) do
        let msPart = chunk |> Array.map (fun id -> sprintf "_regardingobjectid_value eq %s" id) |> String.concat " or "
        let ownerFilter = if allUsers then "" else sprintf " and _ownerid_value eq %s" (JsonDocument.Parse(get "/WhoAmI").RootElement.GetProperty("UserId").GetString())
        let taskFilter = sprintf "(%s) and scheduledend ge %s%s" msPart cutoff ownerFilter
        let taskSel = "activityid,subject,scheduledend,statecode,msp_taskcategory,_regardingobjectid_value,_ownerid_value"
        let taskPath = sprintf "/tasks?$select=%s&$filter=%s&$orderby=%s&$top=200" (Uri.EscapeDataString(taskSel)) (Uri.EscapeDataString(taskFilter)) (Uri.EscapeDataString("scheduledend desc"))
        allTasks.AddRange(getArray (get taskPath))

    eprintfn "Tasks: %d" allTasks.Count
    printfn "DATE|SUBJECT|CATEGORY|MILESTONE|OWNER|STATE"
    for t in allTasks |> Seq.sortBy (fun t -> getProp t "scheduledend") do
        let d = let raw = getProp t "scheduledend" in if raw.Length >= 10 then raw.Substring(0,10) else raw
        let subj = getProp t "subject"
        let cat = let c = getProp t "msp_taskcategory" in if c = "" then "Unknown" else categoryName (int c)
        let msId = getProp t "_regardingobjectid_value"
        let msName = if msNameMap.ContainsKey(msId) then msNameMap.[msId] else ""
        let owner = getProp t "_ownerid_value"
        let st = getProp t "statecode"
        printfn "%s|%s|%s|%s|%s|%s" d subj cat msName owner st

// ─── Shared: Load accounts.csv with segment filter ────────────────────────

let loadMyAccounts () =
    let csvPath =
        let dir = Path.GetDirectoryName(fsi.CommandLineArgs.[0])
        Path.Combine(dir, "accounts.csv")
    let csvLines = File.ReadAllLines(csvPath)
    let headers = csvLines.[0].Split(',') |> Array.map (fun s -> s.Trim().Trim('"'))
    let idxSalesId = headers |> Array.findIndex (fun h -> h = "ms_sales_id")
    let idxName = headers |> Array.findIndex (fun h -> h = "account_name")
    let idxAccel = headers |> Array.findIndex (fun h -> h = "accelerate")
    let csvAccounts =
        csvLines |> Array.skip 1 |> Array.map (fun line ->
            let parts = line.Split(',') |> Array.map (fun s -> s.Trim().Trim('"'))
            (parts.[idxSalesId], parts.[idxName], parts.[idxAccel]))
    let filterAccel = getOpt "--segment=" |> Option.map (fun s -> s.ToLowerInvariant())
    match filterAccel with
    | Some seg -> csvAccounts |> Array.filter (fun (_, _, a) -> a.ToLowerInvariant() = seg)
    | None -> csvAccounts

// Resolve sales_ids → account GUIDs in bulk
let resolveAccountGuids (salesIds: string[]) =
    let lookup = System.Collections.Generic.Dictionary<string, string * string>() // salesId → (accountid, name)
    for chunk in (salesIds |> Array.chunkBySize 20) do
        let f = chunk |> Array.map (fun id -> sprintf "msp_mssalesid eq '%s'" (esc id)) |> String.concat " or "
        let p = sprintf "/accounts?$select=%s&$filter=%s&$top=500" (Uri.EscapeDataString("accountid,name,msp_mssalesid")) (Uri.EscapeDataString(f))
        let batch = getArray (get p)
        for a in batch do
            lookup.[getProp a "msp_mssalesid"] <- (getProp a "accountid", getProp a "name")
    lookup

// ─── revenue-all: Portfolio GitHub revenue ───────────────────────────────────

let cmdRevenueAll () =
    let accounts = loadMyAccounts ()
    eprintfn "Loading revenue for %d accounts..." accounts.Length
    let salesIds = accounts |> Array.map (fun (sid, _, _) -> sid)
    let acctLookup = resolveAccountGuids salesIds
    let guidToSalesId = System.Collections.Generic.Dictionary<string, string>()
    for kv in acctLookup do
        let (guid, _) = kv.Value
        guidToSalesId.[guid] <- kv.Key

    let accountGuids = acctLookup.Values |> Seq.map fst |> Seq.filter (fun s -> s <> "") |> Seq.toArray
    let minRev = getOpt "--min=" |> Option.map float |> Option.defaultValue 0.0

    // Query all GitHub/GHAS opportunities for these accounts
    let results = System.Collections.Generic.Dictionary<string, (float * float * float)>() // salesId → (ghBilled, totalEst, netNew)
    for chunk in (accountGuids |> Array.chunkBySize 15) do
        let acctFilter = chunk |> Array.map (fun id -> sprintf "_parentaccountid_value eq %s" id) |> String.concat " or "
        let fullFilter = sprintf "(%s) and (contains(name,'GitHub') or contains(name,'GHAS') or contains(name,'Copilot') or msp_solutionarea eq 394380000)" acctFilter
        let sel = "_parentaccountid_value,name,msp_consumptionconsumedrecurring,msp_consumptionconsumednonrecurring,estimatedvalue,msp_netnewusage,statecode,msp_solutionarea"
        let p = sprintf "/opportunities?$select=%s&$filter=%s&$top=500" (Uri.EscapeDataString(sel)) (Uri.EscapeDataString(fullFilter))
        let opps = getArray (get p)
        for o in opps do
            let acctGuid = getProp o "_parentaccountid_value"
            let sid = if guidToSalesId.ContainsKey(acctGuid) then guidToSalesId.[acctGuid] else ""
            if sid <> "" then
                let recurring = try (getProp o "msp_consumptionconsumedrecurring") |> float with _ -> 0.0
                let nonRecurring = try (getProp o "msp_consumptionconsumednonrecurring") |> float with _ -> 0.0
                let est = try (getProp o "estimatedvalue") |> float with _ -> 0.0
                let (curRec, curEst, curNR) = if results.ContainsKey(sid) then results.[sid] else (0.0, 0.0, 0.0)
                results.[sid] <- (curRec + recurring, curEst + est, curNR + nonRecurring)

    printfn "SALES_ID|ACCOUNT|SEGMENT|GITHUB_BILLED_RECURRING|NON_RECURRING|TOTAL_BILLED|EST_PIPELINE"
    let sorted =
        accounts
        |> Array.map (fun (sid, name, seg) ->
            let (rec_, est, nr) = if results.ContainsKey(sid) then results.[sid] else (0.0, 0.0, 0.0)
            (sid, name, seg, rec_, nr, rec_ + nr, est))
        |> Array.sortByDescending (fun (_, _, _, _, _, total, _) -> total)
        |> Array.filter (fun (_, _, _, _, _, total, _) -> total >= minRev)
    for (sid, name, seg, rec_, nr, total, est) in sorted do
        printfn "%s|%s|%s|%.0f|%.0f|%.0f|%.0f" sid name seg rec_ nr total est
    eprintfn "Accounts with revenue: %d / %d" (sorted |> Array.filter (fun (_, _, _, _, _, t, _) -> t > 0.0) |> Array.length) accounts.Length

// ─── seats-all: Portfolio GitHub seats/usage ─────────────────────────────────

let cmdSeatsAll () =
    let accounts = loadMyAccounts ()
    eprintfn "Loading seat/usage data for %d accounts..." accounts.Length
    let salesIds = accounts |> Array.map (fun (sid, _, _) -> sid)
    let acctLookup = resolveAccountGuids salesIds
    let guidToSalesId = System.Collections.Generic.Dictionary<string, string>()
    for kv in acctLookup do
        let (guid, _) = kv.Value
        guidToSalesId.[guid] <- kv.Key

    let accountGuids = acctLookup.Values |> Seq.map fst |> Seq.filter (fun s -> s <> "") |> Seq.toArray

    // Query GitHub milestones (workload 606820012) for these accounts
    let milestoneRows = ResizeArray<string[]>()
    for chunk in (accountGuids |> Array.chunkBySize 15) do
        let acctFilter = chunk |> Array.map (fun id -> sprintf "_msp_parentaccount_value eq %s" id) |> String.concat " or "
        let fullFilter = sprintf "(%s) and (msp_milestoneworkload eq 606820012 or contains(msp_name,'GitHub') or contains(msp_name,'GHAS'))" acctFilter
        let sel = "_msp_parentaccount_value,msp_name,msp_monthlyuse,msp_seatsentitlements,msp_noofactivations,statuscode,msp_milestonecategory"
        let p = sprintf "/msp_engagementmilestones?$select=%s&$filter=%s&$top=500" (Uri.EscapeDataString(sel)) (Uri.EscapeDataString(fullFilter))
        let ms = getArray (get p)
        for m in ms do
            let acctGuid = getProp m "_msp_parentaccount_value"
            let sid = if guidToSalesId.ContainsKey(acctGuid) then guidToSalesId.[acctGuid] else ""
            if sid <> "" then
                let name = accounts |> Array.tryFind (fun (s, _, _) -> s = sid) |> Option.map (fun (_, n, _) -> n) |> Option.defaultValue ""
                milestoneRows.Add([| sid; name; getProp m "msp_name"; getProp m "msp_monthlyuse"; getProp m "msp_seatsentitlements"; getProp m "msp_noofactivations"; getProp m "statuscode"; getProp m "msp_milestonecategory" |])

    printfn "SALES_ID|ACCOUNT|MILESTONE|MONTHLY_USE|SEATS_ENTITLED|ACTIVATIONS|STATUS|CATEGORY"
    let sorted = milestoneRows |> Seq.sortByDescending (fun r -> try float r.[3] with _ -> 0.0) |> Seq.toArray
    for r in sorted do
        printfn "%s" (String.Join("|", r))
    eprintfn "Total GitHub milestones found: %d" sorted.Length

// ─── pipeline: Open GitHub/GHAS deals ────────────────────────────────────────

let cmdPipeline () =
    let accounts = loadMyAccounts ()
    eprintfn "Loading pipeline for %d accounts..." accounts.Length
    let salesIds = accounts |> Array.map (fun (sid, _, _) -> sid)
    let acctLookup = resolveAccountGuids salesIds
    let guidToSalesId = System.Collections.Generic.Dictionary<string, string>()
    for kv in acctLookup do
        let (guid, _) = kv.Value
        guidToSalesId.[guid] <- kv.Key

    let accountGuids = acctLookup.Values |> Seq.map fst |> Seq.filter (fun s -> s <> "") |> Seq.toArray
    let solutionFilter = getOpt "--solution=" |> Option.defaultValue "GitHub"
    let kw = solutionFilter.ToLowerInvariant()

    let rows = ResizeArray<string[]>()
    for chunk in (accountGuids |> Array.chunkBySize 15) do
        let acctFilter = chunk |> Array.map (fun id -> sprintf "_parentaccountid_value eq %s" id) |> String.concat " or "
        let fullFilter = sprintf "(%s) and statecode eq 0 and (contains(name,'%s') or contains(name,'Copilot') or contains(name,'GHAS') or msp_solutionarea eq 394380000)" acctFilter (esc kw)
        let sel = "_parentaccountid_value,name,estimatedvalue,estimatedclosedate,msp_activesalesstage,msp_daysinstage,msp_riskscore,_msp_primarycompetitor_value,msp_solutionarea,msp_netnewusage"
        let p = sprintf "/opportunities?$select=%s&$filter=%s&$top=500" (Uri.EscapeDataString(sel)) (Uri.EscapeDataString(fullFilter))
        let opps = getArray (get p)
        for o in opps do
            let acctGuid = getProp o "_parentaccountid_value"
            let sid = if guidToSalesId.ContainsKey(acctGuid) then guidToSalesId.[acctGuid] else ""
            if sid <> "" then
                let acctName = accounts |> Array.tryFind (fun (s, _, _) -> s = sid) |> Option.map (fun (_, n, _) -> n) |> Option.defaultValue ""
                rows.Add([| sid; acctName; getProp o "name"; getProp o "estimatedvalue"; getProp o "estimatedclosedate"; getProp o "msp_activesalesstage"; getProp o "msp_daysinstage"; getProp o "msp_riskscore"; getProp o "_msp_primarycompetitor_value"; getProp o "msp_netnewusage" |])

    printfn "SALES_ID|ACCOUNT|OPP_NAME|EST_VALUE|CLOSE_DATE|STAGE|DAYS_IN_STAGE|RISK_SCORE|COMPETITOR_ID|NET_NEW"
    let sorted = rows |> Seq.sortByDescending (fun r -> try float r.[3] with _ -> 0.0) |> Seq.toArray
    for r in sorted do
        printfn "%s" (String.Join("|", r))
    eprintfn "Open GitHub/GHAS pipeline: %d opportunities" sorted.Length

// ─── account-health: Full account intelligence ───────────────────────────────

let cmdAccountHealth () =
    if args.Length < 2 then failwith "Usage: account-health <salesId>"
    let salesId = args.[1]

    // 1. Account metadata
    let acctFilter = sprintf "msp_mssalesid eq '%s'" (esc salesId)
    let acctSelect = "accountid,name,msp_mssalesid,msp_numberofprofessionaldevelopers,li_companyid,msp_activecontacts,msp_managedstatuscode,msp_segmentgroup,numberofemployees,websiteurl,address1_city,address1_stateorprovince"
    let acctPath = sprintf "/accounts?$select=%s&$filter=%s" (Uri.EscapeDataString(acctSelect)) (Uri.EscapeDataString(acctFilter))
    let accts = getArray (get acctPath)
    if accts.Length = 0 then failwith (sprintf "No account found for sales_id: %s" salesId)
    let acct = accts.[0]
    let accountid = getProp acct "accountid"

    printfn "=== ACCOUNT METADATA ==="
    printfn "NAME|%s" (getProp acct "name")
    printfn "SALES_ID|%s" salesId
    printfn "CITY|%s" (getProp acct "address1_city")
    printfn "STATE|%s" (getProp acct "address1_stateorprovince")
    printfn "WEBSITE|%s" (getProp acct "websiteurl")
    printfn "EMPLOYEES|%s" (getProp acct "numberofemployees")
    printfn "PRO_DEVELOPERS|%s" (getProp acct "msp_numberofprofessionaldevelopers")
    printfn "LINKEDIN_COMPANY_ID|%s" (getProp acct "li_companyid")
    printfn "ACTIVE_CONTACTS|%s" (getProp acct "msp_activecontacts")

    // 2. Account plan
    printfn ""
    printfn "=== ACCOUNT PLAN ==="
    let planFilter = sprintf "contains(msp_name,'%s')" (esc (getProp acct "name"))
    let planSel = "msp_name,msp_overallsummary,msp_healthsignals,msp_topsuggestedactions,msp_competitivethreats,msp_keygrowthareas,msp_customerrelationshipstatus"
    let planPath = sprintf "/msp_accountplans?$select=%s&$filter=%s&$top=1" (Uri.EscapeDataString(planSel)) (Uri.EscapeDataString(planFilter))
    let plans = getArray (get planPath)
    if plans.Length > 0 then
        let plan = plans.[0]
        printfn "SUMMARY|%s" (getProp plan "msp_overallsummary")
        printfn "HEALTH|%s" (getProp plan "msp_healthsignals")
        printfn "SUGGESTED_ACTIONS|%s" (getProp plan "msp_topsuggestedactions")
        printfn "COMPETITIVE_THREATS|%s" (getProp plan "msp_competitivethreats")
        printfn "GROWTH_AREAS|%s" (getProp plan "msp_keygrowthareas")
        printfn "RELATIONSHIP|%s" (getProp plan "msp_customerrelationshipstatus")
    else printfn "(no account plan found)"

    // 3. GitHub opportunities (revenue)
    printfn ""
    printfn "=== GITHUB OPPORTUNITIES ==="
    let oppFilter = sprintf "_parentaccountid_value eq %s and (contains(name,'GitHub') or contains(name,'GHAS') or contains(name,'Copilot') or msp_solutionarea eq 394380000)" accountid
    let oppSel = "name,statecode,msp_consumptionconsumedrecurring,estimatedvalue,estimatedclosedate,msp_activesalesstage,msp_daysinstage,msp_riskscore,_msp_primarycompetitor_value,msp_netnewusage"
    let oppPath = sprintf "/opportunities?$select=%s&$filter=%s&$top=20" (Uri.EscapeDataString(oppSel)) (Uri.EscapeDataString(oppFilter))
    let opps = getArray (get oppPath)
    printfn "NAME|STATE|BILLED_RECURRING|EST_VALUE|CLOSE_DATE|STAGE|DAYS_IN_STAGE|RISK|COMPETITOR|NET_NEW"
    for o in opps do
        printfn "%s|%s|%s|%s|%s|%s|%s|%s|%s|%s" (getProp o "name") (getProp o "statecode") (getProp o "msp_consumptionconsumedrecurring") (getProp o "estimatedvalue") (getProp o "estimatedclosedate") (getProp o "msp_activesalesstage") (getProp o "msp_daysinstage") (getProp o "msp_riskscore") (getProp o "_msp_primarycompetitor_value") (getProp o "msp_netnewusage")

    // 4. GitHub milestones (seats/usage)
    printfn ""
    printfn "=== GITHUB MILESTONES ==="
    let msFilter = sprintf "_msp_parentaccount_value eq %s and (msp_milestoneworkload eq 606820012 or contains(msp_name,'GitHub') or contains(msp_name,'GHAS'))" accountid
    let msSel = "msp_name,msp_monthlyuse,msp_seatsentitlements,statuscode,msp_milestonedate,msp_milestonecategory"
    let msPath = sprintf "/msp_engagementmilestones?$select=%s&$filter=%s&$top=20" (Uri.EscapeDataString(msSel)) (Uri.EscapeDataString(msFilter))
    let milestones = getArray (get msPath)
    printfn "NAME|MONTHLY_USE|SEATS|STATUS|DATE|CATEGORY"
    for m in milestones do
        printfn "%s|%s|%s|%s|%s|%s" (getProp m "msp_name") (getProp m "msp_monthlyuse") (getProp m "msp_seatsentitlements") (getProp m "statuscode") (getProp m "msp_milestonedate") (getProp m "msp_milestonecategory")

    // 5. Consumption plan
    printfn ""
    printfn "=== CONSUMPTION PLAN ==="
    let cpFilter = sprintf "msp_accounttpid eq '%s' and msp_cloudproduct eq 606820000" (esc salesId)
    let cpSel = "msp_name,msp_cp_actuals,msp_cp_target,msp_cp_budget,msp_cp_forecast,msp_planstage"
    let cpPath = sprintf "/msp_consumptionplans?$select=%s&$filter=%s&$top=5" (Uri.EscapeDataString(cpSel)) (Uri.EscapeDataString(cpFilter))
    let cps = getArray (get cpPath)
    if cps.Length > 0 then
        printfn "NAME|ACTUALS|TARGET|BUDGET|FORECAST|STAGE"
        for cp in cps do
            printfn "%s|%s|%s|%s|%s|%s" (getProp cp "msp_name") (getProp cp "msp_cp_actuals") (getProp cp "msp_cp_target") (getProp cp "msp_cp_budget") (getProp cp "msp_cp_forecast") (getProp cp "msp_planstage")
    else printfn "(no GitHub consumption plan)"

// ─── account-enrichment: Metadata for prospecting ────────────────────────────

let cmdAccountEnrichment () =
    if args.Length < 2 then failwith "Usage: account-enrichment <salesId1,salesId2,...>"
    let salesIds = args.[1].Split(',') |> Array.map (fun s -> s.Trim()) |> Array.filter (fun s -> s <> "")

    printfn "SALES_ID|ACCOUNT|PRO_DEVELOPERS|LINKEDIN_ID|WEBSITE|CITY|STATE|EMPLOYEES|MANAGED_STATUS"
    for chunk in (salesIds |> Array.chunkBySize 20) do
        let f = chunk |> Array.map (fun id -> sprintf "msp_mssalesid eq '%s'" (esc id)) |> String.concat " or "
        let sel = "msp_mssalesid,name,msp_numberofprofessionaldevelopers,li_companyid,websiteurl,address1_city,address1_stateorprovince,numberofemployees,msp_managedstatuscode"
        let p = sprintf "/accounts?$select=%s&$filter=%s&$top=500" (Uri.EscapeDataString(sel)) (Uri.EscapeDataString(f))
        let batch = getArray (get p)
        for a in batch do
            printfn "%s|%s|%s|%s|%s|%s|%s|%s|%s" (getProp a "msp_mssalesid") (getProp a "name") (getProp a "msp_numberofprofessionaldevelopers") (getProp a "li_companyid") (getProp a "websiteurl") (getProp a "address1_city") (getProp a "address1_stateorprovince") (getProp a "numberofemployees") (getProp a "msp_managedstatuscode")

// ─── competitors: Competitive landscape ──────────────────────────────────────

let cmdCompetitors () =
    let accounts = loadMyAccounts ()
    eprintfn "Scanning competitive landscape for %d accounts..." accounts.Length
    let salesIds = accounts |> Array.map (fun (sid, _, _) -> sid)
    let acctLookup = resolveAccountGuids salesIds
    let guidToSalesId = System.Collections.Generic.Dictionary<string, string>()
    for kv in acctLookup do
        let (guid, _) = kv.Value
        guidToSalesId.[guid] <- kv.Key

    let accountGuids = acctLookup.Values |> Seq.map fst |> Seq.filter (fun s -> s <> "") |> Seq.toArray

    let rows = ResizeArray<string[]>()
    for chunk in (accountGuids |> Array.chunkBySize 15) do
        let acctFilter = chunk |> Array.map (fun id -> sprintf "_parentaccountid_value eq %s" id) |> String.concat " or "
        let fullFilter = sprintf "(%s) and _msp_primarycompetitor_value ne null and (contains(name,'GitHub') or contains(name,'GHAS') or contains(name,'Copilot') or msp_solutionarea eq 394380000)" acctFilter
        let sel = "_parentaccountid_value,name,_msp_primarycompetitor_value,msp_competethreatlevel,msp_activesalesstage,statecode,estimatedvalue"
        let p = sprintf "/opportunities?$select=%s&$filter=%s&$top=500" (Uri.EscapeDataString(sel)) (Uri.EscapeDataString(fullFilter))
        let opps = getArray (get p)
        for o in opps do
            let acctGuid = getProp o "_parentaccountid_value"
            let sid = if guidToSalesId.ContainsKey(acctGuid) then guidToSalesId.[acctGuid] else ""
            if sid <> "" then
                let acctName = accounts |> Array.tryFind (fun (s, _, _) -> s = sid) |> Option.map (fun (_, n, _) -> n) |> Option.defaultValue ""
                rows.Add([| sid; acctName; getProp o "name"; getProp o "_msp_primarycompetitor_value"; getProp o "msp_competethreatlevel"; getProp o "msp_activesalesstage"; getProp o "statecode"; getProp o "estimatedvalue" |])

    printfn "SALES_ID|ACCOUNT|OPP_NAME|COMPETITOR_ID|THREAT_LEVEL|STAGE|STATE|EST_VALUE"
    for r in rows do
        printfn "%s" (String.Join("|", r))
    eprintfn "Competitive situations found: %d" rows.Count

// ─── consumption-plan: GitHub consumption plans ──────────────────────────────

let cmdConsumptionPlan () =
    let accounts = loadMyAccounts ()
    eprintfn "Loading GitHub consumption plans for %d accounts..." accounts.Length
    let productCode = getOpt "--product=" |> Option.defaultValue "606820000" // GitHub default

    // Build TPID lookup from accounts
    let salesIdToName = System.Collections.Generic.Dictionary<string, string>()
    for (sid, name, _) in accounts do
        salesIdToName.[sid] <- name

    // Query consumption plans by product
    let filter = sprintf "msp_cloudproduct eq %s" productCode
    let sel = "msp_name,msp_accounttpid,msp_cp_actuals,msp_cp_target,msp_cp_budget,msp_cp_forecast,msp_cp_pipeline,msp_planstage,msp_enddate"
    let p = sprintf "/msp_consumptionplans?$select=%s&$filter=%s&$top=500" (Uri.EscapeDataString(sel)) (Uri.EscapeDataString(filter))
    let plans = getArray (get p)

    // Filter to my accounts by matching msp_accounttpid to ms_sales_id
    let myPlans = plans |> Array.filter (fun cp ->
        let tpid = getProp cp "msp_accounttpid"
        salesIdToName.ContainsKey(tpid))

    printfn "SALES_ID|ACCOUNT|PLAN_NAME|ACTUALS|TARGET|BUDGET|FORECAST|PIPELINE|STAGE|END_DATE"
    for cp in myPlans do
        let tpid = getProp cp "msp_accounttpid"
        let name = salesIdToName.[tpid]
        printfn "%s|%s|%s|%s|%s|%s|%s|%s|%s|%s" tpid name (getProp cp "msp_name") (getProp cp "msp_cp_actuals") (getProp cp "msp_cp_target") (getProp cp "msp_cp_budget") (getProp cp "msp_cp_forecast") (getProp cp "msp_cp_pipeline") (getProp cp "msp_planstage") (getProp cp "msp_enddate")
    eprintfn "GitHub consumption plans on my accounts: %d" myPlans.Length

// ─── STRATEGIC INTELLIGENCE (OData Deep Entities) ────────────────────────────

// Helper: resolve a single salesId → (accountid, name)
let resolveOneAccount (salesId: string) =
    let acctFilter = sprintf "msp_mssalesid eq '%s'" (esc salesId)
    let acctSelect = "accountid,name,msp_mssalesid"
    let acctPath = sprintf "/accounts?$select=%s&$filter=%s" (Uri.EscapeDataString(acctSelect)) (Uri.EscapeDataString(acctFilter))
    let accts = getArray (get acctPath)
    if accts.Length = 0 then failwith (sprintf "No account found for sales_id: %s" salesId)
    let acct = accts.[0]
    (getProp acct "accountid", getProp acct "name")

// Helper: get current user ID (cached)
let mutable cachedUserId = ""
let getMyUserId () =
    if cachedUserId <> "" then cachedUserId
    else
        let body = get "/WhoAmI"
        let uid = JsonDocument.Parse(body).RootElement.GetProperty("UserId").GetString()
        cachedUserId <- uid
        uid

// ─── account-plan: Strategic account plan (deep fields) ──────────────────────

let cmdAccountPlan () =
    if args.Length < 2 then failwith "Usage: account-plan <salesId>"
    let salesId = args.[1]
    let (accountid, acctName) = resolveOneAccount salesId

    // Account plans link via _msp_parentaccountid_value or name match
    let planFilter = sprintf "_msp_parentaccountid_value eq %s" accountid
    let planSel = "msp_name,msp_overallsummary,msp_healthsignals,msp_topsuggestedactions,msp_competitivethreats,msp_keygrowthareas,msp_customerrelationshipstatus,msp_customermission,msp_customerchallenges,msp_competestrategy,msp_primarycloudprovider,msp_coveragemodel,msp_planstage,createdon,modifiedon"
    let planPath = sprintf "/msp_accountplans?$select=%s&$filter=%s&$top=1&$orderby=%s" (Uri.EscapeDataString(planSel)) (Uri.EscapeDataString(planFilter)) (Uri.EscapeDataString("modifiedon desc"))
    let plans = getArray (get planPath)

    // Fallback: try name-based match if no parent link
    let plans =
        if plans.Length = 0 then
            let fallbackFilter = sprintf "contains(msp_name,'%s')" (esc acctName)
            let fallbackPath = sprintf "/msp_accountplans?$select=%s&$filter=%s&$top=1&$orderby=%s" (Uri.EscapeDataString(planSel)) (Uri.EscapeDataString(fallbackFilter)) (Uri.EscapeDataString("modifiedon desc"))
            getArray (get fallbackPath)
        else plans

    if plans.Length = 0 then
        printfn "(no account plan found for %s [%s])" acctName salesId
    else
        let p = plans.[0]
        printfn "ACCOUNT|%s" acctName
        printfn "SALES_ID|%s" salesId
        printfn "PLAN_NAME|%s" (getProp p "msp_name")
        printfn "PLAN_STAGE|%s" (getProp p "msp_planstage")
        printfn "MODIFIED|%s" (getProp p "modifiedon")
        printfn "---"
        printfn "OVERALL_SUMMARY|%s" (getProp p "msp_overallsummary")
        printfn "CUSTOMER_MISSION|%s" (getProp p "msp_customermission")
        printfn "CUSTOMER_CHALLENGES|%s" (getProp p "msp_customerchallenges")
        printfn "HEALTH_SIGNALS|%s" (getProp p "msp_healthsignals")
        printfn "SUGGESTED_ACTIONS|%s" (getProp p "msp_topsuggestedactions")
        printfn "COMPETITIVE_THREATS|%s" (getProp p "msp_competitivethreats")
        printfn "COMPETE_STRATEGY|%s" (getProp p "msp_competestrategy")
        printfn "KEY_GROWTH_AREAS|%s" (getProp p "msp_keygrowthareas")
        printfn "STRATEGIC_FOCUS|%s" (getProp p "msp_strategicfocus")
        printfn "PRIMARY_CLOUD_PROVIDER|%s" (getProp p "msp_primarycloudprovider")
        printfn "COVERAGE_MODEL|%s" (getProp p "msp_coveragemodel")
        printfn "RELATIONSHIP_STATUS|%s" (getProp p "msp_customerrelationshipstatus")

// ─── stakeholders: Account org chart from stakeholder maps ───────────────────

let cmdStakeholders () =
    if args.Length < 2 then failwith "Usage: stakeholders <salesId>"
    let salesId = args.[1]
    let (accountid, acctName) = resolveOneAccount salesId

    // 1. Find stakeholder maps for this account
    let mapFilter = sprintf "_msp_account_id_value eq %s" accountid
    let mapSel = "msp_stakeholdermapid,msp_name"
    let mapPath = sprintf "/msp_stakeholdermaps?$select=%s&$filter=%s&$top=5" (Uri.EscapeDataString(mapSel)) (Uri.EscapeDataString(mapFilter))
    let maps = getArray (get mapPath)

    if maps.Length = 0 then
        printfn "(no stakeholder map found for %s [%s])" acctName salesId
    else
        eprintfn "Found %d stakeholder map(s) for %s" maps.Length acctName
        printfn "CONTACT_NAME|JOB_ROLE|STAKEHOLDER_ROLE|RELATIONSHIP_LEVEL|SENTIMENT|INFLUENCE"
        for m in maps do
            let mapId = getProp m "msp_stakeholdermapid"
            // 2. Get stakeholders linked to this map
            let shFilter = sprintf "_msp_stakeholdermapid_value eq %s" mapId
            let shSel = "msp_name,msp_jobrole,msp_stakeholderrole,msp_relationshiplevel,msp_sentiment,msp_influence,_msp_contactid_value"
            let shPath = sprintf "/msp_stakeholders?$select=%s&$filter=%s&$top=50" (Uri.EscapeDataString(shSel)) (Uri.EscapeDataString(shFilter))
            let stakeholders = getArray (get shPath)
            for s in stakeholders do
                printfn "%s|%s|%s|%s|%s|%s" (getProp s "msp_name") (getProp s "msp_jobrole") (getProp s "msp_stakeholderrole") (getProp s "msp_relationshiplevel") (getProp s "msp_sentiment") (getProp s "msp_influence")
        eprintfn "Total stakeholders printed."

// ─── close-plan: Deal close plan details ─────────────────────────────────────

let cmdClosePlan () =
    // Can take an opportunity ID directly or a salesId (will show all close plans for account)
    if args.Length < 2 then failwith "Usage: close-plan <salesId|opportunityId>"
    let idArg = args.[1]

    // Heuristic: if it looks like a GUID, treat as opportunity ID; otherwise treat as salesId
    let closePlans =
        if idArg.Contains("-") && idArg.Length > 30 then
            let cpFilter = sprintf "_msp_opportunity_value eq %s" idArg
            let cpSel = "msp_name,msp_commercialstrat,msp_dealtype,msp_timeline,msp_direction,msp_compprop,msp_partner,createdon,modifiedon,_msp_opportunity_value"
            let cpPath = sprintf "/msp_closeplans?$select=%s&$filter=%s&$top=5&$orderby=%s" (Uri.EscapeDataString(cpSel)) (Uri.EscapeDataString(cpFilter)) (Uri.EscapeDataString("modifiedon desc"))
            getArray (get cpPath)
        else
            let (accountid, _) = resolveOneAccount idArg
            let oppFilter = sprintf "_parentaccountid_value eq %s and (contains(name,'GitHub') or contains(name,'GHAS') or contains(name,'Copilot') or msp_solutionarea eq 394380000)" accountid
            let oppPath = sprintf "/opportunities?$select=%s&$filter=%s&$top=20" (Uri.EscapeDataString("opportunityid,name")) (Uri.EscapeDataString(oppFilter))
            let opps = getArray (get oppPath)
            if opps.Length = 0 then [||]
            else
                let oppIds = opps |> Array.map (fun o -> getProp o "opportunityid")
                let cpFilter = oppIds |> Array.map (fun id -> sprintf "_msp_opportunity_value eq %s" id) |> String.concat " or "
                let cpSel = "msp_name,msp_commercialstrat,msp_dealtype,msp_timeline,msp_direction,msp_compprop,msp_partner,createdon,modifiedon,_msp_opportunity_value"
                let cpPath = sprintf "/msp_closeplans?$select=%s&$filter=%s&$top=10&$orderby=%s" (Uri.EscapeDataString(cpSel)) (Uri.EscapeDataString(cpFilter)) (Uri.EscapeDataString("modifiedon desc"))
                getArray (get cpPath)

    if closePlans.Length = 0 then
        printfn "(no close plans found)"
    else
        for cp in closePlans do
            printfn "=== CLOSE PLAN: %s ===" (getProp cp "msp_name")
            printfn "OPP_ID|%s" (getProp cp "_msp_opportunity_value")
            printfn "DEAL_TYPE|%s" (getProp cp "msp_dealtype")
            printfn "COMMERCIAL_STRATEGY|%s" (getProp cp "msp_commercialstrat")
            printfn "TIMELINE|%s" (getProp cp "msp_timeline")
            printfn "DIRECTION|%s" (getProp cp "msp_direction")
            printfn "COMPETITIVE_PROPOSAL|%s" (getProp cp "msp_compprop")
            printfn "PARTNER|%s" (getProp cp "msp_partner")
            printfn "MODIFIED|%s" (getProp cp "modifiedon")
            printfn ""
        eprintfn "Close plans found: %d" closePlans.Length

// ─── deal-team: Who's on the deal ───────────────────────────────────────────

let cmdDealTeam () =
    if args.Length < 2 then failwith "Usage: deal-team <salesId|opportunityId>"
    let idArg = args.[1]

    let oppIds =
        if idArg.Contains("-") && idArg.Length > 30 then [| idArg |]
        else
            let (accountid, _) = resolveOneAccount idArg
            let oppFilter = sprintf "_parentaccountid_value eq %s and (contains(name,'GitHub') or contains(name,'GHAS') or contains(name,'Copilot') or msp_solutionarea eq 394380000)" accountid
            let oppPath = sprintf "/opportunities?$select=%s&$filter=%s&$top=20" (Uri.EscapeDataString("opportunityid,name")) (Uri.EscapeDataString(oppFilter))
            let opps = getArray (get oppPath)
            opps |> Array.map (fun o -> getProp o "opportunityid")

    if oppIds.Length = 0 then
        printfn "(no GitHub/GHAS opportunities found)"
    else
        printfn "OPP_ID|USER_NAME|IS_OWNER|DATE_ADDED"
        for chunk in (oppIds |> Array.chunkBySize 10) do
            let dtFilter = chunk |> Array.map (fun id -> sprintf "_msp_parentopportunityid_value eq %s" id) |> String.concat " or "
            let dtSel = "_msp_parentopportunityid_value,_msp_dealteamuserid_value,msp_isowner,msp_dateadded,msp_name"
            let dtPath = sprintf "/msp_dealteams?$select=%s&$filter=%s&$top=100" (Uri.EscapeDataString(dtSel)) (Uri.EscapeDataString(dtFilter))
            let members = getArray (get dtPath)
            for m in members do
                printfn "%s|%s|%s|%s" (getProp m "_msp_parentopportunityid_value") (getProp m "msp_name") (getProp m "msp_isowner") (getProp m "msp_dateadded")
        eprintfn "Deal team members listed."

// ─── violations: Milestone hygiene violations ────────────────────────────────

let cmdViolations () =
    let myOnly = not (getFlag "--all")
    let userId = if myOnly then getMyUserId () else ""

    // Get violations — optionally filtered by owner
    let vFilter =
        if myOnly then sprintf "_ownerid_value eq %s" userId
        else "statecode eq 0" // active only
    let vSel = "msp_name,msp_violatedrules,msp_hygienecount,_msp_milestoneid_value,_ownerid_value,createdon,statecode"
    let vPath = sprintf "/msp_milestoneviolations?$select=%s&$filter=%s&$top=100&$orderby=%s" (Uri.EscapeDataString(vSel)) (Uri.EscapeDataString(vFilter)) (Uri.EscapeDataString("createdon desc"))
    let violations = getArray (get vPath)

    if violations.Length = 0 then
        printfn "(no violations found%s)" (if myOnly then " for your milestones" else "")
    else
        printfn "MILESTONE_ID|VIOLATED_RULES|HYGIENE_COUNT|CREATED|STATE"
        for v in violations do
            printfn "%s|%s|%s|%s|%s" (getProp v "_msp_milestoneid_value") (getProp v "msp_violatedrules") (getProp v "msp_hygienecount") (getProp v "createdon") (getProp v "statecode")
        eprintfn "Violations: %d%s" violations.Length (if myOnly then " (mine)" else " (all)")

// ─── account-team: Who works this account ────────────────────────────────────

let cmdAccountTeam () =
    if args.Length < 2 then failwith "Usage: account-team <salesId>"
    let salesId = args.[1]
    let (accountid, acctName) = resolveOneAccount salesId

    // msp_accountteams — field names vary; query with minimal select to discover
    let atFilter = sprintf "_msp_accountid_value eq %s" accountid
    let atSel = "msp_name,msp_accountteamid"
    let atPath = sprintf "/msp_accountteams?$select=%s&$filter=%s&$top=50" (Uri.EscapeDataString(atSel)) (Uri.EscapeDataString(atFilter))
    let members = getArray (get atPath)

    if members.Length = 0 then
        printfn "(no account team records found via msp_accountteams for %s [%s])" acctName salesId
    else
        printfn "ACCOUNT|%s (%s)" acctName salesId
        printfn "NAME|TEAM_ID"
        for m in members do
            printfn "%s|%s" (getProp m "msp_name") (getProp m "msp_accountteamid")
        eprintfn "Account team for %s: %d member(s)" acctName members.Length

// ─── Main dispatch ───────────────────────────────────────────────────────────

let command = if args.Length > 0 then args.[0] else ""

match command with
| "whoami" -> cmdWhoami ()
| "my-tasks" -> cmdMyTasks ()
| "my-activity" -> cmdMyActivity ()
| "coverage" -> cmdCoverage ()
| "account-tasks" -> cmdAccountTasks ()
| "odata" -> cmdOData ()
| "revenue-all" -> cmdRevenueAll ()
| "seats-all" -> cmdSeatsAll ()
| "pipeline" -> cmdPipeline ()
| "account-health" -> cmdAccountHealth ()
| "account-enrichment" -> cmdAccountEnrichment ()
| "competitors" -> cmdCompetitors ()
| "consumption-plan" -> cmdConsumptionPlan ()
| "milestones" | "milestones-bulk" -> cmdMilestones ()
| "contacts" -> cmdContacts ()
| "contacts-odata" -> cmdContactsOData ()
| "create-task" -> cmdCreateTask ()
| "create-tasks" -> cmdCreateTasks ()
| "activities" -> cmdActivities ()
| "team" -> cmdTeam ()
| "add-team" -> cmdAddTeam ()
| "patch-task" -> cmdPatchTask ()
| "complete-tasks" -> cmdCompleteTasks ()
| "categories" -> cmdCategories ()
| "reclassify" -> cmdReclassify ()
| "account-plan" -> cmdAccountPlan ()
| "stakeholders" -> cmdStakeholders ()
| "close-plan" -> cmdClosePlan ()
| "deal-team" -> cmdDealTeam ()
| "violations" -> cmdViolations ()
| "account-team" -> cmdAccountTeam ()
| "" | "--help" | "-h" ->
    printfn "Usage: dotnet fsi msx-cli.fsx <command> [args...]"
    printfn ""
    printfn "Portfolio intelligence:"
    printfn "  revenue-all [--segment=X] [--min=0]      GitHub billed revenue across all accounts"
    printfn "  seats-all [--segment=X]                  GitHub milestone seats/usage across all accounts"
    printfn "  pipeline [--segment=X] [--solution=GitHub]  Open GitHub/GHAS deals with close dates & risk"
    printfn "  competitors [--segment=X]                Competitive landscape (GitHub/GHAS opps w/ competitor)"
    printfn "  consumption-plan [--product=606820000]   GitHub consumption plans (budget/target/actuals)"
    printfn ""
    printfn "Account deep-dive:"
    printfn "  account-health <salesId>                 Full intelligence: metadata + plan + opps + milestones"
    printfn "  account-enrichment <ids>                 Prospecting metadata: dev count, LinkedIn ID, domain"
    printfn "  account-tasks <salesId> [--days=365]     All tasks for one account [--all-users]"
    printfn ""
    printfn "Strategic intelligence:"
    printfn "  account-plan <salesId>                   Deep account plan (mission, challenges, health, compete)"
    printfn "  stakeholders <salesId>                   Org chart from stakeholder maps"
    printfn "  close-plan <salesId|oppId>               Deal close plan details"
    printfn "  deal-team <salesId|oppId>                Deal team members"
    printfn "  violations [--all]                       Milestone hygiene violations (default: mine)"
    printfn "  account-team <salesId>                   Account team roster"
    printfn ""
    printfn "Activity tracking:"
    printfn "  my-activity <start> <end> [--top=500]    Fully resolved tasks → ACCOUNT|DATE|SUBJECT|CATEGORY"
    printfn "  coverage [--segment=Accelerate]          Per-account activity summary (count, last touch)"
    printfn "  odata <entity> <filter> [--select=X] [--top=N] [--orderby=X] [--expand=X]"
    printfn "                                           Raw OData query with pipe-delimited output"
    printfn ""
    printfn "MSX operations:"
    printfn "  whoami                                   Current user info"
    printfn "  my-tasks <start> <end>                   Raw tasks (unresolved milestone IDs)"
    printfn "  milestones <ids> [--keyword=X] [--completed]"
    printfn "  contacts <ids> [--keyword=X]"
    printfn "  contacts-odata <filter>"
    printfn "  create-task <msId> <subject> [--date=X] [--desc=X] [--cat=N]"
    printfn "  create-tasks <json-file>"
    printfn "  activities <milestoneId> [--days=30]"
    printfn "  team <milestoneId>"
    printfn "  add-team <msId> <userId> [--role=SSP]"
    printfn "  complete-tasks <filter>"
    printfn "  patch-task <taskId>"
    printfn "  reclassify <msId> [--to=Demo] [--dry-run]"
    printfn "  categories"
| x ->
    eprintfn "Unknown command: %s" x
    exit 1
