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

let files = [|"The_Simpsons__Dogtown_17.05.21_20-00_uswnyw_30_TVOON_DE.mpg.HQ.avi";
                 "The_Simpsons__Moho_House_17.05.07_20-00_uswnyw_30_TVOON_DE.mpg.HQ.avi";
                 "Doctor_Who_17.05.06_19-20_ukbbc_45_TVOON_DE.mpg.HQ.avi" |]
let parseShowName (file:string) = 
    if file.IndexOf "__" <> -1 then
        match file.Split([|"__"|], StringSplitOptions.RemoveEmptyEntries) with
        |[|show;_|] -> Some(show.Replace("_", " " ))
        |_ -> None
    else
        let pointIdx = file.IndexOf '.'
        Some(file.Substring(0, pointIdx - 3).Replace("_", " "))

let parseEpisodeName (file:string) = 
    let pointIdx = file.IndexOf '.'
    let dblUnderscoreIdx = file.IndexOf "__"

    if pointIdx = -1 || dblUnderscoreIdx = -1 then
        None
    else
        let length = (pointIdx - 3) - (dblUnderscoreIdx + 2)
        match length with
        |_ when length > 0 -> Some(file.Substring(dblUnderscoreIdx + 2, length).Replace("_", " "))
        |_ -> None

let parseDate (file:string) = 
    let pointIdx = file.IndexOf '.'
    match pointIdx with
    |(-1) -> None
    |_ -> 
        let datePart = file.Substring(pointIdx - 2, 8)
        Some(DateTime.ParseExact(datePart, "yy.MM.dd", null))


let choose (options: (int*string) seq) =
    options
    |> Seq.iter(fun (idx, desc) -> printfn "[%d]: %s" idx desc)
    printf "Which option? "
    let input = Console.ReadLine()    
    printfn "your choice: %s" input
    input |> int

let canonizeEpisode (episode:string) = 
    (episode |> String.filter(Char.IsLetter)).ToLower()

let findEpisodes file = async {
    let! loggedIn = login
    token <- loggedIn
    let showName = file |> parseShowName
    match showName with
    |Some name ->
        let! shows = searchShow name
        let date = parseDate file
        let chosenIdx = choose (shows.data |> Seq.mapi(fun idx show -> (idx, show.seriesName)))
        let searchedShow = shows.data.[chosenIdx]
        printfn "SEARCHED SHOW: %A" searchedShow.seriesName
        let! episodes = getEpisodes searchedShow
        let episodeName = parseEpisodeName file
        let matchingEpisode = episodes.data |> Seq.tryFind(fun e -> 
            match (episodeName, date) with
            |(None, Some d) -> not (isNull e.firstAired) && e.firstAired.Length > 0 && DateTime.Parse(e.firstAired) = d
            |(Some n, _) -> (canonizeEpisode n) = (canonizeEpisode e.episodeName)
            |_ -> false)

        match matchingEpisode with
        |None -> printfn "No episode found!"
        |Some episode -> printfn "episode found: %s" episode.episodeName
    |_ -> printfn "Konnte keinen Shownamen ermitteln"
}

for ep in files do
    findEpisodes ep |> Async.RunSynchronously