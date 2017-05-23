#r "packages/Newtonsoft.Json/lib/netstandard1.3/Newtonsoft.Json.dll"


open System
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

let result = async {
        let! loggedIn = login
        token <- loggedIn
        let! drwho = searchShow "Doctor Who (2005)"
        return getEpisodes drwho.data.[0]
    }

let ticker = async {
    for x in [ 1 .. 200 ] do
        let! s = Async.Sleep(10)
        printfn "slept 10ms!"
}

let printEpisodes = async {
        let! loggedIn = login
        token <- loggedIn
        Async.Start(ticker)
        let! drwho = searchShow "Doctor Who (2005)"
        let! episodes = getEpisodes drwho.data.[0]
        for e in episodes.data do
            printfn "episode: %A %s" e.absoluteNumber e.episodeName
    }

Async.Start printEpisodes