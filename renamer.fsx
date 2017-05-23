#r "packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"

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
    use client = new WebClient()
    let body = "{\"apikey\":\"" + apikey + "\", \"username\":\"" + user + "\", \"userkey\":\"" + userkey + "\"}" 
    client.Headers.Set("Content-Type", "application/json")
    let response = client.UploadData(apiUrl + "/login", "POST",  body |> Encoding.UTF8.GetBytes)

    JObject.Parse(Encoding.UTF8.GetString response)

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
    let client = getAuthorizedClient token
    let response = client.DownloadString(apiUrl + "/search/series?name=" + show)

    JsonConvert.DeserializeObject<SearchResponse>(response)

let drwho = searchShow "Doctor Who (2005)"
printfn "%s" (drwho |> string)

type Links = { first: Nullable<int>; last: Nullable<int>; next: Nullable<int>; prev: Nullable<int> }
type Episode = { absoluteNumber: Nullable<int>; airedEpisodeNumber: Nullable<int>; airedSeason: Nullable<int>; episodeName: string; firstAired: string }
type EpisodeResult = { data: Episode[] }

let getEpisodes show = 
    let client = getAuthorizedClient token
    let response = client.DownloadString(apiUrl + "/series/" + (show.id |> string) + "/episodes")

    JsonConvert.DeserializeObject<EpisodeResult>(response)

let whoepisodes = getEpisodes drwho.data.[0]
printfn "%A" whoepisodes