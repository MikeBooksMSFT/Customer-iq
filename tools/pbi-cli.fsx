#!/usr/bin/env dotnet fsi
// PBI CLI — Power BI DAX Query Engine for GitHub/Copilot Territory Intelligence
// Queries the "MS GH Joint Sales Dashboard" dataset via Power BI REST API
// Usage: dotnet fsi pbi-cli.fsx <command> [options]
//
// Commands:
//   territory <tpid1,tpid2,...>  — Full territory view for specific accounts
//   portfolio [--top=N]         — All my-accounts ranked by GH ACR
//   copilot <tpid1,...>         — Copilot engagement detail (licenses, WAU, WEU)
//   whitespace [--top=N]        — Whitespace / TAM / eligibility flags
//   acr-trend <tpid>            — Monthly ACR trend for an account
//   account-360 <tpid>          — Everything we know about one account
//   roles <tpid>                — Account team roles from PBI
//   search <name>               — Find a TPID by account name

open System
open System.IO
open System.Net.Http
open System.Text
open System.Text.Json

// ============================================================
// CONFIG
// ============================================================
let groupId = "e4469a61-5615-4543-adb3-e70a35f4a492"
let datasetId = "72fd1b40-358b-4148-925c-a649074523f5"
let baseUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{groupId}/datasets/{datasetId}/executeQueries"
let myAccountsCsv = Path.Combine(__SOURCE_DIRECTORY__, "accounts.csv")

// ============================================================
// AUTH
// ============================================================
let getToken () =
    let psi = Diagnostics.ProcessStartInfo(@"C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd", "account get-access-token --resource https://analysis.windows.net/powerbi/api --query accessToken -o tsv")
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false
    psi.CreateNoWindow <- true
    let proc = Diagnostics.Process.Start(psi)
    let token = proc.StandardOutput.ReadToEnd().Trim()
    proc.WaitForExit()
    if proc.ExitCode <> 0 || String.IsNullOrWhiteSpace(token) then
        failwith "ERROR: az CLI auth failed. Run: az login"
    token

// ============================================================
// DAX QUERY EXECUTION
// ============================================================
let client = new HttpClient()

let executeDax (token: string) (dax: string) =
    let body = $"""{{ "queries": [{{ "query": "{dax.Replace("\"", "\\\"")}" }}], "serializerSettings": {{ "includeNulls": true }} }}"""
    let req = new HttpRequestMessage(HttpMethod.Post, baseUrl)
    req.Headers.Add("Authorization", $"Bearer {token}")
    req.Content <- new StringContent(body, Encoding.UTF8, "application/json")
    let resp = client.Send(req)
    let content = resp.Content.ReadAsStringAsync().Result
    if not resp.IsSuccessStatusCode then
        eprintfn "ERROR %d: %s" (int resp.StatusCode) (content.Substring(0, min 500 content.Length))
        None
    else
        Some (JsonDocument.Parse(content))

let getRows (doc: JsonDocument) =
    try
        let results = doc.RootElement.GetProperty("results")
        let first = results.[0]
        let tables = first.GetProperty("tables")
        let table0 = tables.[0]
        let rows = table0.GetProperty("rows")
        rows.EnumerateArray() |> Seq.toArray
    with _ -> [||]

// ============================================================
// MY-ACCOUNTS LOADER
// ============================================================
type Account = { MsSalesId: string; Name: string; Segment: string; Atu: string; Territory: string; Accelerate: string }

let loadMyAccounts () =
    if not (File.Exists myAccountsCsv) then [||]
    else
        File.ReadAllLines(myAccountsCsv)
        |> Array.skip 1
        |> Array.map (fun line ->
            let parts = line.Split(',')
            { MsSalesId = parts.[0]; Name = parts.[1]; Segment = parts.[2]; Atu = parts.[3]; Territory = parts.[4]; Accelerate = if parts.Length > 5 then parts.[5] else "" })

// ============================================================
// COMMANDS
// ============================================================

let cmdTerritory (token: string) (tpids: string[]) =
    let tpidFilter = tpids |> Array.map (fun t -> $"[TPID] = {t}") |> String.concat " || "
    let dax = sprintf "EVALUATE FILTER(SELECTCOLUMNS('TPID_Cohort Mapping', \"TPID\", 'TPID_Cohort Mapping'[TPID], \"GHE_Lic\", 'TPID_Cohort Mapping'[GHE Standalone Licenses], \"GHAS\", 'TPID_Cohort Mapping'[GHAS Purchased (MSFT Paper)], \"GH_ACR\", 'TPID_Cohort Mapping'[GH ACR FYTD], \"Copilot_Rev\", 'TPID_Cohort Mapping'[GH Consump rev (Copilot) FYTD], \"UBU\", 'TPID_Cohort Mapping'[GH Unique Billable Users], \"DevCount\", 'TPID_Cohort Mapping'[GH Estimated Dev Count], \"Copilot_Lic\", 'TPID_Cohort Mapping'[Current Copilot Licenses], \"WAU\", 'TPID_Cohort Mapping'[Weekly Active Users (WAU)], \"WEU\", 'TPID_Cohort Mapping'[Weekly Engaged Users (WEU)]), %s)" tpidFilter
    match executeDax token dax with
    | None -> ()
    | Some doc ->
        printfn "TPID|GHE_LIC|GHAS|GH_ACR_FYTD|COPILOT_REV|UBU|DEV_COUNT|COPILOT_LIC|WAU|WEU"
        for row in getRows doc do
            let g k = try row.GetProperty(k: string).ToString() with _ -> ""
            printfn "%s|%s|%s|%s|%s|%s|%s|%s|%s|%s" (g "[TPID]") (g "[GHE_Lic]") (g "[GHAS]") (g "[GH_ACR]") (g "[Copilot_Rev]") (g "[UBU]") (g "[DevCount]") (g "[Copilot_Lic]") (g "[WAU]") (g "[WEU]")

let cmdPortfolio (token: string) (top: int) =
    let accounts = loadMyAccounts()
    if accounts.Length = 0 then eprintfn "ERROR: accounts.csv not found"; ()
    else
        // Query in batches of 50
        let allTpids = accounts |> Array.map (fun a -> a.MsSalesId)
        let mutable allRows = []
        for batch in allTpids |> Array.chunkBySize 20 do
            let filter = batch |> Array.map (fun t -> sprintf "[TPID] = %s" t) |> String.concat " || "
            let dax = sprintf "EVALUATE FILTER(SELECTCOLUMNS('TPID_Cohort Mapping', \"TPID\", 'TPID_Cohort Mapping'[TPID], \"GH_ACR\", 'TPID_Cohort Mapping'[GH ACR FYTD], \"Copilot_Rev\", 'TPID_Cohort Mapping'[GH Consump rev (Copilot) FYTD], \"GHE_Lic\", 'TPID_Cohort Mapping'[GHE Standalone Licenses], \"GHAS\", 'TPID_Cohort Mapping'[GHAS Purchased (MSFT Paper)], \"UBU\", 'TPID_Cohort Mapping'[GH Unique Billable Users], \"Copilot_Lic\", 'TPID_Cohort Mapping'[Current Copilot Licenses]), %s)" filter
            match executeDax token dax with
            | Some doc -> allRows <- allRows @ (getRows doc |> Array.toList)
            | None -> ()
            Threading.Thread.Sleep(500)
        
        // Sort by GH_ACR descending, join with account names
        let acctMap = accounts |> Array.map (fun a -> a.MsSalesId, a) |> Map.ofArray
        let sorted =
            allRows
            |> List.map (fun row ->
                let tpid = try row.GetProperty("[TPID]").GetInt64() |> string with _ -> ""
                let acr = try row.GetProperty("[GH_ACR]").GetDouble() with _ -> 0.0
                (tpid, acr, row))
            |> List.sortByDescending (fun (_, acr, _) -> acr)
            |> List.take (min top allRows.Length)
        
        printfn "TPID|ACCOUNT|GH_ACR_FYTD|COPILOT_REV|GHE_LIC|GHAS|UBU|COPILOT_LIC"
        for (tpid, _, row) in sorted do
            let name = match acctMap.TryFind tpid with Some a -> a.Name | None -> "?"
            let g k = try row.GetProperty(k: string).ToString() with _ -> ""
            printfn "%s|%s|%s|%s|%s|%s|%s|%s" tpid name (g "[GH_ACR]") (g "[Copilot_Rev]") (g "[GHE_Lic]") (g "[GHAS]") (g "[UBU]") (g "[Copilot_Lic]")

let cmdCopilot (token: string) (tpids: string[]) =
    let tpidFilter = tpids |> Array.map (fun t -> $"[TPID] = {t}") |> String.concat " || "
    let dax = sprintf "EVALUATE FILTER(SELECTCOLUMNS('TPID_Cohort Mapping', \"TPID\", 'TPID_Cohort Mapping'[TPID], \"Copilot_Rev\", 'TPID_Cohort Mapping'[GH Consump rev (Copilot) FYTD], \"Copilot_Lic\", 'TPID_Cohort Mapping'[Current Copilot Licenses], \"WAU\", 'TPID_Cohort Mapping'[Weekly Active Users (WAU)], \"WEU\", 'TPID_Cohort Mapping'[Weekly Engaged Users (WEU)], \"CfB_MEU\", 'TPID_Cohort Mapping'[CfB MEU], \"CfB_ACR\", 'TPID_Cohort Mapping'[CfB ACR FYTD], \"CfB_Trial\", 'TPID_Cohort Mapping'[CfB Trial Seats], \"UBU\", 'TPID_Cohort Mapping'[GH Unique Billable Users], \"GHE_Lic\", 'TPID_Cohort Mapping'[GHE Standalone Licenses]), %s)" tpidFilter
    match executeDax token dax with
    | None -> ()
    | Some doc ->
        printfn "TPID|COPILOT_REV_FYTD|COPILOT_LIC|WAU|WEU|CFB_MEU|CFB_ACR_FYTD|CFB_TRIAL|UBU|GHE_LIC"
        for row in getRows doc do
            let g k = try row.GetProperty(k: string).ToString() with _ -> ""
            printfn "%s|%s|%s|%s|%s|%s|%s|%s|%s|%s" (g "[TPID]") (g "[Copilot_Rev]") (g "[Copilot_Lic]") (g "[WAU]") (g "[WEU]") (g "[CfB_MEU]") (g "[CfB_ACR]") (g "[CfB_Trial]") (g "[UBU]") (g "[GHE_Lic]")

let cmdWhitespace (token: string) (top: int) =
    let accounts = loadMyAccounts()
    if accounts.Length = 0 then eprintfn "ERROR: accounts.csv not found"; ()
    else
        let allTpids = accounts |> Array.map (fun a -> a.MsSalesId)
        let mutable allRows = []
        for batch in allTpids |> Array.chunkBySize 20 do
            let filter = batch |> Array.map (fun t -> sprintf "[TPID] = %s" t) |> String.concat " || "
            let dax = sprintf "EVALUATE FILTER(SELECTCOLUMNS('TPID_Cohort Mapping', \"TPID\", 'TPID_Cohort Mapping'[TPID], \"GHE_TAM\", 'TPID_Cohort Mapping'[TAM GHE], \"GHE_RAM\", 'TPID_Cohort Mapping'[RAM GHE], \"GHE_SAM\", 'TPID_Cohort Mapping'[GHE SAM [70%% Attainment]], \"Copilot_TAM\", 'TPID_Cohort Mapping'[Copilot Business TAM], \"Copilot_RAM\", 'TPID_Cohort Mapping'[Copilot Business RAM], \"GHAS_TAM\", 'TPID_Cohort Mapping'[GHAS TAM_FY25], \"GHAS_RAM\", 'TPID_Cohort Mapping'[GHAS RAM], \"Whitespace\", 'TPID_Cohort Mapping'[WhitespaceFLAG], \"GHE_Elig\", 'TPID_Cohort Mapping'[GHE_Eligibility_FINAL FLAG], \"GHAS_Elig\", 'TPID_Cohort Mapping'[GHAS Eligiblity FLAG_FINAL], \"DevCount\", 'TPID_Cohort Mapping'[GH Estimated Dev Count]), %s)" filter
            match executeDax token dax with
            | Some doc -> allRows <- allRows @ (getRows doc |> Array.toList)
            | None -> ()
            Threading.Thread.Sleep(500)
        
        let acctMap = accounts |> Array.map (fun a -> a.MsSalesId, a) |> Map.ofArray
        // Filter to rows with whitespace or eligibility flags
        let filtered =
            allRows
            |> List.map (fun row ->
                let tpid = try row.GetProperty("[TPID]").GetInt64() |> string with _ -> ""
                let tam = try row.GetProperty("[GHE_TAM]").GetDouble() with _ -> 0.0
                (tpid, tam, row))
            |> List.sortByDescending (fun (_, tam, _) -> tam)
            |> List.take (min top allRows.Length)
        
        printfn "TPID|ACCOUNT|GHE_TAM|GHE_RAM|GHE_SAM|COPILOT_TAM|COPILOT_RAM|GHAS_TAM|GHAS_RAM|WHITESPACE|GHE_ELIG|GHAS_ELIG|DEV_COUNT"
        for (tpid, _, row) in filtered do
            let name = match acctMap.TryFind tpid with Some a -> a.Name | None -> "?"
            let g k = try row.GetProperty(k: string).ToString() with _ -> ""
            printfn "%s|%s|%s|%s|%s|%s|%s|%s|%s|%s|%s|%s|%s" tpid name (g "[GHE_TAM]") (g "[GHE_RAM]") (g "[GHE_SAM]") (g "[Copilot_TAM]") (g "[Copilot_RAM]") (g "[GHAS_TAM]") (g "[GHAS_RAM]") (g "[Whitespace]") (g "[GHE_Elig]") (g "[GHAS_Elig]") (g "[DevCount]")

let cmdAccount360 (token: string) (tpid: string) =
    // Query specific columns from cohort + accounts dimension
    let daxIdentity = sprintf "EVALUATE FILTER(SELECTCOLUMNS('___DIM_Accounts_NEW SPM', \"TPID\", '___DIM_Accounts_NEW SPM'[TPID], \"Name\", '___DIM_Accounts_NEW SPM'[MSSalesAccountName], \"Segment\", '___DIM_Accounts_NEW SPM'[Segment], \"SubSeg\", '___DIM_Accounts_NEW SPM'[SubSegment], \"Territory\", '___DIM_Accounts_NEW SPM'[SalesTerritoryName], \"ATU\", '___DIM_Accounts_NEW SPM'[ATUName], \"Industry\", '___DIM_Accounts_NEW SPM'[Industry], \"Vertical\", '___DIM_Accounts_NEW SPM'[Vertical]), [TPID] = %s)" tpid
    let daxCohort = sprintf "EVALUATE FILTER(SELECTCOLUMNS('TPID_Cohort Mapping', \"TPID\", 'TPID_Cohort Mapping'[TPID], \"GHE_Lic\", 'TPID_Cohort Mapping'[GHE Standalone Licenses], \"GHE_Bundle\", 'TPID_Cohort Mapping'[GHE Bundle], \"GHAS_MSFT\", 'TPID_Cohort Mapping'[GHAS Purchased (MSFT Paper)], \"GHAS_GH\", 'TPID_Cohort Mapping'[GHAS Purchased (GH Paper)], \"GHAS_Committers\", 'TPID_Cohort Mapping'[GHAS Committers (T90 Days)], \"GH_ACR\", 'TPID_Cohort Mapping'[GH ACR FYTD], \"Copilot_Rev\", 'TPID_Cohort Mapping'[GH Consump rev (Copilot) FYTD], \"Actions_Rev\", 'TPID_Cohort Mapping'[GH Consump rev (Actions) FYTD], \"CfB_ACR\", 'TPID_Cohort Mapping'[CfB ACR FYTD], \"GHAS_BilledRev\", 'TPID_Cohort Mapping'[GHAS Billed Rev FYTD], \"Copilot_Lic\", 'TPID_Cohort Mapping'[Current Copilot Licenses], \"WAU\", 'TPID_Cohort Mapping'[Weekly Active Users (WAU)], \"WEU\", 'TPID_Cohort Mapping'[Weekly Engaged Users (WEU)], \"CfB_MEU\", 'TPID_Cohort Mapping'[CfB MEU], \"CfB_Trial\", 'TPID_Cohort Mapping'[CfB Trial Seats], \"UBU\", 'TPID_Cohort Mapping'[UBU], \"DevCount\", 'TPID_Cohort Mapping'[GH Estimated Dev Count], \"GH_MEU\", 'TPID_Cohort Mapping'[GH MEU], \"GH_BAM\", 'TPID_Cohort Mapping'[GH BAM]), [TPID] = %s)" tpid
    let daxWhitespace = sprintf "EVALUATE FILTER(SELECTCOLUMNS('TPID_Cohort Mapping', \"TPID\", 'TPID_Cohort Mapping'[TPID], \"GHE_TAM\", 'TPID_Cohort Mapping'[TAM GHE], \"GHE_RAM\", 'TPID_Cohort Mapping'[RAM GHE], \"Copilot_TAM\", 'TPID_Cohort Mapping'[Copilot Business TAM], \"Copilot_RAM\", 'TPID_Cohort Mapping'[Copilot Business RAM], \"GHAS_TAM\", 'TPID_Cohort Mapping'[GHAS TAM_FY25], \"GHAS_RAM\", 'TPID_Cohort Mapping'[GHAS RAM], \"Whitespace\", 'TPID_Cohort Mapping'[WhitespaceFLAG], \"GHE_Elig\", 'TPID_Cohort Mapping'[GHE_Eligibility_FINAL FLAG], \"GHAS_Elig\", 'TPID_Cohort Mapping'[GHAS Eligiblity FLAG_FINAL], \"GitLab_Users\", 'TPID_Cohort Mapping'[Unique Users - GitLab/BB (Last Month)], \"NotGit_Users\", 'TPID_Cohort Mapping'[Unique Users - NotUsingGit (Last Month)], \"EA_Exp\", 'TPID_Cohort Mapping'[Next EA Expiration Date]), [TPID] = %s)" tpid
    
    printfn "=== ACCOUNT 360: TPID %s ===" tpid
    printfn ""
    
    match executeDax token daxIdentity with
    | Some doc ->
        for row in getRows doc do
            let g k = try row.GetProperty(k: string).ToString() with _ -> "—"
            printfn "[IDENTITY]"
            printfn "  Name: %s" (g "[Name]")
            printfn "  Segment: %s" (g "[Segment]")
            printfn "  SubSegment: %s" (g "[SubSeg]")
            printfn "  Territory: %s" (g "[Territory]")
            printfn "  ATU: %s" (g "[ATU]")
            printfn "  Industry: %s" (g "[Industry]")
            printfn "  Vertical: %s" (g "[Vertical]")
    | None -> printfn "[IDENTITY] — query failed"
    printfn ""
    
    match executeDax token daxCohort with
    | Some doc ->
        for row in getRows doc do
            let g k = try row.GetProperty(k: string).ToString() with _ -> "—"
            printfn "[GITHUB LICENSING]"
            printfn "  GHE Licenses (Standalone): %s" (g "[GHE_Lic]")
            printfn "  GHE Licenses (Bundle): %s" (g "[GHE_Bundle]")
            printfn "  GHAS (MSFT Paper): %s" (g "[GHAS_MSFT]")
            printfn "  GHAS (GH Paper): %s" (g "[GHAS_GH]")
            printfn "  GHAS Committers (T90): %s" (g "[GHAS_Committers]")
            printfn ""
            printfn "[REVENUE]"
            printfn "  GH ACR FYTD: %s" (g "[GH_ACR]")
            printfn "  Copilot Rev FYTD: %s" (g "[Copilot_Rev]")
            printfn "  Actions Rev FYTD: %s" (g "[Actions_Rev]")
            printfn "  CfB ACR FYTD: %s" (g "[CfB_ACR]")
            printfn "  GHAS Billed Rev FYTD: %s" (g "[GHAS_BilledRev]")
            printfn ""
            printfn "[COPILOT ENGAGEMENT]"
            printfn "  Current Copilot Licenses: %s" (g "[Copilot_Lic]")
            printfn "  Weekly Active Users (WAU): %s" (g "[WAU]")
            printfn "  Weekly Engaged Users (WEU): %s" (g "[WEU]")
            printfn "  CfB MEU: %s" (g "[CfB_MEU]")
            printfn "  CfB Trial Seats: %s" (g "[CfB_Trial]")
            printfn ""
            printfn "[USAGE]"
            printfn "  Unique Billable Users: %s" (g "[UBU]")
            printfn "  Estimated Dev Count: %s" (g "[DevCount]")
            printfn "  GH MEU: %s" (g "[GH_MEU]")
            printfn "  GH BAM: %s" (g "[GH_BAM]")
    | None -> printfn "[COHORT DATA] — query failed"
    printfn ""
    
    match executeDax token daxWhitespace with
    | Some doc ->
        for row in getRows doc do
            let g k = try row.GetProperty(k: string).ToString() with _ -> "—"
            printfn "[WHITESPACE & ELIGIBILITY]"
            printfn "  GHE TAM: %s" (g "[GHE_TAM]")
            printfn "  GHE RAM: %s" (g "[GHE_RAM]")
            printfn "  Copilot Business TAM: %s" (g "[Copilot_TAM]")
            printfn "  Copilot Business RAM: %s" (g "[Copilot_RAM]")
            printfn "  GHAS TAM: %s" (g "[GHAS_TAM]")
            printfn "  GHAS RAM: %s" (g "[GHAS_RAM]")
            printfn "  Whitespace Flag: %s" (g "[Whitespace]")
            printfn "  GHE Eligibility: %s" (g "[GHE_Elig]")
            printfn "  GHAS Eligibility: %s" (g "[GHAS_Elig]")
            printfn ""
            printfn "[COMPETITIVE]"
            printfn "  GitLab/BB Users (Last Mo): %s" (g "[GitLab_Users]")
            printfn "  NotUsingGit Users (Last Mo): %s" (g "[NotGit_Users]")
            printfn "  Next EA Expiration: %s" (g "[EA_Exp]")
    | None -> printfn "[WHITESPACE] — query failed"

let cmdRoles (token: string) (tpid: string) =
    // Check MSSalesAccountId in roles table (same format as ms_sales_id)
    let dax = sprintf "EVALUATE FILTER(SELECTCOLUMNS('___DIM_MS_Roles_SPM', \"AcctId\", '___DIM_MS_Roles_SPM'[MSSalesAccountId], \"Alias\", '___DIM_MS_Roles_SPM'[Alias], \"Role\", '___DIM_MS_Roles_SPM'[RolePlayed], \"RoleType\", '___DIM_MS_Roles_SPM'[RoleType], \"Summary\", '___DIM_MS_Roles_SPM'[RoleSummary]), [AcctId] = %s)" tpid
    match executeDax token dax with
    | None -> ()
    | Some doc ->
        printfn "ACCT_ID|ALIAS|ROLE|ROLE_TYPE|SUMMARY"
        for row in getRows doc do
            let g k = try row.GetProperty(k: string).ToString() with _ -> ""
            printfn "%s|%s|%s|%s|%s" (g "[AcctId]") (g "[Alias]") (g "[Role]") (g "[RoleType]") (g "[Summary]")

let cmdSearch (token: string) (name: string) =
    let dax = sprintf "EVALUATE TOPN(10, FILTER(SELECTCOLUMNS('___DIM_Accounts_NEW SPM', \"TPID\", '___DIM_Accounts_NEW SPM'[TPID], \"Name\", '___DIM_Accounts_NEW SPM'[MSSalesAccountName], \"Segment\", '___DIM_Accounts_NEW SPM'[Segment], \"Territory\", '___DIM_Accounts_NEW SPM'[SalesTerritoryName]), SEARCH(\"%s\", [Name], 1, 0) > 0))" (name.Replace("\"", ""))
    match executeDax token dax with
    | None -> ()
    | Some doc ->
        printfn "TPID|ACCOUNT_NAME|SEGMENT|TERRITORY"
        for row in getRows doc do
            let g k = try row.GetProperty(k: string).ToString() with _ -> ""
            printfn "%s|%s|%s|%s" (g "[TPID]") (g "[Name]") (g "[Segment]") (g "[Territory]")

let cmdAcrTrend (token: string) (tpid: string) =
    // ACR_GitHub columns have embedded brackets (e.g., "Calendar[Fiscal Month]")
    // Use SELECTCOLUMNS with the weird naming, quoting with single quotes
    let dax = sprintf "EVALUATE FILTER(ADDCOLUMNS(SUMMARIZE('ACR_GitHub', 'ACR_GitHub'[Calendar[Fiscal Month]], 'ACR_GitHub'[Calendar[Fiscal Year]], 'ACR_GitHub'[Calendar[Fiscal Quarter]]), \"ACR\", SUM('ACR_GitHub'[[Azure Consumed Revenue]])), 'ACR_GitHub'[Account Information[TPID]] = %s)" tpid
    match executeDax token dax with
    | Some doc ->
        let rows = getRows doc
        if rows.Length = 0 then
            // Fallback: try querying from the cohort table for current FYTD only
            printfn "No monthly ACR breakdown available. Current FYTD from cohort:"
            let daxFallback = sprintf "EVALUATE FILTER(SELECTCOLUMNS('TPID_Cohort Mapping', \"TPID\", 'TPID_Cohort Mapping'[TPID], \"GH_ACR_FYTD\", 'TPID_Cohort Mapping'[GH ACR FYTD], \"Actions_FYTD\", 'TPID_Cohort Mapping'[GH Consump rev (Actions) FYTD], \"Copilot_FYTD\", 'TPID_Cohort Mapping'[GH Consump rev (Copilot) FYTD], \"CfB_ACR_FYTD\", 'TPID_Cohort Mapping'[CfB ACR FYTD], \"GHAS_BilledRev\", 'TPID_Cohort Mapping'[GHAS Billed Rev FYTD]), [TPID] = %s)" tpid
            match executeDax token daxFallback with
            | Some d2 ->
                printfn "TPID|GH_ACR_FYTD|ACTIONS_FYTD|COPILOT_FYTD|CFB_ACR_FYTD|GHAS_BILLED_REV"
                for row in getRows d2 do
                    let g k = try row.GetProperty(k: string).ToString() with _ -> ""
                    printfn "%s|%s|%s|%s|%s|%s" (g "[TPID]") (g "[GH_ACR_FYTD]") (g "[Actions_FYTD]") (g "[Copilot_FYTD]") (g "[CfB_ACR_FYTD]") (g "[GHAS_BilledRev]")
            | None -> ()
        else
            printfn "FISCAL_YEAR|FISCAL_QTR|FISCAL_MONTH|ACR"
            for row in rows do
                let g k = try row.GetProperty(k: string).ToString() with _ -> ""
                printfn "%s|%s|%s|%s" (g "[Calendar[Fiscal Year]]") (g "[Calendar[Fiscal Quarter]]") (g "[Calendar[Fiscal Month]]") (g "[ACR]")
    | None ->
        // The ACR_GitHub table may use weird column refs; fall back to revenue breakdown
        printfn "Monthly trend query failed. Showing FYTD breakdown:"
        let daxFallback = sprintf "EVALUATE FILTER(SELECTCOLUMNS('TPID_Cohort Mapping', \"TPID\", 'TPID_Cohort Mapping'[TPID], \"GH_ACR_FYTD\", 'TPID_Cohort Mapping'[GH ACR FYTD], \"Actions_FYTD\", 'TPID_Cohort Mapping'[GH Consump rev (Actions) FYTD], \"Copilot_FYTD\", 'TPID_Cohort Mapping'[GH Consump rev (Copilot) FYTD], \"CfB_ACR_FYTD\", 'TPID_Cohort Mapping'[CfB ACR FYTD], \"GHAS_BilledRev\", 'TPID_Cohort Mapping'[GHAS Billed Rev FYTD]), [TPID] = %s)" tpid
        match executeDax token daxFallback with
        | Some d2 ->
            printfn "TPID|GH_ACR_FYTD|ACTIONS_FYTD|COPILOT_FYTD|CFB_ACR_FYTD|GHAS_BILLED_REV"
            for row in getRows d2 do
                let g k = try row.GetProperty(k: string).ToString() with _ -> ""
                printfn "%s|%s|%s|%s|%s|%s" (g "[TPID]") (g "[GH_ACR_FYTD]") (g "[Actions_FYTD]") (g "[Copilot_FYTD]") (g "[CfB_ACR_FYTD]") (g "[GHAS_BilledRev]")
        | None -> ()

// ============================================================
// CLI DISPATCHER
// ============================================================
let args = fsi.CommandLineArgs |> Array.skip 1

if args.Length = 0 then
    printfn "PBI CLI — Power BI Territory Intelligence"
    printfn ""
    printfn "Usage: dotnet fsi pbi-cli.fsx <command> [args]"
    printfn ""
    printfn "Commands:"
    printfn "  territory <tpid1,tpid2,...>  Full data for specific accounts"
    printfn "  portfolio [--top=N]          All accounts ranked by GH ACR (default top 30)"
    printfn "  copilot <tpid1,...>          Copilot engagement metrics"
    printfn "  whitespace [--top=N]         TAM/RAM/SAM/eligibility for portfolio"
    printfn "  acr-trend <tpid>             Monthly ACR trend for an account"
    printfn "  account-360 <tpid>           Complete 360° view of one account"
    printfn "  roles <tpid>                 Account team from PBI"
    printfn "  search <name>               Find TPID by name"
    printfn ""
    printfn "TPID = ms_sales_id from accounts.csv (same thing)"
    printfn ""
    printfn "Examples:"
    printfn "  dotnet fsi pbi-cli.fsx territory 88459439"
    printfn "  dotnet fsi pbi-cli.fsx portfolio --top=20"
    printfn "  dotnet fsi pbi-cli.fsx account-360 88459439"
    printfn "  dotnet fsi pbi-cli.fsx search contoso"
else
    let token = getToken()
    match args.[0].ToLower() with
    | "territory" ->
        let tpids = if args.Length > 1 then args.[1].Split(',') else [||]
        if tpids.Length = 0 then eprintfn "ERROR: provide TPID(s)"
        else cmdTerritory token tpids
    | "portfolio" ->
        let top = 
            args |> Array.tryFind (fun a -> a.StartsWith("--top="))
            |> Option.map (fun a -> int (a.Replace("--top=", "")))
            |> Option.defaultValue 30
        cmdPortfolio token top
    | "copilot" ->
        let tpids = if args.Length > 1 then args.[1].Split(',') else [||]
        if tpids.Length = 0 then eprintfn "ERROR: provide TPID(s)"
        else cmdCopilot token tpids
    | "whitespace" ->
        let top = 
            args |> Array.tryFind (fun a -> a.StartsWith("--top="))
            |> Option.map (fun a -> int (a.Replace("--top=", "")))
            |> Option.defaultValue 30
        cmdWhitespace token top
    | "account-360" ->
        if args.Length < 2 then eprintfn "ERROR: provide TPID"
        else cmdAccount360 token args.[1]
    | "acr-trend" ->
        if args.Length < 2 then eprintfn "ERROR: provide TPID"
        else cmdAcrTrend token args.[1]
    | "roles" ->
        if args.Length < 2 then eprintfn "ERROR: provide TPID"
        else cmdRoles token args.[1]
    | "search" ->
        if args.Length < 2 then eprintfn "ERROR: provide search term"
        else cmdSearch token (args.[1..] |> String.concat " ")
    | cmd -> eprintfn "Unknown command: %s" cmd

