#r "packages/Newtonsoft.Json/lib/netstandard1.3/Newtonsoft.Json.dll"

let mutable apikey = ""
let mutable user = ""
let mutable userkey = ""

open System
open System.IO

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

open System.Net
open System.Text
open Newtonsoft.Json.Linq
open Newtonsoft.Json

let apiUrl = "https://api.thetvdb.com"

let login =
    let tokenResponse = async {
        use client = new WebClient()
        let body = "{\"apikey\":\"" + apikey + "\", \"username\":\"" + user + "\", \"userkey\":\"" + userkey + "\"}" 
        client.Headers.Set("Content-Type", "application/json")
        let! response = client.UploadDataTaskAsync(apiUrl + "/login", "POST",  body |> Encoding.UTF8.GetBytes) |> Async.AwaitTask
        return JObject.Parse(Encoding.UTF8.GetString response) } 
    tokenResponse |> Async.RunSynchronously

let token = login.["token"] |> string

let getAuthorizedClient t =
    let client = new WebClient()
    client.Headers.Set("Content-Type", "application/json")
    client.Headers.Set("Authorization", "Bearer " + t)
    client.Headers.Set("Accept-Language", "en")
    client

type Show = { aliases: string[]; id: int; seriesName: string }
type SearchResponse = { data: Show[] }

let searchShow show = 
    let result = async {
        let client = getAuthorizedClient token
        let! response = client.AsyncDownloadString(Uri(apiUrl + "/search/series?name=" + show))

        return JsonConvert.DeserializeObject<SearchResponse>(response) }
    result |> Async.RunSynchronously


let drwho = searchShow "Doctor Who (2005)"
printfn "%s" (drwho |> string)

type Links = { first: Nullable<int>; last: Nullable<int>; next: Nullable<int>; prev: Nullable<int> }
type Episode = { absoluteNumber: Nullable<int>; airedEpisodeNumber: Nullable<int>; airedSeason: Nullable<int>; episodeName: string; firstAired: string }
type EpisodeResult = { data: Episode[] }

let getEpisodes show = 
    let result = async {
        let client = getAuthorizedClient token
        let! response = client.AsyncDownloadString(new Uri(apiUrl + "/series/" + (show.id |> string) + "/episodes"))

        return JsonConvert.DeserializeObject<EpisodeResult>(response) }
    result |> Async.RunSynchronously

let whoepisodes = getEpisodes drwho.data.[0]
printfn "%A" whoepisodes