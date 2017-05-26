#r "packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"


open System
open System.Collections.Generic
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
type EpisodeResult = { links: Links; data: Episode[] }

let showMap = Dictionary<string, Show>()
let showEpisodes = Dictionary<Show, Episode seq>()

let extractValue (line:string) = 
    (line.Split '=').[1].Trim()

let readcredentials = 
    match File.ReadAllLines("./credentials.cred") |> Array.take 3 with
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

let getEpisodePage show page = async{
    let client = getAuthorizedClient token
    let! response = client.AsyncDownloadString(Uri(apiUrl + "/series/" + (show.id |> string) + "/episodes?page=" + (page |> string)))
    let episodeResponse = JsonConvert.DeserializeObject<EpisodeResult>(response)

    return episodeResponse
}

let getEpisodes show = async {
        match showEpisodes.TryGetValue show with
        |true, episodes -> return episodes
        |_ ->   let! firstPage = getEpisodePage show 1
                if firstPage.links.last.HasValue && firstPage.links.last.Value > 1 then
                    let additionalEpisodes =    [ 2 .. firstPage.links.last.Value ]
                                                |> Seq.collect(fun page ->  let pageResult = (getEpisodePage show page) |> Async.RunSynchronously
                                                                            pageResult.data)
                    let episodes = Seq.concat [firstPage.data; additionalEpisodes |> Array.ofSeq]
                    showEpisodes.[show] <- episodes
                    return episodes
                else
                    showEpisodes.[show] <- firstPage.data
                    return firstPage.data |> Seq.ofArray
}

let files = [|  "Marvel_s_Agents_of_S_H_I_E_L_D___The_Bridge_13.12.10_20-00_uswabc_61_TVOON_DE.mpg.HQ.avi";
                "The_Simpsons__Dogtown_17.05.21_20-00_uswnyw_30_TVOON_DE.mpg.HQ.avi";
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
    match options with
    |_ when Seq.length options = 1 -> 
            printfn "automatically chose first option"
            0
    |_ ->   options
            |> Seq.iter(fun (idx, desc) -> printfn "[%d]: %s" idx desc)
            printf "Which option? "
            let input = Console.ReadLine()    
            printfn "your choice: %s" input
            input |> int

let canonizeEpisode (episode:string) = 
    (episode |> String.filter(Char.IsLetter)).ToLower()

let matchEpisode show file = async{
    let date = parseDate file
    let! episodes = getEpisodes show
    let episodeName = parseEpisodeName file
    let matchingEpisode = episodes |> Seq.tryFind(fun e -> 
        match (episodeName, date) with
        |(None, Some d) -> not (isNull e.firstAired) && e.firstAired.Length > 0 && DateTime.Parse(e.firstAired) = d
        |(Some n, Some d) -> (canonizeEpisode n) = (canonizeEpisode e.episodeName)
                             || not (isNull e.firstAired) && e.firstAired.Length > 0 && DateTime.Parse(e.firstAired) = d
        |(Some n, None) -> (canonizeEpisode n) = (canonizeEpisode e.episodeName)
        |(None, None) -> false)

    return matchingEpisode
}

let deserializeShow (line: string) = 
    let parts = line.Split([|"->"|], StringSplitOptions.RemoveEmptyEntries)
    let parsedName = parts.[0].Trim()
    let values = parts.[1].Split([|"***"|], StringSplitOptions.RemoveEmptyEntries)
    (parsedName, { id = values.[1].Trim() |> int; seriesName = values.[0].Trim(); aliases = [|""|]})


let fillShowCache =
    File.ReadAllLines("./shows.map")
    |> Seq.map deserializeShow
    |> Seq.iter(fun (key, show) -> showMap.[key] <- show)

let cacheShow parsedName foundShow = 
    showMap.[parsedName] <- foundShow
    let mapping = sprintf "%s -> %s *** %d" parsedName foundShow.seriesName foundShow.id
    if not (File.ReadAllLines("./shows.map") |> Seq.exists(fun line -> line = mapping)) then
        File.AppendAllText("./shows.map", mapping + Environment.NewLine)

let rec findShow showName =

    let rec findShowInDb original current = async{
        try
            match showMap.TryGetValue current with
            |true, mappedShow -> return Some(mappedShow)
            |false, _ ->    let! shows = searchShow current
                            let chosenIdx = choose (shows.data |> Seq.mapi(fun idx show -> (idx, show.seriesName)))
                            let chosenShow = shows.data.[chosenIdx]
                            cacheShow original chosenShow      
                            return Some(chosenShow)
        with
            | :? WebException as ex ->  printfn "Konnte zu '%s' keine Show ermitteln" current
                                        printfn "Unbekannte Show, bitte Namen eingeben:"
                                        let newShowName = Console.ReadLine()
                                        if newShowName <> "x" then
                                            let! nextShow = findShowInDb original newShowName
                                            return nextShow
                                        else
                                            return None
    }
    match showName with
    |None -> async { return  None }
    |Some n -> findShowInDb n n


let findEpisode file = async {
    fillShowCache
    let! loggedIn = login
    token <- loggedIn
    let parsedName = file |> parseShowName
    let! show = findShow parsedName
    match show with
    |None -> printfn "Keine Show gefunden!"
    |Some s ->  let! episode = matchEpisode s file
                printfn "gefundene Episode: %A" episode
}

for ep in files do
    findEpisode ep |> Async.RunSynchronously