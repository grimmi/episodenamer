#r "packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"


open System
open System.Globalization
open System.IO
open System.Net
open System.Text
open Newtonsoft.Json.Linq
open Newtonsoft.Json
let mutable apikey = ""
let mutable user = ""
let mutable userkey = ""

let mutable token = ""

type Show = { aliases: string[]; id: int; seriesName: string }
type SearchResponse = { data: Show[] }
type Links = { first: Nullable<int>; last: Nullable<int>; next: Nullable<int>; prev: Nullable<int> }
type Episode = { absoluteNumber: Nullable<int>; airedEpisodeNumber: Nullable<int>; airedSeason: Nullable<int>; episodeName: string; firstAired: string }
type EpisodeResult = { data: Episode[] }


let extractValue (line:string) = 
    (line.Split '=').[1].Trim()

let readcredentials = 
    match File.ReadAllLines("./credentials.cred") with
    |[|keyline;userline;pwline|] ->
        apikey <- keyline |> extractValue
        user <- userline |> extractValue
        userkey <- pwline |> extractValue
    |_ -> raise (Exception "invalid configuration")

readcredentials


let apiUrl = "https://api.thetvdb.com"

let login = async {
        use client = new WebClient()
        let body = "{\"apikey\":\"" + apikey + "\", \"username\":\"" + user + "\", \"userkey\":\"" + userkey + "\"}" 
        client.Headers.Set("Content-Type", "application/json")
        let! response = client.UploadDataTaskAsync(apiUrl + "/login", "POST",  body |> Encoding.UTF8.GetBytes) |> Async.AwaitTask
        let jsonToken = JObject.Parse(Encoding.UTF8.GetString response)
        return jsonToken.["token"] |> string }     

let getAuthorizedClient t =
    let client = new WebClient()
    client.Headers.Set("Content-Type", "application/json")
    client.Headers.Set("Authorization", "Bearer " + t)
    client.Headers.Set("Accept-Language", "en")
    client

let searchShow show = async {
        let client = getAuthorizedClient token
        let! response = client.AsyncDownloadString(Uri(apiUrl + "/search/series?name=" + show))
        return JsonConvert.DeserializeObject<SearchResponse>(response) }

let getEpisodes show = async {
        let client = getAuthorizedClient token
        let! response = client.AsyncDownloadString(Uri(apiUrl + "/series/" + (show.id |> string) + "/episodes"))

        return JsonConvert.DeserializeObject<EpisodeResult>(response) }

let file = "Doctor_Who_17.05.13_19-15_ukbbc_45_TVOON_DE.mpg.HQ.avi"

let parseShow (f:string) = 
    let showPart = f 
                   |> Seq.takeWhile(fun c -> not (Char.IsDigit c)) 
                   |> Array.ofSeq
                   |> System.String
    let cleanShow = showPart.Replace("_", " ").Trim()
    cleanShow

let parseDate (f:string) =
    let datePart = f
                   |> Seq.skipWhile(fun c -> not (Char.IsDigit c))
                   |> Seq.takeWhile(fun c -> Char.IsDigit c || c = '_' || c = '-' || c = '.')
                   |> Array.ofSeq |> System.String
    let date = DateTime.ParseExact(datePart, "yy.MM.dd_HH-mm_", null)
    date.Date

let d = parseDate file

// let printEpisodes = async {
//         let! loggedIn = login
//         token <- loggedIn
//         Async.Start(ticker)
//         let! drwho = searchShow "Doctor Who (2005)"
//         let! episodes = getEpisodes drwho.data.[0]
//         for e in episodes.data do
//             printfn "episode: %A %s" e.absoluteNumber e.episodeName
//     }


let choose (options: (int*string) seq) =
    options
    |> Seq.iter(fun (idx, desc) -> printfn "[%d]: %s" idx desc)
    printf "Which option? "
    let input = Console.ReadLine()
    // printfn "your choice: %s" input
    // input |> int
    9

let findEpisodes = async {
    let! loggedIn = login
    token <- loggedIn
    let! show = searchShow (file |> parseShow)
    let date = parseDate file
    let chosenIdx = choose (show.data |> Seq.mapi(fun idx show -> (idx, show.seriesName)))
    let searchedShow = show.data.[chosenIdx]
    printfn "SEARCHED SHOW: %A" searchedShow
    let! episodes = getEpisodes searchedShow
    let matchingEpisode = episodes.data |> Seq.tryFind(fun e -> 
        not (isNull e.firstAired) && e.firstAired.Length > 0 && DateTime.Parse(e.firstAired) = date)

    match matchingEpisode with
    |None -> printfn "No episode found!"
    |Some episode -> printfn "episode found: %s" episode.episodeName
}

Async.Start findEpisodes